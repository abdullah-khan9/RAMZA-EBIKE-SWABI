using Microsoft.EntityFrameworkCore;
using Ramza_EBike_Swabi.Data;
using Ramza_EBike_Swabi.Services;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Ramza_EBike_Swabi.Views.Pages
{
    public partial class SalesReportPage : Page
    {
        private readonly AppDbContext _context;
        private readonly MainLayout _mainLayout;
        private readonly AccountService _accountService = new();
        private readonly VendorCashService _vendorCashService = new();
        private readonly ProfitService _profitService = new();

        public SalesReportPage(MainLayout mainLayout)
        {
            InitializeComponent();
            _mainLayout = mainLayout;
            _context = new AppDbContext();
            LoadReport();
        }

        private async void LoadReport(DateTime? from = null, DateTime? to = null)
        {
            try
            {
                // ── Queryables ───────────────────────────────────────────
                var invoices = _context.CustomerInvoices.AsQueryable();
                var vendorBills = _context.VendorBill.AsQueryable();
                var invoiceItems = _context.CustomerInvoiceItems.AsQueryable();
                var vendorBillItems = _context.VendorBillItem.AsQueryable();

                if (from.HasValue)
                {
                    invoices = invoices.Where(i => i.InvoiceDate.Date >= from.Value.Date);
                    vendorBills = vendorBills.Where(v => v.BillDate.Date >= from.Value.Date);
                }
                if (to.HasValue)
                {
                    invoices = invoices.Where(i => i.InvoiceDate.Date <= to.Value.Date);
                    vendorBills = vendorBills.Where(v => v.BillDate.Date <= to.Value.Date);
                }

                // ── Customer figures ─────────────────────────────────────
                decimal custSales = invoices.Sum(i => (decimal?)i.NetBill) ?? 0;
                decimal custPaid = invoices.Sum(i => (decimal?)i.AmountPaid) ?? 0;
                decimal custRemaining = invoices.Sum(i => (decimal?)i.RemainingBalance) ?? 0;

                // ── Vendor figures ───────────────────────────────────────
                decimal vendorTotal = vendorBills.Sum(v => (decimal?)v.TotalAmount) ?? 0;
                decimal vendorPaid = vendorBills.Sum(v => (decimal?)v.AmountPaid) ?? 0;
                decimal vendorRemaining = vendorTotal - vendorPaid;

                // ── Bike counts ──────────────────────────────────────────
                var filteredInvoiceIds = await invoices.Select(i => i.CustomerInvoiceId).ToListAsync();
                int bikesSold = await _context.CustomerInvoiceItems
                    .Where(ii => filteredInvoiceIds.Contains(ii.CustomerInvoiceId))
                    .SumAsync(ii => (int?)ii.Quantity) ?? 0;

                var filteredBillIds = await vendorBills.Select(b => b.Id).ToListAsync();
                int bikesPurchased = await _context.VendorBillItem
                    .Where(bi => filteredBillIds.Contains(bi.VendorBillId))
                    .SumAsync(bi => (int?)bi.Qty) ?? 0;

                // ── Inventory (always current — not date-filtered) ───────
                var availableBikes = await _context.Bikes.ToListAsync();
                int totalAvailableBikes = availableBikes.Sum(b => b.Quantity);
                decimal inventoryWorth = availableBikes.Sum(b => b.Quantity * b.Price);

                // ── Live account balances (always current) ───────────────
                var balance = await _accountService.GetBalanceAsync();
                decimal vendorCash = await _vendorCashService.GetTotalVendorCashAsync();
                decimal totalBalance = balance.CashBalance + balance.BankBalance + vendorCash;

                // ── Profit (respects date filter) ────────────────────────
                decimal totalProfit;
                if (from.HasValue || to.HasValue)
                {
                    var profits = await _profitService.GetProfitByDateRangeAsync(
                        from ?? DateTime.MinValue,
                        to ?? DateTime.MaxValue);
                    totalProfit = profits.Sum(p => p.Profit);
                }
                else
                {
                    totalProfit = await _profitService.GetTotalProfitAsync();
                }

                // ── Update UI ─────────────────────────────────────────────

                // Overview
                txtTotalProfit.Text = $"₨ {totalProfit:N0}";
                txtCashBalance.Text = $"₨ {balance.CashBalance:N0}";
                txtBankBalance.Text = $"₨ {balance.BankBalance:N0}";
                txtTotalBalance.Text = $"₨ {totalBalance:N0}";
                txtVendorCash.Text = $"₨ {vendorCash:N0}";
                txtBikesSold.Text = bikesSold.ToString("N0");
                txtBikesPurchased.Text = bikesPurchased.ToString("N0");

                // Inventory
                txtAvailableBikes.Text = totalAvailableBikes.ToString("N0");
                txtInventoryWorth.Text = $"₨ {inventoryWorth:N0}";

                // Customer
                txtCustomerSales.Text = $"₨ {custSales:N0}";
                txtCustomerPaid.Text = $"₨ {custPaid:N0}";
                txtCustomerRemaining.Text = $"₨ {custRemaining:N0}";

                // Vendor
                txtVendorPurchase.Text = $"₨ {vendorTotal:N0}";
                txtVendorPaid.Text = $"₨ {vendorPaid:N0}";
                txtVendorRemaining.Text = $"₨ {vendorRemaining:N0}";

                // Filter label
                if (from.HasValue && to.HasValue)
                    txtFilterLabel.Text = $"Showing: {from.Value:dd-MMM-yyyy}  →  {to.Value:dd-MMM-yyyy}";
                else if (from.HasValue)
                    txtFilterLabel.Text = $"From {from.Value:dd-MMM-yyyy} onwards";
                else if (to.HasValue)
                    txtFilterLabel.Text = $"Up to {to.Value:dd-MMM-yyyy}";
                else
                    txtFilterLabel.Text = "Showing all-time data";

                // ── Auto-fill Zakat inputs from live report figures ───────
                txtZakatCash.Text = balance.CashBalance.ToString("F0");
                txtZakatBank.Text = balance.BankBalance.ToString("F0");
                txtZakatInventory.Text = inventoryWorth.ToString("F0");
                txtZakatReceivable.Text = custRemaining.ToString("F0");
                txtZakatVendorDue.Text = vendorRemaining.ToString("F0");

                // Run Zakat calculation immediately with these defaults
                RunZakatCalculation(
                    balance.CashBalance,
                    balance.BankBalance,
                    inventoryWorth,
                    custRemaining,
                    vendorRemaining);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading report: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Zakat: Calculate button click ────────────────────────────────
        private void CalculateZakat_Click(object sender, RoutedEventArgs e)
        {
            if (!decimal.TryParse(txtZakatCash.Text, out decimal cash)) cash = 0;
            if (!decimal.TryParse(txtZakatBank.Text, out decimal bank)) bank = 0;
            if (!decimal.TryParse(txtZakatInventory.Text, out decimal inventory)) inventory = 0;
            if (!decimal.TryParse(txtZakatReceivable.Text, out decimal receivable)) receivable = 0;
            if (!decimal.TryParse(txtZakatVendorDue.Text, out decimal vendorDue)) vendorDue = 0;

            RunZakatCalculation(cash, bank, inventory, receivable, vendorDue);
        }

        // ── Core Zakat logic ─────────────────────────────────────────────
        private void RunZakatCalculation(
            decimal cash, decimal bank, decimal inventory,
            decimal receivable, decimal vendorDue)
        {
            decimal nisab = decimal.TryParse(txtZakatNisab.Text, out decimal n) ? n : 170000;

            decimal total = cash + bank + inventory + receivable - vendorDue;
            bool applicable = total >= nisab;
            decimal zakatDue = applicable ? total * 0.025m : 0;

            // Breakdown display
            txtZkCash.Text = $"₨ {cash:N0}";
            txtZkBank.Text = $"₨ {bank:N0}";
            txtZkInventory.Text = $"₨ {inventory:N0}";
            txtZkReceivable.Text = $"₨ {receivable:N0}";
            txtZkVendorDue.Text = $"- ₨ {vendorDue:N0}";
            txtZkTotal.Text = $"₨ {total:N0}";
            txtZkNisabDisplay.Text = $"₨ {nisab:N0}";

            txtZkApplicable.Text = applicable ? "Yes" : "No (below nisab)";
            txtZkApplicable.Foreground = applicable
                ? new System.Windows.Media.SolidColorBrush(
                      System.Windows.Media.Color.FromRgb(15, 110, 86))
                : new System.Windows.Media.SolidColorBrush(
                      System.Windows.Media.Color.FromRgb(163, 45, 45));

            txtZakatDue.Text = $"₨ {zakatDue:N0}";
        }

        private void Search_Click(object sender, RoutedEventArgs e)
            => LoadReport(dpFrom.SelectedDate, dpTo.SelectedDate);

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            dpFrom.SelectedDate = null;
            dpTo.SelectedDate = null;
            LoadReport();
        }

        private void Back_Click(object sender, RoutedEventArgs e)
            => _mainLayout.Navigate(new DashboardPage(_mainLayout));
    }
}