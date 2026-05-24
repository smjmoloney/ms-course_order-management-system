using System.Diagnostics;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using WarehouseXServer.Models;

namespace WarehouseXServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PerformanceController : ControllerBase
{
    private readonly string _connectionString;

    public PerformanceController(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("WarehouseX")
            ?? throw new InvalidOperationException("Connection string 'WarehouseX' not found.");
    }

    // ---------------------------------------------------------------
    // N+1 Loop demo
    // ---------------------------------------------------------------

    // Unoptimized path step 1 — returns bare order rows with no product name.
    // The client must then call /product/{id} once per row (N additional calls).
    [HttpGet("orders")]
    public async Task<IActionResult> GetOrders()
    {
        using var conn = new SqliteConnection(_connectionString);
        var orders = await conn.QueryAsync<OrderItem>(
            "SELECT OrderID, ProductID, Quantity FROM OptOrders LIMIT 30");
        return Ok(orders);
    }

    // Unoptimized path step 2 — individual product lookup, called N times by the client.
    [HttpGet("product/{id:int}")]
    public async Task<IActionResult> GetProduct(int id)
    {
        using var conn = new SqliteConnection(_connectionString);
        var product = await conn.QueryFirstOrDefaultAsync<ProductItem>(
            "SELECT ProductID, ProductName FROM OptProducts WHERE ProductID = @id",
            new { id });
        return product is null ? NotFound() : Ok(product);
    }

    // Optimized path — orders pre-joined with product names; one call replaces N+1.
    [HttpGet("orders/joined")]
    public async Task<IActionResult> GetOrdersJoined()
    {
        using var conn = new SqliteConnection(_connectionString);
        var rows = await conn.QueryAsync<OrderWithProduct>(
            """
            SELECT o.OrderID, p.ProductName, o.Quantity
            FROM OptOrders o
            JOIN OptProducts p ON p.ProductID = o.ProductID
            LIMIT 30
            """);
        return Ok(rows);
    }

    // Optimized application code — dictionary preload pattern.
    // 1. Retrieves all relevant products in a single query (no redundant DB calls).
    // 2. Stores products in a dictionary for O(1) lookups.
    // 3. Loops through orders efficiently using preloaded product data.
    [HttpGet("orders/preloaded")]
    public async Task<IActionResult> GetOrdersPreloaded()
    {
        using var conn = new SqliteConnection(_connectionString);

        // Step 1: single query — fetch all products up front
        var products = await conn.QueryAsync<ProductItem>(
            "SELECT ProductID, ProductName FROM OptProducts");

        // Step 2: store in a dictionary for fast lookups
        var productDict = products.ToDictionary(p => p.ProductID);

        // Step 3: fetch orders, then loop using preloaded product data
        var orders = await conn.QueryAsync<OrderItem>(
            "SELECT OrderID, ProductID, Quantity FROM OptOrders LIMIT 30");

        var result = orders.Select(o => new OrderWithProduct
        {
            OrderID     = o.OrderID,
            ProductName = productDict.TryGetValue(o.ProductID, out var p) ? p.ProductName : "Unknown",
            Quantity    = o.Quantity
        });

        return Ok(result);
    }

    // ---------------------------------------------------------------
    // Redundant Calls demo
    // ---------------------------------------------------------------

    // Three individual stat endpoints — each opens its own connection and
    // runs a separate query against the 200 000-row orders table.
    // The 20 ms delay simulates realistic overhead per call: connection-pool
    // contention, query-plan compilation, or a slow downstream service.
    [HttpGet("stats/order-count")]
    public async Task<IActionResult> GetOrderCount()
    {
        await Task.Delay(20);
        using var conn = new SqliteConnection(_connectionString);
        var count = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM OptOrders");
        return Ok(new { Count = count });
    }

    [HttpGet("stats/total-quantity")]
    public async Task<IActionResult> GetTotalQuantity()
    {
        await Task.Delay(20);
        using var conn = new SqliteConnection(_connectionString);
        var total = await conn.ExecuteScalarAsync<long>("SELECT SUM(Quantity) FROM OptOrders");
        return Ok(new { Total = total });
    }

    [HttpGet("stats/top-product")]
    public async Task<IActionResult> GetTopProduct()
    {
        await Task.Delay(20);
        using var conn = new SqliteConnection(_connectionString);
        var name = await conn.ExecuteScalarAsync<string>(
            """
            SELECT p.ProductName
            FROM OptOrders o
            JOIN OptProducts p ON p.ProductID = o.ProductID
            GROUP BY p.ProductID
            ORDER BY SUM(o.Quantity) DESC
            LIMIT 1
            """);
        return Ok(new { ProductName = name });
    }

    // Optimized combined endpoint — all three stats in one server round-trip,
    // sharing a single connection.
    [HttpGet("stats/all")]
    public async Task<IActionResult> GetAllStats()
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        var orderCount = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM OptOrders");
        var totalQty   = await conn.ExecuteScalarAsync<long>("SELECT SUM(Quantity) FROM OptOrders");
        var topProduct = await conn.ExecuteScalarAsync<string>(
            """
            SELECT p.ProductName
            FROM OptOrders o
            JOIN OptProducts p ON p.ProductID = o.ProductID
            GROUP BY p.ProductID
            ORDER BY SUM(o.Quantity) DESC
            LIMIT 1
            """);
        return Ok(new { OrderCount = orderCount, TotalQuantity = totalQty, TopProduct = topProduct });
    }

    // ---------------------------------------------------------------
    // Sequential vs Parallel I/O demo
    //
    // Both endpoints simulate 3 independent data-source calls using
    // Task.Delay(35). The server measures its own elapsed time with a
    // Stopwatch and returns it, so the result is unaffected by Blazor
    // WASM's single-threaded fetch dispatch.
    // ---------------------------------------------------------------

    [HttpGet("io/sequential")]
    public async Task<IActionResult> GetSequentialIO()
    {
        var sw = Stopwatch.StartNew();
        await Task.Delay(35); // data source A
        await Task.Delay(35); // data source B
        await Task.Delay(35); // data source C
        sw.Stop();
        return Ok(new { DurationMs = sw.ElapsedMilliseconds, Detail = "3 sequential awaits" });
    }

    [HttpGet("io/parallel")]
    public async Task<IActionResult> GetParallelIO()
    {
        var sw = Stopwatch.StartNew();
        await Task.WhenAll(
            Task.Delay(35), // data source A
            Task.Delay(35), // data source B
            Task.Delay(35)  // data source C
        );
        sw.Stop();
        return Ok(new { DurationMs = sw.ElapsedMilliseconds, Detail = "3 parallel awaits" });
    }
}
