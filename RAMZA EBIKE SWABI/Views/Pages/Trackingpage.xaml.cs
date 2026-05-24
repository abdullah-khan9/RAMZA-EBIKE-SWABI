// TrackingPage.xaml.cs
using Microsoft.EntityFrameworkCore;
using Ramza_EBike_Swabi.Models;
using Ramza_EBike_Swabi.Services;
using Ramza_EBike_Swabi.Services.Pdf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Ramza_EBike_Swabi.Views.Pages
{
    public partial class TrackingPage : Page
    {
        private readonly InvoiceService _invoiceService = new();
        private readonly DocumentIssuanceService _issuanceService = new();
        private readonly MainLayout? _mainLayout;

        private List<TrackingCardViewModel> _allResults = new();
        private List<CustomerInvoice> _allInvoices = new();

        public TrackingPage(MainLayout? mainLayout = null)
        {
            InitializeComponent();
            _mainLayout = mainLayout;
            Loaded += async (_, __) => await LoadAllInvoicesAsync();
        }

        private async Task LoadAllInvoicesAsync()
        {
            _allInvoices = await _invoiceService.GetAllInvoicesAsync();
        }

        // ══════════════════════════════════════════════════════════
        // BACK
        // ══════════════════════════════════════════════════════════
        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (_mainLayout != null)
                _mainLayout.Navigate(new DashboardPage(_mainLayout));
            else if (NavigationService?.CanGoBack == true)
                NavigationService.GoBack();
        }

        // ══════════════════════════════════════════════════════════
        // SEARCH
        // ══════════════════════════════════════════════════════════
        private async void BtnSearch_Click(object sender, RoutedEventArgs e)
            => await DoSearchAsync();

        private async void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) await DoSearchAsync();
        }

        private async void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtSearch.Text))
                await DoSearchAsync();
        }

        private async Task DoSearchAsync()
        {
            string query = txtSearch.Text?.Trim() ?? string.Empty;
            string searchType = (cmbSearchType.SelectedItem as ComboBoxItem)
                                    ?.Content?.ToString() ?? "Name";

            if (_allInvoices.Count == 0)
                _allInvoices = await _invoiceService.GetAllInvoicesAsync();

            IEnumerable<CustomerInvoice> filtered = _allInvoices;

            if (!string.IsNullOrWhiteSpace(query))
            {
                string q = query.ToLower();
                filtered = searchType switch
                {
                    "CNIC" => filtered.Where(i =>
                        (i.Customer?.CNIC ?? "").ToLower().Contains(q)),
                    "Motor Number" => filtered.Where(i =>
                        i.Items != null &&
                        i.Items.Any(item => (item.MotorNumber ?? "").ToLower().Contains(q))),
                    _ => filtered.Where(i =>
                        (i.Customer?.Name ?? "").ToLower().Contains(q))
                };
            }

            await ApplyFiltersAsync(filtered.ToList());
        }

        private async Task ApplyFiltersAsync(List<CustomerInvoice> invoices)
        {
            bool filterWarranty    = chkFilterWarranty.IsChecked    == true;
            bool filterVoucherCust = chkFilterVoucherCust.IsChecked == true;
            bool filterVoucherComp = chkFilterVoucherComp.IsChecked == true;
            bool filterHasDue      = chkFilterHasDue.IsChecked      == true;

            var result = invoices.AsEnumerable();
            if (filterWarranty)    result = result.Where(i => i.WarrantyCardGiven);
            if (filterVoucherCust) result = result.Where(i => i.VoucherGivenToCustomer);
            if (filterVoucherComp) result = result.Where(i => i.VoucherIssuedByCompany);
            if (filterHasDue)      result = result.Where(i => i.RemainingBalance > 0);

            var invoiceList = result.ToList();

            var vms = new List<TrackingCardViewModel>();
            foreach (var inv in invoiceList)
            {
                var history = await _issuanceService.GetHistoryAsync(inv.CustomerInvoiceId);
                vms.Add(BuildViewModel(inv, history));
            }

            _allResults = vms;
            RenderResults();
        }

        private void RenderResults()
        {
            txtResultCount.Text = $"{_allResults.Count} record(s) found";

            bool noSearch = string.IsNullOrWhiteSpace(txtSearch.Text);
            bool noFilters = chkFilterWarranty.IsChecked    != true
                          && chkFilterVoucherCust.IsChecked != true
                          && chkFilterVoucherComp.IsChecked != true
                          && chkFilterHasDue.IsChecked      != true;

            if (!_allResults.Any() && noSearch && noFilters)
            {
                pnlEmpty.Visibility     = Visibility.Visible;
                icResults.Visibility    = Visibility.Collapsed;
                pnlNoResults.Visibility = Visibility.Collapsed;
                return;
            }

            pnlEmpty.Visibility = Visibility.Collapsed;

            if (_allResults.Count == 0)
            {
                icResults.Visibility    = Visibility.Collapsed;
                pnlNoResults.Visibility = Visibility.Visible;
            }
            else
            {
                pnlNoResults.Visibility = Visibility.Collapsed;
                icResults.Visibility    = Visibility.Visible;
                icResults.ItemsSource   = _allResults;
            }
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            txtSearch.Clear();
            chkFilterWarranty.IsChecked    = false;
            chkFilterVoucherCust.IsChecked = false;
            chkFilterVoucherComp.IsChecked = false;
            chkFilterHasDue.IsChecked      = false;

            _allResults.Clear();
            icResults.ItemsSource   = null;
            icResults.Visibility    = Visibility.Collapsed;
            pnlNoResults.Visibility = Visibility.Collapsed;
            pnlEmpty.Visibility     = Visibility.Visible;
            txtResultCount.Text     = string.Empty;
        }

        private async void FilterChanged(object sender, RoutedEventArgs e)
            => await DoSearchAsync();

        // ══════════════════════════════════════════════════════════
        // BUILD VIEW MODEL
        // ══════════════════════════════════════════════════════════
        private static TrackingCardViewModel BuildViewModel(
            CustomerInvoice inv,
            List<DocumentIssuanceRecord> history)
        {
            var c = inv.Customer;

            string statusColor = inv.Status switch
            {
                "Clear"  => "#16A34A",
                "Unpaid" => "#DC2626",
                _        => "#D97706"
            };

            bool warrantyDelivered   = inv.WarrantyCardGiven
                                    || history.Any(h => h.WarrantyCardIssued);
            bool voucherCustDelivered = inv.VoucherGivenToCustomer
                                    || history.Any(h => h.VoucherCustomerIssued);
            bool voucherCompDelivered = inv.VoucherIssuedByCompany
                                    || history.Any(h => h.VoucherCompanyIssued);

            bool allDelivered = warrantyDelivered && voucherCustDelivered && voucherCompDelivered;

            static string DeliveredAt(bool atBilling,
                List<DocumentIssuanceRecord> hist,
                Func<DocumentIssuanceRecord, bool> predicate)
            {
                if (atBilling) return "Given at Billing";
                var first = hist.Where(predicate).OrderBy(h => h.IssuanceDate).FirstOrDefault();
                return first != null
                    ? $"Issued {first.IssuanceDate:dd MMM yyyy}"
                    : "Not Issued";
            }

            static string DocIcon(bool delivered) => delivered ? "✅" : "⬜";
            static string DocBg(bool delivered)   => delivered ? "#F0FDF4" : "#FFF7F7";
            static string DocFg(bool delivered)   => delivered ? "#16A34A" : "#DC2626";

            var bikes = inv.Items?
                .Where(i => !string.IsNullOrWhiteSpace(i.MotorNumber))
                .Select(i => new BikeSummaryViewModel
                {
                    ModelBrand    = $"{i.Model}  •  {i.Brand}",
                    MotorNumber   = i.MotorNumber   ?? "",
                    ChassisNumber = i.ChassisNumber ?? "",
                    MotorChassis  = $"Motor: {i.MotorNumber}  |  Chassis: {i.ChassisNumber}",
                    ColorPower    = $"Color: {i.Color}  |  Power: {i.MotorPower}  |  Warranty: {i.Warranty}"
                }).ToList() ?? new List<BikeSummaryViewModel>();

            // ── History rows ──────────────────────────────────────
            var historyRows = new List<HistoryRowViewModel>();

            // Synthetic billing-time row
            if (inv.WarrantyCardGiven || inv.VoucherGivenToCustomer || inv.VoucherIssuedByCompany)
            {
                var docs = new List<string>();
                if (inv.WarrantyCardGiven)      docs.Add("Warranty Card");
                if (inv.VoucherGivenToCustomer) docs.Add("Voucher (Customer)");
                if (inv.VoucherIssuedByCompany) docs.Add("Voucher (Company)");

                historyRows.Add(new HistoryRowViewModel
                {
                    RecordId        = 0,
                    InvoiceId       = inv.CustomerInvoiceId,
                    DateDisplay     = inv.InvoiceDate.ToString("dd MMM yyyy  hh:mm tt"),
                    DocumentsIssued = string.Join(", ", docs),
                    IssuedBy        = "— (Given at Billing)",
                    ReceivedBy      = c?.Name ?? "Customer",
                    Notes           = "Delivered at time of invoice creation.",
                    IsBillingEntry  = true
                });
            }

            // Real DB records
            foreach (var h in history.OrderBy(x => x.IssuanceDate))
            {
                var docs = new List<string>();
                if (h.WarrantyCardIssued)    docs.Add("Warranty Card");
                if (h.VoucherCustomerIssued) docs.Add("Voucher (Customer)");
                if (h.VoucherCompanyIssued)  docs.Add("Voucher (Company)");

                historyRows.Add(new HistoryRowViewModel
                {
                    RecordId              = h.DocumentIssuanceId,
                    InvoiceId             = inv.CustomerInvoiceId,
                    DateDisplay           = h.IssuanceDate.ToString("dd MMM yyyy  hh:mm tt"),
                    IssuanceDate          = h.IssuanceDate,
                    DocumentsIssued       = string.Join(", ", docs),
                    IssuedBy              = h.IssuedBy,
                    ReceivedBy            = h.ReceivedBy,
                    Notes                 = h.Notes ?? string.Empty,
                    IsBillingEntry        = false,
                    WarrantyCardIssued    = h.WarrantyCardIssued,
                    VoucherCustomerIssued = h.VoucherCustomerIssued,
                    VoucherCompanyIssued  = h.VoucherCompanyIssued
                });
            }

            return new TrackingCardViewModel
            {
                InvoiceId       = inv.CustomerInvoiceId,
                CustomerName    = c?.Name       ?? "—",
                FatherName      = c?.FatherName ?? "—",
                Contact         = c?.Contact    ?? "—",
                CNIC            = c?.CNIC       ?? "—",
                Address         = c?.Address    ?? "—",
                InvoiceDate     = inv.InvoiceDate,
                Status          = inv.Status,
                StatusColor     = statusColor,
                Remarks         = inv.Remarks ?? string.Empty,
                RemarksVisibility = string.IsNullOrWhiteSpace(inv.Remarks)
                    ? Visibility.Collapsed : Visibility.Visible,

                DueDate         = inv.DueDate,
                DueDateText     = inv.DueDate.HasValue
                    ? inv.DueDate.Value.ToString("dd MMM yyyy") : "",
                DueDateVisibility = inv.DueDate.HasValue
                    ? Visibility.Visible : Visibility.Collapsed,

                WarrantyCardGiven      = inv.WarrantyCardGiven,
                VoucherGivenToCustomer = inv.VoucherGivenToCustomer,
                VoucherIssuedByCompany = inv.VoucherIssuedByCompany,

                WarrantyDelivered    = warrantyDelivered,
                VoucherCustDelivered = voucherCustDelivered,
                VoucherCompDelivered = voucherCompDelivered,
                AllDelivered         = allDelivered,

                AllDeliveredVisibility = allDelivered
                    ? Visibility.Visible : Visibility.Collapsed,
                IssueButtonVisibility  = allDelivered
                    ? Visibility.Collapsed : Visibility.Visible,

                WarrantyStatus = DeliveredAt(inv.WarrantyCardGiven,      history, h => h.WarrantyCardIssued),
                WarrantyIcon   = DocIcon(warrantyDelivered),
                WarrantyBg     = DocBg(warrantyDelivered),
                WarrantyFg     = DocFg(warrantyDelivered),

                VoucherCustStatus = DeliveredAt(inv.VoucherGivenToCustomer, history, h => h.VoucherCustomerIssued),
                VoucherCustIcon   = DocIcon(voucherCustDelivered),
                VoucherCustBg     = DocBg(voucherCustDelivered),
                VoucherCustFg     = DocFg(voucherCustDelivered),

                VoucherCompStatus = DeliveredAt(inv.VoucherIssuedByCompany, history, h => h.VoucherCompanyIssued),
                VoucherCompIcon   = DocIcon(voucherCompDelivered),
                VoucherCompBg     = DocBg(voucherCompDelivered),
                VoucherCompFg     = DocFg(voucherCompDelivered),

                BikesSummary     = bikes,
                HistoryRows      = historyRows,
                HistoryVisibility = historyRows.Count > 0
                    ? Visibility.Visible : Visibility.Collapsed
            };
        }

        // ══════════════════════════════════════════════════════════
        // ISSUE DOCUMENT
        // ══════════════════════════════════════════════════════════
        private void BtnIssue_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is not TrackingCardViewModel vm) return;

            var win = new IssueDocumentWindow(
                vm.InvoiceId,
                vm.CustomerName,
                needsWarranty:    !vm.WarrantyDelivered,
                needsVoucherCust: !vm.VoucherCustDelivered,
                needsVoucherComp: !vm.VoucherCompDelivered);

            win.Owner = Window.GetWindow(this);
            win.DocumentIssued += async () =>
            {
                _allInvoices = await _invoiceService.GetAllInvoicesAsync();
                await DoSearchAsync();
            };
            win.ShowDialog();
        }

        // ══════════════════════════════════════════════════════════
        // EDIT HISTORY ROW
        // ══════════════════════════════════════════════════════════
        private void BtnEditHistory_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is not HistoryRowViewModel row) return;
            if (row.IsBillingEntry) return;

            var win = new EditIssuanceWindow(row);
            win.Owner = Window.GetWindow(this);
            win.RecordUpdated += async () =>
            {
                _allInvoices = await _invoiceService.GetAllInvoicesAsync();
                await DoSearchAsync();
            };
            win.ShowDialog();
        }

        // ══════════════════════════════════════════════════════════
        // DELETE SINGLE HISTORY ROW  (🗑 per-row button)
        // ══════════════════════════════════════════════════════════
        private async void BtnDeleteHistoryRow_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is not HistoryRowViewModel row) return;
            if (row.IsBillingEntry) return;

            var confirm = MessageBox.Show(
                $"Delete this issuance record from {row.DateDisplay}?\n\n" +
                $"Documents : {row.DocumentsIssued}\n" +
                $"Issued by : {row.IssuedBy}\n\n" +
                "Invoice document checkboxes will be recalculated from remaining records.",
                "Confirm Delete Record",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                await _issuanceService.DeleteIssuanceAsync(row.RecordId);
                await RecalculateInvoiceCheckboxesAsync(row.InvoiceId);

                _allInvoices = await _invoiceService.GetAllInvoicesAsync();
                await DoSearchAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Delete failed:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ══════════════════════════════════════════════════════════
        // DELETE ALL HISTORY  (card-level button)
        // ══════════════════════════════════════════════════════════
        private async void BtnDeleteRecord_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is not TrackingCardViewModel vm) return;

            var confirm = MessageBox.Show(
                $"Delete all post-billing issuance records for\n" +
                $"INV-{vm.InvoiceId:D4}  ({vm.CustomerName})?\n\n" +
                "Invoice document checkboxes (Warranty, Vouchers) will also be reset.",
                "Confirm Delete All History",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            await _issuanceService.DeleteAllHistoryAsync(vm.InvoiceId);
            await ResetInvoiceCheckboxesAsync(vm.InvoiceId);

            _allInvoices = await _invoiceService.GetAllInvoicesAsync();
            await DoSearchAsync();
        }

        // ══════════════════════════════════════════════════════════
        // CHECKBOX RECALCULATION HELPERS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// After a single-row delete: OR the remaining records to decide
        /// which flags should still be true on the invoice.
        /// </summary>
        private async Task RecalculateInvoiceCheckboxesAsync(int invoiceId)
        {
            var remaining = await _issuanceService.GetIssuancesForInvoiceAsync(invoiceId);

            bool warranty    = remaining.Any(r => r.WarrantyCardIssued);
            bool voucherCust = remaining.Any(r => r.VoucherCustomerIssued);
            bool voucherComp = remaining.Any(r => r.VoucherCompanyIssued);

            await SaveInvoiceCheckboxesAsync(invoiceId, warranty, voucherCust, voucherComp);
        }

        /// <summary>After Clear All: set all three flags to false directly.</summary>
        private Task ResetInvoiceCheckboxesAsync(int invoiceId)
            => SaveInvoiceCheckboxesAsync(invoiceId, false, false, false);

        private async Task SaveInvoiceCheckboxesAsync(
            int invoiceId, bool warranty, bool voucherCust, bool voucherComp)
        {
            using var db = new Data.AppDbContext();
            var invoice = await db.CustomerInvoices
                .FirstOrDefaultAsync(i => i.CustomerInvoiceId == invoiceId);

            if (invoice == null) return;

            invoice.WarrantyCardGiven      = warranty;
            invoice.VoucherGivenToCustomer = voucherCust;
            invoice.VoucherIssuedByCompany = voucherComp;

            await db.SaveChangesAsync();
        }

        // ══════════════════════════════════════════════════════════
        // PDF DOWNLOAD
        // ══════════════════════════════════════════════════════════
        private void BtnDownloadPdf_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is not TrackingCardViewModel vm) return;
            TrackingPdfService.Generate(vm);
        }
    }

    // ══════════════════════════════════════════════════════════════
    // VIEW MODELS
    // ══════════════════════════════════════════════════════════════
    public class TrackingCardViewModel
    {
        public int InvoiceId { get; set; }
        public string CustomerName { get; set; } = "";
        public string FatherName { get; set; } = "";
        public string Contact { get; set; } = "";
        public string CNIC { get; set; } = "";
        public string Address { get; set; } = "";
        public DateTime InvoiceDate { get; set; }
        public string Status { get; set; } = "";
        public string StatusColor { get; set; } = "#111827";
        public string Remarks { get; set; } = "";
        public Visibility RemarksVisibility { get; set; } = Visibility.Collapsed;

        public DateTime? DueDate { get; set; }
        public string DueDateText { get; set; } = "";
        public Visibility DueDateVisibility { get; set; } = Visibility.Collapsed;

        public bool WarrantyCardGiven { get; set; }
        public bool VoucherGivenToCustomer { get; set; }
        public bool VoucherIssuedByCompany { get; set; }

        public bool WarrantyDelivered { get; set; }
        public bool VoucherCustDelivered { get; set; }
        public bool VoucherCompDelivered { get; set; }
        public bool AllDelivered { get; set; }

        public Visibility AllDeliveredVisibility { get; set; } = Visibility.Collapsed;
        public Visibility IssueButtonVisibility { get; set; } = Visibility.Visible;

        public string WarrantyStatus { get; set; } = "";
        public string WarrantyIcon { get; set; } = "";
        public string WarrantyBg { get; set; } = "";
        public string WarrantyFg { get; set; } = "";

        public string VoucherCustStatus { get; set; } = "";
        public string VoucherCustIcon { get; set; } = "";
        public string VoucherCustBg { get; set; } = "";
        public string VoucherCustFg { get; set; } = "";

        public string VoucherCompStatus { get; set; } = "";
        public string VoucherCompIcon { get; set; } = "";
        public string VoucherCompBg { get; set; } = "";
        public string VoucherCompFg { get; set; } = "";

        public List<BikeSummaryViewModel> BikesSummary { get; set; } = new();
        public List<HistoryRowViewModel> HistoryRows { get; set; } = new();
        public Visibility HistoryVisibility { get; set; } = Visibility.Collapsed;
    }

    public class BikeSummaryViewModel
    {
        public string ModelBrand { get; set; } = "";
        public string MotorNumber { get; set; } = "";
        public string ChassisNumber { get; set; } = "";
        public string MotorChassis { get; set; } = "";
        public string ColorPower { get; set; } = "";
    }

    public class HistoryRowViewModel
    {
        public int RecordId { get; set; }
        public int InvoiceId { get; set; }        // ✅ needed for per-row delete recalc
        public string DateDisplay { get; set; } = "";
        public string DocumentsIssued { get; set; } = "";
        public string IssuedBy { get; set; } = "";
        public string ReceivedBy { get; set; } = "";
        public string Notes { get; set; } = "";
        public bool IsBillingEntry { get; set; }

        public DateTime IssuanceDate { get; set; }

        public bool WarrantyCardIssued { get; set; }
        public bool VoucherCustomerIssued { get; set; }
        public bool VoucherCompanyIssued { get; set; }

        public string RowBg    => IsBillingEntry ? "#EFF6FF" : "White";
        public string RowLabel => IsBillingEntry ? "At Billing" : "Post-Billing";
        public string LabelBg  => IsBillingEntry ? "#DBEAFE" : "#F3F4F6";
        public string LabelFg  => IsBillingEntry ? "#1D4ED8" : "#6B7280";
        public Visibility EditButtonVisibility =>
            IsBillingEntry ? Visibility.Collapsed : Visibility.Visible;
    }
}