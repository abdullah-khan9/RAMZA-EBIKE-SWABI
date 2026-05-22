// PayDueWindow.xaml.cs
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
            lblInvoiceRef.Text = $"Invoice #INV-{_invoice.CustomerInvoiceId:D4}  —  {name}";
            lblNetBill.Text = $"₨ {_invoice.NetBill:N0}";
            lblAlreadyPaid.Text = $"₨ {_invoice.AmountPaid:N0}";
            lblRemaining.Text = $"₨ {_invoice.RemainingBalance:N0}";
            lblNewRemaining.Text = $"₨ {_invoice.RemainingBalance:N0}";
            txtReceivedBy.Text = name;
            dpDate.SelectedDate = DateTime.Today;
        }

        // ✅ Live preview — Cash + Account total
        private void TxtAmount_TextChanged(object sender, TextChangedEventArgs e)
        {
            decimal cash = decimal.TryParse(txtCashAmount?.Text, out var c) ? c : 0;
            decimal account = decimal.TryParse(txtAccountAmount?.Text, out var a) ? a : 0;
            decimal total = cash + account;
            decimal newRem = _invoice.RemainingBalance - total;

            if (lblNewRemaining != null)
                lblNewRemaining.Text = newRem < 0
                    ? "⚠ Exceeds remaining!"
                    : $"₨ {newRem:N0}";
        }

        private async void Confirm_Click(object sender, RoutedEventArgs e)
        {
            decimal cashAmount = decimal.TryParse(txtCashAmount?.Text, out var c) ? c : 0;
            decimal accountAmount = decimal.TryParse(txtAccountAmount?.Text, out var a) ? a : 0;
            decimal totalPay = cashAmount + accountAmount;

            // ── Validation ──
            if (totalPay <= 0)
            {
                MessageBox.Show("Please enter Cash or Account amount.",
                    "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (totalPay > _invoice.RemainingBalance)
            {
                MessageBox.Show(
                    $"Total payment (₨ {totalPay:N0}) exceeds remaining balance (₨ {_invoice.RemainingBalance:N0}).",
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
            string invoiceRef = $"INV-{_invoice.CustomerInvoiceId:D4}";
            string receivedBy = txtReceivedBy.Text.Trim();
            string customerName = _invoice.Customer?.Name ?? receivedBy;

            // Payment method string
            string method = (cashAmount > 0 && accountAmount > 0) ? "Cash + Account"
                          : accountAmount > 0 ? "Bank Transfer"
                                                                   : "Cash";
            try
            {
                int? cashTxnId = null;
                int? accountTxnId = null;

                using (var db = new AppDbContext())
                {
                    // ── 1. Update CustomerInvoice ──
                    var inv = await db.CustomerInvoices
                        .FirstOrDefaultAsync(i => i.CustomerInvoiceId == _invoice.CustomerInvoiceId);

                    if (inv == null)
                    {
                        MessageBox.Show("Invoice not found.", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    inv.AmountPaid += totalPay;
                    inv.AmountPaidCash += cashAmount;
                    inv.AmountPaidAccount += accountAmount;
                    inv.RemainingBalance -= totalPay;

                    if (inv.RemainingBalance <= 0)
                    {
                        inv.RemainingBalance = 0;
                        inv.Status = "Clear";
                    }
                    else
                    {
                        inv.Status = "Partially Paid";
                    }

                    decimal newRemaining = inv.RemainingBalance;

                    // ── 2. Record Account Transactions ──
                    var balance = await db.AccountBalances.FirstOrDefaultAsync();
                    if (balance == null)
                    {
                        balance = new AccountBalance { CashBalance = 0, BankBalance = 0 };
                        db.AccountBalances.Add(balance);
                        await db.SaveChangesAsync();
                    }

                    // Cash transaction
                    if (cashAmount > 0)
                    {
                        balance.CashBalance += cashAmount;
                        var cashTxn = new AccountTransaction
                        {
                            Type = TransactionType.CashDeposit,
                            ByWhom = customerName,
                            Amount = cashAmount,
                            TransactionDate = payDate,
                            Remarks = $"{invoiceRef} | {customerName} | Due payment (Cash)",
                            CashBalanceAfter = balance.CashBalance,
                            BankBalanceAfter = balance.BankBalance
                        };
                        db.AccountTransactions.Add(cashTxn);
                        await db.SaveChangesAsync();
                        cashTxnId = cashTxn.Id;
                    }

                    // Account transaction
                    if (accountAmount > 0)
                    {
                        balance.BankBalance += accountAmount;
                        var accountTxn = new AccountTransaction
                        {
                            Type = TransactionType.DepositToAccount,
                            ByWhom = customerName,
                            Amount = accountAmount,
                            TransactionDate = payDate,
                            Remarks = $"{invoiceRef} | {customerName} | Due payment (Account)",
                            CashBalanceAfter = balance.CashBalance,
                            BankBalanceAfter = balance.BankBalance
                        };
                        db.AccountTransactions.Add(accountTxn);
                        await db.SaveChangesAsync();
                        accountTxnId = accountTxn.Id;
                    }

                    // ── 3. Save Payment History ──
                    db.CustomerPaymentHistories.Add(new CustomerPaymentHistory
                    {
                        CustomerInvoiceId = _invoice.CustomerInvoiceId,
                        AmountPaid = totalPay,
                        AmountPaidCash = cashAmount,
                        AmountPaidAccount = accountAmount,
                        PaymentDate = payDate,
                        ReceivedBy = receivedBy,
                        PaymentMethod = method,
                        RemainingAfter = newRemaining,
                        CashTransactionId = cashTxnId,
                        AccountTransactionId = accountTxnId
                    });

                    await db.SaveChangesAsync();
                }

                MessageBox.Show(
                    $"Payment of ₨ {totalPay:N0} recorded successfully!" +
                    (cashAmount > 0 ? $"\n  Cash: ₨ {cashAmount:N0}" : "") +
                    (accountAmount > 0 ? $"\n  Account: ₨ {accountAmount:N0}" : ""),
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