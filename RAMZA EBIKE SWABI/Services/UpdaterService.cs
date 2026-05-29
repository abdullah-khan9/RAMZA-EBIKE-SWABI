using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;

namespace Ramza_EBike_Swabi.Services
{
    public class UpdaterService
    {
        private const string GitHubUser = "abdullah-khan9";
        private const string GitHubRepo = "RAMZA-EBIKE-SWABI";
        public const string CurrentVersion = "1.0.3";

        // ── Sirf button click se call hoga ──────────────────────────────
        public static async Task CheckForUpdatesManualAsync()
        {
            try
            {
                using var http = new HttpClient();
                http.Timeout = TimeSpan.FromMinutes(10);
                http.DefaultRequestHeaders.Add("User-Agent", "RamzaEBikeApp");

                var url = $"https://api.github.com/repos/{GitHubUser}/{GitHubRepo}/releases/latest";
                var json = await http.GetStringAsync(url);
                var root = JsonDocument.Parse(json).RootElement;

                string latest = root.GetProperty("tag_name").GetString()!.TrimStart('v');
                string publishedAt = root.GetProperty("published_at").GetString()!;
                DateTime releaseDate = DateTime.Parse(publishedAt).ToLocalTime();

                // ── No update ────────────────────────────────────────────
                if (latest == CurrentVersion)
                {
                    MessageBox.Show(
                        $"✅ Your software is up to date!\n\n" +
                        $"Current Version :  v{CurrentVersion}\n" +
                        $"Last Update     :  {releaseDate:dd MMM yyyy   hh:mm tt}",
                        "No Updates Available",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                // ── Update available ─────────────────────────────────────
                string downloadUrl = root
                    .GetProperty("assets")[0]
                    .GetProperty("browser_download_url")
                    .GetString()!;

                var result = MessageBox.Show(
                    $"🔄 A new update is available!\n\n" +
                    $"Your Version   :  v{CurrentVersion}\n" +
                    $"New Version    :  v{latest}\n" +
                    $"Release Date   :  {releaseDate:dd MMM yyyy   hh:mm tt}\n\n" +
                    $"Would you like to update now?",
                    "Update Available",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                    await DownloadAndApply(http, downloadUrl);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"⚠️ Unable to check for updates.\n\nPlease check your internet connection.\n\nError: {ex.Message}",
                    "Update Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        // ── Download Installer + Progress + Run ──────────────────────────
        private static async Task DownloadAndApply(HttpClient http, string downloadUrl)
        {
            string tempInstaller = Path.Combine(Path.GetTempPath(), "RamzaSetup.exe");

            // ── Progress Window ──────────────────────────────────────────
            System.Windows.Controls.ProgressBar progressBar = null!;
            System.Windows.Controls.TextBlock label = null!;
            System.Windows.Controls.TextBlock percentLabel = null!;
            Window progressWindow = null!;

            Application.Current.Dispatcher.Invoke(() =>
            {
                label = new System.Windows.Controls.TextBlock
                {
                    Text = "Downloading update, please wait...",
                    FontSize = 14,
                    Margin = new Thickness(0, 0, 0, 10)
                };

                progressBar = new System.Windows.Controls.ProgressBar
                {
                    Height = 25,
                    Minimum = 0,
                    Maximum = 100,
                    Value = 0
                };

                percentLabel = new System.Windows.Controls.TextBlock
                {
                    Text = "0%",
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    Margin = new Thickness(0, 8, 0, 0)
                };

                var stack = new System.Windows.Controls.StackPanel { Margin = new Thickness(25) };
                stack.Children.Add(label);
                stack.Children.Add(progressBar);
                stack.Children.Add(percentLabel);

                progressWindow = new Window
                {
                    Title = "Updating Ramza E-Bike Swabi",
                    Width = 430,
                    Height = 170,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    ResizeMode = ResizeMode.NoResize,
                    WindowStyle = WindowStyle.ToolWindow,
                    Content = stack
                };

                progressWindow.Show();
            });

            // ── Download ─────────────────────────────────────────────────
            using var response = await http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            long totalBytes = response.Content.Headers.ContentLength ?? 0;
            using var stream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(tempInstaller, FileMode.Create);

            byte[] buffer = new byte[8192];
            long downloaded = 0;
            int read;

            while ((read = await stream.ReadAsync(buffer)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read));
                downloaded += read;

                if (totalBytes > 0)
                {
                    int percent = (int)(downloaded * 100 / totalBytes);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        progressBar.Value = percent;
                        percentLabel.Text = $"{percent}%  —  {downloaded / 1024 / 1024} MB / {totalBytes / 1024 / 1024} MB";
                    });
                }
            }

            fileStream.Close();

            Application.Current.Dispatcher.Invoke(() =>
            {
                label.Text = "Download complete. Installing update...";
                progressBar.Value = 100;
                percentLabel.Text = "Please wait...";
            });

            // ── Installer Silent Run ──────────────────────────────────────
            Application.Current.Dispatcher.Invoke(() => progressWindow.Close());

            Process.Start(new ProcessStartInfo
            {
                FileName = tempInstaller,
                Arguments = "/SILENT",
                UseShellExecute = true
            });

            Application.Current.Shutdown();
        }
    }
}