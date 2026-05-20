// DashboardPage.xaml.cs — UPDATED
// Change: AddCash_Click now navigates to VendorCashPage (full page) instead of popup

using System;
using System.Windows;
using System.Windows.Controls;

namespace Ramza_EBike_Swabi.Views.Pages
{
    public partial class DashboardPage : Page
    {
        private readonly MainLayout _mainLayout;

        public DashboardPage(MainLayout mainLayout)
        {
            InitializeComponent();
            _mainLayout = mainLayout;
        }

        private void Bikes_Click(object sender, RoutedEventArgs e)
            => _mainLayout.Navigate(new BikePage(_mainLayout));

        private void Vendors_Click(object sender, RoutedEventArgs e)
            => _mainLayout.Navigate(new VendorPage(_mainLayout));

        private void Invoice_Click(object sender, RoutedEventArgs e)
        {
            if (_mainLayout.InvoiceTabManager == null)
                _mainLayout.InvoiceTabManager = new InvoiceTabManagerPage(_mainLayout);
            _mainLayout.Navigate(_mainLayout.InvoiceTabManager);
        }

        private void SearchInvoice_Click(object sender, RoutedEventArgs e)
            => _mainLayout.Navigate(new SearchInvoicePage(_mainLayout));

        private void Dues_Click(object sender, RoutedEventArgs e)
            => _mainLayout.Navigate(new CustomerDuesPage());

        private void Report_Click(object sender, RoutedEventArgs e)
            => _mainLayout.Navigate(new SalesReportPage(_mainLayout));

        private void Account_Click(object sender, RoutedEventArgs e)
            => _mainLayout.Navigate(new AccountPage(_mainLayout));

        // ✅ UPDATED: Opens full VendorCashPage instead of popup
        private void AddCash_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _mainLayout.Navigate(new VendorCashPage(_mainLayout));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening Vendor Cash:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Incentives_Click(object sender, RoutedEventArgs e)
            => _mainLayout.Navigate(new VendorIncentivesPage(_mainLayout));

        private void Tracking_Click(object sender, RoutedEventArgs e)
            => _mainLayout.Navigate(new TrackingPage());
        private void Zakat_Click(object sender, RoutedEventArgs e)
    => _mainLayout.Navigate(new ZakatPage(_mainLayout));
    }
}