using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using WarehouseXServer.Models;

namespace WarehouseXServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ResilienceController : ControllerBase
{
    private readonly string _connectionString;

    public ResilienceController(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("WarehouseX")
            ?? throw new InvalidOperationException("Connection string 'WarehouseX' not found.");
    }

    // ---------------------------------------------------------------
    // Null Reference demo
    // Both endpoints query for ProductID 99999, which does not exist.
    // ---------------------------------------------------------------

    // Fragile — no null check; dereferences the result regardless.
    // Throws NullReferenceException, which propagates to the global exception handler.
    [HttpGet("null-ref/fragile")]
    public async Task<IActionResult> FragileNullRef()
    {
        using var conn = new SqliteConnection(_connectionString);
        ProductItem? product = await conn.QueryFirstOrDefaultAsync<ProductItem>(
            "SELECT ProductID, ProductName FROM OptProducts WHERE ProductID = @id",
            new { id = 99999 });

        // No null check — NullReferenceException thrown here and propagates uncaught.
        string name = product!.ProductName;
        return Ok(new { message = $"Product: {name}" });
    }

    // Resilient — checks for null and returns a descriptive 404.
    [HttpGet("null-ref/resilient")]
    public async Task<IActionResult> ResilientNullRef()
    {
        using var conn = new SqliteConnection(_connectionString);
        var product = await conn.QueryFirstOrDefaultAsync<ProductItem>(
            "SELECT ProductID, ProductName FROM OptProducts WHERE ProductID = @id",
            new { id = 99999 });

        if (product is null)
            return NotFound(new { message = "Product 99999 not found." });

        return Ok(new { message = $"Product: {product.ProductName}" });
    }

    // ---------------------------------------------------------------
    // Unhandled Exception demo
    // Both endpoints attempt to process an order for 9,999,999 units
    // of Wireless Headphones (stock: 120).
    // ---------------------------------------------------------------

    // Fragile — no try-catch; exception propagates to the global exception handler.
    [HttpGet("exception/fragile")]
    public async Task<IActionResult> FragileException()
    {
        using var conn = new SqliteConnection(_connectionString);
        var stock = await conn.ExecuteScalarAsync<int>(
            "SELECT Stock FROM OptProducts WHERE ProductID = 1");

        const int requested = 9_999_999;

        // Subtract first, check after — discovers corrupt state too late.
        int newStock = stock - requested;
        if (newStock < 0)
            throw new InvalidOperationException(
                $"Stock would become {newStock:N0}. Operation aborted.");

        return Ok(new { message = "Order processed." });
    }

    // Resilient — pre-validates AND wraps in try-catch as a safety net.
    [HttpGet("exception/resilient")]
    public async Task<IActionResult> ResilientException()
    {
        try
        {
            using var conn = new SqliteConnection(_connectionString);
            var stock = await conn.ExecuteScalarAsync<int>(
                "SELECT Stock FROM OptProducts WHERE ProductID = 1");

            const int requested = 9_999_999;

            // Validate BEFORE modifying — fail fast with a clear message.
            if (requested > stock)
                return BadRequest(new
                {
                    message = $"Insufficient stock: requested {requested:N0}, available {stock:N0}."
                });

            return Ok(new { message = "Order processed." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = $"Order rejected: {ex.Message}" });
        }
    }

    // ---------------------------------------------------------------
    // Edge Case demo
    // Both endpoints receive quantity = 0 via query string.
    // ---------------------------------------------------------------

    // Fragile — no input validation; silently accepts an invalid order.
    [HttpGet("edge/fragile")]
    public IActionResult FragileEdgeCase([FromQuery] int quantity = 0)
    {
        // No guard — processes any quantity, including 0 and negatives.
        // Stock would be decremented by 0: a silent no-op that reports success.
        return Ok(new
        {
            message  = $"Order accepted for quantity {quantity}.",
            note     = "Stock unchanged — zero-quantity order silently accepted."
        });
    }

    // Resilient — rejects invalid input at the boundary before any processing.
    [HttpGet("edge/resilient")]
    public IActionResult ResilientEdgeCase([FromQuery] int quantity = 0)
    {
        if (quantity <= 0)
            return BadRequest(new { message = $"Quantity must be at least 1. Received: {quantity}." });

        return Ok(new { message = $"Order processed for quantity {quantity}." });
    }
}
