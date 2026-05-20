using System;
using Microsoft.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Ramza_EBike_Swabi.Data;

namespace Ramza_EBike_Swabi
{
    public partial class App : Application
    {
        private const string DatabaseName = "RamzaEBikeSwabiDb";
        private readonly string connectionString =
            "Server=.\\SQLEXPRESS;Database=RamzaEBikeSwabiDb;Trusted_Connection=True;TrustServerCertificate=True;";

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            EnsureDatabaseReady();
        }

        private void EnsureDatabaseReady()
        {
            try
            {
                var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
                optionsBuilder.UseSqlServer(connectionString);

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

        protected override void OnExit(ExitEventArgs e)
        {
            TryBackup();
            base.OnExit(e);
        }

        private void TryBackup()
        {
            try
            {
                // ✅ Try to find D:, E: or any non-C: drive first
                string backupDrive = GetNonSystemDrive();

                string backupFolder;
                if (backupDrive != null)
                {
                    // ✅ Safe from Windows corruption
                    backupFolder = Path.Combine(backupDrive, "RamzaEBikeBackups");
                }
                else
                {
                    // ✅ Fallback: Documents on C: if no other drive exists
                    backupFolder = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        "RamzaEBikeBackups");
                }

                if (!Directory.Exists(backupFolder))
                    Directory.CreateDirectory(backupFolder);

                string today = DateTime.Now.ToString("yyyy-MM-dd");

                // ✅ Delete older backups of today, keep only latest
                var todayBackups = new DirectoryInfo(backupFolder)
                    .GetFiles($"RamzaBackup_{today}_*.bak")
                    .OrderByDescending(f => f.CreationTime)
                    .ToList();

                foreach (var oldTodayFile in todayBackups)
                    oldTodayFile.Delete();

                // ✅ Create new backup with timestamp
                string newFileName = $"RamzaBackup_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.bak";
                string newFullPath = Path.Combine(backupFolder, newFileName);
                string safePath = newFullPath.Replace("'", "''");

                string backupQuery = $@"
                    BACKUP DATABASE [{DatabaseName}]
                    TO DISK = N'{safePath}'
                    WITH INIT, FORMAT";

                using (SqlConnection con = new SqlConnection(connectionString))
                {
                    con.Open();
                    using (SqlCommand cmd = new SqlCommand(backupQuery, con))
                    {
                        cmd.CommandTimeout = 0;
                        cmd.ExecuteNonQuery();
                    }
                }

                // ✅ Keep only last 10 days of backups
                var allBackups = new DirectoryInfo(backupFolder)
                    .GetFiles("RamzaBackup_*.bak")
                    .OrderByDescending(f => f.CreationTime)
                    .ToList();

                if (allBackups.Count > 10)
                {
                    foreach (var oldFile in allBackups.Skip(10))
                        oldFile.Delete();
                }
            }
            catch
            {
                // Silent fail - do not disturb user
            }
        }

        // ✅ Finds first available non-C: fixed hard drive with 500MB+ free
        private string GetNonSystemDrive()
        {
            try
            {
                var drive = DriveInfo.GetDrives()
                    .Where(d =>
                        d.IsReady &&
                        d.DriveType == DriveType.Fixed &&
                        !d.Name.StartsWith("C", StringComparison.OrdinalIgnoreCase) &&
                        d.AvailableFreeSpace > 500 * 1024 * 1024)
                    .OrderBy(d => d.Name)
                    .FirstOrDefault();

                return drive?.RootDirectory.FullName;
            }
            catch
            {
                return null;
            }
        }
    }
}