using System.Windows;
using Ramza_EBike_Swabi.Views.Pages;

namespace Ramza_EBike_Swabi.Views.Windows
{
    
    public partial class LoginWindow : Window
    {
        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        public LoginWindow()
        {
            InitializeComponent();
            // Load the LoginPage inside the frame
            ShellFrame.Navigate(new LoginPage());
        }

    }
}
