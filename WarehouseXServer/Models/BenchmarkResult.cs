namespace WarehouseXServer.Models;

public class BenchmarkResult
{
    public long DurationMs { get; set; }
    public List<ProductSales> Results { get; set; } = [];
}
