-- WarehouseX Database Schema

CREATE TABLE Customers (
    CustomerID   INT           PRIMARY KEY IDENTITY(1,1),
    FirstName    NVARCHAR(50)  NOT NULL,
    LastName     NVARCHAR(50)  NOT NULL,
    Email        NVARCHAR(100) NOT NULL UNIQUE
);

CREATE TABLE Products (
    ProductID    INT            PRIMARY KEY IDENTITY(1,1),
    ProductName  NVARCHAR(100)  NOT NULL,
    Category     NVARCHAR(50)   NOT NULL,
    Price        DECIMAL(10, 2) NOT NULL,
    Stock        INT            NOT NULL DEFAULT 0
);

CREATE TABLE Orders (
    OrderID     INT  PRIMARY KEY IDENTITY(1,1),
    CustomerID  INT  NOT NULL REFERENCES Customers(CustomerID),
    ProductID   INT  NOT NULL REFERENCES Products(ProductID),
    Quantity    INT  NOT NULL,
    OrderDate   DATE NOT NULL DEFAULT GETDATE()
);

-- ------------------------------------------------------------
-- Seed data
-- ------------------------------------------------------------

INSERT INTO Customers (FirstName, LastName, Email) VALUES
    ('Alice',  'Nguyen',   'alice.nguyen@example.com'),
    ('Bob',    'Patel',    'bob.patel@example.com'),
    ('Carol',  'Schmidt',  'carol.schmidt@example.com');

INSERT INTO Products (ProductName, Category, Price, Stock) VALUES
    ('Wireless Headphones', 'Electronics',  89.99, 120),
    ('USB-C Hub',           'Electronics',  34.99, 200),
    ('Laptop Stand',        'Accessories',  49.99,  85),
    ('Mechanical Keyboard', 'Electronics', 109.99,  60),
    ('Desk Lamp',           'Accessories',  24.99, 150);

INSERT INTO Orders (CustomerID, ProductID, Quantity, OrderDate) VALUES
    (1, 1, 2, '2026-04-10'),
    (2, 1, 1, '2026-04-15'),
    (3, 2, 4, '2026-04-18'),
    (1, 4, 1, '2026-04-20'),
    (2, 3, 2, '2026-04-22'),
    (3, 4, 3, '2026-05-01'),
    (1, 2, 2, '2026-05-05'),
    (2, 5, 1, '2026-05-10');
