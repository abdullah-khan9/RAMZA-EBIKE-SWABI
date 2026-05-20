using System.Windows;
using System.Windows.Controls;
using Ramza_EBike_Swabi.Services;
using Ramza_EBike_Swabi.Models;

namespace Ramza_EBike_Swabi.Views.Pages
{
    public partial class VendorPage : Page
    {
        private readonly VendorService _service = new();
        private readonly MainLayout _layout;

        public VendorPage(MainLayout layout)
        {
            InitializeComponent();
            _layout = layout;
            LoadVendors();
        }

        private async void LoadVendors(string? search = null)
        {
            VendorGrid.ItemsSource = await _service.GetVendorsAsync(search);
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            _layout.Navigate(new DashboardPage(_layout));
        }

        private void Search_Click(object sender, RoutedEventArgs e) => LoadVendors(SearchBox.Text);

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => LoadVendors(SearchBox.Text);

        private void AddVendor_Click(object sender, RoutedEventArgs e) => _layout.Navigate(new VendorForm(_layout));

        private void EditVendor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Vendor vendor)
            {
                _layout.Navigate(new VendorForm(_layout, vendor.Id));
            }
        }

        private void ViewPurchases_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Vendor vendor)
            {
                _layout.Navigate(new VendorPurchasePage(_layout, vendor.Id));
            }
        }

        private async void DeleteVendor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Vendor vendor)
            {
                string name = vendor.VendorName ?? "Vendor";

                if (MessageBox.Show($"Delete vendor '{name}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    await _service.DeleteVendorAsync(vendor.Id);
                    LoadVendors(SearchBox.Text);
                }
            }
        }
    }
}
