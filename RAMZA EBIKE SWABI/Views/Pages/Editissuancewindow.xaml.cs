// EditIssuanceWindow.xaml.cs
using System;
using System.Collections.Generic;
using System.Windows;
using Ramza_EBike_Swabi.Services;

namespace Ramza_EBike_Swabi.Views.Pages
{
    public partial class EditIssuanceWindow : Window
    {
        private readonly int _recordId;
        private readonly DocumentIssuanceService _service = new();

        public event Action? RecordUpdated;

        public EditIssuanceWindow(HistoryRowViewModel row)
        {
            InitializeComponent();

            _recordId = row.RecordId;

            lblSubtitle.Text = $"Record ID: {row.RecordId}  •  Originally issued on {row.DateDisplay}";
            txtIssuedBy.Text = row.IssuedBy;
            txtReceivedBy.Text = row.ReceivedBy;
            txtNotes.Text = row.Notes;
            dpDate.SelectedDate = row.IssuanceDate.Date;
            txtTime.Text = row.IssuanceDate.ToString("hh:mm tt");

            // Pre-tick the checkboxes that were originally checked
            chkWarranty.IsChecked = row.WarrantyCardIssued;
            chkVoucherCust.IsChecked = row.VoucherCustomerIssued;
            chkVoucherComp.IsChecked = row.VoucherCompanyIssued;
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            bool warranty = chkWarranty.IsChecked == true;
            bool voucherCust = chkVoucherCust.IsChecked == true;
            bool voucherComp = chkVoucherComp.IsChecked == true;

            if (!warranty && !voucherCust && !voucherComp)
            {
                MessageBox.Show("At least one document must remain checked.\n\n" +
                    "If no document was given, delete this record instead.",
                    "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(txtIssuedBy.Text))
            {
                MessageBox.Show("Please enter who issued the document.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(txtReceivedBy.Text))
            {
                MessageBox.Show("Please enter who received the document.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (dpDate.SelectedDate == null)
            {
                MessageBox.Show("Please select a date.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Parse time — fallback to current time if unreadable
            TimeSpan time;
            if (!DateTime.TryParse(txtTime.Text.Trim(), out DateTime parsedDt))
            {
                MessageBox.Show("Time format not recognised. Use e.g. 02:30 PM or 14:30.",
                    "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            time = parsedDt.TimeOfDay;

            DateTime newDateTime = dpDate.SelectedDate!.Value.Date + time;

            await _service.UpdateIssuanceAsync(
                _recordId,
                warranty, voucherCust, voucherComp,
                txtIssuedBy.Text.Trim(),
                txtReceivedBy.Text.Trim(),
                newDateTime,
                txtNotes.Text?.Trim() ?? string.Empty);

            MessageBox.Show("Record updated successfully.", "Saved",
                MessageBoxButton.OK, MessageBoxImage.Information);

            RecordUpdated?.Invoke();
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => Close();
    }
}