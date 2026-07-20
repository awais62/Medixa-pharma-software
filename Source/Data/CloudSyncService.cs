using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Data;
using System.Data.SQLite;

namespace PharmaBilling.Source.Data
{
    /// <summary>
    /// MEDIXA CLOUD SYNC SERVICE
    /// 
    /// Uploads every Sale and Purchase to Supabase cloud (keyed by License Key).
    /// On new machine + same license, RestoreFromCloud() pulls all records back.
    /// 
    /// All cloud ops run on background threads — they NEVER block the UI.
    /// </summary>
    public static class CloudSyncService
    {
        private static readonly string SupabaseUrl  = "https://idnfkbgswrbhmqzsnxnk.supabase.co";
        private static readonly string SupabaseKey  = "sb_publishable_-Uwrrbhxubrc3dDYDw6gMw_1xRb2oYd";
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        // ── PUBLIC ENTRY POINTS ───────────────────────────────────────────────

        /// <summary>Upload a completed sale to the cloud (fire-and-forget).</summary>
        public static void UploadSaleAsync(int saleId, string customerName,
                                            string saleDate, double total,
                                            double discount, double netPaid,
                                            string status,
                                            List<Dictionary<string, object>> items)
        {
            string key = LicenseManager.CurrentLicenseKey;
            if (string.IsNullOrEmpty(key)) return;

            Task.Run(() =>
            {
                try
                {
                    var payload = new Dictionary<string, object>
                    {
                        { "license_key",    key          },
                        { "local_sale_id",  saleId       },
                        { "sale_date",      saleDate     },
                        { "customer_name",  customerName },
                        { "total_amount",   total        },
                        { "discount",       discount     },
                        { "net_paid",       netPaid      },
                        { "status",         status       },
                        { "items_json",     Json.Serialize(items) }
                    };
                    Upsert("cloud_sales", payload);
                }
                catch { /* silent — cloud sync must never crash the app */ }
            });
        }

        /// <summary>
        /// Periodically called to sync the last 50 sales to the cloud to ensure
        /// that any offline sales, edits, or returns are eventually synced.
        /// </summary>
        public static void SyncRecentDataAsync()
        {
            string key = LicenseManager.CurrentLicenseKey;
            if (string.IsNullOrEmpty(key)) return;

            Task.Run(() =>
            {
                try
                {
                    var db = new DbHelper();
                    var dt = db.GetDataTable("SELECT * FROM Sales ORDER BY SaleID DESC LIMIT 50");
                    foreach (DataRow row in dt.Rows)
                    {
                        int saleId = Convert.ToInt32(row["SaleID"]);
                        
                        // Get Customer Name
                        int custId = Convert.ToInt32(row["CustomerID"]);
                        string customerName = "Walk-in Customer";
                        var custDt = db.GetDataTable("SELECT Name FROM Customers WHERE CustomerID = " + custId);
                        if (custDt.Rows.Count > 0) customerName = custDt.Rows[0]["Name"].ToString();

                        // Get Items
                        var itemsDt = db.GetDataTable("SELECT sd.*, m.Name as MedicineName FROM SaleDetails sd LEFT JOIN Medicines m ON sd.MedicineID = m.MedicineID WHERE sd.SaleID = " + saleId);
                        var items = new List<Dictionary<string, object>>();
                        foreach (DataRow iRow in itemsDt.Rows)
                        {
                            items.Add(new Dictionary<string, object>
                            {
                                { "MedicineID",  iRow["MedicineID"] == DBNull.Value ? null : iRow["MedicineID"] },
                                { "MedicineName",iRow["MedicineName"] == DBNull.Value ? null : iRow["MedicineName"] },
                                { "BatchNo",     iRow["BatchNo"] == DBNull.Value ? null : iRow["BatchNo"] },
                                { "RackNo",      iRow["RackNo"] == DBNull.Value ? null : iRow["RackNo"] },
                                { "Quantity",    iRow["Quantity"] == DBNull.Value ? 0 : Convert.ToDouble(iRow["Quantity"]) },
                                { "UnitPrice",   iRow["UnitPrice"] == DBNull.Value ? 0 : Convert.ToDouble(iRow["UnitPrice"]) },
                                { "TotalPrice",  iRow["TotalPrice"] == DBNull.Value ? 0 : Convert.ToDouble(iRow["TotalPrice"]) }
                            });
                        }

                        var payload = new Dictionary<string, object>
                        {
                            { "license_key",    key },
                            { "local_sale_id",  saleId },
                            { "sale_date",      Convert.ToDateTime(row["SaleDate"]).ToString("yyyy-MM-dd HH:mm:ss") },
                            { "customer_name",  customerName },
                            { "total_amount",   Convert.ToDouble(row["TotalAmount"]) },
                            { "discount",       Convert.ToDouble(row["Discount"]) },
                            { "net_paid",       Convert.ToDouble(row["NetPaid"]) },
                            { "status",         row["Status"].ToString() },
                            { "items_json",     items }
                        };
                        
                        try
                        {
                            Upsert("cloud_sales", payload);
                        }
                        catch (Exception ex)
                        {
                            System.IO.File.AppendAllText(@"C:\Users\ma516\OneDrive\Desktop\sync_error.txt", "Sale Sync Error (" + saleId + "): " + ex.ToString() + Environment.NewLine);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.IO.File.AppendAllText(@"C:\Users\ma516\OneDrive\Desktop\sync_error.txt", "Sync Error: " + ex.ToString() + Environment.NewLine);
                }
            });
        }

        public static void DeleteSaleAsync(int saleId)
        {
            string key = LicenseManager.CurrentLicenseKey;
            if (string.IsNullOrEmpty(key)) return;

            Task.Run(() =>
            {
                try
                {
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                    string url = string.Format("{0}/rest/v1/cloud_sales?license_key=eq.{1}&local_sale_id=eq.{2}", 
                        SupabaseUrl, Uri.EscapeDataString(key), saleId);
                    
                    var req = (HttpWebRequest)WebRequest.Create(url);
                    req.Method = "DELETE";
                    req.Timeout = 10000;
                    req.Headers.Add("apikey", SupabaseKey);
                    req.Headers.Add("Authorization", "Bearer " + SupabaseKey);
                    
                    using (req.GetResponse()) { }
                }
                catch { }
            });
        }

        public static void SyncRecentPurchasesAsync()
        {
            string key = LicenseManager.CurrentLicenseKey;
            if (string.IsNullOrEmpty(key)) return;

            Task.Run(() =>
            {
                try
                {
                    var db = new DbHelper();
                    var dt = db.GetDataTable("SELECT * FROM Purchases ORDER BY PurchaseID DESC LIMIT 50");
                    foreach (DataRow row in dt.Rows)
                    {
                        int purchaseId = Convert.ToInt32(row["PurchaseID"]);
                        
                        // Get Supplier Name
                        int suppId = Convert.ToInt32(row["SupplierID"]);
                        string supplierName = "Walk-in Supplier";
                        var suppDt = db.GetDataTable("SELECT Name FROM Suppliers WHERE SupplierID = " + suppId);
                        if (suppDt.Rows.Count > 0) supplierName = suppDt.Rows[0]["Name"].ToString();

                        // Get Items
                        var itemsDt = db.GetDataTable("SELECT pd.*, m.Name as MedicineName FROM PurchaseDetails pd LEFT JOIN Medicines m ON pd.MedicineID = m.MedicineID WHERE pd.PurchaseID = " + purchaseId);
                        var items = new List<Dictionary<string, object>>();
                        foreach (DataRow iRow in itemsDt.Rows)
                        {
                            items.Add(new Dictionary<string, object>
                            {
                                { "MedicineID",  iRow["MedicineID"] == DBNull.Value ? null : iRow["MedicineID"] },
                                { "MedicineName",iRow["MedicineName"] == DBNull.Value ? null : iRow["MedicineName"] },
                                { "BatchNo",     iRow["BatchNo"] == DBNull.Value ? null : iRow["BatchNo"] },
                                { "Quantity",    iRow["Quantity"] == DBNull.Value ? 0 : Convert.ToDouble(iRow["Quantity"]) },
                                { "UnitPrice",   iRow["PurchasePrice"] == DBNull.Value ? 0 : Convert.ToDouble(iRow["PurchasePrice"]) },
                                { "TotalPrice",  iRow["TotalPrice"] == DBNull.Value ? 0 : Convert.ToDouble(iRow["TotalPrice"]) }
                            });
                        }

                        var payload = new Dictionary<string, object>
                        {
                            { "license_key",       key },
                            { "local_purchase_id", purchaseId },
                            { "purchase_date",     Convert.ToDateTime(row["PurchaseDate"]).ToString("yyyy-MM-dd HH:mm:ss") },
                            { "supplier_name",     supplierName },
                            { "invoice_no",        row["InvoiceNo"] == DBNull.Value ? "" : row["InvoiceNo"].ToString() },
                            { "total_amount",      Convert.ToDouble(row["TotalAmount"]) },
                            { "status",            row["Status"].ToString() },
                            { "items_json",        Json.Serialize(items) }
                        };
                        
                        try
                        {
                            Upsert("cloud_purchases", payload);
                        }
                        catch (Exception ex)
                        {
                            System.IO.File.AppendAllText(@"C:\Users\ma516\OneDrive\Desktop\sync_error.txt", "Purchase Sync Error (" + purchaseId + "): " + ex.ToString() + Environment.NewLine);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.IO.File.AppendAllText(@"C:\Users\ma516\OneDrive\Desktop\sync_error.txt", "Sync Error: " + ex.ToString() + Environment.NewLine);
                }
            });
        }

        public static void SyncMetricsAsync()
        {
            string key = LicenseManager.CurrentLicenseKey;
            if (string.IsNullOrEmpty(key)) return;

            Task.Run(() =>
            {
                try
                {
                    var db = new DbHelper();
                    
                    int totalMedicines = Convert.ToInt32(db.ExecuteScalar("SELECT COUNT(*) FROM Medicines"));
                    int lowStockCount = Convert.ToInt32(db.ExecuteScalar("SELECT COUNT(*) FROM (SELECT m.MedicineID, COALESCE(SUM(s.Quantity), 0) as TotalStock, m.MinStock FROM Medicines m LEFT JOIN Stocks s ON m.MedicineID = s.MedicineID GROUP BY m.MedicineID) WHERE TotalStock <= MinStock"));
                    int expiredCount = Convert.ToInt32(db.ExecuteScalar("SELECT COUNT(*) FROM Stocks WHERE ExpiryDate IS NOT NULL AND ExpiryDate != '' AND date(ExpiryDate) < date('now', '+6 months') AND Quantity > 0"));
                    double totalStockValue = Convert.ToDouble(db.ExecuteScalar("SELECT SUM(s.Quantity * m.PurchasePrice) FROM Stocks s JOIN Medicines m ON s.MedicineID = m.MedicineID WHERE s.Quantity > 0") ?? 0);
                    
                    var kpi = new Dictionary<string, object> {
                        { "totalMedicines", totalMedicines },
                        { "lowStockCount", lowStockCount },
                        { "expiredCount", expiredCount },
                        { "totalStockValue", totalStockValue }
                    };

                    var availStockDt = db.GetDataTable(@"
                        SELECT s.MedicineID, m.Name, s.BatchNo, s.ExpiryDate, m.MinStock,
                               SUM(s.Quantity) as TotalStock
                        FROM Stocks s
                        JOIN Medicines m ON s.MedicineID = m.MedicineID
                        GROUP BY s.MedicineID, s.BatchNo, s.ExpiryDate
                        HAVING TotalStock > 0
                        ORDER BY TotalStock DESC
                        LIMIT 200");
                    var availStock = new List<Dictionary<string, object>>();
                    foreach(DataRow r in availStockDt.Rows) {
                        availStock.Add(new Dictionary<string, object> {
                            { "id", r["MedicineID"] }, { "name", r["Name"] },
                            { "generic", "Expiry: " + r["ExpiryDate"] },
                            { "batch", r["BatchNo"] }, { "stock", r["TotalStock"] }, { "min", r["MinStock"] }
                        });
                    }

                    var expiredDt = db.GetDataTable("SELECT s.MedicineID, m.Name, m.GenericFormula, s.BatchNo, s.Quantity, s.ExpiryDate FROM Stocks s JOIN Medicines m ON s.MedicineID = m.MedicineID WHERE s.ExpiryDate IS NOT NULL AND s.ExpiryDate != '' AND date(s.ExpiryDate) < date('now', '+6 months') AND s.Quantity > 0 ORDER BY s.ExpiryDate ASC LIMIT 100");
                    var expired = new List<Dictionary<string, object>>();
                    foreach(DataRow r in expiredDt.Rows) {
                        expired.Add(new Dictionary<string, object> {
                            { "id", r["MedicineID"] }, { "name", r["Name"] }, { "generic", r["GenericFormula"] },
                            { "batch", r["BatchNo"] }, { "stock", r["Quantity"] }, 
                            { "expiry", r["ExpiryDate"] }
                        });
                    }

                    var payload = new Dictionary<string, object>
                    {
                        { "license_key", key },
                        { "kpi_json", kpi },
                        { "low_stock_json", availStock },
                        { "expired_json", expired }
                    };

                    try
                    {
                        Upsert("cloud_metrics", payload);
                    }
                    catch (Exception ex)
                    {
                        System.IO.File.AppendAllText(@"C:\Users\ma516\OneDrive\Desktop\sync_error.txt", "Metrics API Error: " + ex.ToString() + Environment.NewLine);
                    }
                }
                catch (Exception ex)
                {
                    System.IO.File.AppendAllText(@"C:\Users\ma516\OneDrive\Desktop\sync_error.txt", "Metrics DB Error: " + ex.ToString() + Environment.NewLine);
                }
            });
        }

        /// <summary>Upload a completed purchase to the cloud (fire-and-forget).</summary>
        public static void UploadPurchaseAsync(int purchaseId, string supplierName,
                                                string invoiceNo, string purchaseDate,
                                                double total, string status,
                                                List<Dictionary<string, object>> items)
        {
            string key = LicenseManager.CurrentLicenseKey;
            if (string.IsNullOrEmpty(key)) return;

            Task.Run(() =>
            {
                try
                {
                    var payload = new Dictionary<string, object>
                    {
                        { "license_key",       key          },
                        { "local_purchase_id", purchaseId   },
                        { "purchase_date",     purchaseDate },
                        { "supplier_name",     supplierName },
                        { "invoice_no",        invoiceNo    },
                        { "total_amount",      total        },
                        { "status",            status       },
                        { "items_json",        items }
                    };
                    Upsert("cloud_purchases", payload);
                }
                catch { /* silent */ }
            });
        }

        /// <summary>
        /// Called after license activation on a new machine.
        /// Downloads all cloud Sales + Purchases and inserts them into local SQLite.
        /// Returns (salesRestored, purchasesRestored) count.
        /// </summary>
        public static Tuple<int, int> RestoreFromCloud(string licenseKey)
        {
            int salesCount    = 0;
            int purchasesCount = 0;

            try
            {
                var db = new DbHelper();

                // ── RESTORE SALES ─────────────────────────────────────────────
                string salesJson = Get(string.Format("/rest/v1/cloud_sales?license_key=eq.{0}&order=local_sale_id.asc", Uri.EscapeDataString(licenseKey)));
                var salesArray = Json.Deserialize<object[]>(salesJson);

                if (salesArray != null)
                {
                    foreach (var s in salesArray)
                    {
                        var sale = s as Dictionary<string, object>;
                        if (sale == null) continue;

                        int    localSaleId   = Convert.ToInt32(sale["local_sale_id"]);
                        string saleDate      = sale.ContainsKey("sale_date") && sale["sale_date"] != null ? sale["sale_date"].ToString() : DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                        string customerName  = sale.ContainsKey("customer_name") && sale["customer_name"] != null ? sale["customer_name"].ToString() : "Walk-in Customer";
                        double totalAmount   = Convert.ToDouble(sale["total_amount"]);
                        double discount      = Convert.ToDouble(sale["discount"]);
                        double netPaid       = Convert.ToDouble(sale["net_paid"]);
                        string status        = sale.ContainsKey("status") && sale["status"] != null ? sale["status"].ToString() : "Paid";
                        string itemsJson     = sale.ContainsKey("items_json") && sale["items_json"] != null ? sale["items_json"].ToString() : "[]";

                        // Skip if this sale already exists locally
                        object existing = db.ExecuteScalar("SELECT COUNT(*) FROM Sales WHERE SaleID = @ID",
                            new SQLiteParameter[] { new SQLiteParameter("@ID", localSaleId) });
                        if (Convert.ToInt32(existing) > 0) continue;

                        // Get or create customer
                        object custIdObj = db.ExecuteScalar(
                            "SELECT CustomerID FROM Customers WHERE Name = @N LIMIT 1",
                            new SQLiteParameter[] { new SQLiteParameter("@N", customerName) });

                        int custId;
                        if (custIdObj == null || custIdObj == DBNull.Value)
                        {
                            db.ExecuteNonQuery(
                                "INSERT INTO Customers (Name, Contact, Email, Address, Balance) VALUES (@N,'','','',0)",
                                new SQLiteParameter[] { new SQLiteParameter("@N", customerName) });
                            custId = Convert.ToInt32(db.ExecuteScalar("SELECT last_insert_rowid()"));
                        }
                        else
                        {
                            custId = Convert.ToInt32(custIdObj);
                        }

                        // Insert the Sale with its original SaleID preserved
                        db.ExecuteNonQuery(
                            @"INSERT OR IGNORE INTO Sales (SaleID, CustomerID, SaleDate, TotalAmount, Discount, NetPaid, Status)
                              VALUES (@ID, @CustId, @Date, @Total, @Disc, @Net, @Status)",
                            new SQLiteParameter[]
                            {
                                new SQLiteParameter("@ID",     localSaleId),
                                new SQLiteParameter("@CustId", custId),
                                new SQLiteParameter("@Date",   saleDate),
                                new SQLiteParameter("@Total",  totalAmount),
                                new SQLiteParameter("@Disc",   discount),
                                new SQLiteParameter("@Net",    netPaid),
                                new SQLiteParameter("@Status", status)
                            });

                        // Insert SaleDetails items
                        var items = Json.Deserialize<object[]>(itemsJson);
                        if (items != null)
                        {
                            foreach (var i in items)
                            {
                                var item = i as Dictionary<string, object>;
                                if (item == null) continue;
                                db.ExecuteNonQuery(
                                    @"INSERT OR IGNORE INTO SaleDetails (SaleID, MedicineID, BatchNo, RackNo, Quantity, UnitPrice, TotalPrice)
                                      VALUES (@SID, @MID, @Batch, @Box, @Qty, @Price, @Total)",
                                    new SQLiteParameter[]
                                    {
                                        new SQLiteParameter("@SID",   localSaleId),
                                        new SQLiteParameter("@MID",   item.ContainsKey("MedicineID") && item["MedicineID"] != null ? (object)Convert.ToInt32(item["MedicineID"]) : DBNull.Value),
                                        new SQLiteParameter("@Batch", item.ContainsKey("BatchNo") && item["BatchNo"] != null ? item["BatchNo"].ToString()  : "Default"),
                                        new SQLiteParameter("@Box",   item.ContainsKey("RackNo") && item["RackNo"] != null ? item["RackNo"].ToString()   : "Rack-1"),
                                        new SQLiteParameter("@Qty",   item.ContainsKey("Quantity") && item["Quantity"] != null ? (object)Convert.ToDouble(item["Quantity"]) : 0),
                                        new SQLiteParameter("@Price", item.ContainsKey("UnitPrice") && item["UnitPrice"] != null ? (object)Convert.ToDouble(item["UnitPrice"]) : 0),
                                        new SQLiteParameter("@Total", item.ContainsKey("TotalPrice") && item["TotalPrice"] != null ? (object)Convert.ToDouble(item["TotalPrice"]) : 0)
                                    });
                            }
                        }

                        salesCount++;
                    }
                }

                // ── RESTORE PURCHASES ─────────────────────────────────────────
                string purchJson = Get(string.Format("/rest/v1/cloud_purchases?license_key=eq.{0}&order=local_purchase_id.asc", Uri.EscapeDataString(licenseKey)));
                var purchArray = Json.Deserialize<object[]>(purchJson);

                if (purchArray != null)
                {
                    foreach (var p in purchArray)
                    {
                        var purch = p as Dictionary<string, object>;
                        if (purch == null) continue;

                        int    localPurchId  = Convert.ToInt32(purch["local_purchase_id"]);
                        string purchDate     = purch.ContainsKey("purchase_date") && purch["purchase_date"] != null ? purch["purchase_date"].ToString() : DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                        string supplierName  = purch.ContainsKey("supplier_name") && purch["supplier_name"] != null ? purch["supplier_name"].ToString() : "Unknown";
                        string invoiceNo     = purch.ContainsKey("invoice_no") && purch["invoice_no"] != null ? purch["invoice_no"].ToString() : "";
                        double totalAmount   = Convert.ToDouble(purch["total_amount"]);
                        string status        = purch.ContainsKey("status") && purch["status"] != null ? purch["status"].ToString() : "Received";
                        string itemsJson     = purch.ContainsKey("items_json") && purch["items_json"] != null ? purch["items_json"].ToString() : "[]";

                        // Skip if exists locally
                        object existing = db.ExecuteScalar("SELECT COUNT(*) FROM Purchases WHERE PurchaseID = @ID",
                            new SQLiteParameter[] { new SQLiteParameter("@ID", localPurchId) });
                        if (Convert.ToInt32(existing) > 0) continue;

                        // Get or create supplier
                        object suppIdObj = db.ExecuteScalar(
                            "SELECT SupplierID FROM Suppliers WHERE Name = @N LIMIT 1",
                            new SQLiteParameter[] { new SQLiteParameter("@N", supplierName) });

                        int suppId;
                        if (suppIdObj == null || suppIdObj == DBNull.Value)
                        {
                            db.ExecuteNonQuery(
                                "INSERT INTO Suppliers (Name, Contact, Email, Address) VALUES (@N,'','','')",
                                new SQLiteParameter[] { new SQLiteParameter("@N", supplierName) });
                            suppId = Convert.ToInt32(db.ExecuteScalar("SELECT last_insert_rowid()"));
                        }
                        else
                        {
                            suppId = Convert.ToInt32(suppIdObj);
                        }

                        // Insert Purchase with original ID preserved
                        db.ExecuteNonQuery(
                            @"INSERT OR IGNORE INTO Purchases (PurchaseID, SupplierID, InvoiceNo, PurchaseDate, TotalAmount, Status, PurchaseType)
                              VALUES (@ID, @SuppId, @Inv, @Date, @Total, @Status, 'Normal')",
                            new SQLiteParameter[]
                            {
                                new SQLiteParameter("@ID",     localPurchId),
                                new SQLiteParameter("@SuppId", suppId),
                                new SQLiteParameter("@Inv",    invoiceNo),
                                new SQLiteParameter("@Date",   purchDate),
                                new SQLiteParameter("@Total",  totalAmount),
                                new SQLiteParameter("@Status", status)
                            });

                        // Insert PurchaseDetails
                        var items = Json.Deserialize<object[]>(itemsJson);
                        if (items != null)
                        {
                            foreach (var i in items)
                            {
                                var item = i as Dictionary<string, object>;
                                if (item == null) continue;
                                db.ExecuteNonQuery(
                                    @"INSERT OR IGNORE INTO PurchaseDetails (PurchaseID, MedicineID, BatchNo, ExpiryDate, Quantity, PurchasePrice, TotalPrice)
                                      VALUES (@PID, @MID, @Batch, @Exp, @Qty, @Price, @Total)",
                                    new SQLiteParameter[]
                                    {
                                        new SQLiteParameter("@PID",   localPurchId),
                                        new SQLiteParameter("@MID",   item.ContainsKey("MedicineID") && item["MedicineID"] != null ? (object)Convert.ToInt32(item["MedicineID"]) : DBNull.Value),
                                        new SQLiteParameter("@Batch", item.ContainsKey("BatchNo") && item["BatchNo"] != null ? item["BatchNo"].ToString() : "Default"),
                                        new SQLiteParameter("@Exp",   item.ContainsKey("ExpiryDate") && item["ExpiryDate"] != null ? item["ExpiryDate"].ToString() : ""),
                                        new SQLiteParameter("@Qty",   item.ContainsKey("Quantity") && item["Quantity"] != null ? (object)Convert.ToDouble(item["Quantity"]) : 0),
                                        new SQLiteParameter("@Price", item.ContainsKey("PurchasePrice") && item["PurchasePrice"] != null ? (object)Convert.ToDouble(item["PurchasePrice"]) : 0),
                                        new SQLiteParameter("@Total", item.ContainsKey("TotalPrice") && item["TotalPrice"] != null ? (object)Convert.ToDouble(item["TotalPrice"]) : 0)
                                    });
                            }
                        }

                        purchasesCount++;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("CloudSync RestoreFromCloud error: " + ex.Message);
            }

            return new Tuple<int, int>(salesCount, purchasesCount);
        }

        // ── HTTP HELPERS ──────────────────────────────────────────────────────

        private static void Upsert(string table, Dictionary<string, object> payload)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            string json = Json.Serialize(payload);
            byte[] data = Encoding.UTF8.GetBytes(json);

            string url = SupabaseUrl + "/rest/v1/" + table;
            
            if (table == "cloud_sales")
                url += "?on_conflict=license_key,local_sale_id";
            else if (table == "cloud_purchases")
                url += "?on_conflict=license_key,local_purchase_id";
            else if (table == "cloud_metrics")
                url += "?on_conflict=license_key";

            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method         = "POST";
            req.ContentType    = "application/json";
            req.ContentLength  = data.Length;
            req.Timeout        = 10000;
            req.Headers.Add("apikey",         SupabaseKey);
            req.Headers.Add("Authorization",  "Bearer " + SupabaseKey);
            req.Headers.Add("Prefer",         "resolution=merge-duplicates,return=minimal");

            using (var stream = req.GetRequestStream())
                stream.Write(data, 0, data.Length);

            using (req.GetResponse()) { }   // consume — no body needed
        }

        private static string Get(string endpoint)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            var req = (HttpWebRequest)WebRequest.Create(SupabaseUrl + endpoint);
            req.Method  = "GET";
            req.Timeout = 15000;
            req.Headers.Add("apikey",        SupabaseKey);
            req.Headers.Add("Authorization", "Bearer " + SupabaseKey);

            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var reader = new StreamReader(resp.GetResponseStream()))
                return reader.ReadToEnd();
        }
    }
}
