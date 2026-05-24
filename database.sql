-- WarehouseX Database Schema (SQLite reference)
-- The server initializes and seeds this database automatically on first run.
-- This file is kept for reference only — you do not need to run it manually.
--
-- SQLite does not support schemas, so tables are prefixed instead:
--   Unopt* — no extra indexes (slow path)
--   Opt*   — with covering indexes (fast path)
-- ============================================================

CREATE TABLE IF NOT EXISTS Customers (
    CustomerID INTEGER PRIMARY KEY AUTOINCREMENT,
    FirstName  TEXT NOT NULL,
    LastName   TEXT NOT NULL,
    Email      TEXT NOT NULL UNIQUE
);

-- Unoptimized tables — no indexes beyond primary keys
CREATE TABLE IF NOT EXISTS UnoptProducts (
    ProductID   INTEGER PRIMARY KEY AUTOINCREMENT,
    ProductName TEXT NOT NULL,
    Category    TEXT NOT NULL,
    Price       REAL NOT NULL,
    Stock       INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS UnoptOrders (
    OrderID    INTEGER PRIMARY KEY AUTOINCREMENT,
    CustomerID INTEGER NOT NULL REFERENCES Customers(CustomerID),
    ProductID  INTEGER NOT NULL REFERENCES UnoptProducts(ProductID),
    Quantity   INTEGER NOT NULL,
    OrderDate  TEXT NOT NULL DEFAULT (date('now'))
);

-- Optimized tables — with covering indexes
CREATE TABLE IF NOT EXISTS OptProducts (
    ProductID   INTEGER PRIMARY KEY AUTOINCREMENT,
    ProductName TEXT NOT NULL,
    Category    TEXT NOT NULL,
    Price       REAL NOT NULL,
    Stock       INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS OptOrders (
    OrderID    INTEGER PRIMARY KEY AUTOINCREMENT,
    CustomerID INTEGER NOT NULL REFERENCES Customers(CustomerID),
    ProductID  INTEGER NOT NULL REFERENCES OptProducts(ProductID),
    Quantity   INTEGER NOT NULL,
    OrderDate  TEXT NOT NULL DEFAULT (date('now'))
);

-- Index 1: seek directly to rows where Category = 'Electronics'
CREATE INDEX IF NOT EXISTS IX_OptProducts_Category ON OptProducts (Category);

-- Index 2: seek matching orders by ProductID; Quantity included to avoid a table lookup
CREATE INDEX IF NOT EXISTS IX_OptOrders_ProductID ON OptOrders (ProductID, Quantity);

-- ============================================================
-- Reference queries (executed by the server endpoints)
-- ============================================================

-- UNOPTIMIZED: full table scan on UnoptProducts + UnoptOrders
-- SELECT p.ProductName, SUM(o.Quantity) AS TotalSold
-- FROM UnoptOrders o
-- JOIN UnoptProducts p ON o.ProductID = p.ProductID
-- WHERE p.Category = 'Electronics'
-- GROUP BY p.ProductName
-- ORDER BY TotalSold DESC;

-- OPTIMIZED: index seek on Category, covering index on ProductID/Quantity
-- SELECT p.ProductName, SUM(o.Quantity) AS TotalSold
-- FROM OptOrders o
-- JOIN OptProducts p ON o.ProductID = p.ProductID
-- WHERE p.Category = 'Electronics'
-- GROUP BY p.ProductName
-- ORDER BY TotalSold DESC;
