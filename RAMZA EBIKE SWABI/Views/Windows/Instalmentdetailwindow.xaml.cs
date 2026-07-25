using Ramza_EBike_Swabi.Models;
using Ramza_EBike_Swabi.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Ramza_EBike_Swabi.Views.Windows
{
    public class InstalmentDisplayVM
    {
        private readonly InvoiceInstalment _inst;
        public InstalmentDisplayVM(InvoiceInstalment inst) => _inst = inst;

        public int Id => _inst.Id;
        public int InstalmentNumber => _inst.InstalmentNumber;
        public string DueDateFormatted => _inst.DueDate.ToString("dd-MMM-yyyy");
        public string AmountFormatted => $"PKR {_inst.Amount:N2}";
        public string Status => _inst.Status;
        public string StatusDisplay => _inst.Status switch
        {
            "Paid" => "✅ Paid",
            "Partially Paid" => "🟡 Partially Paid",
            "Overdue" => "⚠ Overdue",
            _ => "🕐 Pending"
        };
        public string PaidOnFormatted => _inst.PaidOn.HasValue
            ? _inst.PaidOn.Value.ToString("dd-MMM-yyyy") : "—";
        public string PaidAmountFormatted => _inst.PaidAmount > 0
            ? $"PKR {_inst.PaidAmount:N2}" : "—";
        public string PaymentSourceDisplay => _inst.PaymentSource ?? "—";
        public string? Remarks => _inst.Remarks;
        public string CanPayVisibility => _inst.Status != "Paid" ? "Visible" : "Collapsed";

        // Row color based on status
        public string RowBg => _inst.Status switch
        {
            "Paid" => "#F0FDF4",
            "Partially Paid" => "#FFFBEA",
            "Overdue" => "#FFF5F5",
            _ => "White"
        };
    }

    public partial class InstalmentDetailWindow : Window
    {
        private readonly InstalmentService _service = new();
        private readonly AccountService _accountService = new();
        private readonly CustomerInvoice _invoice;
        private List<InvoiceInstalment> _instalments = new();
        public bool WasModified { get; private set; } = false;

        public InstalmentDetailWindow(CustomerInvoice invoice)
        {
            InitializeComponent();
            _invoice = invoice;
            TitleText.Text = $"📅 Instalment Plan — {invoice.Customer?.Name}";
            SubTitleText.Text = $"Invoice #{invoice.CustomerInvoiceId}  |  Net Bill: PKR {invoice.NetBill:N2}  |  Remaining: PKR {invoice.RemainingBalance:N2}";
            _ = LoadAsync();
        }

        private async System.Threading.Tasks.Task LoadAsync()
        {
            _instalments = await _service.GetInstalmentsAsync(_invoice.CustomerInvoiceId);

            if (_instalments.Count == 0)
            {
                MessageBox.Show("Is invoice ka koi instalment plan nahi hai.", "No Plan",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
                return;
            }
            RefreshGrid();
        }

        private void RefreshGrid()
        {
            var vms = _instalments.Select(i => new InstalmentDisplayVM(i)).ToList();
            dgInstalments.ItemsSource = null;
            dgInstalments.ItemsSource = vms;

            decimal total = _instalments.Sum(i => i.Amount);
            decimal paid = _instalments
                .Where(i => i.Status == "Paid" || i.Status == "Partially Paid")
                .Sum(i => i.PaidAmount);
            decimal remaining = total - paid;

            var next = _instalments
                .Where(i => i.Status != "Paid")
                .OrderBy(i => i.DueDate)
                .FirstOrDefault();

            TotalText.Text = $"PKR {total:N2}";
            PaidText.Text = $"PKR {paid:N2}";
            RemainingText.Text = $"PKR {remaining:N2}";
            NextDueText.Text = next != null
                ? next.DueDate.ToString("dd-MMM-yyyy") : "All Paid ✅";
        }

        private async void MarkPaid_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is not InstalmentDisplayVM vm) return;
            var inst = _instalments.FirstOrDefault(i => i.Id == vm.Id);
            if (inst == null) return;

            var win = new PayInstalmentWindow(inst) { Owner = this };

            if (win.ShowDialog() == true)
            {
                await _service.MarkPaidAsync(
                    inst.Id,
                    win.CashAmount,
                    win.AccountAmount,
                    win.Remarks,
                    _accountService);

                WasModified = true;
                _instalments = await _service.GetInstalmentsAsync(_invoice.CustomerInvoiceId);
                RefreshGrid();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}