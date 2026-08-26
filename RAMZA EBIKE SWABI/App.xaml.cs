using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Ramza_EBike_Swabi.Data;

namespace Ramza_EBike_Swabi
{
    public partial class App : Application
    {
        // ✅ No longer hardcoded — read from appsettings.json (same source the rest
        // of the app already uses via AppDbContext.OnConfiguring). Previously this
        // was a separate hardcoded string that could silently drift out of sync with
        // appsettings.json, causing startup migrations AND the exit-time backup to
        // target the wrong server/database whenever appsettings.json was changed
        // (e.g. moved to a different SQL Server instance).
        private string GetConnectionString()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            return configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "DefaultConnection not found in appsettings.json.");
        }

        // ════════════════════════════════════════════════════════
        // SUPABASE LICENSE CONFIG
        // ════════════════════════════════════════════════════════
        private const string SupabaseUrl = "https://bdodynkzdouehahqhnkn.supabase.co";
        private const string SupabaseAnonKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6ImJkb2R5bmt6ZG91ZWhhaHFobmtuIiwicm9sZSI6ImFub24iLCJpYXQiOjE3ODE2Nzc1NDQsImV4cCI6MjA5NzI1MzU0NH0.7kQkI1ieCZeZxtd8cQdGTtxgmtkYNPyUS-TP7xyURgI";
        private const string AppLicenseKey = "RAMZA-EBIKE-001";

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            bool licensed = await CheckLicenseAsync();
            if (!licensed) return;

            EnsureDatabaseReady();
        }

        // ════════════════════════════════════════════════════════
        // LICENSE CHECK
        // ════════════════════════════════════════════════════════
        private async Task<bool> CheckLicenseAsync()
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(8);
                client.DefaultRequestHeaders.Add("apikey", SupabaseAnonKey);
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", SupabaseAnonKey);

                string url = $"{SupabaseUrl}/rest/v1/app_licenses" +
                             $"?app_key=eq.{AppLicenseKey}" +
                             $"&select=is_active,client_name";

                var response = await client.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    // Server error — offline tolerance, app chalane do
                    return true;
                }

                string json = await response.Content.ReadAsStringAsync();

                var records = JsonSerializer.Deserialize<LicenseRecord[]>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (records == null || records.Length == 0)
                {
                    ShowAccessDenied("License not found.\nPlease contact developer.");
                    return false;
                }

                if (!records[0].Is_Active)
                {
                    ShowAccessDenied(
                        "Application Access Denied\n\n" +
                        "Contact Developer:\n" +
                        "Abdullah Khan — +92 311 9484920");
                    return false;
                }

                return true;
            }
            catch (HttpRequestException)
            {
                // Internet nahi — chalane do
                return true;
            }
            catch (TaskCanceledException)
            {
                // Timeout — chalane do
                return true;
            }
            catch
            {
                // Koi bhi unexpected error — chalane do
                return true;
            }
        }

        private void ShowAccessDenied(string message)
        {
            MessageBox.Show(
                message,
                "Access Denied — Ramza Electric E-Bikes",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }

        // ════════════════════════════════════════════════════════
        // DATABASE
        // ════════════════════════════════════════════════════════
        private void EnsureDatabaseReady()
        {
            try
            {
                var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
                optionsBuilder.UseSqlServer(GetConnectionString());
                using var context = new AppDbContext(optionsBuilder.Options);
                context.Database.Migrate();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Database setup failed:\n\n{ex.Message}\n\nPlease make sure SQL Server Express is installed and running.",
                    "Database Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown();
            }
        }

        // ════════════════════════════════════════════════════════
        // EXIT — BACKUP
        // ════════════════════════════════════════════════════════
        protected override void OnExit(ExitEventArgs e)
        {
            TryBackup();
            base.OnExit(e);
        }

        private void TryBackup()
        {
            try
            {
                string connStr = GetConnectionString();
                string databaseName = new SqlConnectionStringBuilder(connStr).InitialCatalog;

                string backupDrive = GetNonSystemDrive();
                string backupFolder = backupDrive != null
                    ? Path.Combine(backupDrive, "RamzaEBikeBackups")
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "RamzaEBikeBackups");

                if (!Directory.Exists(backupFolder))
                    Directory.CreateDirectory(backupFolder);

                string today = DateTime.Now.ToString("yyyy-MM-dd");

                var todayBackups = new DirectoryInfo(backupFolder)
                    .GetFiles($"RamzaBackup_{today}_*.bak")
                    .OrderByDescending(f => f.CreationTime)
                    .ToList();

                foreach (var f in todayBackups) f.Delete();

                string fileName = $"RamzaBackup_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.bak";
                string fullPath = Path.Combine(backupFolder, fileName);
                string safePath = fullPath.Replace("'", "''");

                using var con = new SqlConnection(connStr);
                con.Open();
                using var cmd = new SqlCommand(
                    $"BACKUP DATABASE [{databaseName}] TO DISK = N'{safePath}' WITH INIT, FORMAT",
                    con)
                { CommandTimeout = 0 };
                cmd.ExecuteNonQuery();

                var allBackups = new DirectoryInfo(backupFolder)
                    .GetFiles("RamzaBackup_*.bak")
                    .OrderByDescending(f => f.CreationTime)
                    .ToList();

                if (allBackups.Count > 10)
                    foreach (var old in allBackups.Skip(10))
                        old.Delete();
            }
            catch { /* silent fail */ }
        }

        private string GetNonSystemDrive()
        {
            try
            {
                return DriveInfo.GetDrives()
                    .Where(d =>
                        d.IsReady &&
                        d.DriveType == DriveType.Fixed &&
                        !d.Name.StartsWith("C", StringComparison.OrdinalIgnoreCase) &&
                        d.AvailableFreeSpace > 500 * 1024 * 1024)
                    .OrderBy(d => d.Name)
                    .FirstOrDefault()
                    ?.RootDirectory.FullName;
            }
            catch { return null; }
        }
    }

    // ════════════════════════════════════════════════════════
    // LICENSE RECORD
    // ════════════════════════════════════════════════════════
    internal class LicenseRecord
    {
        public bool Is_Active { get; set; }
        public string Client_Name { get; set; } = "";
    }
}