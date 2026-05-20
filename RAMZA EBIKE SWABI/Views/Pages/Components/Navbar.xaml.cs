using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Ramza_EBike_Swabi.Views.Pages.Components
{
    public partial class Navbar : UserControl
    {
        public event EventHandler? LogoutClicked;

        private readonly DispatcherTimer _timer;

        private string _username = "User";
        public string Username
        {
            get => _username;
            set
            {
                _username = value;
                WelcomeText.Text = $"Welcome: {_username}";
            }
        }

        public Navbar()
        {
            InitializeComponent();

            // Setup time
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (s, e) => TimeText.Text = DateTime.Now.ToString("dd MMM yyyy HH:mm:ss");
            _timer.Start();
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            LogoutClicked?.Invoke(this, EventArgs.Empty);
        }
    }
}
