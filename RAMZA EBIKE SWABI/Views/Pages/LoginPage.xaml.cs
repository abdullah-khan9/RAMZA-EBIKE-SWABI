using System.Windows;
using System.Windows.Controls;
using Ramza_EBike_Swabi.Services;
using Ramza_EBike_Swabi.Views.Pages;
using Ramza_EBike_Swabi.Views.Pages.Components;
using Ramza_EBike_Swabi.Views.Windows;

namespace Ramza_EBike_Swabi.Views.Pages
{
    public partial class LoginPage : Page
    {
        private readonly AuthService _auth = new();

        public LoginPage()
        {
            InitializeComponent();
        }

        private async void Login_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "";

            var username = UsernameBox.Text?.Trim() ?? "";
            var password = PasswordBox.Password;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            {
                StatusText.Text = "Enter username and password.";
                return;
            }

            try
            {
                var user = await _auth.LoginAsync(username, password);

                if (user != null)
                {
                    var mainLayout = new MainLayout(user.FullName, user.Role);
                    mainLayout.Show();

                    Window.GetWindow(this)?.Close();
                }
                else
                {
                    StatusText.Text = "Invalid username or password.";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"LOGIN ERROR:\n\n{ex}",
                    "Application Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                StatusText.Text = "Database connection failed.";
            }
        }


        private void Register_Click(object sender, RoutedEventArgs e)
        {
            // navigate to register page if you have one
            if (this.NavigationService != null)
                this.NavigationService.Navigate(new RegisterPage());
        }
    }
}
