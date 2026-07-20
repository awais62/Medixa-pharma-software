-- Pharma Billing System - SQLite Schema

-- 1. Medicines Table
CREATE TABLE IF NOT EXISTS Medicines (
    MedicineID INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    Type TEXT, -- Tab, Syp, Cap, etc.
    Category TEXT,
    Manufacturer TEXT,
    Unit TEXT, -- e.g., Box, Strip, Piece
    Barcode TEXT,
    GenericFormula TEXT,
    WholesalePrice REAL DEFAULT 0,
    PurchasePrice REAL DEFAULT 0,
    SalePrice REAL DEFAULT 0,
    MinStock INTEGER DEFAULT 10,
    Status TEXT DEFAULT 'Active'
);

-- 2. Stocks Table (Batch-wise management)
CREATE TABLE IF NOT EXISTS Stocks (
    StockID INTEGER PRIMARY KEY AUTOINCREMENT,
    MedicineID INTEGER,
    BatchNo TEXT NOT NULL,
    RackNo TEXT DEFAULT 'Rack-1',
    ExpiryDate TEXT, -- YYYY-MM-DD
    Quantity INTEGER DEFAULT 0,
    SupplierID INTEGER,
    DateAdded TEXT DEFAULT (date('now')),
    FOREIGN KEY (MedicineID) REFERENCES Medicines(MedicineID)
);

-- 3. Suppliers Table
CREATE TABLE IF NOT EXISTS Suppliers (
    SupplierID INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    Contact TEXT,
    Email TEXT,
    Address TEXT,
    LedgerID INTEGER
);

-- 4. Customers Table
CREATE TABLE IF NOT EXISTS Customers (
    CustomerID INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    Contact TEXT,
    Email TEXT,
    Address TEXT,
    Balance REAL DEFAULT 0,
    LedgerID INTEGER
);

-- 5. Sales Table
CREATE TABLE IF NOT EXISTS Sales (
    SaleID INTEGER PRIMARY KEY AUTOINCREMENT,
    CustomerID INTEGER,
    SaleDate TEXT DEFAULT (datetime('now')),
    TotalAmount REAL DEFAULT 0,
    Discount REAL DEFAULT 0,
    Tax REAL DEFAULT 0,
    NetPaid REAL DEFAULT 0,
    Status TEXT DEFAULT 'Paid', -- Paid, Partial, Credit
    FBRInvoiceNo TEXT,
    FOREIGN KEY (CustomerID) REFERENCES Customers(CustomerID)
);

-- 6. SaleDetails Table
CREATE TABLE IF NOT EXISTS SaleDetails (
    DetailID INTEGER PRIMARY KEY AUTOINCREMENT,
    SaleID INTEGER,
    MedicineID INTEGER,
    BatchNo TEXT,
    RackNo TEXT,
    Quantity INTEGER,
    UnitPrice REAL,
    TotalPrice REAL,
    FOREIGN KEY (SaleID) REFERENCES Sales(SaleID),
    FOREIGN KEY (MedicineID) REFERENCES Medicines(MedicineID)
);

-- 7. Purchases Table
CREATE TABLE IF NOT EXISTS Purchases (
    PurchaseID INTEGER PRIMARY KEY AUTOINCREMENT,
    SupplierID INTEGER,
    InvoiceNo TEXT,
    PurchaseDate TEXT DEFAULT (datetime('now')),
    TotalAmount REAL DEFAULT 0,
    Tax REAL DEFAULT 0,
    Status TEXT DEFAULT 'Received',
    PurchaseType TEXT DEFAULT 'Normal', -- Normal, Loose, Opening
    FOREIGN KEY (SupplierID) REFERENCES Suppliers(SupplierID)
);

-- 8. PurchaseDetails Table
CREATE TABLE IF NOT EXISTS PurchaseDetails (
    DetailID INTEGER PRIMARY KEY AUTOINCREMENT,
    PurchaseID INTEGER,
    MedicineID INTEGER,
    BatchNo TEXT,
    ExpiryDate TEXT,
    Quantity INTEGER,
    PurchasePrice REAL,
    BonusQuantity INTEGER DEFAULT 0,
    TotalPrice REAL,
    FOREIGN KEY (PurchaseID) REFERENCES Purchases(PurchaseID),
    FOREIGN KEY (MedicineID) REFERENCES Medicines(MedicineID)
);

-- 9. Ledgers Table
CREATE TABLE IF NOT EXISTS Ledgers (
    LedgerID INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT UNIQUE NOT NULL,
    Type TEXT, -- Customer, Supplier, Cash, Bank, Expense, Income
    Balance REAL DEFAULT 0
);

-- 10. LedgerTransactions Table
CREATE TABLE IF NOT EXISTS LedgerTransactions (
    TransactionID INTEGER PRIMARY KEY AUTOINCREMENT,
    LedgerID INTEGER,
    TransactionDate TEXT DEFAULT (datetime('now')),
    Description TEXT,
    Debit REAL DEFAULT 0,
    Credit REAL DEFAULT 0,
    Balance REAL DEFAULT 0,
    FOREIGN KEY (LedgerID) REFERENCES Ledgers(LedgerID)
);

-- 11. Users Table
CREATE TABLE IF NOT EXISTS Users (
    UserID INTEGER PRIMARY KEY AUTOINCREMENT,
    Username TEXT UNIQUE NOT NULL,
    Password TEXT NOT NULL,
    Role TEXT DEFAULT 'Admin'
);

-- 12. Purchase Returns Table
CREATE TABLE IF NOT EXISTS PurchaseReturns (
    ReturnID INTEGER PRIMARY KEY AUTOINCREMENT,
    PurchaseID INTEGER,
    SupplierID INTEGER,
    ReturnDate TEXT DEFAULT (datetime('now')),
    TotalAmount REAL DEFAULT 0,
    Reason TEXT,
    Status TEXT DEFAULT 'Returned',
    FOREIGN KEY (PurchaseID) REFERENCES Purchases(PurchaseID),
    FOREIGN KEY (SupplierID) REFERENCES Suppliers(SupplierID)
);

-- 13. Purchase Return Details Table
CREATE TABLE IF NOT EXISTS PurchaseReturnDetails (
    DetailID INTEGER PRIMARY KEY AUTOINCREMENT,
    ReturnID INTEGER,
    MedicineID INTEGER,
    BatchNo TEXT,
    Quantity INTEGER,
    PurchasePrice REAL,
    BonusQuantity INTEGER DEFAULT 0,
    TotalPrice REAL,
    FOREIGN KEY (ReturnID) REFERENCES PurchaseReturns(ReturnID),
    FOREIGN KEY (MedicineID) REFERENCES Medicines(MedicineID)
);

-- 14. Sale Returns Table
CREATE TABLE IF NOT EXISTS SaleReturns (
    ReturnID INTEGER PRIMARY KEY AUTOINCREMENT,
    SaleID INTEGER,
    CustomerID INTEGER,
    ReturnDate TEXT DEFAULT (datetime('now')),
    TotalAmount REAL DEFAULT 0,
    Reason TEXT,
    Status TEXT DEFAULT 'Returned',
    FBRInvoiceNo TEXT,
    FOREIGN KEY (SaleID) REFERENCES Sales(SaleID),
    FOREIGN KEY (CustomerID) REFERENCES Customers(CustomerID)
);

-- 15. Sale Return Details Table
CREATE TABLE IF NOT EXISTS SaleReturnDetails (
    DetailID INTEGER PRIMARY KEY AUTOINCREMENT,
    ReturnID INTEGER,
    MedicineID INTEGER,
    BatchNo TEXT,
    Quantity INTEGER,
    UnitPrice REAL,
    TotalPrice REAL,
    FOREIGN KEY (ReturnID) REFERENCES SaleReturns(ReturnID),
    FOREIGN KEY (MedicineID) REFERENCES Medicines(MedicineID)
);

-- 16. Initial Data
INSERT OR IGNORE INTO Users (Username, Password, Role) VALUES ('admin', 'admin123', 'Admin');
INSERT OR IGNORE INTO Ledgers (Name, Type, Balance) VALUES ('Cash Account', 'Cash', 0);
INSERT OR IGNORE INTO Ledgers (Name, Type, Balance) VALUES ('Sales Income', 'Income', 0);
INSERT OR IGNORE INTO Ledgers (Name, Type, Balance) VALUES ('Purchase Expense', 'Expense', 0);
INSERT OR IGNORE INTO Ledgers (Name, Type, Balance) VALUES ('Purchase Returns', 'Income', 0);
INSERT OR IGNORE INTO Ledgers (Name, Type, Balance) VALUES ('Sale Returns', 'Expense', 0);

-- 17. Staff Table
CREATE TABLE IF NOT EXISTS Staff (
    StaffID INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    Designation TEXT,
    Contact TEXT,
    BaseSalary REAL DEFAULT 0,
    LedgerID INTEGER
);

-- 18. Attendance Table
CREATE TABLE IF NOT EXISTS Attendance (
    AttendanceID INTEGER PRIMARY KEY AUTOINCREMENT,
    StaffID INTEGER,
    Date TEXT,
    Status TEXT
);

-- 19. Payroll Table
CREATE TABLE IF NOT EXISTS Payroll (
    PayrollID INTEGER PRIMARY KEY AUTOINCREMENT,
    StaffID INTEGER,
    Month TEXT,
    AmountPaid REAL,
    Bonuses REAL,
    Deductions REAL,
    DatePaid TEXT
);
