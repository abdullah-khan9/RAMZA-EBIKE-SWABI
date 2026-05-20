using System.Windows;
using System.Windows.Controls;
using Ramza_EBike_Swabi.Services;
using Ramza_EBike_Swabi.Models;

namespace Ramza_EBike_Swabi.Views.Pages
{
    public partial class VendorForm : Page
    {
        private readonly VendorService _service = new();
        private readonly MainLayout _layout;
        private readonly int? _vendorId;

        public VendorForm(MainLayout layout, int? vendorId = null)
        {
            InitializeComponent();
            _layout = layout;
            _vendorId = vendorId;

            if (_vendorId != null)
            {
                LoadVendor(_vendorId.Value);
            }
        }

        private async void LoadVendor(int id)
        {
            var vendor = await _service.GetVendorAsync(id);
            if (vendor == null)
            {
                MessageBox.Show("Vendor not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _layout.Navigate(new VendorPage(_layout));
                return;
            }

            VendorNameBox.Text = vendor.VendorName;
            PhoneBox.Text = vendor.Phone;
            AddressBox.Text = vendor.Address;
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(VendorNameBox.Text))
            {
                MessageBox.Show("Vendor name is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                VendorNameBox.Focus();
                return;
            }

            if (_vendorId == null)
            {
                var vendor = new Vendor
                {
                    VendorName = VendorNameBox.Text.Trim(),
                    Phone = PhoneBox.Text?.Trim(),
                    Address = AddressBox.Text?.Trim()
                };

                await _service.AddVendorAsync(vendor);
                MessageBox.Show("Vendor added successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                var existing = await _service.GetVendorAsync(_vendorId.Value);
                if (existing == null)
                {
                    MessageBox.Show("Vendor not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    _layout.Navigate(new VendorPage(_layout));
                    return;
                }

                existing.VendorName = VendorNameBox.Text.Trim();
                existing.Phone = PhoneBox.Text?.Trim();
                existing.Address = AddressBox.Text?.Trim();

                await _service.UpdateVendorAsync(existing);
                MessageBox.Show("Vendor updated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            _layout.Navigate(new VendorPage(_layout));
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            _layout.Navigate(new VendorPage(_layout));
        }
    }
}
