using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using PharmaBilling.Source.Data;
using PharmaBilling.Source.Models;
using PharmaBilling.Source.ViewModels;

namespace PharmaBilling.Source.Views
{
    public partial class LoosePurchaseWindow : Window
    {
        private PurchaseViewModel _vm;
        private DispatcherTimer _debounceTimer;

        public LoosePurchaseWindow()
        {
            InitializeComponent();
            _vm = new PurchaseViewModel();
            _vm.InvoiceNo = "LOOSE-" + DateTime.Now.ToString("yyyyMMddHHmm");
            this.DataContext = _vm;

            _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _debounceTimer.Tick += async (s, e) =>
            {
                _debounceTimer.Stop();
                var cbo = cboMedicines;
                var tb = cbo.Template.FindName("PART_EditableTextBox", cbo) as TextBox;
                if (tb != null)
                {
                    string text = tb.Text;
                    int caret = tb.CaretIndex;
                    await _vm.FilterMedicinesAsync(text);
                    if (!cbo.IsDropDownOpen) cbo.IsDropDownOpen = true;
                    if (tb.Text != text) { tb.Text = text; tb.CaretIndex = caret; }
                }
            };
        }

        private void cboMedicines_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Up   || e.Key == System.Windows.Input.Key.Down  ||
                e.Key == System.Windows.Input.Key.Escape || e.Key == System.Windows.Input.Key.Tab) return;
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                AddItem_Click(null, null);
                return;
            }
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        private void AddItem_Click(object sender, RoutedEventArgs e)
        {
            Medicine selected = cboMedicines.SelectedItem as Medicine;

            if (selected == null && !string.IsNullOrWhiteSpace(cboMedicines.Text))
            {
                foreach (Medicine m in _vm.AllMedicines)
                {
                    if (m.Name.Equals(cboMedicines.Text, StringComparison.OrdinalIgnoreCase))
                    { selected = m; break; }
                }
                if (selected == null)
                {
                    if (MessageBox.Show("'" + cboMedicines.Text + "' not found. Create it?",
                        "New Medicine", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                        selected = _vm.CreateQuickMedicine(cboMedicines.Text);
                }
            }

            if (selected != null)
            {
                // For loose purchase: add item with Packs=0, PackSize=1, LooseQty=1 → Qty=1
                var item = new PurchaseItemViewModel
                {
                    MedicineID   = selected.MedicineID,
                    MedicineName = selected.Name,
                    Packs        = 0,
                    PackSize     = 1,
                    LooseQty     = 1,
                    TP           = selected.PurchasePrice,
                    Retail       = selected.SalePrice,
                    RetailQty    = selected.SalePrice,
                    BatchNo      = "LOOSE-" + DateTime.Now.ToString("MMdd"),
                    RackNo       = "A-1",
                    ExpiryDate   = DateTime.Now.AddYears(1).ToString("yyyy-MM-dd")
                };
                _vm.PurchaseItems.Add(item);
                cboMedicines.SelectedItem = null;
                cboMedicines.Text = "";
            }
            else
            {
                MessageBox.Show("Please select a valid medicine.", "Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void RemoveItem_Click(object sender, RoutedEventArgs e)
        {
            var btn = (Button)sender;
            var item = btn.DataContext as PurchaseItemViewModel;
            if (item != null) _vm.RemoveFromPurchase(item);
        }

        private void SavePurchase_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.SelectedSupplier == null && !string.IsNullOrWhiteSpace(cboSupplier.Text))
                _vm.SelectedSupplier = _vm.CreateQuickSupplier(cboSupplier.Text);

            if (_vm.PurchaseItems.Count == 0)
            {
                MessageBox.Show("Add at least one medicine item.", "No Items", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show("Save this loose purchase? Stock will be updated.",
                "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                bool ok = _vm.SavePurchaseWithType("Loose");
                if (ok)
                {
                    // ── Notify cloud sync immediately ────────────────────────
                    AppEvents.OnPurchaseDataChanged();

                    MessageBox.Show("Loose purchase saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    this.DialogResult = true;
                    this.Close();
                }
                else
                    MessageBox.Show("Error saving purchase.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                if (cboMedicines.IsDropDownOpen || cboSupplier.IsDropDownOpen)
                {
                    cboMedicines.IsDropDownOpen = false;
                    cboSupplier.IsDropDownOpen = false;
                }
                else
                {
                    this.Close();
                }
            }
        }
        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (!e.Handled && e.Key == System.Windows.Input.Key.Escape)
            {
                this.Close();
                e.Handled = true;
            }
        }
    }
}

