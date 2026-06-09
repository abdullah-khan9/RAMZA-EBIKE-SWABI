using Microsoft.EntityFrameworkCore;
using Ramza_EBike_Swabi.Models;
using Ramza_EBike_Swabi.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Ramza_EBike_Swabi.Views.Pages
{
    file class BillDraft
    {
        public int VendorId { get; set; }
        public string DocumentNumber { get; set; } = "";
        public string AmountPaid { get; set; } = "";
        public string PaySource { get; set; } = "Cash";
        public string Remarks { get; set; } = "";
        public List<BillItemDraft> Items { get; set; } = new();
    }

    file class BillItemDraft
    {
        public string Model { get; set; } = "";
        public string Brand { get; set; } = "";
        public string MotorPower { get; set; } = "";
        public string BatteryCapacity { get; set; } = "";
        public string MotorNumber { get; set; } = "";
        public string ChassisNumber { get; set; } = "";
        public string Color { get; set; } = "";
        public string Warranty { get; set; } = "";
        public int Qty { get; set; } = 1;

        // ===== NEW: Flat Pricing =====
        public bool IsFlatPrice { get; set; } = false;
        public decimal FlatPurchasePrice { get; set; }
        public decimal RetailSalePrice { get; set; }

        public decimal BillRate { get; set; }
        public decimal CommissionPercent { get; set; } = 8;
        public decimal TaxOnCommissionPercent { get; set; } = 12;
        public decimal ExtraDiscountPercent { get; set; } = 2;
        public bool IsIncentiveBike { get; set; } = false; // ← ADD KARO
    }

    public partial class VendorBillForm : Page
    {
        private readonly VendorService _service = new();
        private readonly BikeModelService _bikeModelService = new();
        private readonly AccountService _accountService = new();
        private readonly VendorCashService _vendorCashService = new();
        private readonly MainLayout _layout;

        private Vendor? _vendor;
        private int? _editingBillId = null;

        private decimal _editingOldAmountPaid = 0m;
        private string _editingOldPaySource = "None";

        private decimal _previousBalance = 0;
        private bool _previousBalanceLockedForEdit = false;

        private ObservableCollection<VendorBillItem> _items = new();
        private List<BikeModel> _allBikeModels = new();

        private decimal _availableCash = 0m;
        private decimal _availableAccount = 0m;
        private decimal _availableVendorCash = 0m;

        private bool _suppressTextChanged = false;

        private TextBox? _activeModelBox;
        private Popup? _activePopup;
        private ListBox? _activeSuggestionList;
        private VendorBillItem? _activeItem;

        private string DraftFilePath(int vendorId)
        {
            string dir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RamzaEBike");
            System.IO.Directory.CreateDirectory(dir);
            return System.IO.Path.Combine(dir, $"draft_vendor_{vendorId}.json");
        }

        // ===========================
        // CONSTRUCTORS
        // ===========================
        public VendorBillForm(MainLayout layout, int vendorId)
        {
            InitializeComponent();
            _layout = layout;
            ItemsGrid.ItemsSource = _items;

            rbPayCash.Checked += PaySource_Changed;
            rbPayAccount.Checked += PaySource_Changed;
            rbPayVendorCash.Checked += PaySource_Changed;
            rbPayNone.Checked += PaySource_Changed;

            LoadBikeModels();
            LoadAccountBalances();
            LoadVendor(vendorId);
        }

        public VendorBillForm(MainLayout layout, int vendorId, int billId)
            : this(layout, vendorId)
        {
            _editingBillId = billId;
            _previousBalanceLockedForEdit = true;
            FormTitle.Text = "Edit Vendor Bill";
            LoadBillForEdit(billId);
        }

        // ===========================
        // LOAD
        // ===========================
        private async void LoadBikeModels()
            => _allBikeModels = await _bikeModelService.GetAllAsync();

        private async void LoadAccountBalances()
        {
            try
            {
                var balance = await _accountService.GetBalanceAsync();
                _availableCash = balance.CashBalance;
                _availableAccount = balance.BankBalance;
                UpdatePaySourceHint();
            }
            catch { }
        }

        private async void LoadVendor(int vendorId)
        {
            _vendor = await _service.GetVendorAsync(vendorId);
            if (_vendor == null) { MessageBox.Show("Vendor not found!"); return; }

            VendorName.Text = _vendor.VendorName;

            // Only set previousBalance in Add mode — edit mode locks it from LoadBillForEdit
            if (!_previousBalanceLockedForEdit)
            {
                decimal trueRemaining = 0m;

                try
                {
                    using var db = new Ramza_EBike_Swabi.Data.AppDbContext();

                    var lastBill = await db.VendorBill
                        .Where(b => b.VendorId == vendorId)
                        .OrderByDescending(b => b.Id)
                        .FirstOrDefaultAsync();

                    decimal billRemaining = lastBill?.RemainingBalance ?? 0m;

                    decimal standalonePaid = await db.VendorPaymentRecords
                        .Where(p => p.VendorId == vendorId)
                        .SumAsync(p => (decimal?)p.AmountPaid) ?? 0m;

                    trueRemaining = Math.Max(0, billRemaining - standalonePaid);
                }
                catch { trueRemaining = 0m; }

                _previousBalance = trueRemaining;
                PreviousBalance.Text = $"{_previousBalance:N2}";
            }

            _availableVendorCash = await _vendorCashService.GetVendorCashBalanceAsync(vendorId);
            UpdatePaySourceHint();
            UpdateRemainingPreview();

            if (!_editingBillId.HasValue)
                TryRestoreDraft(vendorId);
        }

        // ===========================
        // LOAD BILL FOR EDIT
        // ===========================
        private async void LoadBillForEdit(int billId)
        {
            try
            {
                var bill = await _service.GetBillByIdAsync(billId);
                if (bill == null) return;

                _previousBalance = bill.PreviousBalance;
                PreviousBalance.Text = $"{_previousBalance:N2}";
                PreviousBalanceLabel.Text = "Previous Balance (before this bill):";

                _editingOldAmountPaid = bill.AmountPaid;
                _editingOldPaySource = bill.PaymentSource ?? "None";

                _suppressTextChanged = true;
                AmountPaidBox.Text = bill.AmountPaid.ToString("N2");
                RemarksBox.Text = bill.Remarks ?? "";
                _suppressTextChanged = false;

                switch (bill.PaymentSource)
                {
                    case "Account": rbPayAccount.IsChecked = true; break;
                    case "VendorCash": rbPayVendorCash.IsChecked = true; break;
                    case "None": rbPayNone.IsChecked = true; break;
                    default: rbPayCash.IsChecked = true; break;
                }

                _items.Clear();
                foreach (var item in bill.Items)
                    _items.Add(item);

                if (bill.Items.Any())
                    DocumentNumberBox.Text = bill.Items.First().DocumentNumber;

                AddEmptyRow();
                RecalculateAll();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading bill for edit:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ===========================
        // DRAFT
        // ===========================
        private void SaveDraft()
        {
            if (_vendor == null || _editingBillId.HasValue) return;

            var draft = new BillDraft
            {
                VendorId = _vendor.Id,
                DocumentNumber = DocumentNumberBox.Text ?? "",
                AmountPaid = AmountPaidBox.Text ?? "",
                Remarks = RemarksBox.Text ?? "",
                PaySource = rbPayAccount.IsChecked == true ? "Account"
                               : rbPayVendorCash.IsChecked == true ? "VendorCash"
                               : rbPayNone.IsChecked == true ? "None" : "Cash",
                Items = _items.Where(i => !string.IsNullOrWhiteSpace(i.Model))
                    .Select(i => new BillItemDraft
                    {
                        Model = i.Model,
                        Brand = i.Brand,
                        MotorPower = i.MotorPower,
                        BatteryCapacity = i.BatteryCapacity,
                        MotorNumber = i.MotorNumber,
                        ChassisNumber = i.ChassisNumber,
                        Color = i.Color,
                        Warranty = i.Warranty,
                        Qty = i.Qty,
                        IsFlatPrice = i.IsFlatPrice,
                        FlatPurchasePrice = i.FlatPurchasePrice,
                        RetailSalePrice = i.RetailSalePrice,
                        BillRate = i.BillRate,
                        CommissionPercent = i.CommissionPercent,
                        TaxOnCommissionPercent = i.TaxOnCommissionPercent,
                        ExtraDiscountPercent = i.ExtraDiscountPercent,
                        IsIncentiveBike = i.IsIncentiveBike, // ← ADD KARO
                    }).ToList()
            };

            try
            {
                System.IO.File.WriteAllText(DraftFilePath(_vendor.Id),
                    JsonSerializer.Serialize(draft));
            }
            catch { }
        }

        private void TryRestoreDraft(int vendorId)
        {
            string path = DraftFilePath(vendorId);
            if (!System.IO.File.Exists(path)) { AddEmptyRow(); return; }

            try
            {
                var draft = JsonSerializer.Deserialize<BillDraft>(
                    System.IO.File.ReadAllText(path));

                if (draft == null || draft.Items.Count == 0) { AddEmptyRow(); return; }

                _suppressTextChanged = true;
                DocumentNumberBox.Text = draft.DocumentNumber;
                AmountPaidBox.Text = draft.AmountPaid;
                RemarksBox.Text = draft.Remarks;
                _suppressTextChanged = false;

                switch (draft.PaySource)
                {
                    case "Account": rbPayAccount.IsChecked = true; break;
                    case "VendorCash": rbPayVendorCash.IsChecked = true; break;
                    case "None": rbPayNone.IsChecked = true; break;
                    default: rbPayCash.IsChecked = true; break;
                }

                _items.Clear();
                foreach (var d in draft.Items)
                    _items.Add(new VendorBillItem
                    {
                        Model = d.Model,
                        Brand = d.Brand,
                        MotorPower = d.MotorPower,
                        BatteryCapacity = d.BatteryCapacity,
                        MotorNumber = d.MotorNumber,
                        ChassisNumber = d.ChassisNumber,
                        Color = d.Color,
                        Warranty = d.Warranty,
                        Qty = d.Qty,
                        IsFlatPrice = d.IsFlatPrice,
                        FlatPurchasePrice = d.FlatPurchasePrice,
                        RetailSalePrice = d.RetailSalePrice,
                        BillRate = d.BillRate,
                        CommissionPercent = d.CommissionPercent,
                        TaxOnCommissionPercent = d.TaxOnCommissionPercent,
                        ExtraDiscountPercent = d.ExtraDiscountPercent,
                         IsIncentiveBike = d.IsIncentiveBike, // ← ADD KARO
                    });

                AddEmptyRow();
                RecalculateAll();
                DraftBadge.Visibility = Visibility.Visible;
            }
            catch { AddEmptyRow(); }
        }

        private void ClearDraftFile()
        {
            if (_vendor == null) return;
            try { System.IO.File.Delete(DraftFilePath(_vendor.Id)); } catch { }
        }

        private void ClearDraft_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Clear current draft?", "Confirm",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            ClearDraftFile();
            _items.Clear();
            DocumentNumberBox.Text = "";
            AmountPaidBox.Text = "";
            RemarksBox.Text = "";
            rbPayCash.IsChecked = true;
            AddEmptyRow();
            DraftBadge.Visibility = Visibility.Collapsed;
            RecalculateAll();
        }

        // ===========================
        // PAYMENT SOURCE UI
        // ===========================
        private void UpdatePaySourceHint()
        {
            if (txtPaySourceHint == null) return;

            if (rbPayNone?.IsChecked == true)
            {
                txtPaySourceHint.Text = "No balance will be deducted on save.";
                if (txtPayAfterPreview != null) txtPayAfterPreview.Text = string.Empty;
                return;
            }

            decimal available = GetAvailableForSelectedSource();
            string sourceName = GetSelectedSourceName();
            txtPaySourceHint.Text = $"Available {sourceName}: PKR {available:N2}";
            UpdateRemainingPreview();
        }

        private void UpdateRemainingPreview()
        {
            if (txtPayAfterPreview == null || txtRemainingPreview == null) return;

            decimal.TryParse(AmountPaidBox?.Text, out decimal paid);
            decimal.TryParse(TotalAmountText?.Text?.Replace(",", ""), out decimal itemsTotal);

            decimal remaining = Math.Max(0, _previousBalance + itemsTotal - paid);
            txtRemainingPreview.Text = $"PKR {remaining:N2}";

            if (rbPayNone?.IsChecked == true || paid <= 0)
            {
                txtPayAfterPreview.Text = string.Empty;
                return;
            }

            decimal available = GetAvailableForSelectedSource();
            string sourceName = GetSelectedSourceName();

            if (_editingBillId.HasValue)
            {
                decimal delta = paid - _editingOldAmountPaid;

                if (delta == 0)
                {
                    txtPayAfterPreview.Text = $"No payment change — {sourceName} unchanged.";
                    txtPayAfterPreview.Foreground =
                        new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Colors.Gray);
                }
                else if (delta > 0)
                {
                    decimal afterBalance = available - delta;
                    txtPayAfterPreview.Text =
                        $"Extra PKR {delta:N2} from {sourceName} | Balance after: PKR {afterBalance:N2}";
                    txtPayAfterPreview.Foreground = afterBalance >= 0
                        ? new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromRgb(0x15, 0x80, 0x3D))
                        : new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromRgb(0xB0, 0x00, 0x20));
                }
                else
                {
                    decimal returned = Math.Abs(delta);
                    txtPayAfterPreview.Text =
                        $"PKR {returned:N2} returned to {_editingOldPaySource} (original source)";
                    txtPayAfterPreview.Foreground =
                        new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromRgb(0x15, 0x80, 0x3D));
                }
            }
            else
            {
                decimal afterBalance = available - paid;
                txtPayAfterPreview.Text = afterBalance >= 0
                    ? $"{sourceName} after payment: PKR {afterBalance:N2}"
                    : $"⚠ Insufficient {sourceName} (short by PKR {Math.Abs(afterBalance):N2})";
                txtPayAfterPreview.Foreground = afterBalance >= 0
                    ? new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0x15, 0x80, 0x3D))
                    : new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0xB0, 0x00, 0x20));
            }
        }

        private decimal GetAvailableForSelectedSource()
        {
            if (rbPayAccount?.IsChecked == true) return _availableAccount;
            if (rbPayVendorCash?.IsChecked == true) return _availableVendorCash;
            return _availableCash;
        }

        private string GetSelectedSourceName()
        {
            if (rbPayAccount?.IsChecked == true) return "Account Balance";
            if (rbPayVendorCash?.IsChecked == true) return "Vendor Cash";
            if (rbPayNone?.IsChecked == true) return "None";
            return "Cash Balance";
        }

        private string GetSelectedSourceKey()
        {
            if (rbPayAccount?.IsChecked == true) return "Account";
            if (rbPayVendorCash?.IsChecked == true) return "VendorCash";
            if (rbPayNone?.IsChecked == true) return "None";
            return "Cash";
        }

        private void PaySource_Changed(object sender, RoutedEventArgs e) => UpdatePaySourceHint();

        // ===========================
        // ROW MANAGEMENT
        // ===========================
        private void AddEmptyRow()
        {
            _items.Add(new VendorBillItem
            {
                Qty = 1,
                IsFlatPrice = false,
                CommissionPercent = 8,
                TaxOnCommissionPercent = 12,
                ExtraDiscountPercent = 2
            });
        }

        private void CheckAutoAddRow()
        {
            var lastRow = _items.LastOrDefault();
            if (lastRow != null && !string.IsNullOrWhiteSpace(lastRow.Model))
                AddEmptyRow();
        }

        private void AddRow_Click(object sender, RoutedEventArgs e)
        {
            AddEmptyRow();
            ItemsGrid.Items.Refresh();
            ItemsGrid.ScrollIntoView(_items.Last());
        }

        private void RemoveRow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is VendorBillItem item)
            {
                _items.Remove(item);
                RecalculateAll();
                SaveDraftIfDirty();
            }
        }

        // ===========================
        // AUTOCOMPLETE
        // ===========================
        private void ModelAutoBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressTextChanged) return;
            if (sender is not TextBox tb || tb.Parent is not Grid grid) return;

            var popup = grid.Children.OfType<Popup>().FirstOrDefault();
            var listBox = popup?.Child is Border border ? border.Child as ListBox : null;
            if (popup == null || listBox == null) return;

            _activeModelBox = tb;
            _activePopup = popup;
            _activeSuggestionList = listBox;
            _activeItem = tb.DataContext as VendorBillItem;

            string typed = tb.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(typed)) { popup.IsOpen = false; return; }

            var suggestions = _allBikeModels
                .Where(b => b.Model.ToLower().Contains(typed.ToLower()) ||
                            b.Brand.ToLower().Contains(typed.ToLower()))
                .Take(12).ToList();

            if (suggestions.Count == 0) { popup.IsOpen = false; return; }
            listBox.ItemsSource = suggestions;
            listBox.DisplayMemberPath = "Model";
            popup.IsOpen = true;
        }

        private void ModelSuggestionList_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not ListBox lb) return;
            BikeModel? selected = lb.SelectedItem as BikeModel;
            if (selected == null && lb.InputHitTest(e.GetPosition(lb)) is FrameworkElement fe)
                selected = fe.DataContext as BikeModel;
            if (selected == null) return;
            ApplyBikeModelToItem(selected);
            if (_activePopup != null) _activePopup.IsOpen = false;
        }

        private void ModelAutoBox_LostFocus(object sender, RoutedEventArgs e)
        {
            Dispatcher.InvokeAsync(() =>
            {
                if (_activePopup != null && !_activePopup.IsKeyboardFocusWithin)
                    _activePopup.IsOpen = false;
            }, System.Windows.Threading.DispatcherPriority.Background);
        }

        private void ApplyBikeModelToItem(BikeModel bikeModel)
        {
            if (_activeItem == null || _activeModelBox == null) return;

            _suppressTextChanged = true;
            _activeItem.Model = bikeModel.Model;
            _activeItem.Brand = bikeModel.Brand;
            _activeItem.MotorPower = bikeModel.MotorPower;
            _activeItem.BatteryCapacity = bikeModel.BatteryCapacity;
            _activeItem.Color = bikeModel.Color;
            _activeItem.Warranty = bikeModel.Warranty;
            _activeModelBox.Text = bikeModel.Model;
            _suppressTextChanged = false;

            Dispatcher.InvokeAsync(() =>
            {
                ItemsGrid.CommitEdit(DataGridEditingUnit.Row, true);
                ItemsGrid.Items.Refresh();
                RecalculateAll();
                CheckAutoAddRow();
                SaveDraftIfDirty();
            }, System.Windows.Threading.DispatcherPriority.Background);
        }

        private void RecalculateAll()
        {
            decimal total = 0, totalCommission = 0, totalTax = 0;

            foreach (var item in _items.Where(i => !string.IsNullOrWhiteSpace(i.Model)))
            {
                if (item.Qty <= 0) item.Qty = 1;

                if (item.IsIncentiveBike)
                {
                    // ===== INCENTIVE BIKE =====
                    // Purchase price = 0 (free se mili)
                    // BillRate = Retail price (user enter karega)
                    // Bill total mein count nahi hogi
                    item.IsFlatPrice = true;
                    item.FlatPurchasePrice = 0;
                    item.CommissionAmount = 0;
                    item.TaxOnCommissionAmount = 0;
                    item.ExtraCommissionAmount = 0;
                    item.FinalCommissionAmount = 0;
                    item.TotalCommissionAmount = 0;
                    item.TotalWholesalePrice = 0; // Bill amount mein zero
                    item.RetailSalePrice = item.BillRate;
                    // Total mein add NAHI karte
                }
                else if (item.IsFlatPrice)
                {
                    if (item.FlatPurchasePrice < 0) item.FlatPurchasePrice = 0;
                    if (item.BillRate < 0) item.BillRate = 0;

                    item.CommissionAmount = 0;
                    item.TaxOnCommissionAmount = 0;
                    item.ExtraCommissionAmount = 0;
                    item.FinalCommissionAmount = 0;
                    item.TotalCommissionAmount = 0;
                    item.TotalWholesalePrice = item.FlatPurchasePrice * item.Qty;
                    item.RetailSalePrice = item.BillRate;

                    total += item.TotalWholesalePrice;
                    totalCommission += item.TotalCommissionAmount;
                    totalTax += item.TaxOnCommissionAmount * item.Qty;
                }
                else
                {
                    if (item.BillRate < 0) item.BillRate = 0;

                    decimal retailPrice = item.BillRate;

                    item.CommissionAmount = retailPrice * item.CommissionPercent / 100m;
                    item.TaxOnCommissionAmount = item.CommissionAmount * item.TaxOnCommissionPercent / 100m;
                    item.ExtraCommissionAmount = (retailPrice - item.CommissionAmount) * item.ExtraDiscountPercent / 100m;
                    item.FinalCommissionAmount = item.CommissionAmount + item.ExtraCommissionAmount;
                    item.TotalCommissionAmount = item.FinalCommissionAmount * item.Qty;
                    item.TotalWholesalePrice = ((retailPrice - item.FinalCommissionAmount) + item.TaxOnCommissionAmount) * item.Qty;
                    item.RetailSalePrice = retailPrice;
                    item.FlatPurchasePrice = 0;

                    total += item.TotalWholesalePrice;
                    totalCommission += item.TotalCommissionAmount;
                    totalTax += item.TaxOnCommissionAmount * item.Qty;
                }
            }

            ItemsGrid.Items.Refresh();
            TotalAmountText.Text = $"{total:N2}";
            TotalCommissionText.Text = $"{totalCommission:N2}";
            TotalTaxText.Text = $"{totalTax:N2}";
            UpdateRemainingPreview();
        }

        private void ItemsGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            Dispatcher.InvokeAsync(() =>
            {
                RecalculateAll();
                CheckAutoAddRow();
                SaveDraftIfDirty();
            });
        }

        private void AmountPaidBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressTextChanged) return;
            RecalculateAll();
            SaveDraftIfDirty();
        }

        private void DocumentNumberBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressTextChanged) return;
            SaveDraftIfDirty();
        }

        private void RemarksBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressTextChanged) return;
            SaveDraftIfDirty();
        }

        private void SaveDraftIfDirty()
        {
            if (!_editingBillId.HasValue) SaveDraft();
        }

        private void Calculate_Click(object sender, RoutedEventArgs e) => RecalculateAll();

        // ===========================
        // VALIDATE
        // ===========================
        private bool ValidateDocumentNumber()
        {
            if (string.IsNullOrWhiteSpace(DocumentNumberBox.Text))
            {
                MessageBox.Show("Document Number is required!");
                DocumentNumberBox.Focus();
                return false;
            }
            return true;
        }

        private bool ValidatePaymentSource(decimal amountPaid)
        {
            if (rbPayNone?.IsChecked == true) return true;

            decimal amountToCheck;
            if (_editingBillId.HasValue)
            {
                decimal delta = amountPaid - _editingOldAmountPaid;
                if (delta <= 0) return true;
                amountToCheck = delta;
            }
            else
            {
                if (amountPaid <= 0) return true;
                amountToCheck = amountPaid;
            }

            decimal available = GetAvailableForSelectedSource();
            if (available >= amountToCheck) return true;

            MessageBox.Show(
                $"Insufficient {GetSelectedSourceName()}.\n" +
                $"Available: PKR {available:N2}\n" +
                $"Required:  PKR {amountToCheck:N2}",
                "Insufficient Balance", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        // ===========================
        // SAVE
        // ===========================
        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            if (_vendor == null) return;
            if (!ValidateDocumentNumber()) return;

            decimal amountPaid = 0;
            if (!string.IsNullOrWhiteSpace(AmountPaidBox.Text) &&
                !decimal.TryParse(AmountPaidBox.Text, out amountPaid))
            {
                MessageBox.Show("Invalid paid amount!"); return;
            }

            if (!ValidatePaymentSource(amountPaid)) return;

            RecalculateAll();

            string docNo = DocumentNumberBox.Text.Trim();
            var validItems = _items.Where(i => !string.IsNullOrWhiteSpace(i.Model)).ToList();
            if (validItems.Count == 0)
            { MessageBox.Show("Please add at least one bike item."); return; }

            foreach (var item in validItems)
                item.DocumentNumber = docNo;

            string paySource = GetSelectedSourceKey();

            var bill = new VendorBill
            {
                Id = _editingBillId ?? 0,
                VendorId = _vendor.Id,
                PreviousBalance = _previousBalance,
                AmountPaid = amountPaid,
                Remarks = RemarksBox.Text?.Trim(),
                PaymentSource = paySource,
                Items = validItems
            };

            try
            {
                if (_editingBillId.HasValue) await _service.UpdateBillAsync(bill);
                else await _service.AddBillAsync(bill);

                if (paySource != "None")
                {
                    if (_editingBillId.HasValue)
                        await ProcessEditPaymentAsync(
                            _editingOldAmountPaid, amountPaid,
                            _editingOldPaySource, paySource, docNo);
                    else if (amountPaid > 0)
                        await DeductFromSourceAsync(amountPaid, paySource, docNo);
                }

                ClearDraftFile();
                MessageBox.Show(
                    "Bill saved successfully!\nBike records have been updated automatically.",
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                _layout.Navigate(new VendorPurchasePage(_layout, _vendor.Id));
            }
            catch (Exception ex)
            {
                string detail = ex.InnerException?.InnerException?.Message
                             ?? ex.InnerException?.Message ?? "No detail";
                MessageBox.Show($"Error saving bill:\n{ex.Message}\n\nDetail:\n{detail}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ===========================
        // EDIT PAYMENT — DELTA ONLY
        // ===========================
        private async Task ProcessEditPaymentAsync(
            decimal oldPaid, decimal newPaid,
            string oldSource, string newSource,
            string docNo)
        {
            decimal delta = newPaid - oldPaid;
            string vendorName = _vendor!.VendorName;
            string billRef = $"Bill #{_editingBillId}";

            if (delta == 0) return;

            using var db = new Ramza_EBike_Swabi.Data.AppDbContext();
            var balance = await db.AccountBalances.FirstOrDefaultAsync()
                            ?? new AccountBalance { CashBalance = 0, BankBalance = 0 };

            if (delta > 0)
            {
                string remark =
                    $"{billRef} Edit — Additional payment PKR {delta:N2} from {newSource} | " +
                    $"Was: PKR {oldPaid:N2} → Now: PKR {newPaid:N2} | Doc: {docNo}";

                if (newSource == "Cash")
                {
                    balance.CashBalance -= delta;
                    db.AccountTransactions.Add(new AccountTransaction
                    {
                        Type = TransactionType.WithdrawFromCash,
                        ByWhom = vendorName,
                        Amount = delta,
                        TransactionDate = DateTime.Now,
                        Remarks = remark,
                        CashBalanceAfter = balance.CashBalance,
                        BankBalanceAfter = balance.BankBalance
                    });
                    await db.SaveChangesAsync();
                }
                else if (newSource == "Account")
                {
                    balance.BankBalance -= delta;
                    db.AccountTransactions.Add(new AccountTransaction
                    {
                        Type = TransactionType.CashWithdraw,
                        ByWhom = vendorName,
                        Amount = delta,
                        TransactionDate = DateTime.Now,
                        Remarks = remark,
                        CashBalanceAfter = balance.CashBalance,
                        BankBalanceAfter = balance.BankBalance
                    });
                    await db.SaveChangesAsync();
                }
                else if (newSource == "VendorCash")
                {
                    var vc = await db.VendorCashBalances
                        .FirstOrDefaultAsync(v => v.VendorId == _vendor.Id);
                    if (vc != null) { vc.Balance -= delta; if (vc.Balance < 0) vc.Balance = 0; }
                    db.VendorCashLedger.Add(new VendorCashLedger
                    {
                        VendorId = _vendor.Id,
                        VendorName = vendorName,
                        Type = VendorCashType.Applied,
                        ByWhom = vendorName,
                        Amount = delta,
                        Source = "VendorCash",
                        TransactionDate = DateTime.Now,
                        Remarks = remark,
                        VendorCashBalanceAfter = vc?.Balance ?? 0
                    });
                    await db.SaveChangesAsync();
                }
            }
            else
            {
                decimal returned = Math.Abs(delta);
                string remark =
                    $"{billRef} Edit — Payment reduced, PKR {returned:N2} returned to {oldSource} | " +
                    $"Was: PKR {oldPaid:N2} → Now: PKR {newPaid:N2} | Doc: {docNo}";

                if (oldSource == "Cash")
                {
                    balance.CashBalance += returned;
                    db.AccountTransactions.Add(new AccountTransaction
                    {
                        Type = TransactionType.CashDeposit,
                        ByWhom = vendorName,
                        Amount = returned,
                        TransactionDate = DateTime.Now,
                        Remarks = remark,
                        CashBalanceAfter = balance.CashBalance,
                        BankBalanceAfter = balance.BankBalance
                    });
                    await db.SaveChangesAsync();
                }
                else if (oldSource == "Account")
                {
                    balance.BankBalance += returned;
                    db.AccountTransactions.Add(new AccountTransaction
                    {
                        Type = TransactionType.DepositToAccount,
                        ByWhom = vendorName,
                        Amount = returned,
                        TransactionDate = DateTime.Now,
                        Remarks = remark,
                        CashBalanceAfter = balance.CashBalance,
                        BankBalanceAfter = balance.BankBalance
                    });
                    await db.SaveChangesAsync();
                }
                else if (oldSource == "VendorCash")
                {
                    var vc = await db.VendorCashBalances
                        .FirstOrDefaultAsync(v => v.VendorId == _vendor.Id);
                    if (vc != null) vc.Balance += returned;
                    db.VendorCashLedger.Add(new VendorCashLedger
                    {
                        VendorId = _vendor.Id,
                        VendorName = vendorName,
                        Type = VendorCashType.Refunded,
                        ByWhom = vendorName,
                        Amount = returned,
                        Source = "VendorCash",
                        TransactionDate = DateTime.Now,
                        Remarks = remark,
                        VendorCashBalanceAfter = vc?.Balance ?? 0
                    });
                    await db.SaveChangesAsync();
                }
            }
        }

        private async Task DeductFromSourceAsync(decimal amount, string source, string docNo)
        {
            string vendorName = _vendor!.VendorName;
            string remarks = $"Bill payment — Vendor: {vendorName} | Doc: {docNo}";

            if (source == "Cash")
                await _accountService.ProcessTransactionAsync(
                    TransactionType.WithdrawFromCash, amount, vendorName, DateTime.Now, remarks);
            else if (source == "Account")
                await _accountService.ProcessTransactionAsync(
                    TransactionType.CashWithdraw, amount, vendorName, DateTime.Now, remarks);
            else if (source == "VendorCash")
            {
                var (success, message) = await _vendorCashService
                    .DeductVendorCashAsync(_vendor.Id, amount, remarks);
                if (!success) throw new InvalidOperationException(message);
            }
        }
        private void IncentiveCheckbox_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is not CheckBox cb) return;
                if (cb.DataContext is not VendorBillItem item) return;

                // Commit current edit pehle
                ItemsGrid.CommitEdit(DataGridEditingUnit.Row, true);

                bool isIncentive = cb.IsChecked == true;
                item.IsIncentiveBike = isIncentive;

                if (isIncentive)
                {
                    item.IsFlatPrice = true;
                    item.FlatPurchasePrice = 0;
                    item.CommissionPercent = 0;
                    item.TaxOnCommissionPercent = 0;
                    item.ExtraDiscountPercent = 0;
                }

                Dispatcher.InvokeAsync(() =>
                {
                    RecalculateAll();
                    ItemsGrid.Items.Refresh();
                    SaveDraftIfDirty();
                }, System.Windows.Threading.DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Debug",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (!_editingBillId.HasValue) SaveDraft();
            if (_vendor != null) _layout.Navigate(new VendorPurchasePage(_layout, _vendor.Id));
        }
    }
}