using Microsoft.EntityFrameworkCore;
using Ramza_EBike_Swabi.Models;
using Microsoft.Extensions.Configuration;

namespace Ramza_EBike_Swabi.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext() { }
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Bike> Bikes { get; set; } = null!;
        public DbSet<Vendor> Vendor { get; set; } = null!;
        public DbSet<VendorBill> VendorBill { get; set; } = null!;
        public DbSet<VendorBillItem> VendorBillItem { get; set; } = null!;
        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<CustomerInvoice> CustomerInvoices => Set<CustomerInvoice>();
        public DbSet<CustomerInvoiceItem> CustomerInvoiceItems => Set<CustomerInvoiceItem>();
        public DbSet<BikeModel> BikeModels { get; set; } = null!;
        public DbSet<AccountBalance> AccountBalances { get; set; } = null!;
        public DbSet<AccountTransaction> AccountTransactions { get; set; } = null!;
        public DbSet<ProfitRecord> ProfitRecords { get; set; } = null!;
        public DbSet<InvoiceDraft> InvoiceDrafts { get; set; } = null!;
        public DbSet<VendorCashBalance> VendorCashBalances { get; set; } = null!;
        public DbSet<VendorCashLedger> VendorCashLedger { get; set; } = null!;
        public DbSet<VendorIncentive> VendorIncentives { get; set; } = null!;
        public DbSet<CustomerPaymentHistory> CustomerPaymentHistories { get; set; } = null!;
        public DbSet<VendorPaymentRecord> VendorPaymentRecords { get; set; } = null!;
        public DbSet<DocumentIssuanceRecord> DocumentIssuanceRecords { get; set; } = null!;

        public DbSet<ZakatRecord> ZakatRecords { get; set; } = null!;
        public DbSet<ZakatPayment> ZakatPayments { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: false)
                    .Build();
                var connStr = configuration.GetConnectionString("DefaultConnection");
                optionsBuilder.UseSqlServer(connStr);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<CustomerInvoiceItem>().Ignore(i => i.TotalPrice);

            modelBuilder.Entity<Customer>()
                .HasMany(c => c.Invoices).WithOne(i => i.Customer)
                .HasForeignKey(i => i.CustomerId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CustomerInvoice>()
                .HasMany(i => i.Items).WithOne(item => item.Invoice)
                .HasForeignKey(item => item.CustomerInvoiceId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CustomerInvoice>().Property(i => i.TotalAmount).HasPrecision(18, 2);
            modelBuilder.Entity<CustomerInvoice>().Property(i => i.Discount).HasPrecision(18, 2);
            modelBuilder.Entity<CustomerInvoice>().Property(i => i.NetBill).HasPrecision(18, 2);
            modelBuilder.Entity<CustomerInvoice>().Property(i => i.AmountPaid).HasPrecision(18, 2);
            modelBuilder.Entity<CustomerInvoice>().Property(i => i.RemainingBalance).HasPrecision(18, 2);
            modelBuilder.Entity<CustomerInvoice>().Property(i => i.DueDate).IsRequired(false);

            modelBuilder.Entity<CustomerInvoiceItem>().Property(i => i.Price).HasPrecision(18, 2);

            modelBuilder.Entity<VendorBill>().HasQueryFilter(b => !b.IsDeleted);
            modelBuilder.Entity<VendorBill>()
                .HasOne(b => b.Vendor).WithMany(v => v.Bills)
                .HasForeignKey(b => b.VendorId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<VendorBillItem>()
                .HasOne(i => i.VendorBill).WithMany(b => b.Items)
                .HasForeignKey(i => i.VendorBillId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<VendorBill>(entity =>
            {
                entity.Property(vb => vb.PreviousBalance).HasPrecision(18, 2).HasDefaultValue(0);
                entity.Property(vb => vb.TotalAmount).HasPrecision(18, 2).HasDefaultValue(0);
                entity.Property(vb => vb.AmountPaid).HasPrecision(18, 2).HasDefaultValue(0);
                entity.Property(vb => vb.NetBill).HasPrecision(18, 2)
                    .HasComputedColumnSql("[PreviousBalance] + [TotalAmount] - [AmountPaid]");
                entity.Property(vb => vb.RemainingBalance).HasPrecision(18, 2).HasDefaultValue(0);
                entity.Property(vb => vb.TotalCommission).HasPrecision(18, 2).HasDefaultValue(0);
                entity.Property(vb => vb.TotalTaxPaid).HasPrecision(18, 2).HasDefaultValue(0);
                entity.Property(vb => vb.BillDate).HasDefaultValueSql("GETDATE()");
            });

            modelBuilder.Entity<DocumentIssuanceRecord>()
                .Property(r => r.IssuanceDate).HasDefaultValueSql("GETDATE()");
            modelBuilder.Entity<DocumentIssuanceRecord>()
                .HasOne(r => r.Invoice)
                .WithMany()
                .HasForeignKey(r => r.CustomerInvoiceId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<VendorBillItem>(entity =>
            {
                entity.Property(i => i.BillRate).HasPrecision(18, 2).IsRequired();
                entity.Property(i => i.CommissionPercent).HasPrecision(18, 2).HasDefaultValue(8);
                entity.Property(i => i.TaxOnCommissionPercent).HasPrecision(18, 2).HasDefaultValue(12);
                entity.Property(i => i.ExtraDiscountPercent).HasPrecision(18, 2).HasDefaultValue(2);
                entity.Property(i => i.CommissionAmount).HasPrecision(18, 2).HasDefaultValue(0);
                entity.Property(i => i.TaxOnCommissionAmount).HasPrecision(18, 2).HasDefaultValue(0);
                entity.Property(i => i.FinalCommissionAmount).HasPrecision(18, 2).HasDefaultValue(0);
                entity.Property(i => i.ExtraCommissionAmount).HasPrecision(18, 2).HasDefaultValue(0);
                entity.Property(i => i.TotalCommissionAmount).HasPrecision(18, 2).HasDefaultValue(0);
                entity.Property(i => i.TotalWholesalePrice).HasPrecision(18, 2).HasDefaultValue(0);
            });
            // ── ZakatRecord ──────────────────────────────────────────
            modelBuilder.Entity<ZakatRecord>(entity =>
            {
                entity.Property(z => z.CashOnHand).HasPrecision(18, 2).HasDefaultValue(0);
                entity.Property(z => z.BankBalance).HasPrecision(18, 2).HasDefaultValue(0);
                entity.Property(z => z.VendorCash).HasPrecision(18, 2).HasDefaultValue(0);
                entity.Property(z => z.InventoryWorth).HasPrecision(18, 2).HasDefaultValue(0);
                entity.Property(z => z.CustomerReceivables).HasPrecision(18, 2).HasDefaultValue(0);
                entity.Property(z => z.VendorDues).HasPrecision(18, 2).HasDefaultValue(0);
                entity.Property(z => z.NisabThreshold).HasPrecision(18, 2).HasDefaultValue(170000);
                entity.Property(z => z.NetZakatableWealth).HasPrecision(18, 2).HasDefaultValue(0);
                entity.Property(z => z.TotalZakatDue).HasPrecision(18, 2).HasDefaultValue(0);
                entity.Property(z => z.TotalPaid).HasPrecision(18, 2).HasDefaultValue(0);
                entity.Property(z => z.CalculationDate).HasDefaultValueSql("GETDATE()");
                // RemainingDue is a computed C# property — NOT mapped to a column
                entity.Ignore(z => z.RemainingDue);
            });

            modelBuilder.Entity<ZakatPayment>(entity =>
            {
                entity.Property(p => p.Amount).HasPrecision(18, 2);
                entity.Property(p => p.PaymentDate).HasDefaultValueSql("GETDATE()");
                entity.Property(p => p.AccountTransactionId).IsRequired(false);

                entity.HasOne(p => p.ZakatRecord)
                      .WithMany(r => r.Payments)
                      .HasForeignKey(p => p.ZakatRecordId)
                      .OnDelete(DeleteBehavior.Cascade);
            });


            modelBuilder.Entity<Vendor>().Property(v => v.CreatedDate).HasDefaultValueSql("GETDATE()");
            modelBuilder.Entity<Bike>().Property(b => b.AddedDate).HasDefaultValueSql("GETDATE()");
            modelBuilder.Entity<User>().Property(u => u.CreatedAt).HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<BikeModel>().Property(b => b.CreatedDate).HasDefaultValueSql("GETDATE()");
            modelBuilder.Entity<BikeModel>().HasIndex(b => new { b.Model, b.Brand }).IsUnique();

            modelBuilder.Entity<AccountBalance>()
                .Property(a => a.CashBalance).HasPrecision(18, 2).HasDefaultValue(0);
            modelBuilder.Entity<AccountBalance>()
                .Property(a => a.BankBalance).HasPrecision(18, 2).HasDefaultValue(0);

            modelBuilder.Entity<AccountTransaction>()
                .Property(t => t.Amount).HasPrecision(18, 2);
            modelBuilder.Entity<AccountTransaction>()
                .Property(t => t.CashBalanceAfter).HasPrecision(18, 2);
            modelBuilder.Entity<AccountTransaction>()
                .Property(t => t.BankBalanceAfter).HasPrecision(18, 2);
            modelBuilder.Entity<AccountTransaction>()
                .Property(t => t.TransactionDate).HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<ProfitRecord>()
                .Property(p => p.TotalSalePrice).HasPrecision(18, 2);
            modelBuilder.Entity<ProfitRecord>()
                .Property(p => p.TotalWholesaleCost).HasPrecision(18, 2);
            modelBuilder.Entity<ProfitRecord>()
                .Property(p => p.Discount).HasPrecision(18, 2);
            modelBuilder.Entity<ProfitRecord>()
                .Property(p => p.Profit).HasPrecision(18, 2);
            modelBuilder.Entity<ProfitRecord>()
                .Property(p => p.SaleDate).HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<InvoiceDraft>()
                .Property(d => d.LastModified).HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<VendorCashBalance>()
                .Property(v => v.Balance).HasPrecision(18, 2).HasDefaultValue(0);
            modelBuilder.Entity<VendorCashBalance>()
                .HasIndex(v => v.VendorId).IsUnique();

            modelBuilder.Entity<VendorCashLedger>()
                .Property(l => l.Amount).HasPrecision(18, 2);
            modelBuilder.Entity<VendorCashLedger>()
                .Property(l => l.VendorCashBalanceAfter).HasPrecision(18, 2);
            modelBuilder.Entity<VendorCashLedger>()
                .Property(l => l.TransactionDate).HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<VendorPaymentRecord>()
                .Property(p => p.AmountPaid).HasPrecision(18, 2);
            modelBuilder.Entity<VendorPaymentRecord>()
                .Property(p => p.BalanceBefore).HasPrecision(18, 2);
            modelBuilder.Entity<VendorPaymentRecord>()
                .Property(p => p.BalanceAfter).HasPrecision(18, 2);
            modelBuilder.Entity<VendorPaymentRecord>()
                .Property(p => p.PaymentDate).HasDefaultValueSql("GETDATE()");

            // ── AccountTransactionId: nullable, no FK constraint so deleting a
            //    transaction does not cascade-delete the payment record.
            modelBuilder.Entity<VendorPaymentRecord>()
                .Property(p => p.AccountTransactionId).IsRequired(false);

            modelBuilder.Entity<VendorIncentive>()
                .Property(i => i.Amount).HasPrecision(18, 2);
            modelBuilder.Entity<VendorIncentive>()
                .Property(i => i.IncentiveDate).HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<CustomerPaymentHistory>()
                .Property(p => p.AmountPaid).HasPrecision(18, 2);
            modelBuilder.Entity<CustomerPaymentHistory>()
                .Property(p => p.RemainingAfter).HasPrecision(18, 2);
            modelBuilder.Entity<CustomerPaymentHistory>()
                .Property(p => p.PaymentDate).HasDefaultValueSql("GETDATE()");
        }
    }
}