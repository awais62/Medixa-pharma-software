using System.Windows;
using PharmaBilling.Source.ViewModels;
using PharmaBilling.Source.Data;

namespace PharmaBilling.Source.Views
{
    public partial class SaleReturnWindow : Window
    {
        private SaleReturnViewModel _vm;

        public SaleReturnWindow()
        {
            InitializeComponent();
            _vm = new SaleReturnViewModel();
            this.DataContext = _vm;
        }

        private void SaveReturn_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.SelectedSale == null)
            {
                MessageBox.Show("Please select a sale invoice first.", "Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            double total = _vm.TotalAmount;
            if (total <= 0)
            {
                MessageBox.Show("Please enter return quantity (> 0) for at least one item.", "No Items", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show(
                string.Format("Process sale return of Rs. {0:N2}?\nStock will be restored and ledger will be updated.", total),
                "Confirm Return", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                bool ok = _vm.SaveReturn();
                if (ok)
                {
                    // ── Notify cloud sync immediately ────────────────────────
                    AppEvents.OnSaleDataChanged();

                    MessageBox.Show("Sale return processed successfully!\nStock restored and ledger posted.",
                        "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    this.DialogResult = true;
                    this.Close();
                }
                else
                {
                    MessageBox.Show("An error occurred while processing the return. Please try again.",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
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

