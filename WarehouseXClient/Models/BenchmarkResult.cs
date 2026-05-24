namespace WarehouseXClient.Models;

public class ProductSales
{
    public string ProductName { get; set; } = string.Empty;
    public int TotalSold { get; set; }
}

public class BenchmarkResult
{
    public long DurationMs { get; set; }
    public List<ProductSales> Results { get; set; } = [];
}
