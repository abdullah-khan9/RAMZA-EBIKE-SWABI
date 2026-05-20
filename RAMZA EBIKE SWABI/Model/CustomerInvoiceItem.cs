using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;

namespace Ramza_EBike_Swabi.Models
{
    public class CustomerInvoiceItem : INotifyPropertyChanged
    {
        public int CustomerInvoiceItemId { get; set; }
        public int CustomerInvoiceId { get; set; }
        public CustomerInvoice Invoice { get; set; } = null!;

        // ✅ Nullable — not every item comes from a bike record (manual entry allowed)
        public int? BikeId { get; set; }

        public string Model { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string MotorPower { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string MotorNumber { get; set; } = string.Empty;
        public string ChassisNumber { get; set; } = string.Empty;
        public string Warranty { get; set; } = string.Empty;

        private decimal _price;
        public decimal Price
        {
            get => _price;
            set
            {
                _price = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalPrice));
            }
        }

        private int _quantity = 1;
        public int Quantity
        {
            get => _quantity;
            set
            {
                _quantity = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalPrice));
            }
        }

        [NotMapped]
        public decimal TotalPrice => Price * Quantity;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string prop = "")
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}