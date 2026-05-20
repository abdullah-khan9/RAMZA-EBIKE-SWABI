using System.Windows;
using System.Windows.Controls;
using Ramza_EBike_Swabi.Services;

namespace Ramza_EBike_Swabi.Views.Pages
{
    public partial class RegisterPage : Page
    {
        private readonly AuthService _auth = new();

        public RegisterPage()
        {
            InitializeComponent();
        }

        private async void Create_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "";

            var fullName = FullNameBox.Text?.Trim() ?? "";
            var username = UsernameBox.Text?.Trim() ?? "";
            var password = PasswordBox.Password;
            var confirm = ConfirmPasswordBox.Password;
            var role = ((ComboBoxItem)RoleBox.SelectedItem).Content.ToString()!;

            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                StatusText.Text = "Please fill all fields.";
                return;
            }

            if (password != confirm)
            {
                StatusText.Text = "Passwords do not match.";
                return;
            }

            var (success, error) = await _auth.RegisterAsync(username, password, fullName, role);
            if (success)
            {
                MessageBox.Show("Registered successfully — please login.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                this.NavigationService?.Navigate(new LoginPage());

            }
            else
            {
                StatusText.Text = error;
            }
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            this.NavigationService?.Navigate(new LoginPage());
        }
    }
}
