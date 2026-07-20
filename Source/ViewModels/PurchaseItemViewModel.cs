using System;

namespace PharmaBilling.Source.ViewModels
{
    public class PurchaseItemViewModel : BaseViewModel
    {
        // When true, suppresses intermediate recalculations while
        // multiple fields are being set at once (e.g., during Edit load).
        private bool _isLoading = false;

        private int _medicineID;
        public int MedicineID
        {
            get { return _medicineID; }
            set { _medicineID = value; OnPropertyChanged("MedicineID"); }
        }

        private string _medicineName;
        public string MedicineName
        {
            get { return _medicineName; }
            set { _medicineName = value; OnPropertyChanged("MedicineName"); }
        }

        private string _batchNo;
        public string BatchNo
        {
            get { return _batchNo; }
            set { _batchNo = value; OnPropertyChanged("BatchNo"); }
        }

        private string _rackNo;
        public string RackNo
        {
            get { return _rackNo; }
            set { _rackNo = value; OnPropertyChanged("RackNo"); }
        }

        private string _expiryDate;
        public string ExpiryDate
        {
            get { return _expiryDate; }
            set { _expiryDate = value; OnPropertyChanged("ExpiryDate"); }
        }

        // ── PACKS & PACK SIZE (Auto-calculate Quantity) ───────────────────────
        private string _packsStr = "1";
        public string PacksStr
        {
            get { return _packsStr; }
            set
            {
                _packsStr = value;
                OnPropertyChanged("PacksStr");
                OnPropertyChanged("Packs");
                if (!_isLoading) RecalculateQuantity();
            }
        }
        /// <summary>Number of packs/boxes being purchased.</summary>
        public double Packs
        {
            get { return double.TryParse(_packsStr, out double v) ? (v < 0 ? 0 : v) : 0; }
            set { PacksStr = value.ToString(); }
        }

        private string _packSizeStr = "1";
        public string PackSizeStr
        {
            get { return _packSizeStr; }
            set
            {
                _packSizeStr = value;
                OnPropertyChanged("PackSizeStr");
                OnPropertyChanged("PackSize");
                if (!_isLoading) { RecalculateQuantity(); RecalculateRetailQty(); }
            }
        }
        /// <summary>Units per pack (e.g. 10 tablets per strip, 30 strips per box).</summary>
        public double PackSize
        {
            get { return double.TryParse(_packSizeStr, out double v) ? (v < 1 ? 1 : v) : 1; }
            set { PackSizeStr = value.ToString(); }
        }

        private string _looseQtyStr = "0";
        public string LooseQtyStr
        {
            get { return _looseQtyStr; }
            set
            {
                _looseQtyStr = value;
                OnPropertyChanged("LooseQtyStr");
                OnPropertyChanged("LooseQty");
                if (!_isLoading) RecalculateQuantity();
            }
        }
        /// <summary>Loose units being purchased alongside full packs.</summary>
        public double LooseQty
        {
            get { return double.TryParse(_looseQtyStr, out double v) ? (v < 0 ? 0 : v) : 0; }
            set { LooseQtyStr = value.ToString(); }
        }

        private string _quantityStr = "1";
        public string QuantityStr
        {
            get { return _quantityStr; }
            set
            {
                _quantityStr = value;
                OnPropertyChanged("QuantityStr");
                OnPropertyChanged("Quantity");
                if (!_isLoading) CalculateTotal();
            }
        }
        /// <summary>Total units = (Packs x PackSize) + LooseQty. Auto-updated.</summary>
        public double Quantity
        {
            get { return double.TryParse(_quantityStr, out double v) ? v : 0; }
            set { QuantityStr = value.ToString(); }
        }

        private void RecalculateQuantity()
        {
            double q = (Packs * PackSize) + LooseQty;
            _quantityStr = q.ToString();
            OnPropertyChanged("QuantityStr");
            OnPropertyChanged("Quantity");
            RecalculateRetailQty();
            CalculateTotal();
        }

        private void RecalculateRetailQty()
        {
            if (PackSize > 0)
            {
                RetailQtyStr = (Retail / PackSize).ToString("0.##");
            }
        }

        // ── PRICING ───────────────────────────────────────────────────────────
        // RULE: TP always means the price for ONE FULL PACK.
        //       e.g. A strip of 10 tablets costs Rs.50 → TP = 50, PackSize = 10.
        private string _tpStr = "0";
        public string TPStr
        {
            get { return _tpStr; }
            set
            {
                _tpStr = value;
                OnPropertyChanged("TPStr");
                OnPropertyChanged("TP");
                if (!_isLoading) CalculateTotal();
            }
        }
        public double TP
        {
            get { return double.TryParse(_tpStr, out double v) ? v : 0; }
            set { TPStr = value.ToString(); }
        }

        private string _retailStr = "0";
        public string RetailStr
        {
            get { return _retailStr; }
            set
            {
                _retailStr = value;
                OnPropertyChanged("RetailStr");
                OnPropertyChanged("Retail");
                if (!_isLoading) RecalculateRetailQty();
            }
        }
        public double Retail
        {
            get { return double.TryParse(_retailStr, out double v) ? v : 0; }
            set { RetailStr = value.ToString(); }
        }

        private string _retailQtyStr = "0";
        public string RetailQtyStr
        {
            get { return _retailQtyStr; }
            set
            {
                _retailQtyStr = value;
                OnPropertyChanged("RetailQtyStr");
                OnPropertyChanged("RetailQty");
            }
        }
        public double RetailQty
        {
            get { return double.TryParse(_retailQtyStr, out double v) ? v : 0; }
            set { RetailQtyStr = value.ToString(); }
        }

        private double _totalPrice;
        public double TotalPrice
        {
            get { return _totalPrice; }
            set { _totalPrice = value; OnPropertyChanged("TotalPrice"); }
        }

        /// <summary>
        /// Call this before setting multiple fields at once (e.g., in Edit load)
        /// to suppress intermediate recalculations that fire with stale data.
        /// Always call EndBatchUpdate() after to finalize the calculation.
        /// </summary>
        public void BeginBatchUpdate() { _isLoading = true; }

        /// <summary>
        /// Call this after all fields have been set in a batch.
        /// Runs one clean recalculation pass.
        /// </summary>
        public void EndBatchUpdate()
        {
            _isLoading = false;
            RecalculateQuantity(); // Internally calls CalculateTotal at the end
        }

        /// <summary>
        /// End batch update but use the stored DB total directly.
        /// This guarantees edit-window total = list-view total = DB total.
        /// No recalculation — we trust the saved data.
        /// </summary>
        public void EndBatchUpdateWithTotal(double storedTotal)
        {
            _isLoading = false;
            // Update Quantity display without triggering CalculateTotal
            double q = (Packs * PackSize) + LooseQty;
            _quantityStr = q > 0 ? q.ToString() : _quantityStr;
            OnPropertyChanged("QuantityStr");
            OnPropertyChanged("Quantity");
            // Use stored total directly — never recalculate on load
            TotalPrice = storedTotal;
            RecalculateRetailQty();
        }

        private void CalculateTotal()
        {
            // ── OFFICIAL FORMULA (agreed) ─────────────────────────────────────
            // Normal Purchase: Total = Packs × TP
            // Loose Purchase (Packs = 0): Total = Quantity × TP  (LoosePurchaseWindow binds to QuantityStr)
            if (Packs > 0)
                TotalPrice = Math.Round(TP * Packs, 2);
            else
                TotalPrice = Math.Round(TP * Quantity, 2);
        }
    }
}
