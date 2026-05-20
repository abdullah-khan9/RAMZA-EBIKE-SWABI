using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using Ramza_EBike_Swabi.Data;
using Ramza_EBike_Swabi.Models;
using Ramza_EBike_Swabi.Services;

namespace Ramza_EBike_Swabi.Views.Pages
{
    public partial class PayDueWindow : Window
    {
        private readonly CustomerInvoice _invoice;
        private readonly AccountService _accountService = new();

        public PayDueWindow(CustomerInvoice invoice)
        {
            InitializeComponent();
            _invoice = invoice;
            PopulateInfo();
        }

        private void PopulateInfo()
        {
            string name = _invoice.Customer?.Name ?? "Customer";
            lblInvoiceRef.Text =
                $"Invoice #INV-{_invoice.CustomerInvoiceId:D4}  —  {name}";
            lblNetBill.Text = $"₨ {_invoice.NetBill:N0}";
            lblAlreadyPaid.Text = $"₨ {_invoice.AmountPaid:N0}";
            lblRemaining.Text = $"₨ {_invoice.RemainingBalance:N0}";
            lblNewRemaining.Text = $"₨ {_invoice.RemainingBalance:N0}";

            txtReceivedBy.Text = name;
            dpDate.SelectedDate = DateTime.Today;
        }

        // Live preview of new remaining
        private void TxtPayAmount_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (decimal.TryParse(txtPayAmount.Text, out decimal amount))
            {
                decimal newRem = _invoice.RemainingBalance - amount;
                lblNewRemaining.Text = newRem < 0
                    ? "⚠ Exceeds remaining!"
                    : $"₨ {newRem:N0}";
            }
            else
            {
                lblNewRemaining.Text = $"₨ {_invoice.RemainingBalance:N0}";
            }
        }

        private async void Confirm_Click(object sender, RoutedEventArgs e)
        {
            // ── Validation ──
            if (!decimal.TryParse(txtPayAmount.Text, out decimal payAmount) || payAmount <= 0)
            {
                MessageBox.Show("Please enter a valid payment amount.",
                    "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (payAmount > _invoice.RemainingBalance)
            {
                MessageBox.Show(
                    $"Payment amount (₨ {payAmount:N0}) exceeds remaining balance (₨ {_invoice.RemainingBalance:N0}).",
                    "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtReceivedBy.Text))
            {
                MessageBox.Show("Please enter who received the payment.",
                    "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DateTime payDate = dpDate.SelectedDate ?? DateTime.Today;
            bool isCash = rbCash.IsChecked == true;
            string invoiceRef = $"INV-{_invoice.CustomerInvoiceId:D4}";
            string receivedBy = txtReceivedBy.Text.Trim();

            try
            {
                // ── 1. Update CustomerInvoice in DB ──
                using (var db = new AppDbContext())
                {
                    var inv = await db.CustomerInvoices
                        .FirstOrDefaultAsync(i =>
                            i.CustomerInvoiceId == _invoice.CustomerInvoiceId);

                    if (inv == null)
                    {
                        MessageBox.Show("Invoice not found.", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    inv.AmountPaid += payAmount;
                    inv.RemainingBalance -= payAmount;

                    if (inv.RemainingBalance <= 0)
                    {
                        inv.RemainingBalance = 0;
                        inv.Status = "Clear";
                    }
                    else
                    {
                        inv.Status = "Partially Paid";
                    }

                    // ── 2. Save payment history record ──
                    db.CustomerPaymentHistories.Add(new CustomerPaymentHistory
                    {
                        CustomerInvoiceId = _invoice.CustomerInvoiceId,
                        AmountPaid = payAmount,
                        PaymentDate = payDate,
                        ReceivedBy = receivedBy,
                        PaymentMethod = isCash ? "Cash" : "Bank Transfer",
                        RemainingAfter = inv.RemainingBalance
                    });

                    await db.SaveChangesAsync();
                }

                // ── 3. Record in account ledger ──
                await _accountService.RecordInvoicePaymentAsync(
                    payAmount,
                    receivedBy,
                    $"{invoiceRef} | Due payment",
                    isCash);

                MessageBox.Show(
                    $"Payment of ₨ {payAmount:N0} recorded successfully!",
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to record payment:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}