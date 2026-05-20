using System.Linq;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Ramza_EBike_Swabi.Data;
using Ramza_EBike_Swabi.Models;

namespace Ramza_EBike_Swabi.Views.Pages
{
    public partial class PaymentHistoryWindow : Window
    {
        private readonly CustomerInvoice _invoice;

        public PaymentHistoryWindow(CustomerInvoice invoice)
        {
            InitializeComponent();
            _invoice = invoice;
            _ = LoadAsync();
        }

        private async System.Threading.Tasks.Task LoadAsync()
        {
            string name = _invoice.Customer?.Name ?? "Customer";
            lblTitle.Text = $"Payment History — {name}";
            lblSubtitle.Text =
                $"Invoice #INV-{_invoice.CustomerInvoiceId:D4}   " +
                $"Net Bill: ₨ {_invoice.NetBill:N0}   " +
                $"Total Paid: ₨ {_invoice.AmountPaid:N0}   " +
                $"Remaining: ₨ {_invoice.RemainingBalance:N0}";

            using var db = new AppDbContext();
            var rows = await db.CustomerPaymentHistories
                .Where(h => h.CustomerInvoiceId == _invoice.CustomerInvoiceId)
                .OrderBy(h => h.PaymentDate)
                .ToListAsync();

            // Add a row-number helper for display
            dgHistory.ItemsSource = rows
                .Select((r, i) => new PaymentHistoryRow
                {
                    RowNumber = i + 1,
                    PaymentDate = r.PaymentDate,
                    AmountPaid = r.AmountPaid,
                    RemainingAfter = r.RemainingAfter,
                    PaymentMethod = r.PaymentMethod,
                    ReceivedBy = r.ReceivedBy
                }).ToList();
        }
    }

    // ViewModel helper — only for display
    public class PaymentHistoryRow
    {
        public int RowNumber { get; set; }
        public System.DateTime PaymentDate { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal RemainingAfter { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string ReceivedBy { get; set; } = string.Empty;
    }
}