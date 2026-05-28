using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Windows;

namespace Ramza_EBike_Swabi.Services
{
    public class UpdaterService
    {
        private const string GitHubUser = "abdullah-khan9";
        private const string GitHubRepo = "RAMZA-EBIKE-SWABI";
        public const string CurrentVersion = "1.0.1";

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

        // ── Download + Progress + Replace + Restart ──────────────────────
        private static async Task DownloadAndApply(HttpClient http, string downloadUrl)
        {
            string tempZip = Path.Combine(Path.GetTempPath(), "RamzaUpdate.zip");
            string extractPath = Path.Combine(Path.GetTempPath(), "RamzaUpdateExtracted");
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string exeName = Path.GetFileName(Environment.ProcessPath!);

            // appsettings.json backup — client ka config safe rahega
            string settingsSrc = Path.Combine(appDir, "appsettings.json");
            string settingsBak = Path.Combine(Path.GetTempPath(), "appsettings_backup.json");
            if (File.Exists(settingsSrc))
                File.Copy(settingsSrc, settingsBak, overwrite: true);

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
                    Title = "Updating...",
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
            using var fileStream = new FileStream(tempZip, FileMode.Create);

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

            // ── Extract ──────────────────────────────────────────────────
            Application.Current.Dispatcher.Invoke(() =>
            {
                label.Text = "Extracting files...";
                progressBar.Value = 100;
                percentLabel.Text = "Please wait...";
            });

            if (Directory.Exists(extractPath))
                Directory.Delete(extractPath, true);
            ZipFile.ExtractToDirectory(tempZip, extractPath);

            // ── Replace + Restart ─────────────────────────────────────────
            Application.Current.Dispatcher.Invoke(() =>
            {
                label.Text = "Installing update... App will restart shortly.";
            });

            string batPath = Path.Combine(Path.GetTempPath(), "ramza_update.bat");
            string bat = $"""
                @echo off
                timeout /t 2 /nobreak > nul
                xcopy /s /y "{extractPath}\*" "{appDir}"
                copy /y "{settingsBak}" "{appDir}appsettings.json"
                start "" "{appDir}{exeName}"
                rmdir /s /q "{extractPath}"
                del "{tempZip}"
                del "{settingsBak}"
                del "%~f0"
                """;

            await File.WriteAllTextAsync(batPath, bat);

            Process.Start(new ProcessStartInfo
            {
                FileName = batPath,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });

            Application.Current.Dispatcher.Invoke(() => progressWindow.Close());
            Application.Current.Shutdown();
        }
    }
}