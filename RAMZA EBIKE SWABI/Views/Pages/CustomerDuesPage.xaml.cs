using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ClosedXML.Excel;
using Ramza_EBike_Swabi.Models;
using Ramza_EBike_Swabi.Services;
using Ramza_EBike_Swabi.Services.Pdf;

namespace Ramza_EBike_Swabi.Views.Pages
{
    // ── ViewModel wrapper ────────────────────────────────────────────────────
    public class CustomerDueRow
    {
        private readonly CustomerInvoice _invoice;

        public CustomerDueRow(CustomerInvoice invoice) => _invoice = invoice;

        // Forwarded properties
        public int CustomerInvoiceId => _invoice.CustomerInvoiceId;
        public Customer Customer => _invoice.Customer;
        public decimal NetBill => _invoice.NetBill;
        public decimal AmountPaid => _invoice.AmountPaid;
        public decimal RemainingBalance => _invoice.RemainingBalance;
        public DateTime? DueDate => _invoice.DueDate;
        public string Status => _invoice.Status;
        public CustomerInvoice Original => _invoice;

        // ── Computed display properties ──────────────────────────────────────

        public bool HasDueDate => _invoice.DueDate.HasValue;

        public bool IsDueToday =>
            _invoice.DueDate.HasValue &&
            _invoice.DueDate.Value.Date == DateTime.Today;

        public bool IsOverdue =>
            _invoice.DueDate.HasValue &&
            _invoice.DueDate.Value.Date < DateTime.Today;

        public string DueDateDisplay
        {
            get
            {
                if (!_invoice.DueDate.HasValue) return "—";
                if (IsDueToday) return "🔔 Today";
                if (IsOverdue) return $"⚠ {_invoice.DueDate.Value:dd MMM yy}";
                return _invoice.DueDate.Value.ToString("dd MMM yyyy");
            }
        }

        public string DueDateColor
        {
            get
            {
                if (IsDueToday) return "#E67E22";   // orange
                if (IsOverdue) return "#E74C3C";    // red
                return "#27AE60";                    // green — future date
            }
        }

        public string DueDateFontWeight =>
            (IsDueToday || IsOverdue) ? "Bold" : "Normal";
    }

    // ── Page code-behind ─────────────────────────────────────────────────────
    public partial class CustomerDuesPage : Page
    {
        private readonly InvoiceService _service = new();
        private List<CustomerDueRow> _allDues = new();
        private readonly HashSet<int> _notifiedIds = new();

        public CustomerDuesPage()
        {
            InitializeComponent();
            _ = LoadAsync();
        }

        // ── Load ─────────────────────────────────────────────────────────────
        private async Task LoadAsync()
        {
            var list = await _service.GetAllInvoicesAsync();

            _allDues = list
                .Where(i => i.RemainingBalance > 0)
                .Select(i => new CustomerDueRow(i))
                .ToList();

            dgDues.ItemsSource = _allDues.ToList();
            ShowDueNotifications();
        }

        // ── Notification banner ───────────────────────────────────────────────
        private void ShowDueNotifications()
        {
            var dueToday = _allDues
                .Where(r => r.IsDueToday)
                .Select(r => r.Original)
                .ToList();

            var overdue = _allDues
                .Where(r => r.IsOverdue)
                .Select(r => r.Original)
                .ToList();

            if (dueToday.Count == 0 && overdue.Count == 0)
            {
                pnlDueNotification.Visibility = Visibility.Collapsed;
                return;
            }

            var parts = new List<string>();
            if (dueToday.Count > 0)
                parts.Add($"{dueToday.Count} payment{(dueToday.Count > 1 ? "s" : "")} due TODAY");
            if (overdue.Count > 0)
                parts.Add($"{overdue.Count} OVERDUE payment{(overdue.Count > 1 ? "s" : "")}");

            lblDueNotificationTitle.Text = "⚠  " + string.Join("  |  ", parts);
            lstDueToday.ItemsSource = dueToday.Concat(overdue).ToList();
            pnlDueNotification.Visibility = Visibility.Visible;
        }

        private void DismissNotification_Click(object sender, RoutedEventArgs e)
            => pnlDueNotification.Visibility = Visibility.Collapsed;

        // ── WhatsApp All (banner button) ──────────────────────────────────────
        private void NotifyAll_Click(object sender, RoutedEventArgs e)
        {
            var targets = _allDues
                .Where(r => (r.IsDueToday || r.IsOverdue) &&
                             !_notifiedIds.Contains(r.CustomerInvoiceId))
                .Select(r => r.Original)
                .ToList();

            if (targets.Count == 0)
            {
                MessageBox.Show(
                    "All due/overdue customers have already been notified this session.",
                    "Nothing to send",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show(
                $"Send WhatsApp reminders to {targets.Count} customer(s)?\n\n" +
                string.Join("\n", targets.Select(i =>
                    $"  • {i.Customer?.Name}  ({i.Customer?.Contact})  —  ₨ {i.RemainingBalance:N0}")),
                "Confirm WhatsApp Reminders",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            foreach (var inv in targets)
            {
                OpenWhatsApp(inv);
                _notifiedIds.Add(inv.CustomerInvoiceId);
                System.Threading.Thread.Sleep(700);
            }
        }

        // ── WhatsApp single row (📲 button) ───────────────────────────────────
        private void Notify_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is not CustomerDueRow row) return;

            var confirm = MessageBox.Show(
                $"Send a WhatsApp payment reminder to:\n\n" +
                $"  Customer  : {row.Customer?.Name}\n" +
                $"  Contact   : {row.Customer?.Contact}\n" +
                $"  Remaining : ₨ {row.RemainingBalance:N0}\n" +
                $"  Due Date  : {row.DueDateDisplay}",
                "Send WhatsApp Reminder",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm == MessageBoxResult.Yes)
            {
                OpenWhatsApp(row.Original);
                _notifiedIds.Add(row.CustomerInvoiceId);
            }
        }

        // ── Core WhatsApp helper ──────────────────────────────────────────────
        private static void OpenWhatsApp(CustomerInvoice invoice)
        {
            string raw = invoice.Customer?.Contact ?? "";

            string phone = raw
                .Replace(" ", "")
                .Replace("-", "")
                .Replace("(", "")
                .Replace(")", "")
                .Trim();

            if (phone.StartsWith("0") && phone.Length >= 10)
                phone = "92" + phone[1..];

            if (!phone.StartsWith("92") && phone.Length == 10)
                phone = "92" + phone;

            string dueDate = invoice.DueDate.HasValue
                ? invoice.DueDate.Value.ToString("dd MMM yyyy")
                : "جلد از جلد";
            string customerName = invoice.Customer?.Name ?? "محترم";
            string invoiceNo = $"INV-{invoice.CustomerInvoiceId:D4}";
            string remaining = invoice.RemainingBalance.ToString("N0");

            string message =
                "🌟 *بِسْمِ اللہِ الرَّحْمٰنِ الرَّحِیْم* 🌟" + "\n\n" +
                "السلام علیکم و رحمۃ اللہ و برکاتہ،" + "\n\n" +
                $"محترم *{customerName}* صاحب،" + "\n\n" +
                "آپ کو مودبانہ اطلاع دی جاتی ہے کہ" + "\n" +
                "*صوابی انٹرپرائزز* کی جانب سے" + "\n" +
                "آپ کی درج ذیل رقم واجب الادا ہے۔" + "\n\n" +
                "🧾 *انوائس نمبر*" + "\n" +
                $"     {invoiceNo}" + "\n\n" +
                "💰 *واجب الادا رقم*" + "\n" +
                $"     روپے *{remaining}*" + "\n\n" +
                "📅 *ادائیگی کی آخری تاریخ*" + "\n" +
                $"     *{dueDate}*" + "\n\n" +
                "⚠️ *اہم گزارش*" + "\n" +
                "براہ کرم مقررہ تاریخ تک رقم ادا فرمائیں" + "\n" +
                "تاکہ کسی قسم کی پریشانی سے بچا جا سکے۔" + "\n\n" +
                "✅ اگر ادائیگی ہو چکی ہے تو" + "\n" +
                "اس پیغام کو نظر انداز فرمائیں۔" + "\n\n" +
                "📞 مزید معلومات کے لیے ہم سے رابطہ کریں۔" + "\n\n" +
                "🏪 *صوابی انٹرپرائزز*" + "\n" +
                "📍 *صوابی*" + "\n\n" +
                "جزاک اللہ خیر 🤲";

            string encoded = Uri.EscapeDataString(message);
            string url = $"https://wa.me/{phone}?text={encoded}";

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"WhatsApp نہیں کھل سکا۔\n\n" +
                    $"براہ کرم یقینی بنائیں کہ براؤزر انسٹال ہے۔\n\n" +
                    $"خرابی: {ex.Message}",
                    "WhatsApp خرابی",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        // ── Search ────────────────────────────────────────────────────────────
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
            => ApplyFilter();

        private void Search_Click(object sender, RoutedEventArgs e)
            => ApplyFilter();

        private void DateFilter_Changed(object sender, SelectionChangedEventArgs e)
            => ApplyFilter();

        private void ClearDates_Click(object sender, RoutedEventArgs e)
        {
            dpFrom.SelectedDate = null;
            dpTo.SelectedDate = null;
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            string q = SearchBox.Text.Trim().ToLower();
            DateTime? from = dpFrom.SelectedDate;
            DateTime? to = dpTo.SelectedDate;

            var result = _allDues.AsEnumerable();

            // Text filter (name or CNIC)
            if (!string.IsNullOrWhiteSpace(q))
                result = result.Where(r =>
                    (r.Customer?.Name?.ToLower().Contains(q) == true) ||
                    (r.Customer?.CNIC?.ToLower().Contains(q) == true));

            // Date-range filter on InvoiceDate
            if (from.HasValue)
                result = result.Where(r => r.Original.InvoiceDate.Date >= from.Value.Date);
            if (to.HasValue)
                result = result.Where(r => r.Original.InvoiceDate.Date <= to.Value.Date);

            dgDues.ItemsSource = result.ToList();
        }

        // ── Navigation ────────────────────────────────────────────────────────
        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainLayout layout)
                layout.Navigate(new DashboardPage(layout));
        }

        // ── PDF ───────────────────────────────────────────────────────────────
        private void Pdf_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is CustomerDueRow row)
                InvoicePdfService.Generate(row.Original);
        }

        // ── Edit ──────────────────────────────────────────────────────────────
        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is not CustomerDueRow row) return;
            var invoice = row.Original;

            var layout = Window.GetWindow(this) as MainLayout;
            if (layout != null)
            {
                if (layout.InvoiceTabManager == null)
                    layout.InvoiceTabManager = new InvoiceTabManagerPage(layout);

                layout.Navigate(layout.InvoiceTabManager);
                _ = layout.InvoiceTabManager.OpenInvoiceForEditAsync(invoice);
            }
            else
            {
                NavigationService?.Navigate(new GenerateInvoicePage(invoice));
            }
        }

        // ── Pay ───────────────────────────────────────────────────────────────
        private async void Pay_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is not CustomerDueRow row) return;

            var win = new PayDueWindow(row.Original)
            {
                Owner = Window.GetWindow(this)
            };

            if (win.ShowDialog() == true)
                await LoadAsync();
        }

        // ── History ───────────────────────────────────────────────────────────
        private void History_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is not CustomerDueRow row) return;

            var win = new PaymentHistoryWindow(row.Original)
            {
                Owner = Window.GetWindow(this)
            };
            win.ShowDialog();
        }

        // ── Download Excel ────────────────────────────────────────────────────
        //
        // Final column layout (18 columns, A–R):
        //
        //   Col  Letter  Header           Merge when >1 bike?
        //    1     A     Invoice #         YES  ─┐
        //    2     B     Invoice Date      YES   │  invoice-level info
        //    3     C     Customer Name     YES   │
        //    4     D     CNIC              YES   │
        //    5     E     Contact           YES  ─┘
        //    6     F     Bike Model        NO   ─┐
        //    7     G     Brand             NO    │
        //    8     H     Motor No          NO    │  per-bike rows
        //    9     I     Chassis No        NO    │
        //   10     J     Price (₨)         NO    │
        //   11     K     Qty               NO    │
        //   12     L     Total (₨)         NO    │  Price × Qty
        //   13     M     Discount (₨)      NO   ─┘
        //   14     N     Net Bill (₨)      YES  ─┐
        //   15     O     Paid (₨)          YES   │  financial summary
        //   16     P     Remaining (₨)     YES   │
        //   17     Q     Due Date          YES   │
        //   18     R     Status            YES  ─┘
        //
        private void Download_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var rows = (dgDues.ItemsSource as List<CustomerDueRow>) ?? _allDues;

                if (!rows.Any())
                {
                    MessageBox.Show("No records to export.", "Nothing to Download",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var dialog = new SaveFileDialog
                {
                    Filter = "Excel File (*.xlsx)|*.xlsx",
                    FileName = $"CustomerDues_{DateTime.Now:yyyyMMdd}.xlsx"
                };
                if (dialog.ShowDialog() != true) return;

                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add("Customer Dues");

                // ── Palette ───────────────────────────────────────────────────
                var headerBg = XLColor.FromHtml("#2B579A");
                var headerFg = XLColor.White;
                var titleBg = XLColor.FromHtml("#1E3A6E");
                var titleFg = XLColor.White;
                var subtitleBg = XLColor.FromHtml("#D6E4F0");
                var altRowBg = XLColor.FromHtml("#F2F7FB");
                var totalBg = XLColor.FromHtml("#E8F0FE");
                var totalFg = XLColor.FromHtml("#1A237E");
                var borderColor = XLColor.FromHtml("#B0BEC5");

                const int TOTAL_COLS = 18; // A–R

                // ── Column index constants (1-based) ──────────────────────────
                const int C_INV_NO = 1;
                const int C_INV_DATE = 2;
                const int C_CUST_NAME = 3;
                const int C_CNIC = 4;
                const int C_CONTACT = 5;
                const int C_MODEL = 6;
                const int C_BRAND = 7;
                const int C_MOTOR = 8;
                const int C_CHASSIS = 9;
                const int C_PRICE = 10;
                const int C_QTY = 11;
                const int C_TOTAL = 12;
                const int C_DISCOUNT = 13;
                const int C_NET_BILL = 14;
                const int C_PAID = 15;
                const int C_REMAINING = 16;
                const int C_DUE_DATE = 17;
                const int C_STATUS = 18;

                int row = 1;

                // ── Row 1: Company Title ──────────────────────────────────────
                var titleRange = ws.Range(row, 1, row, TOTAL_COLS);
                titleRange.Merge();
                titleRange.Value = "Swabi Enterprises";
                titleRange.Style
                    .Font.SetBold(true)
                    .Font.SetFontSize(16)
                    .Font.SetFontColor(titleFg)
                    .Fill.SetBackgroundColor(titleBg)
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                    .Alignment.SetVertical(XLAlignmentVerticalValues.Center);
                ws.Row(row).Height = 28;
                row++;

                // ── Row 2: Subtitle ───────────────────────────────────────────
                var subRange = ws.Range(row, 1, row, TOTAL_COLS);
                subRange.Merge();
                subRange.Value = $"Customer Dues Report  |  As of {DateTime.Now:dd-MMM-yyyy}";
                subRange.Style
                    .Font.SetFontSize(11)
                    .Font.SetFontColor(XLColor.FromHtml("#1E3A6E"))
                    .Fill.SetBackgroundColor(subtitleBg)
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                    .Alignment.SetVertical(XLAlignmentVerticalValues.Center);
                ws.Row(row).Height = 22;
                row++;

                // ── Row 3: Generated On ───────────────────────────────────────
                var genRange = ws.Range(row, 1, row, TOTAL_COLS);
                genRange.Merge();
                genRange.Value = $"Generated on: {DateTime.Now:dd-MMM-yyyy  HH:mm}";
                genRange.Style
                    .Font.SetFontSize(9)
                    .Font.SetItalic(true)
                    .Font.SetFontColor(XLColor.Gray)
                    .Fill.SetBackgroundColor(XLColor.FromHtml("#FAFAFA"))
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
                ws.Row(row).Height = 16;
                row++;

                // ── Blank spacer ──────────────────────────────────────────────
                row++;

                // ── Column Headers ────────────────────────────────────────────
                int headerRow = row;
                string[] headers =
                {
                    "Invoice #",      // 1  A
                    "Invoice Date",   // 2  B
                    "Customer Name",  // 3  C
                    "CNIC",           // 4  D
                    "Contact",        // 5  E
                    "Bike Model",     // 6  F
                    "Brand",          // 7  G
                    "Motor No",       // 8  H
                    "Chassis No",     // 9  I
                    "Price (₨)",      // 10 J
                    "Qty",            // 11 K
                    "Total (₨)",      // 12 L
                    "Discount (₨)",   // 13 M
                    "Net Bill (₨)",   // 14 N
                    "Paid (₨)",       // 15 O
                    "Remaining (₨)",  // 16 P
                    "Due Date",       // 17 Q
                    "Status"          // 18 R
                };

                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = ws.Cell(row, i + 1);
                    cell.Value = headers[i];
                    cell.Style
                        .Font.SetBold(true)
                        .Font.SetFontSize(10)
                        .Font.SetFontColor(headerFg)
                        .Fill.SetBackgroundColor(headerBg)
                        .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                        .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
                        .Border.SetOutsideBorder(XLBorderStyleValues.Thin)
                        .Border.SetOutsideBorderColor(borderColor);
                }
                ws.Row(row).Height = 20;
                row++;

                // ── Data Rows ─────────────────────────────────────────────────
                int dataStartRow = row;
                bool alternate = false;

                foreach (var dueRow in rows)
                {
                    var bikeItems = dueRow.Original.Items
                        .Where(bi => !string.IsNullOrWhiteSpace(bi.MotorNumber))
                        .ToList();

                    var itemsToWrite = bikeItems.Any()
                        ? bikeItems
                        : new List<CustomerInvoiceItem> { null };

                    int firstRowOfInvoice = row;
                    int bikeCount = itemsToWrite.Count;

                    // ── Per-bike rows (cols 6–13, never merged) ───────────────
                    foreach (var bikeItem in itemsToWrite)
                    {
                        var rowBg = alternate ? altRowBg : XLColor.White;

                        decimal bikePrice = bikeItem?.Price ?? 0m;
                        int bikeQty = bikeItem?.Quantity ?? 0;
                        decimal bikeTotal = bikePrice * bikeQty;
                        // Discount is invoice-level — written in the merged columns section below

                        // Local helper: write a single per-bike cell
                        void WritePerBikeCell(int col, object val,
                            bool rightAlign = false, string fmt = null)
                        {
                            var c = ws.Cell(row, col);

                            if (val is decimal dv) c.Value = XLCellValue.FromObject(dv);
                            else if (val is int iv) c.Value = XLCellValue.FromObject(iv);
                            else c.Value = XLCellValue.FromObject(val?.ToString() ?? "");

                            c.Style
                                .Fill.SetBackgroundColor(rowBg)
                                .Font.SetFontSize(10)
                                .Border.SetOutsideBorder(XLBorderStyleValues.Thin)
                                .Border.SetOutsideBorderColor(borderColor)
                                .Alignment.SetHorizontal(rightAlign
                                    ? XLAlignmentHorizontalValues.Right
                                    : XLAlignmentHorizontalValues.Left);

                            if (fmt != null)
                                c.Style.NumberFormat.Format = fmt;
                        }

                        WritePerBikeCell(C_MODEL, bikeItem?.Model ?? "-");
                        WritePerBikeCell(C_BRAND, bikeItem?.Brand ?? "-");
                        WritePerBikeCell(C_MOTOR, bikeItem?.MotorNumber ?? "-");
                        WritePerBikeCell(C_CHASSIS, bikeItem?.ChassisNumber ?? "-");
                        WritePerBikeCell(C_PRICE, bikePrice, rightAlign: true, fmt: "#,##0");
                        WritePerBikeCell(C_QTY, bikeQty, rightAlign: true);
                        WritePerBikeCell(C_TOTAL, bikeTotal, rightAlign: true, fmt: "#,##0");
                        // C_DISCOUNT is invoice-level — written in the merged section below

                        ws.Row(row).Height = 18;
                        alternate = !alternate;
                        row++;
                    }

                    int lastRowOfInvoice = row - 1;
                    var mergeBg = firstRowOfInvoice % 2 == 0 ? XLColor.White : altRowBg;

                    // ── Helper: write a cell/range that merges when bikeCount > 1 ──
                    void WriteMergedColumn(int col, object val,
                        bool rightAlign = false, bool center = false, string fmt = null)
                    {
                        if (bikeCount > 1)
                        {
                            var rng = ws.Range(firstRowOfInvoice, col, lastRowOfInvoice, col);
                            rng.Merge();

                            if (val is decimal dv) rng.Value = dv;
                            else if (val is int iv) rng.Value = iv;
                            else rng.Value = val?.ToString() ?? "";

                            rng.Style
                                .Fill.SetBackgroundColor(mergeBg)
                                .Font.SetFontSize(10)
                                .Border.SetOutsideBorder(XLBorderStyleValues.Thin)
                                .Border.SetOutsideBorderColor(borderColor)
                                .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
                                .Alignment.SetHorizontal(
                                    center ? XLAlignmentHorizontalValues.Center :
                                    rightAlign ? XLAlignmentHorizontalValues.Right :
                                                 XLAlignmentHorizontalValues.Left);

                            if (fmt != null)
                                rng.Style.NumberFormat.Format = fmt;
                        }
                        else
                        {
                            var c = ws.Cell(firstRowOfInvoice, col);

                            if (val is decimal dv) c.Value = XLCellValue.FromObject(dv);
                            else if (val is int iv) c.Value = XLCellValue.FromObject(iv);
                            else c.Value = XLCellValue.FromObject(val?.ToString() ?? "");

                            c.Style
                                .Fill.SetBackgroundColor(mergeBg)
                                .Font.SetFontSize(10)
                                .Border.SetOutsideBorder(XLBorderStyleValues.Thin)
                                .Border.SetOutsideBorderColor(borderColor)
                                .Alignment.SetHorizontal(
                                    center ? XLAlignmentHorizontalValues.Center :
                                    rightAlign ? XLAlignmentHorizontalValues.Right :
                                                 XLAlignmentHorizontalValues.Left);

                            if (fmt != null)
                                c.Style.NumberFormat.Format = fmt;
                        }
                    }

                    // Cols 1–5: invoice-level + customer info (merged when >1 bike)
                    WriteMergedColumn(C_INV_NO, dueRow.CustomerInvoiceId, center: true);
                    WriteMergedColumn(C_INV_DATE,
                        dueRow.Original.InvoiceDate.ToString("dd-MMM-yyyy"),
                        center: true);
                    WriteMergedColumn(C_CUST_NAME, dueRow.Customer?.Name ?? "-");
                    WriteMergedColumn(C_CNIC, dueRow.Customer?.CNIC ?? "-");
                    WriteMergedColumn(C_CONTACT, dueRow.Customer?.Contact ?? "-");

                    // Cols 13–18: financial summary (merged when >1 bike)
                    WriteMergedColumn(C_DISCOUNT, dueRow.Original.Discount, rightAlign: true, fmt: "#,##0");
                    WriteMergedColumn(C_NET_BILL, dueRow.NetBill, rightAlign: true, fmt: "#,##0");
                    WriteMergedColumn(C_PAID, dueRow.AmountPaid, rightAlign: true, fmt: "#,##0");
                    WriteMergedColumn(C_REMAINING, dueRow.RemainingBalance, rightAlign: true, fmt: "#,##0");
                    WriteMergedColumn(C_DUE_DATE,
                        dueRow.DueDate.HasValue
                            ? dueRow.DueDate.Value.ToString("dd-MMM-yyyy")
                            : "—",
                        center: true);
                    WriteMergedColumn(C_STATUS, dueRow.Status ?? "-", center: true);
                }

                int dataEndRow = row - 1;

                // ── Totals Row ────────────────────────────────────────────────
                // Label spans cols 1–9 (A–I)
                var totalLabelRange = ws.Range(row, 1, row, C_CHASSIS);
                totalLabelRange.Merge();
                totalLabelRange.Value = "GRAND TOTAL";
                totalLabelRange.Style
                    .Font.SetBold(true)
                    .Font.SetFontSize(11)
                    .Font.SetFontColor(totalFg)
                    .Fill.SetBackgroundColor(totalBg)
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right)
                    .Border.SetOutsideBorder(XLBorderStyleValues.Medium)
                    .Border.SetOutsideBorderColor(XLColor.FromHtml("#2B579A"));

                // Helper: SUM formula cell in totals row
                void WriteTotalSumCell(int col, string colLetter)
                {
                    var c = ws.Cell(row, col);
                    c.FormulaA1 = $"=SUM({colLetter}{dataStartRow}:{colLetter}{dataEndRow})";
                    c.Style
                        .Font.SetBold(true)
                        .Font.SetFontSize(11)
                        .Font.SetFontColor(totalFg)
                        .Fill.SetBackgroundColor(totalBg)
                        .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right)
                        .Border.SetOutsideBorder(XLBorderStyleValues.Medium)
                        .Border.SetOutsideBorderColor(XLColor.FromHtml("#2B579A"));
                    c.Style.NumberFormat.Format = "#,##0";
                }

                // Helper: empty styled cell in totals row
                void WriteTotalEmptyCell(int col)
                {
                    ws.Cell(row, col).Style
                        .Fill.SetBackgroundColor(totalBg)
                        .Border.SetOutsideBorder(XLBorderStyleValues.Medium)
                        .Border.SetOutsideBorderColor(XLColor.FromHtml("#2B579A"));
                }

                WriteTotalEmptyCell(C_PRICE);          // J  — no unit-price total
                WriteTotalSumCell(C_QTY, "K");    // K  Qty
                WriteTotalSumCell(C_TOTAL, "L");    // L  Total
                WriteTotalSumCell(C_DISCOUNT, "M");    // M  Discount
                WriteTotalSumCell(C_NET_BILL, "N");    // N  Net Bill
                WriteTotalSumCell(C_PAID, "O");    // O  Paid
                WriteTotalSumCell(C_REMAINING, "P");    // P  Remaining
                WriteTotalEmptyCell(C_DUE_DATE);       // Q
                WriteTotalEmptyCell(C_STATUS);         // R

                ws.Row(row).Height = 22;

                // ── Column Widths ─────────────────────────────────────────────
                ws.Column(C_INV_NO).Width = 10;  // Invoice #
                ws.Column(C_INV_DATE).Width = 14;  // Invoice Date
                ws.Column(C_CUST_NAME).Width = 22;  // Customer Name
                ws.Column(C_CNIC).Width = 18;  // CNIC
                ws.Column(C_CONTACT).Width = 14;  // Contact
                ws.Column(C_MODEL).Width = 16;  // Bike Model
                ws.Column(C_BRAND).Width = 12;  // Brand
                ws.Column(C_MOTOR).Width = 16;  // Motor No
                ws.Column(C_CHASSIS).Width = 16;  // Chassis No
                ws.Column(C_PRICE).Width = 13;  // Price
                ws.Column(C_QTY).Width = 6;  // Qty
                ws.Column(C_TOTAL).Width = 14;  // Total
                ws.Column(C_DISCOUNT).Width = 13;  // Discount
                ws.Column(C_NET_BILL).Width = 14;  // Net Bill
                ws.Column(C_PAID).Width = 13;  // Paid
                ws.Column(C_REMAINING).Width = 14;  // Remaining
                ws.Column(C_DUE_DATE).Width = 14;  // Due Date
                ws.Column(C_STATUS).Width = 10;  // Status

                // ── Freeze header row ─────────────────────────────────────────
                ws.SheetView.FreezeRows(headerRow);

                wb.SaveAs(dialog.FileName);
                MessageBox.Show("Excel file downloaded successfully.", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Download failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}