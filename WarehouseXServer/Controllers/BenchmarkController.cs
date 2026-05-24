using System.Diagnostics;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using WarehouseXServer.Models;

namespace WarehouseXServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BenchmarkController : ControllerBase
{
    private readonly string _connectionString;

    public BenchmarkController(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("WarehouseX")
            ?? throw new InvalidOperationException("Connection string 'WarehouseX' not found.");
    }

    // Single endpoint — accepts queryType (unoptimized|optimized) and schemaType (unopt|opt).
    // Table names come from an allowlist switch, never from raw user input.
    [HttpGet("run")]
    public async Task<ActionResult<BenchmarkResult>> Run(
        [FromQuery] string queryType,
        [FromQuery] string schemaType)
    {
        var (ordersTable, productsTable) = schemaType switch
        {
            "unopt" => ("UnoptOrders", "UnoptProducts"),
            "opt"   => ("OptOrders",   "OptProducts"),
            _       => (null, null)
        };

        if (ordersTable is null)
            return BadRequest("schemaType must be 'unopt' or 'opt'.");

        // Unoptimized query: a correlated subquery wrapped in a derived table.
        // LIMIT -1 OFFSET 0 is a documented SQLite feature that disables query flattening,
        // preventing the outer WHERE from being pushed into the subquery. SQLite *must*
        // execute the correlated subquery for every product row before filtering to
        // Electronics — 20 full scans of Orders instead of 4.
        string? sql = queryType switch
        {
            "unoptimized" => $"""
                SELECT ProductName, TotalSold
                FROM (
                    SELECT p.ProductName,
                           p.Category,
                           COALESCE(
                               (SELECT SUM(o.Quantity)
                                FROM {ordersTable} o
                                WHERE o.ProductID = p.ProductID),
                               0) AS TotalSold
                    FROM {productsTable} p
                    LIMIT -1 OFFSET 0
                )
                WHERE Category = 'Electronics'
                ORDER BY TotalSold DESC
                """,

            // Optimized query: applies the Category filter before touching Orders at all.
            // SQLite evaluates Products first (20 rows → 4 Electronics), then does an
            // index-seek (or indexed loop) into Orders for those 4 products only.
            "optimized" => $"""
                SELECT p.ProductName, SUM(o.Quantity) AS TotalSold
                FROM {productsTable} p
                JOIN {ordersTable} o ON p.ProductID = o.ProductID
                WHERE p.Category = 'Electronics'
                GROUP BY p.ProductName
                ORDER BY TotalSold DESC
                """,

            _ => null
        };

        if (sql is null)
            return BadRequest("queryType must be 'unoptimized' or 'optimized'.");

        // Open one connection and run the query twice.
        // The first run loads SQLite pages into the OS page cache; the second run measures
        // query execution cost without cold-start I/O noise, giving stable, comparable timings.
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        await conn.QueryAsync<ProductSales>(sql);   // warm-up — result discarded

        var sw = Stopwatch.StartNew();
        var results = (await conn.QueryAsync<ProductSales>(sql)).ToList();
        sw.Stop();

        return Ok(new BenchmarkResult { DurationMs = sw.ElapsedMilliseconds, Results = results });
    }
}

