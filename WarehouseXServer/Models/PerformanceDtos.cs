namespace WarehouseXServer.Models;

public class OrderItem
{
    public int OrderID   { get; set; }
    public int ProductID { get; set; }
    public int Quantity  { get; set; }
}

public class OrderWithProduct
{
    public int    OrderID     { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int    Quantity    { get; set; }
}

public class ProductItem
{
    public int    ProductID   { get; set; }
    public string ProductName { get; set; } = string.Empty;
}
