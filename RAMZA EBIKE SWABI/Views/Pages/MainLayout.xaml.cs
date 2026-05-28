using System;
using System.Windows;
using System.Windows.Controls;
using Ramza_EBike_Swabi.Views.Pages.Components;
using Ramza_EBike_Swabi.Views.Windows;
using Ramza_EBike_Swabi.Services; // ← ADD KARO

namespace Ramza_EBike_Swabi.Views.Pages
{
    public partial class MainLayout : Window
    {
        private readonly string _fullName;
        private readonly string _role;
        public InvoiceTabManagerPage? InvoiceTabManager { get; set; }

        public MainLayout(string fullName, string role)
        {
            InitializeComponent();
            _fullName = fullName;
            _role = role;
            NavbarControl.Username = $"{_fullName} ({_role})";
            NavbarControl.LogoutClicked -= Navbar_LogoutClicked;
            NavbarControl.LogoutClicked += Navbar_LogoutClicked;
            MainFrame.Navigate(new DashboardPage(this));
        }

        public void Navigate(Page page)
        {
            if (page != null)
                MainFrame.Navigate(page);
        }

        private void Navbar_LogoutClicked(object? sender, EventArgs e)
        {
            var confirm = MessageBox.Show(
                "Are you sure you want to log out?",
                "Logout",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;
            var loginWindow = new LoginWindow();
            loginWindow.Show();
            this.Close();
        }

        // ← ADD KARO
        private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
        {
            await UpdaterService.CheckForUpdatesManualAsync();
        }
    }
}