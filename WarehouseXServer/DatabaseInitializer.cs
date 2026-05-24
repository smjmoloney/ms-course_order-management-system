using Microsoft.Data.Sqlite;

namespace WarehouseXServer;

// Creates and seeds the SQLite database on first run.
// Tables are prefixed (Unopt/Opt) instead of using schemas,
// since SQLite does not support schemas the way SQL Server does.
//
// Schema summary:
//   Customers        — shared reference table
//   UnoptProducts    — no extra indexes (demonstrates slow path)
//   UnoptOrders      — no extra indexes
//   OptProducts      — IX_OptProducts_Category
//   OptOrders        — IX_OptOrders_ProductID covering (ProductID, Quantity)
public static class DatabaseInitializer
{
    private static readonly (string Name, string Category, double Price, int Stock)[] ProductSeed =
    [
        ("Wireless Headphones", "Electronics",  89.99, 120),
        ("USB-C Hub",           "Electronics",  34.99, 200),
        ("Mechanical Keyboard", "Electronics", 109.99,  60),
        ("Smart Speaker",       "Electronics",  59.99,  90),
        ("Laptop Stand",        "Accessories",  49.99,  85),
        ("Desk Lamp",           "Accessories",  24.99, 150),
        ("Cable Organiser",     "Accessories",  14.99, 300),
        ("Monitor Riser",       "Accessories",  39.99,  70),
        ("Office Chair",        "Furniture",   299.99,  25),
        ("Standing Desk",       "Furniture",   499.99,  15),
        ("Desk Pad",            "Furniture",    29.99, 110),
        ("Bookshelf",           "Furniture",   149.99,  40),
        ("Safety Gloves",       "Tools",        12.99, 500),
        ("Torque Wrench",       "Tools",        44.99,  80),
        ("Label Maker",         "Tools",        34.99, 120),
        ("Barcode Scanner",     "Tools",        89.99,  60),
        ("Packing Tape",        "Supplies",      4.99, 800),
        ("Bubble Wrap Roll",    "Supplies",      9.99, 400),
        ("Cardboard Boxes x10", "Supplies",     19.99, 600),
        ("Stretch Wrap",        "Supplies",      7.99, 350),
    ];

    public static void Initialize(string connectionString)
    {
        using var conn = new SqliteConnection(connectionString);
        conn.Open();

        CreateSchema(conn);

        if (IsAlreadySeeded(conn)) return;

        Seed(conn);
    }

    private static void CreateSchema(SqliteConnection conn)
    {
        string[] statements =
        [
            """
            CREATE TABLE IF NOT EXISTS Customers (
                CustomerID INTEGER PRIMARY KEY AUTOINCREMENT,
                FirstName  TEXT NOT NULL,
                LastName   TEXT NOT NULL,
                Email      TEXT NOT NULL UNIQUE
            )
            """,

            // Unoptimized — no indexes beyond the primary key
            """
            CREATE TABLE IF NOT EXISTS UnoptProducts (
                ProductID   INTEGER PRIMARY KEY AUTOINCREMENT,
                ProductName TEXT NOT NULL,
                Category    TEXT NOT NULL,
                Price       REAL NOT NULL,
                Stock       INTEGER NOT NULL DEFAULT 0
            )
            """,
            """
            CREATE TABLE IF NOT EXISTS UnoptOrders (
                OrderID    INTEGER PRIMARY KEY AUTOINCREMENT,
                CustomerID INTEGER NOT NULL REFERENCES Customers(CustomerID),
                ProductID  INTEGER NOT NULL REFERENCES UnoptProducts(ProductID),
                Quantity   INTEGER NOT NULL,
                OrderDate  TEXT NOT NULL DEFAULT (date('now'))
            )
            """,

            // Optimized — covering indexes on Category and ProductID
            """
            CREATE TABLE IF NOT EXISTS OptProducts (
                ProductID   INTEGER PRIMARY KEY AUTOINCREMENT,
                ProductName TEXT NOT NULL,
                Category    TEXT NOT NULL,
                Price       REAL NOT NULL,
                Stock       INTEGER NOT NULL DEFAULT 0
            )
            """,
            """
            CREATE TABLE IF NOT EXISTS OptOrders (
                OrderID    INTEGER PRIMARY KEY AUTOINCREMENT,
                CustomerID INTEGER NOT NULL REFERENCES Customers(CustomerID),
                ProductID  INTEGER NOT NULL REFERENCES OptProducts(ProductID),
                Quantity   INTEGER NOT NULL,
                OrderDate  TEXT NOT NULL DEFAULT (date('now'))
            )
            """,

            "CREATE INDEX IF NOT EXISTS IX_OptProducts_Category ON OptProducts (Category)",
            // Covering index: stores ProductID + Quantity together so SUM(Quantity)
            // WHERE ProductID = ? can be answered from the index alone — no table lookup needed.
            "CREATE INDEX IF NOT EXISTS IX_OptOrders_ProductID  ON OptOrders  (ProductID, Quantity)",
        ];

        foreach (var sql in statements)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }
    }

    private static bool IsAlreadySeeded(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM UnoptOrders";
        return (long)cmd.ExecuteScalar()! > 0;
    }

    private static void Seed(SqliteConnection conn)
    {
        using var tx = conn.BeginTransaction();

        InsertCustomers(conn, tx);
        InsertProducts(conn, tx, "UnoptProducts");
        InsertProducts(conn, tx, "OptProducts");
        InsertOrders(conn, tx, "UnoptOrders", 3, ProductSeed.Length, 200_000);
        InsertOrders(conn, tx, "OptOrders",   3, ProductSeed.Length, 200_000);

        tx.Commit();
    }

    private static void InsertCustomers(SqliteConnection conn, SqliteTransaction tx)
    {
        (string First, string Last, string Email)[] customers =
        [
            ("Alice", "Nguyen",  "alice.nguyen@example.com"),
            ("Bob",   "Patel",   "bob.patel@example.com"),
            ("Carol", "Schmidt", "carol.schmidt@example.com"),
        ];

        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "INSERT INTO Customers (FirstName, LastName, Email) VALUES ($fn, $ln, $em)";
        var pFn = cmd.Parameters.AddWithValue("$fn", "");
        var pLn = cmd.Parameters.AddWithValue("$ln", "");
        var pEm = cmd.Parameters.AddWithValue("$em", "");

        foreach (var (first, last, email) in customers)
        {
            pFn.Value = first;
            pLn.Value = last;
            pEm.Value = email;
            cmd.ExecuteNonQuery();
        }
    }

    private static void InsertProducts(SqliteConnection conn, SqliteTransaction tx, string table)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"""
            INSERT INTO {table} (ProductName, Category, Price, Stock)
            VALUES ($name, $cat, $price, $stock)
            """;
        var pName  = cmd.Parameters.AddWithValue("$name",  "");
        var pCat   = cmd.Parameters.AddWithValue("$cat",   "");
        var pPrice = cmd.Parameters.AddWithValue("$price", 0.0);
        var pStock = cmd.Parameters.AddWithValue("$stock", 0);

        foreach (var (name, category, price, stock) in ProductSeed)
        {
            pName.Value  = name;
            pCat.Value   = category;
            pPrice.Value = price;
            pStock.Value = stock;
            cmd.ExecuteNonQuery();
        }
    }

    private static void InsertOrders(
        SqliteConnection conn, SqliteTransaction tx,
        string table, int customerCount, int productCount, int count)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"""
            INSERT INTO {table} (CustomerID, ProductID, Quantity, OrderDate)
            VALUES ($cid, $pid, $qty, $date)
            """;
        var pCid  = cmd.Parameters.AddWithValue("$cid",  0);
        var pPid  = cmd.Parameters.AddWithValue("$pid",  0);
        var pQty  = cmd.Parameters.AddWithValue("$qty",  0);
        var pDate = cmd.Parameters.AddWithValue("$date", "");

        var rng      = new Random(42);
        var baseDate = DateTime.Today;

        for (int i = 0; i < count; i++)
        {
            pCid.Value  = rng.Next(1, customerCount + 1);
            pPid.Value  = rng.Next(1, productCount  + 1);
            pQty.Value  = rng.Next(1, 11);
            pDate.Value = baseDate.AddDays(-rng.Next(0, 730)).ToString("yyyy-MM-dd");
            cmd.ExecuteNonQuery();
        }
    }
}
