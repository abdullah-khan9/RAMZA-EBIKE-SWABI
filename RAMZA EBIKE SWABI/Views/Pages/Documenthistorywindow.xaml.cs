// DocumentHistoryWindow.xaml.cs
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Ramza_EBike_Swabi.Models;

namespace Ramza_EBike_Swabi.Views.Pages
{
    public partial class DocumentHistoryWindow : Window
    {
        public DocumentHistoryWindow(string customerName, int invoiceId,
            List<DocumentIssuanceRecord> history)
        {
            InitializeComponent();
            lblTitle.Text = $"📜  History — {customerName}";
            lblSubtitle.Text = $"Invoice #INV-{invoiceId:D4}   •   All document issuances recorded for this invoice.";

            if (history == null || history.Count == 0)
            {
                pnlEmpty.Visibility = Visibility.Visible;
                dgHistory.Visibility = Visibility.Collapsed;
                return;
            }

            dgHistory.Visibility = Visibility.Visible;
            pnlEmpty.Visibility = Visibility.Collapsed;

            dgHistory.ItemsSource = history
                .OrderByDescending(h => h.IssuanceDate)
                .Select(h => new
                {
                    IssuanceDateDisplay = h.IssuanceDate.ToString("dd MMM yyyy  hh:mm tt"),
                    WarrantyDisplay = h.WarrantyCardIssued ? "✅ Yes" : "—",
                    VoucherCustDisplay = h.VoucherCustomerIssued ? "✅ Yes" : "—",
                    VoucherCompDisplay = h.VoucherCompanyIssued ? "✅ Yes" : "—",
                    h.IssuedBy,
                    h.ReceivedBy,
                    Notes = h.Notes ?? "—"
                }).ToList();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}