using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Ramza_EBike_Swabi.Models;
using Ramza_EBike_Swabi.Services;

namespace Ramza_EBike_Swabi.Views.Pages
{
    public partial class AddEditBikeWindow : Window
    {
        private readonly BikeService _bikeService = new();
        private readonly BikeModelService _bikeModelService = new();

        private readonly Bike? _editBike;

        private List<BikeModel> _allBikeModels = new();
        private bool _suppressModelTextChanged = false;

        public AddEditBikeWindow(Bike? bike = null)
        {
            InitializeComponent();
            _editBike = bike;
            TitleText.Text = bike == null ? "Add New Bike" : "Edit Bike";

            // Wire LostFocus in code to avoid XAML encoding issues
            ModelBox.LostFocus += ModelBox_LostFocus;

            if (bike != null)
            {
                ModelBox.Text = bike.Model;
                BrandBox.Text = bike.Brand;
                MotorBox.Text = bike.MotorPower;
                BatteryBox.Text = bike.BatteryCapacity;
                MotorNumberBox.Text = bike.MotorNumber;
                ChassisNumberBox.Text = bike.ChassisNumber;
                ColorBox.Text = bike.Color;
                WeightBox.Text = bike.Weight;
                WarrantyBox.Text = bike.Warranty;
                PriceBox.Text = bike.Price.ToString(CultureInfo.InvariantCulture);
                VendorBox.Text = bike.VendorName;
                QuantityBox.Text = bike.Quantity.ToString();
            }

            LoadBikeModels();
        }

        // ===========================
        // LOAD BIKE MODELS
        // ===========================
        private async void LoadBikeModels()
        {
            _allBikeModels = await _bikeModelService.GetAllAsync();
        }

        // ===========================
        // AUTOCOMPLETE
        // ===========================
        private void ModelBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressModelTextChanged) return;

            string typed = ModelBox.Text?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(typed))
            {
                ModelPopup.IsOpen = false;
                return;
            }

            var suggestions = _allBikeModels
                .Where(b => b.Model.ToLower().Contains(typed.ToLower()) ||
                            b.Brand.ToLower().Contains(typed.ToLower()))
                .Take(12)
                .ToList();

            if (suggestions.Count == 0)
            {
                ModelPopup.IsOpen = false;
                return;
            }

            ModelSuggestionList.ItemsSource = suggestions;
            ModelSuggestionList.DisplayMemberPath = "Model";
            ModelPopup.IsOpen = true;
        }

        private void ModelSuggestionList_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            BikeModel? selected = ModelSuggestionList.SelectedItem as BikeModel;

            if (selected == null)
            {
                if (ModelSuggestionList.InputHitTest(e.GetPosition(ModelSuggestionList))
                        is FrameworkElement fe)
                    selected = fe.DataContext as BikeModel;
            }

            if (selected == null) return;

            ApplyBikeModel(selected);
            ModelPopup.IsOpen = false;
        }

        private void ModelBox_LostFocus(object sender, RoutedEventArgs e)
        {
            Dispatcher.InvokeAsync(() =>
            {
                if (!ModelPopup.IsKeyboardFocusWithin)
                    ModelPopup.IsOpen = false;
            }, System.Windows.Threading.DispatcherPriority.Background);
        }

        private void ApplyBikeModel(BikeModel bikeModel)
        {
            _suppressModelTextChanged = true;

            ModelBox.Text = bikeModel.Model;
            BrandBox.Text = bikeModel.Brand;
            MotorBox.Text = bikeModel.MotorPower;
            BatteryBox.Text = bikeModel.BatteryCapacity;
            ColorBox.Text = bikeModel.Color;
            WarrantyBox.Text = bikeModel.Warranty;
            // MotorNumber, ChassisNumber, Price, Quantity left for manual entry

            _suppressModelTextChanged = false;
        }

        // ===========================
        // SAVE
        // ===========================
        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ModelBox.Text) ||
                string.IsNullOrWhiteSpace(BrandBox.Text))
            {
                MessageBox.Show("Please enter at least Model and Brand.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            decimal price = 0m;
            if (!string.IsNullOrWhiteSpace(PriceBox.Text) &&
                !decimal.TryParse(PriceBox.Text, NumberStyles.Number,
                    CultureInfo.InvariantCulture, out price))
            {
                MessageBox.Show("Invalid price format.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int quantity = 0;
            if (!string.IsNullOrWhiteSpace(QuantityBox.Text) &&
                !int.TryParse(QuantityBox.Text, out quantity))
            {
                MessageBox.Show("Quantity must be a whole number.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var bike = _editBike ?? new Bike();
            bike.Model = ModelBox.Text.Trim();
            bike.Brand = BrandBox.Text.Trim();
            bike.MotorPower = MotorBox.Text.Trim();
            bike.BatteryCapacity = BatteryBox.Text.Trim();
            bike.MotorNumber = MotorNumberBox.Text.Trim();
            bike.ChassisNumber = ChassisNumberBox.Text.Trim();
            bike.Color = ColorBox.Text.Trim();
            bike.Weight = WeightBox.Text.Trim();
            bike.Warranty = WarrantyBox.Text.Trim();
            bike.Price = price;
            bike.VendorName = VendorBox.Text.Trim();
            bike.Quantity = quantity;

            if (_editBike == null)
                await _bikeService.AddBikeAsync(bike);
            else
                await _bikeService.UpdateBikeAsync(bike);

            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}