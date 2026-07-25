using Ramza_EBike_Swabi.Models;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Ramza_EBike_Swabi.Views.Windows
{
    // ViewModel for grid rows
    public class InstalmentRowVM : INotifyPropertyChanged
    {
        private decimal _amount;
        private DateTime _dueDate = DateTime.Today.AddMonths(1);
        private string? _remarks;

        public int InstalmentNumber { get; set; }

        public decimal Amount
        {
            get => _amount;
            set { _amount = value; OnChanged(nameof(Amount)); }
        }

        public DateTime DueDate
        {
            get => _dueDate;
            set { _dueDate = value; OnChanged(nameof(DueDate)); OnChanged(nameof(DueDateDisplay)); }
        }

        public string DueDateDisplay => _dueDate.ToString("dd-MMM-yyyy");

        public string? Remarks
        {
            get => _remarks;
            set { _remarks = value; OnChanged(nameof(Remarks)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnChanged(string p) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }

    public partial class InstalmentWindow : Window
    {
        private readonly decimal _remaining;
        public bool Saved { get; private set; } = false;
        public System.Collections.Generic.List<InvoiceInstalment> Result { get; private set; } = new();

        private ObservableCollection<InstalmentRowVM> _rows = new();

        public InstalmentWindow(decimal remaining, string customerName, int invoiceId)
        {
            InitializeComponent();
            _remaining = remaining;
            SubTitle.Text = $"Customer: {customerName}  |  Invoice #{invoiceId}  |  Remaining: PKR {remaining:N2}";
            RemainingText.Text = $"PKR {remaining:N2}";
            dgInstalments.ItemsSource = _rows;
            _rows.CollectionChanged += (_, _) => UpdateSummary();
            cmbCount.SelectedIndex = 1; // default 3
            dpStart.SelectedDate = DateTime.Today.AddMonths(1);
        }

        private void CmbCount_Changed(object sender, SelectionChangedEventArgs e) => AutoGenerate();
        private void DpStart_Changed(object sender, SelectionChangedEventArgs e) => AutoGenerate();

        private void AutoFill_Click(object sender, RoutedEventArgs e) => AutoGenerate();

        private void AutoGenerate()
        {
            if (cmbCount.SelectedItem == null) return;
            int count = int.Parse(((ComboBoxItem)cmbCount.SelectedItem).Content.ToString()!);
            DateTime start = dpStart.SelectedDate ?? DateTime.Today.AddMonths(1);

            _rows.Clear();

            decimal each = Math.Floor(_remaining / count);
            decimal last = _remaining - (each * (count - 1));

            for (int i = 1; i <= count; i++)
            {
                _rows.Add(new InstalmentRowVM
                {
                    InstalmentNumber = i,
                    Amount = i == count ? last : each,
                    DueDate = start.AddMonths(i - 1)
                });
            }

            UpdateSummary();
        }

        private void DgInstalments_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            Dispatcher.InvokeAsync(UpdateSummary, System.Windows.Threading.DispatcherPriority.Background);
        }

        private void UpdateSummary()
        {
            decimal allocated = _rows.Sum(r => r.Amount);
            decimal diff = _remaining - allocated;

            AllocatedText.Text = $"PKR {allocated:N2}";

            if (Math.Abs(diff) < 1)
            {
                DiffText.Text = "✔ Balanced";
                DiffText.Foreground = System.Windows.Media.Brushes.Green;
                ValidationText.Text = "";
            }
            else if (diff > 0)
            {
                DiffText.Text = $"⚠ PKR {diff:N2} unallocated";
                DiffText.Foreground = System.Windows.Media.Brushes.OrangeRed;
                ValidationText.Text = $"PKR {diff:N2} remaining amount is not allocated in any instalment.";
                ValidationText.Foreground = System.Windows.Media.Brushes.OrangeRed;
            }
            else
            {
                DiffText.Text = $"⚠ Over by PKR {Math.Abs(diff):N2}";
                DiffText.Foreground = System.Windows.Media.Brushes.Red;
                ValidationText.Text = $"Total instalments exceed remaining by PKR {Math.Abs(diff):N2}.";
                ValidationText.Foreground = System.Windows.Media.Brushes.Red;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            dgInstalments.CommitEdit(DataGridEditingUnit.Row, true);

            if (_rows.Count == 0)
            {
                MessageBox.Show("Please add at least one instalment.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            foreach (var r in _rows)
            {
                if (r.Amount <= 0)
                {
                    MessageBox.Show($"Instalment #{r.InstalmentNumber} ka amount zero nahi ho sakta.",
                        "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (r.DueDate < DateTime.Today)
                {
                    var ok = MessageBox.Show(
                        $"Instalment #{r.InstalmentNumber} ki due date past mein hai ({r.DueDateDisplay}).\nKya aap continue karna chahte hain?",
                        "Past Date", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (ok != MessageBoxResult.Yes) return;
                }
            }

            // ✅ Instalments ka total remaining balance se exactly match hona chahiye —
            // pehle yeh sirf ek visual warning tha aur save ko block nahi karta tha.
            decimal allocated = _rows.Sum(r => r.Amount);
            decimal diff = _remaining - allocated;
            if (Math.Abs(diff) >= 1)
            {
                MessageBox.Show(
                    diff > 0
                        ? $"Instalments ka total (PKR {allocated:N2}) remaining balance (PKR {_remaining:N2}) se PKR {diff:N2} kam hai.\nBraye meherbani poori raqam allocate karein."
                        : $"Instalments ka total (PKR {allocated:N2}) remaining balance (PKR {_remaining:N2}) se PKR {Math.Abs(diff):N2} zyada hai.\nBraye meherbani amounts theek karein.",
                    "Plan Balanced Nahi Hai", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Result = _rows.Select(r => new InvoiceInstalment
            {
                InstalmentNumber = r.InstalmentNumber,
                Amount = r.Amount,
                DueDate = r.DueDate,
                Status = "Pending",
                Remarks = r.Remarks
            }).ToList();

            Saved = true;
            DialogResult = true; // ← yeh add karo, Close() mat karo
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
    }
}