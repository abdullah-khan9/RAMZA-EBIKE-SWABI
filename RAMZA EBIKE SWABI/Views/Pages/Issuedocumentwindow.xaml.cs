// IssueDocumentWindow.xaml.cs
using System;
using System.Linq;
using System.Windows;
using Microsoft.EntityFrameworkCore;
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

            try
            {
                // Save document issuance record
                await _service.AddIssuanceAsync(record);

                // ✅ Update invoice checklist and regenerate PDF if requested
                bool applyToPdf = chkApplyToPdf.IsChecked == true;

                using (var db = new Data.AppDbContext())
                {
                    var invoice = db.CustomerInvoices
                        .Include(i => i.Customer)
                        .Include(i => i.Items)
                        .FirstOrDefault(i => i.CustomerInvoiceId == _invoiceId);

                    if (invoice != null)
                    {
                        // Update checklist flags in database
                        if (warranty)
                            invoice.WarrantyCardGiven = true;
                        if (voucherCust)
                            invoice.VoucherGivenToCustomer = true;
                        if (voucherComp)
                            invoice.VoucherIssuedByCompany = true;

                        db.SaveChanges();

                        // ✅ Regenerate PDF only if user wants to apply changes
                        if (applyToPdf)
                        {
                            try
                            {
                                Services.Pdf.InvoicePdfService.RegenerateWithoutOpening(invoice);

                                MessageBox.Show(
                                    "Document issuance recorded successfully!\n\n" +
                                    "✓ Invoice checklist updated\n" +
                                    "✓ PDF regenerated with document history",
                                    "Success",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information);
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show(
                                    $"Document issuance recorded, but PDF regeneration failed:\n{ex.Message}\n\n" +
                                    "You can regenerate the PDF manually from the invoice page.",
                                    "Partial Success",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Warning);
                            }
                        }
                        else
                        {
                            MessageBox.Show(
                                "Document issuance recorded successfully!\n\n" +
                                "Note: PDF was not updated. You can regenerate it later if needed.",
                                "Success",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                        }
                    }
                }

                DocumentIssued?.Invoke();
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => Close();
    }
}