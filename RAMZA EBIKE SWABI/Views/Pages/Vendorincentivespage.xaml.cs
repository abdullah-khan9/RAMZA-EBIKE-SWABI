using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Ramza_EBike_Swabi.Models;
using Ramza_EBike_Swabi.Services;

namespace Ramza_EBike_Swabi.Views.Pages
{
    // ===========================
    // DISPLAY ROW (single incentive)
    // ===========================
    public class IncentiveDisplayRow
    {
        public int Id { get; set; }
        public int VendorId { get; set; }
        public string VendorName { get; set; } = string.Empty;
        public string IncentiveName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public IncentiveDestination Destination { get; set; }
        public string DestinationDisplay { get; set; } = string.Empty;

        // Badge colour per destination
        public string DestinationColor => Destination switch
        {
            IncentiveDestination.Cash => "#1E88E5",   // blue
            IncentiveDestination.Account => "#8E24AA",   // purple
            IncentiveDestination.VendorCash => "#F57C00",   // orange
            IncentiveDestination.Gift => "#E53935",   // red
            _ => "#607D8B"
        };

        public DateTime IncentiveDate { get; set; }
        public string? Remarks { get; set; }

        public static IncentiveDisplayRow From(VendorIncentive i) => new()
        {
            Id = i.Id,
            VendorId = i.VendorId,
            VendorName = i.VendorName,
            IncentiveName = i.IncentiveName,
            Amount = i.Amount,
            Destination = i.Destination,
            DestinationDisplay = i.Destination switch
            {
                IncentiveDestination.Cash => "Cash Balance",
                IncentiveDestination.Account => "Account Balance",
                IncentiveDestination.VendorCash => "Vendor Cash",
                IncentiveDestination.Gift => "Gift",
                _ => i.Destination.ToString()
            },
            IncentiveDate = i.IncentiveDate,
            Remarks = i.Remarks
        };
    }

    // ===========================
    // VENDOR GROUP (header + its incentive rows)
    // ===========================
    public class VendorIncentiveGroup
    {
        public int VendorId { get; set; }
        public string VendorName { get; set; } = string.Empty;
        public List<IncentiveDisplayRow> Incentives { get; set; } = new();

        // Total financial amount (gifts excluded)
        public decimal Total => Incentives
            .Where(i => i.Destination != IncentiveDestination.Gift)
            .Sum(i => i.Amount);

        public string TotalLabel => $"PKR {Total:N2}";
        public string CountLabel => $"{Incentives.Count} incentive(s)";
        public string FooterCountLabel => $"{Incentives.Count} transaction(s) for {VendorName}";
    }

    // ===========================
    // PAGE
    // ===========================
    public partial class VendorIncentivesPage : Page
    {
        private readonly VendorIncentiveService _service = new();
        private readonly MainLayout _layout;

        private List<VendorIncentive> _allIncentives = new();

        public VendorIncentivesPage(MainLayout layout)
        {
            InitializeComponent();
            _layout = layout;
            Loaded += async (_, __) => await LoadAllAsync();
        }

        // ===========================
        // LOAD
        // ===========================
        private async System.Threading.Tasks.Task LoadAllAsync()
        {
            try
            {
                _allIncentives = await _service.GetAllIncentivesAsync();
                BindGrouped(_allIncentives);
                UpdateSummaryCards(_allIncentives);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading incentives: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Build vendor groups and bind to ItemsControl
        private void BindGrouped(List<VendorIncentive> incentives)
        {
            var groups = incentives
                .GroupBy(i => new { i.VendorId, i.VendorName })
                .OrderBy(g => g.Key.VendorName)
                .Select(g => new VendorIncentiveGroup
                {
                    VendorId = g.Key.VendorId,
                    VendorName = g.Key.VendorName,
                    Incentives = g.OrderByDescending(i => i.IncentiveDate)
                                  .Select(IncentiveDisplayRow.From)
                                  .ToList()
                })
                .ToList();

            icVendorGroups.ItemsSource = groups;

            int totalTxn = incentives.Count;
            int vendorCount = groups.Count;
            txtResultHint.Text = $"{vendorCount} vendor(s) · {totalTxn} incentive(s)";
        }

        private void UpdateSummaryCards(List<VendorIncentive> incentives)
        {
            decimal totalCash = incentives.Where(i => i.Destination == IncentiveDestination.Cash).Sum(i => i.Amount);
            decimal totalAccount = incentives.Where(i => i.Destination == IncentiveDestination.Account).Sum(i => i.Amount);
            decimal totalVendorCash = incentives.Where(i => i.Destination == IncentiveDestination.VendorCash).Sum(i => i.Amount);
            int giftCount = incentives.Count(i => i.Destination == IncentiveDestination.Gift);
            decimal totalFinancial = totalCash + totalAccount + totalVendorCash;

            txtTotalIncentives.Text = $"PKR {totalFinancial:N2}";
            txtCashIncentives.Text = $"PKR {totalCash + totalAccount:N2}";
            txtVendorCashIncentives.Text = $"PKR {totalVendorCash:N2}";
            txtGiftIncentives.Text = $"{giftCount} item(s)";
        }

        // ===========================
        // SEARCH
        // ===========================
        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            string kw = txtSearch.Text?.Trim().ToLower() ?? "";
            if (string.IsNullOrWhiteSpace(kw))
            {
                BindGrouped(_allIncentives);
                return;
            }

            var filtered = _allIncentives
                .Where(i => i.VendorName.ToLower().Contains(kw) ||
                            i.IncentiveName.ToLower().Contains(kw) ||
                            (i.Remarks != null && i.Remarks.ToLower().Contains(kw)))
                .ToList();

            BindGrouped(filtered);
        }

        private void ClearSearch_Click(object sender, RoutedEventArgs e)
        {
            txtSearch.Clear();
            BindGrouped(_allIncentives);
        }

        // ===========================
        // ADD
        // ===========================
        private async void AddIncentive_Click(object sender, RoutedEventArgs e)
        {
            var win = new AddEditIncentiveWindow
            {
                Owner = Window.GetWindow(this)
            };
            if (win.ShowDialog() == true)
                await LoadAllAsync();
        }

        // ===========================
        // EDIT — DataContext is IncentiveDisplayRow
        // ===========================
        private async void Edit_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is not IncentiveDisplayRow row) return;

            var incentive = await _service.GetByIdAsync(row.Id);
            if (incentive == null)
            {
                MessageBox.Show("Incentive not found.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var win = new AddEditIncentiveWindow(incentive)
            {
                Owner = Window.GetWindow(this)
            };
            if (win.ShowDialog() == true)
                await LoadAllAsync();
        }

        // ===========================
        // DELETE
        // ===========================
        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is not IncentiveDisplayRow row) return;

            var confirm = MessageBox.Show(
                $"Delete incentive \"{row.IncentiveName}\" from {row.VendorName}?\n\n" +
                $"If this incentive added money to a balance, that amount will be reversed.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            var (success, message) = await _service.DeleteIncentiveAsync(row.Id);
            MessageBox.Show(message,
                success ? "Deleted" : "Error",
                MessageBoxButton.OK,
                success ? MessageBoxImage.Information : MessageBoxImage.Error);

            if (success) await LoadAllAsync();
        }

        // ===========================
        // BACK
        // ===========================
        private void Back_Click(object sender, RoutedEventArgs e)
            => _layout.Navigate(new DashboardPage(_layout));
    }
}