// Views/Pages/ZakatPage.xaml.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using Ramza_EBike_Swabi.Models;
using Ramza_EBike_Swabi.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Ramza_EBike_Swabi.Views.Pages
{
    public partial class ZakatPage : Page
    {
        private readonly MainLayout _layout;
        private readonly ZakatService _zakatService = new();
        private readonly AccountService _accountService = new();
        private readonly VendorCashService _vendorCashService = new();
        private readonly BikeService _bikeService = new();

        private ZakatRecord? _currentRecord;

        public ZakatPage(MainLayout layout)
        {
            InitializeComponent();
            _layout = layout;

            // Use Dispatcher so page is fully rendered before DB call
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded,
                new Action(async () => await LoadListAsync()));
        }

        // ═════════════════════════════════════════════════════════
        // LIST — uses flat display model (no computed property binding)
        // ═════════════════════════════════════════════════════════
        private async Task LoadListAsync()
        {
            try
            {
                var records = await _zakatService.GetAllAsync();

                var items = records.Select(r => new ZakatListItem
                {
                    Id = r.Id,
                    ZakatYear = r.ZakatYear,
                    TotalZakatDue = r.TotalZakatDue,
                    TotalPaid = r.TotalPaid,
                    RemainingDue = r.TotalZakatDue - r.TotalPaid,   // plain property
                    IsLocked = r.IsLocked
                }).ToList();

                RecordsList.ItemsSource = items;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load Zakat records:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Flat display model — no EF navigation, no computed C# properties
        private sealed class ZakatListItem
        {
            public int Id { get; set; }
            public string ZakatYear { get; set; } = "";
            public decimal TotalZakatDue { get; set; }
            public decimal TotalPaid { get; set; }
            public decimal RemainingDue { get; set; }
            public bool IsLocked { get; set; }
        }

        private void RecordsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RecordsList.SelectedItem is ZakatListItem item)
                _ = RefreshAndShowAsync(item.Id);
        }

        private async Task RefreshAndShowAsync(int id)
        {
            try
            {
                var fresh = await _zakatService.GetByIdAsync(id);
                if (fresh != null) { _currentRecord = fresh; ShowRecord(fresh); }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load record:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ═════════════════════════════════════════════════════════
        // SHOW RECORD
        // ═════════════════════════════════════════════════════════
        private void ShowRecord(ZakatRecord rec)
        {
            try
            {
                PlaceholderPanel.Visibility = Visibility.Collapsed;
                DetailPanel.Visibility = Visibility.Visible;

                txtYearHeader.Text = rec.ZakatYear ?? "";
                LockedBadge.Visibility = rec.IsLocked ? Visibility.Visible : Visibility.Collapsed;
                UnlockedBadge.Visibility = rec.IsLocked ? Visibility.Collapsed : Visibility.Visible;

                txtCash.Text = rec.CashOnHand.ToString("F0");
                txtBank.Text = rec.BankBalance.ToString("F0");
                txtVendorCash.Text = rec.VendorCash.ToString("F0");
                txtInventory.Text = rec.InventoryWorth.ToString("F0");
                txtReceivable.Text = rec.CustomerReceivables.ToString("F0");
                txtVendorDues.Text = rec.VendorDues.ToString("F0");
                txtNisab.Text = rec.NisabThreshold.ToString("F0");
                txtZakatYear.Text = rec.ZakatYear ?? "";
                txtRemarks.Text = rec.Remarks ?? "";

                decimal remaining = rec.TotalZakatDue - rec.TotalPaid;

                ShowResultCard(rec.NetZakatableWealth, rec.TotalZakatDue,
                               rec.NisabThreshold, rec.TotalPaid, remaining);
                RefreshPaymentsGrid(rec, remaining);

                bool locked = rec.IsLocked;
                bool isSaved = rec.Id > 0;

                btnPayZakat.IsEnabled = isSaved && !locked;
                btnLock.IsEnabled = isSaved && !locked;
                txtLockInfo.Text = locked
                    ? $"Locked on {rec.LockedDate:dd-MMM-yyyy HH:mm}"
                    : "Not yet locked.";

                SetInputsEnabled(!locked);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Display error:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshPaymentsGrid(ZakatRecord rec, decimal remaining)
        {
            PaymentsGrid.ItemsSource = rec.Payments?
                .OrderByDescending(p => p.PaymentDate)
                .ToList()
                ?? new List<ZakatPayment>();

            txtTotalPaid.Text = $"PKR {rec.TotalPaid:N2}";
            txtTotalRemaining.Text = $"PKR {remaining:N2}";
        }

        private void ShowResultCard(decimal net, decimal due, decimal nisab,
                                    decimal paid, decimal remaining)
        {
            ResultCard.Visibility = Visibility.Visible;
            bool applicable = net >= nisab;

            txtResultNet.Text = $"PKR {net:N0}";
            txtResultApplicable.Text = applicable ? "Yes" : "No";
            txtResultApplicable.Foreground = applicable
                ? new SolidColorBrush(Color.FromRgb(15, 110, 86))
                : new SolidColorBrush(Color.FromRgb(163, 45, 45));
            txtResultDue.Text = $"PKR {due:N0}";
            txtResultRemaining.Text = $"PKR {remaining:N0}";
            txtResultRemaining.Foreground = remaining > 0
                ? new SolidColorBrush(Color.FromRgb(176, 0, 32))
                : new SolidColorBrush(Color.FromRgb(15, 110, 86));
        }

        private void SetInputsEnabled(bool on)
        {
            txtCash.IsReadOnly = txtBank.IsReadOnly = txtVendorCash.IsReadOnly =
            txtInventory.IsReadOnly = txtReceivable.IsReadOnly = txtVendorDues.IsReadOnly =
            txtNisab.IsReadOnly = txtZakatYear.IsReadOnly = txtRemarks.IsReadOnly = !on;
        }

        // ═════════════════════════════════════════════════════════
        // NEW RECORD
        // ═════════════════════════════════════════════════════════
        private void NewRecord_Click(object sender, RoutedEventArgs e)
        {
            _currentRecord = new ZakatRecord
            {
                ZakatYear = $"{DateTime.Now.Year}-{DateTime.Now.Year + 1}",
                NisabThreshold = 170000m,
                CalculationDate = DateTime.Now
            };

            RecordsList.SelectedItem = null;
            PlaceholderPanel.Visibility = Visibility.Collapsed;
            DetailPanel.Visibility = Visibility.Visible;
            ResultCard.Visibility = Visibility.Collapsed;
            txtYearHeader.Text = "New Zakat Year";
            LockedBadge.Visibility = Visibility.Collapsed;
            UnlockedBadge.Visibility = Visibility.Visible;
            txtCash.Text = txtBank.Text = txtVendorCash.Text =
            txtInventory.Text = txtReceivable.Text = txtVendorDues.Text = "0";
            txtNisab.Text = "170000";
            txtZakatYear.Text = _currentRecord.ZakatYear;
            txtRemarks.Text = "";
            PaymentsGrid.ItemsSource = null;
            txtTotalPaid.Text = "PKR 0.00";
            txtTotalRemaining.Text = "PKR 0.00";
            SetInputsEnabled(true);
            btnPayZakat.IsEnabled = false;
            btnLock.IsEnabled = false;
            txtLockInfo.Text = "Save the record first before making payments.";
        }

        // ═════════════════════════════════════════════════════════
        // AUTO-FILL
        // ═════════════════════════════════════════════════════════
        private async void AutoFill_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var balance = await _accountService.GetBalanceAsync();
                decimal vcash = await _vendorCashService.GetTotalVendorCashAsync();
                var bikes = await _bikeService.GetAllBikesAsync();
                decimal inv = bikes?.Sum(b => (decimal)b.Quantity * b.Price) ?? 0m;
                decimal custRem = 0m, vendorDue = 0m;

                try
                {
                    using var db = new Ramza_EBike_Swabi.Data.AppDbContext();
                    custRem = await db.CustomerInvoices.SumAsync(i => (decimal?)i.RemainingBalance) ?? 0m;
                    vendorDue = await db.VendorBill.SumAsync(b => (decimal?)b.RemainingBalance) ?? 0m;
                }
                catch { }

                txtCash.Text = balance.CashBalance.ToString("F0");
                txtBank.Text = balance.BankBalance.ToString("F0");
                txtVendorCash.Text = vcash.ToString("F0");
                txtInventory.Text = inv.ToString("F0");
                txtReceivable.Text = custRem.ToString("F0");
                txtVendorDues.Text = vendorDue.ToString("F0");

                MessageBox.Show("Fields filled. Review and click Calculate.",
                    "Auto-Filled", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Auto-fill failed:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ═════════════════════════════════════════════════════════
        // CALCULATE
        // ═════════════════════════════════════════════════════════
        private void Calculate_Click(object sender, RoutedEventArgs e)
        {
            if (!TryReadInputs(out decimal cash, out decimal bank, out decimal vcash,
                               out decimal inv, out decimal recv, out decimal vdue,
                               out decimal nisab)) return;

            decimal net = Math.Max(0, cash + bank + vcash + inv + recv - vdue);
            decimal due = net >= nisab ? Math.Round(net * 0.025m, 2) : 0m;
            decimal paid = _currentRecord?.TotalPaid ?? 0m;

            ShowResultCard(net, due, nisab, paid, due - paid);

            if (_currentRecord != null)
            {
                _currentRecord.CashOnHand = cash;
                _currentRecord.BankBalance = bank;
                _currentRecord.VendorCash = vcash;
                _currentRecord.InventoryWorth = inv;
                _currentRecord.CustomerReceivables = recv;
                _currentRecord.VendorDues = vdue;
                _currentRecord.NisabThreshold = nisab;
                _currentRecord.NetZakatableWealth = net;
                _currentRecord.TotalZakatDue = due;
            }
        }

        private bool TryReadInputs(out decimal cash, out decimal bank, out decimal vcash,
                                   out decimal inv, out decimal recv, out decimal vdue,
                                   out decimal nisab)
        {
            cash = bank = vcash = inv = recv = vdue = nisab = 0;
            return Parse(txtCash.Text, "Cash on Hand", out cash)
                && Parse(txtBank.Text, "Bank Balance", out bank)
                && Parse(txtVendorCash.Text, "Vendor Cash", out vcash)
                && Parse(txtInventory.Text, "Inventory Worth", out inv)
                && Parse(txtReceivable.Text, "Customer Receivables", out recv)
                && Parse(txtVendorDues.Text, "Vendor Dues", out vdue)
                && Parse(txtNisab.Text, "Nisab Threshold", out nisab);
        }

        private static bool Parse(string text, string label, out decimal v)
        {
            if (string.IsNullOrWhiteSpace(text)) { v = 0; return true; }
            if (decimal.TryParse(text, out v)) return true;
            MessageBox.Show($"Invalid value for '{label}'.",
                "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        // ═════════════════════════════════════════════════════════
        // SAVE
        // ═════════════════════════════════════════════════════════
        private async void SaveRecord_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtZakatYear.Text))
            {
                MessageBox.Show("Zakat Year label is required (e.g. 1446 AH).",
                    "Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Calculate_Click(sender, e);
            if (_currentRecord == null) return;

            _currentRecord.ZakatYear = txtZakatYear.Text.Trim();
            _currentRecord.Remarks = txtRemarks.Text.Trim();

            try
            {
                var saved = await _zakatService.SaveAsync(_currentRecord);
                var fresh = await _zakatService.GetByIdAsync(saved.Id);
                if (fresh != null) { _currentRecord = fresh; ShowRecord(fresh); }

                await LoadListAsync();
                MessageBox.Show("Record saved.", "Saved",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Save failed:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ═════════════════════════════════════════════════════════
        // PAY ZAKAT  ← crash-safe, always works after save
        // ═════════════════════════════════════════════════════════
        private async void PayZakat_Click(object sender, RoutedEventArgs e)
        {
            if (_currentRecord == null || _currentRecord.Id == 0)
            {
                MessageBox.Show("Please save the record first.",
                    "Not Saved", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_currentRecord.IsLocked)
            {
                MessageBox.Show("This year is locked.",
                    "Locked", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Reload fresh
            ZakatRecord? fresh;
            try { fresh = await _zakatService.GetByIdAsync(_currentRecord.Id); }
            catch (Exception ex)
            {
                MessageBox.Show($"Cannot load record:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (fresh == null) { MessageBox.Show("Record not found."); return; }
            _currentRecord = fresh;

            decimal remaining = fresh.TotalZakatDue - fresh.TotalPaid;
            bool isVoluntary = false;

            if (fresh.TotalZakatDue <= 0)
            {
                if (MessageBox.Show(
                        "No Zakat is due (below Nisab).\n\nRecord voluntary charity (Sadaqah)?",
                        "No Obligation", MessageBoxButton.YesNo, MessageBoxImage.Information)
                    != MessageBoxResult.Yes) return;
                isVoluntary = true;
            }
            else if (remaining <= 0)
            {
                if (MessageBox.Show(
                        "Zakat fully paid for this year.\n\nRecord voluntary (Sadaqah) payment?",
                        "Fully Paid", MessageBoxButton.YesNo, MessageBoxImage.Information)
                    != MessageBoxResult.Yes) return;
                isVoluntary = true;
            }

            Window? owner = null;
            try { owner = Window.GetWindow(this); } catch { }
            owner ??= Application.Current.MainWindow;

            var dlg = new ZakatPaymentDialog(_currentRecord, _zakatService, _accountService)
            {
                Owner = owner,
                AllowVoluntary = isVoluntary
            };

            if (dlg.ShowDialog() == true)
            {
                await RefreshAndShowAsync(_currentRecord.Id);
                await LoadListAsync();
            }
        }

        // ═════════════════════════════════════════════════════════
        // DELETE PAYMENT
        // ═════════════════════════════════════════════════════════
        private async void DeletePayment_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            if (btn.DataContext is not ZakatPayment p) return;

            if (MessageBox.Show(
                    $"Delete payment of PKR {p.Amount:N2} to '{p.RecipientName}'?\n" +
                    "Amount will be returned to balance.",
                    "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning)
                != MessageBoxResult.Yes) return;

            try
            {
                var (ok, msg) = await _zakatService.DeletePaymentAsync(p.Id);
                MessageBox.Show(msg, ok ? "Done" : "Error",
                    MessageBoxButton.OK,
                    ok ? MessageBoxImage.Information : MessageBoxImage.Error);

                if (ok && _currentRecord != null)
                {
                    await RefreshAndShowAsync(_currentRecord.Id);
                    await LoadListAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Delete failed:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ═════════════════════════════════════════════════════════
        // LOCK
        // ═════════════════════════════════════════════════════════
        private async void LockYear_Click(object sender, RoutedEventArgs e)
        {
            if (_currentRecord == null || _currentRecord.Id == 0) return;

            decimal rem = _currentRecord.TotalZakatDue - _currentRecord.TotalPaid;
            string extra = rem > 0 ? $"\n\nWarning: PKR {rem:N2} still unpaid." : "";

            if (MessageBox.Show(
                    $"Lock '{_currentRecord.ZakatYear}'? Cannot be undone.{extra}",
                    "Confirm Lock", MessageBoxButton.YesNo, MessageBoxImage.Question)
                != MessageBoxResult.Yes) return;

            try
            {
                var (ok, msg) = await _zakatService.LockYearAsync(_currentRecord.Id);
                MessageBox.Show(msg, ok ? "Locked" : "Error",
                    MessageBoxButton.OK,
                    ok ? MessageBoxImage.Information : MessageBoxImage.Error);

                if (ok)
                {
                    await RefreshAndShowAsync(_currentRecord.Id);
                    await LoadListAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lock failed:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ═════════════════════════════════════════════════════════
        // EXPORT
        // ═════════════════════════════════════════════════════════
        private async void Export_Click(object sender, RoutedEventArgs e)
        {
            if (_currentRecord == null)
            { MessageBox.Show("No record selected."); return; }

            if (_currentRecord.Id > 0)
            {
                try
                {
                    var fresh = await _zakatService.GetByIdAsync(_currentRecord.Id);
                    if (fresh != null) _currentRecord = fresh;
                }
                catch { }
            }

            var dlg = new SaveFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                FileName = $"Zakat_{(_currentRecord.ZakatYear ?? "Record").Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.xlsx"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                _zakatService.ExportToExcel(_currentRecord, dlg.FileName);
                MessageBox.Show("Exported successfully!", "Done",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ═════════════════════════════════════════════════════════
        // BACK
        // ═════════════════════════════════════════════════════════
        private void Back_Click(object sender, RoutedEventArgs e)
            => _layout.Navigate(new DashboardPage(_layout));
    }
}