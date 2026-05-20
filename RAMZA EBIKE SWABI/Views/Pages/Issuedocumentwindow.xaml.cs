// IssueDocumentWindow.xaml.cs
using System;
using System.Windows;
using Ramza_EBike_Swabi.Models;
using Ramza_EBike_Swabi.Services;

namespace Ramza_EBike_Swabi.Views.Pages
{
    public partial class IssueDocumentWindow : Window
    {
        private readonly int _invoiceId;
        private readonly DocumentIssuanceService _service = new();

        public event Action? DocumentIssued;

        /// <summary>
        /// needsWarranty / needsVoucherCust / needsVoucherComp control which
        /// checkboxes are visible — only undelivered docs are offered.
        /// </summary>
        public IssueDocumentWindow(int invoiceId, string customerName,
            bool needsWarranty, bool needsVoucherCust, bool needsVoucherComp)
        {
            InitializeComponent();
            _invoiceId = invoiceId;

            lblTitle.Text = $"Issue Document — {customerName}";
            lblSubtitle.Text = $"Invoice #INV-{invoiceId:D4}   •   Only pending documents are shown below.";
            txtDateTime.Text = DateTime.Now.ToString("dd MMM yyyy  hh:mm tt");

            // Show only the documents not yet delivered
            chkWarranty.Visibility = needsWarranty ? Visibility.Visible : Visibility.Collapsed;
            chkVoucherCust.Visibility = needsVoucherCust ? Visibility.Visible : Visibility.Collapsed;
            chkVoucherComp.Visibility = needsVoucherComp ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void BtnSubmit_Click(object sender, RoutedEventArgs e)
        {
            bool warranty = chkWarranty.IsChecked == true && chkWarranty.IsVisible;
            bool voucherCust = chkVoucherCust.IsChecked == true && chkVoucherCust.IsVisible;
            bool voucherComp = chkVoucherComp.IsChecked == true && chkVoucherComp.IsVisible;

            if (!warranty && !voucherCust && !voucherComp)
            {
                MessageBox.Show("Please select at least one document to issue.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
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

            var record = new DocumentIssuanceRecord
            {
                CustomerInvoiceId = _invoiceId,
                WarrantyCardIssued = warranty,
                VoucherCustomerIssued = voucherCust,
                VoucherCompanyIssued = voucherComp,
                IssuedBy = txtIssuedBy.Text.Trim(),
                ReceivedBy = txtReceivedBy.Text.Trim(),
                IssuanceDate = DateTime.Now,
                Notes = txtNotes.Text?.Trim() ?? string.Empty
            };

            await _service.AddIssuanceAsync(record);

            MessageBox.Show("Document issuance recorded successfully.", "Success",
                MessageBoxButton.OK, MessageBoxImage.Information);

            DocumentIssued?.Invoke();
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => Close();
    }
}