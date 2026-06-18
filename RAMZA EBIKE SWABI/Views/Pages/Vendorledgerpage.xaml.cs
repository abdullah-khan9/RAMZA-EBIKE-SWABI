using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Ramza_EBike_Swabi.Data;
using Ramza_EBike_Swabi.Models;
using Ramza_EBike_Swabi.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Ramza_EBike_Swabi.Views.Pages
{
    // ===========================
    // TAB 1 — Bills & Payments
    // ===========================
    public class BillLedgerRow
    {
        public DateTime SortDate { get; set; }
        public string Date { get; set; } = "";
        public string Type { get; set; } = "";
        public string Description { get; set; } = "";
        public string Debit { get; set; } = "";  // Bill amount (red)
        public string Credit { get; set; } = "";  // Cash/Account payment (green)
        public string VendorCashUsed { get; set; } = "";  // VCash used (purple)
        public string Remaining { get; set; } = "";  // Bill balance after
        public string VendorCashBalance { get; set; } = "";  // VCash balance after (purple)
        public string Remarks { get; set; } = "";
        public string RowColor { get; set; } = "";
    }

    // ===========================
    // TAB 2 — Vendor Cash (Added only — Applied shown in Bills tab)
    // ===========================
    public class CashLedgerRow
    {
        public DateTime SortDate { get; set; }
        public string Date { get; set; } = "";
        public string Type { get; set; } = "";
        public string Description { get; set; } = "";
        public string Amount { get; set; } = "";
        public string RemainingVendorCash { get; set; } = "";
        public string Remarks { get; set; } = "";
        public string RowColor { get; set; } = "";
    }

    public partial class VendorLedgerPage : Page
    {
        private readonly VendorService _vendorService = new();
        private readonly VendorCashService _vendorCashService = new();
        private readonly MainLayout _layout;
        private readonly int _vendorId;
        private string _vendorName = "";

        private List<BillLedgerRow> _allBillRows = new();
        private List<CashLedgerRow> _allCashRows = new();
        private bool _loaded = false;

        // Summary values for PDF
        private string _summaryTotalBills = "";
        private string _summaryTotalPaid = "";
        private string _summaryVendorCash = "";
        private string _summaryOutstanding = "";

        public VendorLedgerPage(MainLayout layout, int vendorId, string vendorName)
        {
            InitializeComponent();
            _layout = layout;
            _vendorId = vendorId;
            _vendorName = vendorName;
            PageTitle.Text = $"Vendor Ledger — {vendorName}";
            VendorNameText.Text = vendorName;
            VendorIdText.Text = $"Vendor ID: {vendorId}";
            Loaded += (_, _) => LoadLedger();
        }

        // ===========================
        // LOAD DATA
        // ===========================
        private async void LoadLedger()
        {
            try
            {
                using var db = new AppDbContext();

                // Sequential — EF Core ek context par concurrent queries allow nahi karta
                var bills = await db.VendorBill
                    .Where(b => b.VendorId == _vendorId)
                    .OrderBy(b => b.BillDate).ThenBy(b => b.Id)
                    .ToListAsync();

                var payments = await db.VendorPaymentRecords
                    .Where(p => p.VendorId == _vendorId)
                    .OrderBy(p => p.PaymentDate).ThenBy(p => p.Id)
                    .ToListAsync();

                var cashLedger = await db.VendorCashLedger
                    .Where(c => c.VendorId == _vendorId)
                    .OrderBy(c => c.TransactionDate).ThenBy(c => c.Id)
                    .ToListAsync();

                var billIds = bills.Select(b => b.Id).ToList();
                var billDocMap = billIds.Any()
                    ? await db.VendorBillItem
                        .Where(i => billIds.Contains(i.VendorBillId))
                        .GroupBy(i => i.VendorBillId)
                        .Select(g => new { BillId = g.Key, DocNum = g.OrderBy(i => i.Id).First().DocumentNumber })
                        .ToDictionaryAsync(x => x.BillId, x => x.DocNum ?? "")
                    : new Dictionary<int, string>();

                // ── Track Applied cash ledger rows that get merged into bills/payments ──
                var mergedCashLedgerIds = new HashSet<int>();

                // Running VCash balance — track per transaction for accurate display
                // We use VendorCashBalanceAfter from ledger directly (already stored)
                // We need latest vcBalance before each bill event
                // Build a sorted vc balance map: after each cash ledger entry, what is vcBal?
                // We'll look up "what was vcBal just before this bill" by scanning cashLedger by date

                // ══════════════════════════════════════
                // TAB 1: BILLS & PAYMENTS
                // ══════════════════════════════════════
                _allBillRows = new List<BillLedgerRow>();

                var billEvents = bills.Select(b => (Date: b.BillDate, Id: b.Id, Kind: "Bill", Data: (object)b));
                var payEvents = payments.Select(p => (Date: p.PaymentDate, Id: p.Id, Kind: "Pay", Data: (object)p));

                // Sort: chronological, Bill before Payment on same date, then by Id
                var allEvents = billEvents.Concat(payEvents)
                    .OrderBy(e => e.Date)
                    .ThenBy(e => e.Kind == "Bill" ? 0 : 1)
                    .ThenBy(e => e.Id)
                    .ToList();

                foreach (var ev in allEvents)
                {
                    if (ev.Kind == "Bill")
                    {
                        var bill = (VendorBill)ev.Data;
                        billDocMap.TryGetValue(bill.Id, out var docNum);

                        bool paidByVCash = bill.PaymentSource == "VendorCash" && bill.AmountPaid > 0;
                        bool paidByCashOrAcc = !paidByVCash && bill.AmountPaid > 0;

                        // Find linked Applied cash ledger row for this bill payment
                        decimal vcBalAfter = 0m;
                        bool vcBalFound = false;
                        if (paidByVCash)
                        {
                            var linked = cashLedger.FirstOrDefault(c =>
                                c.Type == VendorCashType.Applied &&
                                c.Amount == bill.AmountPaid &&
                                !mergedCashLedgerIds.Contains(c.Id) &&
                                Math.Abs((c.TransactionDate - bill.BillDate).TotalMinutes) < 10);

                            if (linked != null)
                            {
                                mergedCashLedgerIds.Add(linked.Id);
                                vcBalAfter = linked.VendorCashBalanceAfter;
                                vcBalFound = true;
                            }
                        }

                        string desc = $"Bill #{bill.Id}";
                        if (!string.IsNullOrWhiteSpace(docNum)) desc += $"  |  Doc: {docNum}";
                        if (bill.AmountPaid > 0)
                            desc += $"  |  Paid at entry: PKR {bill.AmountPaid:N2} ({bill.PaymentSource})";

                        _allBillRows.Add(new BillLedgerRow
                        {
                            SortDate = bill.BillDate,
                            Date = bill.BillDate.ToString("dd-MMM-yyyy"),
                            Type = "Bill Created",
                            Description = desc,
                            Debit = $"PKR {bill.TotalAmount:N2}",
                            Credit = paidByCashOrAcc ? $"PKR {bill.AmountPaid:N2}" : "",
                            VendorCashUsed = paidByVCash ? $"PKR {bill.AmountPaid:N2}" : "",
                            Remaining = $"PKR {bill.RemainingBalance:N2}",
                            VendorCashBalance = (paidByVCash && vcBalFound) ? $"PKR {vcBalAfter:N2}" : "",
                            Remarks = bill.Remarks ?? "",
                            RowColor = "Red"
                        });
                    }
                    else
                    {
                        var pay = (VendorPaymentRecord)ev.Data;

                        bool paidByVCash = pay.PaymentSource == "VendorCash";
                        string vcUsed = "";
                        string vcBalStr = "";

                        if (paidByVCash)
                        {
                            var linked = cashLedger.FirstOrDefault(c =>
                                c.Type == VendorCashType.Applied &&
                                c.Amount == pay.AmountPaid &&
                                !mergedCashLedgerIds.Contains(c.Id) &&
                                Math.Abs((c.TransactionDate - pay.PaymentDate).TotalMinutes) < 5);

                            if (linked != null)
                            {
                                mergedCashLedgerIds.Add(linked.Id);
                                vcBalStr = $"PKR {linked.VendorCashBalanceAfter:N2}";
                            }
                            vcUsed = $"PKR {pay.AmountPaid:N2}";
                        }

                        _allBillRows.Add(new BillLedgerRow
                        {
                            SortDate = pay.PaymentDate,
                            Date = pay.PaymentDate.ToString("dd-MMM-yyyy"),
                            Type = paidByVCash ? "V.Cash Payment" : "Payment Made",
                            Description = $"Paid to: {pay.PaidTo}  |  Source: {pay.PaymentSource}  |  Before: PKR {pay.BalanceBefore:N2}",
                            Debit = "",
                            Credit = paidByVCash ? "" : $"PKR {pay.AmountPaid:N2}",
                            VendorCashUsed = vcUsed,
                            Remaining = $"PKR {pay.BalanceAfter:N2}",
                            VendorCashBalance = vcBalStr,
                            Remarks = pay.Remarks ?? "",
                            RowColor = "Blue"
                        });
                    }
                }

                // ══════════════════════════════════════
                // TAB 2: VENDOR CASH — sirf Added entries
                // Applied/Refunded jo bill/payment mein merge hue → skip
                // Standalone Applied (jo merge nahi hue) → show
                // ══════════════════════════════════════
                _allCashRows = new List<CashLedgerRow>();

                foreach (var cash in cashLedger)
                {
                    // Agar yeh Applied row bills tab mein merge ho gayi → skip
                    if (mergedCashLedgerIds.Contains(cash.Id)) continue;

                    string typeLabel = cash.Type switch
                    {
                        VendorCashType.Added => "Vendor Cash Added",
                        VendorCashType.Applied => "Vendor Cash Applied",
                        VendorCashType.Refunded => "Vendor Cash Refunded",
                        _ => "Vendor Cash"
                    };

                    string rowColor = cash.Type == VendorCashType.Added ? "Green" : "Orange";

                    string desc = $"By: {cash.ByWhom}";
                    if (!string.IsNullOrWhiteSpace(cash.Source) && cash.Source != "VendorCash")
                        desc += $"  |  Source: {cash.Source}";

                    _allCashRows.Add(new CashLedgerRow
                    {
                        SortDate = cash.TransactionDate,
                        Date = cash.TransactionDate.ToString("dd-MMM-yyyy"),
                        Type = typeLabel,
                        Description = desc,
                        Amount = $"PKR {cash.Amount:N2}",
                        RemainingVendorCash = $"PKR {cash.VendorCashBalanceAfter:N2}",
                        Remarks = cash.Remarks ?? "",
                        RowColor = rowColor
                    });
                }

                // ── Summary ──
                decimal totalBillAmount = bills.Sum(b => b.TotalAmount);
                decimal totalPaid = payments.Sum(p => p.AmountPaid) + bills.Sum(b => b.AmountPaid);
                decimal vendorCash = await _vendorCashService.GetVendorCashBalanceAsync(_vendorId);
                decimal outstanding = await _vendorService.GetTrueRemainingBalanceAsync(_vendorId);

                _summaryTotalBills = $"PKR {totalBillAmount:N2}";
                _summaryTotalPaid = $"PKR {totalPaid:N2}";
                _summaryVendorCash = $"PKR {vendorCash:N2}";
                _summaryOutstanding = $"PKR {outstanding:N2}";

                TotalBillsText.Text = _summaryTotalBills;
                TotalPaidText.Text = _summaryTotalPaid;
                VendorCashText.Text = _summaryVendorCash;
                OutstandingText.Text = _summaryOutstanding;

                _loaded = true;
                ApplyFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading ledger:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ===========================
        // FILTER
        // ===========================
        private void Filter_Changed(object sender, SelectionChangedEventArgs e) => ApplyFilter();
        private void MainTabs_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyFilter();

        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            _loaded = false;
            FromDate.SelectedDate = null;
            ToDate.SelectedDate = null;
            _loaded = true;
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            if (!_loaded) return;

            DateTime? from = FromDate.SelectedDate;
            DateTime? to = ToDate.SelectedDate;

            if (MainTabs.SelectedIndex == 0)
            {
                var filtered = _allBillRows.AsEnumerable();
                if (from.HasValue) filtered = filtered.Where(r => r.SortDate.Date >= from.Value.Date);
                if (to.HasValue) filtered = filtered.Where(r => r.SortDate.Date <= to.Value.Date);
                var result = filtered.ToList();
                BillsGrid.ItemsSource = null;
                BillsGrid.ItemsSource = result;
                RowCountText.Text = $"{result.Count} record(s)";
            }
            else
            {
                var filtered = _allCashRows.AsEnumerable();
                if (from.HasValue) filtered = filtered.Where(r => r.SortDate.Date >= from.Value.Date);
                if (to.HasValue) filtered = filtered.Where(r => r.SortDate.Date <= to.Value.Date);
                var result = filtered.ToList();
                CashGrid.ItemsSource = null;
                CashGrid.ItemsSource = result;
                RowCountText.Text = $"{result.Count} record(s)";
            }
        }

        // ===========================
        // CLEAR HISTORY
        // ===========================
        private async void ClearHistory_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                $"Kya aap '{_vendorName}' ki poori ledger history delete karna chahte hain?\n\n" +
                "Yeh action undo nahi ho sakti.\n" +
                "Bills aur accounts data affect nahi hoga — sirf ledger view clear hogi.",
                "Clear History — Confirm",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                using var db = new AppDbContext();

                var cashEntries = await db.VendorCashLedger
                    .Where(c => c.VendorId == _vendorId)
                    .ToListAsync();
                db.VendorCashLedger.RemoveRange(cashEntries);

                await db.SaveChangesAsync();

                MessageBox.Show("Ledger history clear ho gayi.", "Done",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                _loaded = false;
                _allBillRows.Clear();
                _allCashRows.Clear();
                BillsGrid.ItemsSource = null;
                CashGrid.ItemsSource = null;
                RowCountText.Text = "0 record(s)";
                _loaded = true;

                LoadLedger();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error clearing history:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ===========================
        // DOWNLOAD PDF
        // ===========================
        private void DownloadPdf_Click(object sender, RoutedEventArgs e)
        {
            bool isBillsTab = MainTabs.SelectedIndex == 0;

            // Get currently filtered rows
            var billRows = isBillsTab
                ? (BillsGrid.ItemsSource as IEnumerable<BillLedgerRow> ?? _allBillRows).ToList()
                : new List<BillLedgerRow>();

            var cashRows = !isBillsTab
                ? (CashGrid.ItemsSource as IEnumerable<CashLedgerRow> ?? _allCashRows).ToList()
                : new List<CashLedgerRow>();

            bool isFiltered = FromDate.SelectedDate.HasValue || ToDate.SelectedDate.HasValue;
            string filterNote = isFiltered
                ? $"Filter: {(FromDate.SelectedDate.HasValue ? FromDate.SelectedDate.Value.ToString("dd-MMM-yyyy") : "Start")} — {(ToDate.SelectedDate.HasValue ? ToDate.SelectedDate.Value.ToString("dd-MMM-yyyy") : "End")}"
                : "All Records";

            string tabName = isBillsTab ? "Bills_Payments" : "Vendor_Cash";

            string filePath = "";
            var dlg = new SaveFileDialog
            {
                Title = "Save Ledger PDF",
                Filter = "PDF Files (*.pdf)|*.pdf",
                DefaultExt = "pdf",
                FileName = $"Ledger_{_vendorName}_{tabName}_{DateTime.Now:yyyyMMdd_HHmmss}"
            };
            if (dlg.ShowDialog() != true) return;
            filePath = dlg.FileName;

            try
            {
                QuestPDF.Settings.License = LicenseType.Community;

                string logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "logo.png");

                Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4.Landscape());
                        page.Margin(18);
                        page.DefaultTextStyle(x => x.FontSize(8.5f).FontFamily("Arial"));

                        // ── HEADER ──
                        page.Header().Column(col =>
                        {
                            col.Item().Row(row =>
                            {
                                if (File.Exists(logoPath))
                                    row.ConstantItem(55).AlignMiddle().Image(logoPath);

                                row.RelativeItem().AlignMiddle().PaddingLeft(10).Column(c =>
                                {
                                    c.Item().Text("RAMZA ELECTRIC E-BIKES SWABI")
                                        .Bold().FontSize(14).FontColor(Color.FromHex("#1B4079"));
                                    c.Item().Text("Main Jahangira Road Near Sadaat CNG, Swabi")
                                        .FontSize(8).FontColor(Color.FromHex("#555555"));
                                    c.Item().Text("+92 345 9996397")
                                        .FontSize(8).FontColor(Color.FromHex("#555555"));
                                });

                                row.ConstantItem(200).AlignMiddle().AlignRight().Column(c =>
                                {
                                    c.Item().Text(isBillsTab ? "VENDOR LEDGER — BILLS & PAYMENTS" : "VENDOR LEDGER — VENDOR CASH")
                                        .Bold().FontSize(11).FontColor(Color.FromHex("#1B4079"));
                                    c.Item().Text($"Vendor: {_vendorName}").Bold().FontSize(9);
                                    c.Item().Text($"Generated: {DateTime.Now:dd-MMM-yyyy hh:mm tt}").FontSize(8);
                                    c.Item().Text(filterNote).FontSize(8).FontColor(Color.FromHex("#666666"));
                                });
                            });

                            col.Item().PaddingTop(6).LineHorizontal(1).LineColor(Color.FromHex("#1B4079"));

                            // Summary bar
                            col.Item().PaddingTop(6).Row(row =>
                            {
                                void SummaryBox(string label, string val, string color)
                                    => row.RelativeItem().Border(0.5f).BorderColor(Color.FromHex("#CCCCCC"))
                                        .Padding(5).Column(c =>
                                        {
                                            c.Item().Text(label).FontSize(7.5f).FontColor(Color.FromHex("#666666"));
                                            c.Item().Text(val).Bold().FontSize(9).FontColor(Color.FromHex(color));
                                        });

                                SummaryBox("Total Bills", _summaryTotalBills, "#1B4079");
                                SummaryBox("Total Paid", _summaryTotalPaid, "#1B8A3D");
                                SummaryBox("Vendor Cash", _summaryVendorCash, "#6A1B9A");
                                SummaryBox("Outstanding", _summaryOutstanding, "#B00020");
                            });

                            col.Item().PaddingTop(6).LineHorizontal(0.5f).LineColor(Color.FromHex("#CCCCCC"));
                        });

                        // ── CONTENT ──
                        page.Content().PaddingTop(8).Column(col =>
                        {
                            if (isBillsTab)
                                BuildBillsPdfTable(col, billRows);
                            else
                                BuildCashPdfTable(col, cashRows);
                        });

                        // ── FOOTER ──
                        page.Footer().AlignCenter().Text(t =>
                        {
                            t.Span("Page ").FontSize(8);
                            t.CurrentPageNumber().FontSize(8);
                            t.Span(" of ").FontSize(8);
                            t.TotalPages().FontSize(8);
                            t.Span($"  |  Ramza Electric E-Bikes Swabi  |  {DateTime.Now:dd-MMM-yyyy}")
                                .FontSize(8).FontColor(Color.FromHex("#666666"));
                        });
                    });
                }).GeneratePdf(filePath);

                if (MessageBox.Show("PDF ready. Open karna chahte hain?", "Done",
                    MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                    Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"PDF error:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static void BuildBillsPdfTable(ColumnDescriptor col, List<BillLedgerRow> rows)
        {
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(75);  // Date
                    c.ConstantColumn(95);  // Type
                    c.RelativeColumn();    // Description
                    c.ConstantColumn(85);  // Bill Amount
                    c.ConstantColumn(90);  // Payment
                    c.ConstantColumn(80);  // V.Cash Used
                    c.ConstantColumn(90);  // Remaining
                    c.ConstantColumn(85);  // V.Cash Bal
                    c.ConstantColumn(100); // Remarks
                });

                // Header
                void Hdr(string t) => table.Cell().Background(Color.FromHex("#1B4079"))
                    .Padding(4).Text(t).Bold().FontSize(7.5f).FontColor(Colors.White);

                Hdr("Date"); Hdr("Type"); Hdr("Description");
                Hdr("Bill Amount"); Hdr("Payment"); Hdr("V.Cash Used");
                Hdr("Remaining"); Hdr("V.Cash Bal."); Hdr("Remarks");

                int rowNum = 0;
                foreach (var r in rows)
                {
                    rowNum++;
                    string bg = r.RowColor == "Red" ? "#FFF8F8"
                              : r.RowColor == "Blue" ? "#F0F7FF"
                              : "#FFFFFF";

                    void Cell(string txt, string? color = null, bool bold = false, bool right = false)
                    {
                        var cell = table.Cell().Background(Color.FromHex(bg)).Padding(3);
                        var text = cell.Text(txt ?? "").FontSize(7.5f);
                        if (bold) text.Bold();
                        if (color != null) text.FontColor(Color.FromHex(color));
                        if (right) { /* QuestPDF text alignment via column style */ }
                    }

                    table.Cell().Background(Color.FromHex(bg)).Padding(3).Text(r.Date).FontSize(7.5f);
                    table.Cell().Background(Color.FromHex(bg)).Padding(3).Text(r.Type).Bold().FontSize(7.5f);
                    table.Cell().Background(Color.FromHex(bg)).Padding(3).Text(r.Description).FontSize(7f);
                    table.Cell().Background(Color.FromHex(bg)).Padding(3).AlignRight().Text(r.Debit).FontSize(7.5f).FontColor(Color.FromHex("#B00020"));
                    table.Cell().Background(Color.FromHex(bg)).Padding(3).AlignRight().Text(r.Credit).FontSize(7.5f).FontColor(Color.FromHex("#1B8A3D"));
                    table.Cell().Background(Color.FromHex(bg)).Padding(3).AlignRight().Text(r.VendorCashUsed).FontSize(7.5f).FontColor(Color.FromHex("#6A1B9A"));
                    table.Cell().Background(Color.FromHex(bg)).Padding(3).AlignRight().Text(r.Remaining).Bold().FontSize(7.5f);
                    table.Cell().Background(Color.FromHex(bg)).Padding(3).AlignRight().Text(r.VendorCashBalance).FontSize(7.5f).FontColor(Color.FromHex("#6A1B9A"));
                    table.Cell().Background(Color.FromHex(bg)).Padding(3).Text(r.Remarks).FontSize(7f).FontColor(Color.FromHex("#555555"));
                }

                if (!rows.Any())
                {
                    for (int i = 0; i < 9; i++)
                        table.Cell().Padding(6).Text(i == 0 ? "No records found." : "").FontSize(8);
                }
            });
        }

        private static void BuildCashPdfTable(ColumnDescriptor col, List<CashLedgerRow> rows)
        {
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(85);  // Date
                    c.ConstantColumn(130); // Type
                    c.RelativeColumn();    // Description
                    c.ConstantColumn(100); // Amount
                    c.ConstantColumn(120); // Remaining V.Cash
                    c.ConstantColumn(180); // Remarks
                });

                void Hdr(string t) => table.Cell().Background(Color.FromHex("#1B4079"))
                    .Padding(4).Text(t).Bold().FontSize(7.5f).FontColor(Colors.White);

                Hdr("Date"); Hdr("Type"); Hdr("Description");
                Hdr("Amount"); Hdr("Remaining V.Cash"); Hdr("Remarks");

                foreach (var r in rows)
                {
                    string bg = r.RowColor == "Green" ? "#F1F8F1" : "#FFFDF0";

                    table.Cell().Background(Color.FromHex(bg)).Padding(3).Text(r.Date).FontSize(7.5f);
                    table.Cell().Background(Color.FromHex(bg)).Padding(3).Text(r.Type).Bold().FontSize(7.5f);
                    table.Cell().Background(Color.FromHex(bg)).Padding(3).Text(r.Description).FontSize(7f);
                    table.Cell().Background(Color.FromHex(bg)).Padding(3).AlignRight().Text(r.Amount).Bold().FontSize(7.5f);
                    table.Cell().Background(Color.FromHex(bg)).Padding(3).AlignRight().Text(r.RemainingVendorCash).Bold().FontSize(7.5f).FontColor(Color.FromHex("#6A1B9A"));
                    table.Cell().Background(Color.FromHex(bg)).Padding(3).Text(r.Remarks).FontSize(7f).FontColor(Color.FromHex("#555555"));
                }

                if (!rows.Any())
                {
                    for (int i = 0; i < 6; i++)
                        table.Cell().Padding(6).Text(i == 0 ? "No records found." : "").FontSize(8);
                }
            });
        }

        // ===========================
        // BACK
        // ===========================
        private void Back_Click(object sender, RoutedEventArgs e)
            => _layout.Navigate(new VendorPurchasePage(_layout, _vendorId));
    }
}