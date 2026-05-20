using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Ramza_EBike_Swabi.Models
{
    public class BillBikeItem : INotifyPropertyChanged
    {
        public int BikeId { get; set; }

        public string Model { get; set; } = "";
        public string Brand { get; set; } = "";
        public string MotorPower { get; set; } = "";
        public string MotorNumber { get; set; } = "";
        public string ChassisNumber { get; set; } = "";
        public string Warranty { get; set; } = "";
        public string Color { get; set; } = "";

        private decimal _price;
        public decimal Price
        {
            get => _price;
            set { _price = value; OnPropertyChanged(); OnPropertyChanged(nameof(TotalPrice)); }
        }

        private int _quantity = 1;
        public int Quantity
        {
            get => _quantity;
            set { _quantity = value; OnPropertyChanged(); OnPropertyChanged(nameof(TotalPrice)); }
        }

        public decimal TotalPrice => Price * Quantity;

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
