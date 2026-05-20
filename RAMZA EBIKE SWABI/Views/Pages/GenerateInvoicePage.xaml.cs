using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Ramza_EBike_Swabi.Models;
using Ramza_EBike_Swabi.Services;
using Ramza_EBike_Swabi.Services.Pdf;

namespace Ramza_EBike_Swabi.Views.Pages
{
    public partial class GenerateInvoicePage : Page
    {
        private readonly BikeService _bikeService = new();
        private readonly InvoiceService _invoiceService = new();
        private readonly AccountService _accountService = new();
        private readonly InvoiceDraftService _draftService = new();

        private InvoiceTabManagerPage? _tabManager;
        private CustomerInvoice? _editingInvoice;

        private int _draftId = 0;
        private int _tabOrder = 0;
        private bool _isDirty = false;
        public bool IsDirty => _isDirty;

        private System.Windows.Threading.DispatcherTimer? _autoSaveTimer;
        private bool _suppressMotorChanged = false;
        private bool _suppressDirty = false;

        private TextBox? _activeMotorBox;
        private Popup? _activeMotorPopup;
        private ListBox? _activeMotorList;
        private CustomerInvoiceItem? _activeItem;

        public ObservableCollection<CustomerInvoiceItem> BillItems { get; } = new();

        // ================== CONSTRUCTORS ==================
        public GenerateInvoicePage()
        {
            InitializeComponent();
            dgBillItems.ItemsSource = BillItems;
            BillItems.CollectionChanged += (_, __) =>
            {
                if (!_suppressDirty) ScheduleAutoSave();
                Recalculate(null, null);
            };

            _autoSaveTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1.5)
            };
            _autoSaveTimer.Tick += async (_, __) =>
            {
                _autoSaveTimer.Stop();
                await PersistDraftAsync();
            };

            AddEmptyRow();
        }

        public GenerateInvoicePage(CustomerInvoice invoice) : this()
        {
            _editingInvoice = invoice;
            LoadInvoiceForEdit(invoice);
            _isDirty = false;
        }

        public void SetTabManager(InvoiceTabManagerPage tabManager)
            => _tabManager = tabManager;

        public void SetDraftId(int draftId) => _draftId = draftId;

        // ================== DRAFT ==================
        private void ScheduleAutoSave()
        {
            _isDirty = true;
            _autoSaveTimer?.Stop();
            _autoSaveTimer?.Start();
        }

        private async Task PersistDraftAsync()
        {
            if (_draftId <= 0) return;
            try
            {
                await _draftService.SaveDraftAsync(
                    _draftId, GetCurrentTabTitle(), CaptureDraftData(), _tabOrder);
            }
            catch { }
        }

        private InvoiceDraftData CaptureDraftData() => new()
        {
            Name = txtName?.Text ?? string.Empty,
            FatherName = txtFName?.Text ?? string.Empty,
            Contact = txtContact?.Text ?? string.Empty,
            CNIC = txtCNIC?.Text ?? string.Empty,
            Address = txtAddress?.Text ?? string.Empty,
            Discount = txtDiscount?.Text ?? string.Empty,
            Paid = txtPaid?.Text ?? string.Empty,
            Remarks = txtRemarks?.Text ?? string.Empty,
            IsCash = rbCash?.IsChecked == true,
            AccountDetail = txtAccountDetail?.Text ?? string.Empty,
            WarrantyCardGiven = chkWarrantyCard?.IsChecked == true,
            VoucherGivenToCustomer = chkVoucherCustomer?.IsChecked == true,
            VoucherIssuedByCompany = chkVoucherCompany?.IsChecked == true,
            // ✅ NEW
            DueDate = dpDueDate?.SelectedDate,
            Items = BillItems
                .Where(i => !string.IsNullOrWhiteSpace(i.MotorNumber))
                .Select(i => new DraftBikeItem
                {
                    BikeId = i.BikeId,
                    Model = i.Model,
                    Brand = i.Brand,
                    MotorPower = i.MotorPower,
                    Color = i.Color,
                    MotorNumber = i.MotorNumber,
                    ChassisNumber = i.ChassisNumber,
                    Warranty = i.Warranty,
                    Price = i.Price,
                    Quantity = i.Quantity
                }).ToList()
        };

        public void RestoreFromDraft(InvoiceDraftData data)
        {
            _suppressDirty = true;

            txtName.Text = data.Name;
            txtFName.Text = data.FatherName;
            txtContact.Text = data.Contact;
            txtCNIC.Text = data.CNIC;
            txtAddress.Text = data.Address;
            txtDiscount.Text = data.Discount;
            txtPaid.Text = data.Paid;
            if (txtRemarks != null) txtRemarks.Text = data.Remarks;
            if (txtAccountDetail != null) txtAccountDetail.Text = data.AccountDetail;
            if (rbCash != null) rbCash.IsChecked = data.IsCash;
            if (rbAccount != null) rbAccount.IsChecked = !data.IsCash;
            if (pnlAccountDetail != null)
                pnlAccountDetail.Visibility = data.IsCash
                    ? Visibility.Collapsed : Visibility.Visible;
            if (chkWarrantyCard != null) chkWarrantyCard.IsChecked = data.WarrantyCardGiven;
            if (chkVoucherCustomer != null) chkVoucherCustomer.IsChecked = data.VoucherGivenToCustomer;
            if (chkVoucherCompany != null) chkVoucherCompany.IsChecked = data.VoucherIssuedByCompany;
            // ✅ NEW
            if (dpDueDate != null) dpDueDate.SelectedDate = data.DueDate;

            BillItems.Clear();
            foreach (var item in data.Items)
                BillItems.Add(new CustomerInvoiceItem
                {
                    BikeId = item.BikeId,
                    Model = item.Model,
                    Brand = item.Brand,
                    MotorPower = item.MotorPower,
                    Color = item.Color,
                    MotorNumber = item.MotorNumber,
                    ChassisNumber = item.ChassisNumber,
                    Warranty = item.Warranty,
                    Price = item.Price,
                    Quantity = item.Quantity
                });

            AddEmptyRow();
            Recalculate(null, null);
            _suppressDirty = false;
            _isDirty = false;
        }

        public async Task ClearDraftAsync()
        {
            if (_draftId > 0)
                await _draftService.DeleteDraftAsync(_draftId);
        }

        private string GetCurrentTabTitle()
        {
            string name = txtName?.Text?.Trim() ?? string.Empty;
            return string.IsNullOrWhiteSpace(name) ? "New Invoice" : name;
        }

        // ================== EMPTY ROW ==================
        private void AddEmptyRow()
        {
            BillItems.Add(new CustomerInvoiceItem
            {
                BikeId = null,
                Quantity = 1,
                Model = string.Empty,
                Brand = string.Empty,
                MotorPower = string.Empty,
                Color = string.Empty,
                MotorNumber = string.Empty,
                ChassisNumber = string.Empty,
                Warranty = string.Empty
            });
        }

        private void MarkDirty(object sender, TextChangedEventArgs e)
        {
            if (!_suppressDirty) ScheduleAutoSave();
        }

        // ✅ NEW — due date picker changed
        private void DpDueDate_Changed(object sender,
            SelectionChangedEventArgs e)
        {
            if (!_suppressDirty) ScheduleAutoSave();
        }

        // ================== LOAD FOR EDIT ==================
        private void LoadInvoiceForEdit(CustomerInvoice invoice)
        {
            _suppressDirty = true;

            txtName.Text = invoice.Customer?.Name ?? string.Empty;
            txtFName.Text = invoice.Customer?.FatherName ?? string.Empty;
            txtContact.Text = invoice.Customer?.Contact ?? string.Empty;
            txtCNIC.Text = invoice.Customer?.CNIC ?? string.Empty;
            txtAddress.Text = invoice.Customer?.Address ?? string.Empty;
            txtDiscount.Text = invoice.Discount.ToString("N2");
            txtPaid.Text = invoice.AmountPaid.ToString("N2");
            if (txtRemarks != null) txtRemarks.Text = invoice.Remarks ?? string.Empty;
            // ✅ NEW
            if (dpDueDate != null) dpDueDate.SelectedDate = invoice.DueDate;

            chkWarrantyCard.IsChecked = invoice.WarrantyCardGiven;
            chkVoucherCustomer.IsChecked = invoice.VoucherGivenToCustomer;
            chkVoucherCompany.IsChecked = invoice.VoucherIssuedByCompany;

            BillItems.Clear();
            if (invoice.Items != null)
                foreach (var item in invoice.Items)
                    BillItems.Add(item);

            AddEmptyRow();
            Recalculate(null, null);
            _suppressDirty = false;
            _isDirty = false;
        }

        // ================== PAYMENT METHOD ==================
        private void PaymentMethod_Changed(object sender, RoutedEventArgs e)
        {
            if (pnlAccountDetail == null) return;
            pnlAccountDetail.Visibility = rbAccount?.IsChecked == true
                ? Visibility.Visible : Visibility.Collapsed;
            ScheduleAutoSave();
        }

        // ================== MOTOR AUTOCOMPLETE ==================
        private async void MotorBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressMotorChanged) return;
            if (sender is not TextBox tb) return;
            if (tb.Parent is not Grid grid) return;

            var popup = grid.Children.OfType<Popup>().FirstOrDefault();
            var listBox = popup?.Child is Border b ? b.Child as ListBox : null;
            if (popup == null || listBox == null) return;

            _activeMotorBox = tb;
            _activeMotorPopup = popup;
            _activeMotorList = listBox;
            _activeItem = tb.DataContext as CustomerInvoiceItem;

            string typed = tb.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(typed)) { popup.IsOpen = false; return; }

            var bikes = await _bikeService.GetAllBikesAsync();
            var suggestions = bikes
                .Where(bk => !string.IsNullOrWhiteSpace(bk.MotorNumber) &&
                              bk.MotorNumber.ToLower().Contains(typed.ToLower()))
                .Take(8).ToList();

            if (suggestions.Count == 0) { popup.IsOpen = false; return; }

            listBox.ItemsSource = suggestions;
            listBox.DisplayMemberPath = "MotorNumber";
            popup.IsOpen = true;
        }

        private void MotorSuggestionList_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not ListBox lb) return;
            Bike? selected = lb.SelectedItem as Bike;
            if (selected == null && lb.InputHitTest(e.GetPosition(lb)) is FrameworkElement fe)
                selected = fe.DataContext as Bike;
            if (selected == null) return;
            ApplyBikeToRow(selected);
            if (_activeMotorPopup != null) _activeMotorPopup.IsOpen = false;
        }

        private void ApplyBikeToRow(Bike bike)
        {
            if (_activeItem == null || _activeMotorBox == null) return;
            _suppressMotorChanged = true;

            _activeItem.BikeId = bike.BikeId;
            _activeItem.MotorNumber = bike.MotorNumber;
            _activeItem.ChassisNumber = bike.ChassisNumber;
            _activeItem.Model = bike.Model;
            _activeItem.Brand = bike.Brand;
            _activeItem.MotorPower = bike.MotorPower;
            _activeItem.Color = bike.Color;
            _activeItem.Warranty = bike.Warranty;
            _activeItem.Price = bike.Price;
            _activeItem.Quantity = 1;

            _activeMotorBox.Text = bike.MotorNumber;
            _suppressMotorChanged = false;

            Dispatcher.InvokeAsync(() =>
            {
                dgBillItems.CommitEdit(DataGridEditingUnit.Row, true);
                dgBillItems.Items.Refresh();
                var lastRow = BillItems.LastOrDefault();
                if (lastRow != null && !string.IsNullOrWhiteSpace(lastRow.MotorNumber))
                    AddEmptyRow();
                Recalculate(null, null);
                ScheduleAutoSave();
            }, System.Windows.Threading.DispatcherPriority.Background);
        }

        private void DgBillItems_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            Dispatcher.InvokeAsync(() =>
            {
                var lastRow = BillItems.LastOrDefault();
                if (lastRow != null && !string.IsNullOrWhiteSpace(lastRow.MotorNumber))
                    AddEmptyRow();
                Recalculate(null, null);
                ScheduleAutoSave();
            }, System.Windows.Threading.DispatcherPriority.Background);
        }

        private void RemoveBike_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is CustomerInvoiceItem item)
            {
                BillItems.Remove(item);
                if (BillItems.Count == 0) AddEmptyRow();
                Recalculate(null, null);
                ScheduleAutoSave();
            }
        }

        // ================== CALCULATIONS ==================
        private void Recalculate(object? sender, TextChangedEventArgs? e)
        {
            try
            {
                decimal total = BillItems
                    .Where(i => !string.IsNullOrWhiteSpace(i.MotorNumber))
                    .Sum(i => i.TotalPrice);
                decimal discount = decimal.TryParse(txtDiscount?.Text, out var d) ? d : 0;
                decimal paid = decimal.TryParse(txtPaid?.Text, out var p) ? p : 0;
                decimal net = total - discount;
                decimal remaining = net - paid;

                if (txtTotal != null) txtTotal.Text = total.ToString("N2");
                if (txtNetBill != null) txtNetBill.Text = net.ToString("N2");
                if (txtRemaining != null) txtRemaining.Text = remaining.ToString("N2");
            }
            catch { }
        }

        // ================== PRINT ==================
        private void BtnPrintInvoice_Click(object sender, RoutedEventArgs e)
        {
            var validItems = BillItems
                .Where(i => !string.IsNullOrWhiteSpace(i.MotorNumber)).ToList();

            if (validItems.Count == 0 ||
                string.IsNullOrWhiteSpace(txtName?.Text) ||
                string.IsNullOrWhiteSpace(txtContact?.Text))
            {
                MessageBox.Show("Please complete customer details and add at least one bike.");
                return;
            }

            try
            {
                Recalculate(null, null);
                InvoicePdfService.Generate(BuildInvoiceObject(validItems));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Print failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ================== SAVE ==================
        private async void BtnSaveInvoice_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(txtName?.Text))
                {
                    MessageBox.Show("Customer Name is required.", "Validation",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (string.IsNullOrWhiteSpace(txtContact?.Text))
                {
                    MessageBox.Show("Customer Contact is required.", "Validation",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var validItems = BillItems
                    .Where(i => !string.IsNullOrWhiteSpace(i.MotorNumber)).ToList();

                if (validItems.Count == 0)
                {
                    MessageBox.Show("Please add at least one bike.", "Validation",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Recalculate(null, null);
                decimal.TryParse(txtPaid?.Text, out decimal newPaid);
                bool isEdit = _editingInvoice != null;

                decimal oldPaid = 0m;
                string oldPaymentMethod = "Cash";
                if (isEdit && _editingInvoice != null)
                {
                    oldPaid = await _invoiceService
                        .GetOldPaidAmountAsync(_editingInvoice.CustomerInvoiceId);
                    oldPaymentMethod = await _invoiceService
                        .GetOldPaymentMethodAsync(_editingInvoice.CustomerInvoiceId);
                }

                var invoice = BuildInvoiceObject(validItems);
                bool success;

                if (!isEdit)
                {
                    success = await _invoiceService.AddInvoiceAsync(invoice, validItems);

                    if (success && newPaid > 0)
                    {
                        string invoiceRef = $"INV-{invoice.CustomerInvoiceId:D4}";
                        await _accountService.RecordInvoicePaymentAsync(
                            newPaid,
                            txtName.Text.Trim(),
                            invoiceRef,
                            isCash: rbCash?.IsChecked == true);
                    }
                }
                else
                {
                    invoice.CustomerInvoiceId = _editingInvoice!.CustomerInvoiceId;
                    success = await _invoiceService.UpdateInvoiceAsync(invoice);

                    if (success)
                    {
                        string invoiceRef = $"INV-{invoice.CustomerInvoiceId:D4}";
                        bool isCash = rbCash?.IsChecked == true;
                        await _accountService.RecordInvoicePaymentEditAsync(
                            oldPaid, newPaid, txtName.Text.Trim(), invoiceRef, isCash: isCash);
                    }
                }

                if (success)
                {
                    MessageBox.Show("Invoice saved successfully!", "Saved",
                        MessageBoxButton.OK, MessageBoxImage.Information);

                    await ClearDraftAsync();
                    _tabManager?.RenameActiveTab($"Saved: {txtName.Text.Trim()}");
                    _isDirty = false;
                    ResetFormPublic();

                    if (_tabManager != null)
                    {
                        string newTitle = $"New Invoice {DateTime.Now:HH:mm}";
                        _draftId = await _draftService.CreateDraftAsync(newTitle, _tabOrder);
                    }
                }
                else
                {
                    MessageBox.Show("Failed to save invoice.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                string inner = ex.InnerException?.InnerException?.Message
                            ?? ex.InnerException?.Message
                            ?? "No detail";
                MessageBox.Show(
                    $"Error saving invoice:\n\n{ex.Message}\n\nDetail:\n{inner}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ================== BUILD INVOICE ==================
        private CustomerInvoice BuildInvoiceObject(List<CustomerInvoiceItem> validItems)
        {
            decimal.TryParse(txtDiscount?.Text, out var discount);
            decimal.TryParse(txtPaid?.Text, out var paid);
            decimal.TryParse(
                txtNetBill?.Text?.Replace(",", "").Replace("PKR", "").Trim() ?? "0",
                out decimal net);

            decimal remaining = net - paid;
            string paymentMethod = rbCash?.IsChecked == true ? "Cash" : "Account";
            string? accountDetail = rbAccount?.IsChecked == true
                ? txtAccountDetail?.Text?.Trim() : null;

            string remarks = txtRemarks?.Text?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(accountDetail))
                remarks = string.IsNullOrWhiteSpace(remarks)
                    ? $"Account: {accountDetail}"
                    : $"{remarks} | Account: {accountDetail}";

            return new CustomerInvoice
            {
                Customer = new Customer
                {
                    Name = txtName?.Text?.Trim() ?? string.Empty,
                    FatherName = txtFName?.Text?.Trim() ?? string.Empty,
                    Contact = txtContact?.Text?.Trim() ?? string.Empty,
                    CNIC = txtCNIC?.Text?.Trim() ?? string.Empty,
                    Address = txtAddress?.Text?.Trim() ?? string.Empty
                },
                InvoiceDate = DateTime.Now,
                // ✅ NEW
                DueDate = dpDueDate?.SelectedDate,
                Items = validItems,
                TotalAmount = validItems.Sum(i => i.TotalPrice),
                Discount = discount,
                NetBill = net,
                AmountPaid = paid,
                RemainingBalance = remaining,
                Status = remaining <= 0 ? "Clear"
                                       : paid == 0 ? "Unpaid"
                                                   : "Partially Paid",
                PaymentMethod = paymentMethod,
                Remarks = remarks,
                WarrantyCardGiven = chkWarrantyCard?.IsChecked == true,
                VoucherGivenToCustomer = chkVoucherCustomer?.IsChecked == true,
                VoucherIssuedByCompany = chkVoucherCompany?.IsChecked == true
            };
        }

        public void ResetFormPublic() => ResetForm();

        private void ResetForm()
        {
            _suppressDirty = true;
            BillItems.Clear();
            AddEmptyRow();

            txtName?.Clear(); txtFName?.Clear();
            txtContact?.Clear(); txtCNIC?.Clear();
            txtAddress?.Clear(); txtDiscount?.Clear();
            txtPaid?.Clear(); txtRemarks?.Clear();
            txtAccountDetail?.Clear();

            // ✅ NEW
            if (dpDueDate != null) dpDueDate.SelectedDate = null;

            if (rbCash != null) rbCash.IsChecked = true;
            if (pnlAccountDetail != null) pnlAccountDetail.Visibility = Visibility.Collapsed;
            if (chkWarrantyCard != null) chkWarrantyCard.IsChecked = false;
            if (chkVoucherCustomer != null) chkVoucherCustomer.IsChecked = false;
            if (chkVoucherCompany != null) chkVoucherCompany.IsChecked = false;

            _editingInvoice = null;
            _isDirty = false;
            _suppressDirty = false;
            Recalculate(null, null);
        }
    }
}