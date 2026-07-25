using Microsoft.EntityFrameworkCore;
using Ramza_EBike_Swabi.Data;
using Ramza_EBike_Swabi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ramza_EBike_Swabi.Services
{
    public class InstalmentService
    {
        // Get all instalments for an invoice
        public async Task<List<InvoiceInstalment>> GetInstalmentsAsync(int invoiceId)
        {
            using var db = new AppDbContext();
            return await db.InvoiceInstalments
                .Where(i => i.CustomerInvoiceId == invoiceId)
                .OrderBy(i => i.InstalmentNumber)
                .ToListAsync();
        }

        // Get all pending/overdue/partially-paid instalments (for notifications & the
        // Instalments tab) — anything that isn't fully "Paid" yet still needs attention.
        public async Task<List<InvoiceInstalment>> GetDueInstalmentsAsync()
        {
            using var db = new AppDbContext();
            return await db.InvoiceInstalments
                .Include(i => i.Invoice)
                    .ThenInclude(inv => inv.Customer)
                .Where(i => i.Status == "Pending" || i.Status == "Overdue" || i.Status == "Partially Paid")
                .OrderBy(i => i.DueDate)
                .ToListAsync();
        }

        // Get invoice IDs that have instalment plans
        public async Task<HashSet<int>> GetInstalmentInvoiceIdsAsync()
        {
            using var db = new AppDbContext();
            var ids = await db.InvoiceInstalments
                .Select(i => i.CustomerInvoiceId)
                .Distinct()
                .ToListAsync();
            return new HashSet<int>(ids);
        }

        // Save instalments for an invoice (replace existing)
        public async Task SaveInstalmentsAsync(int invoiceId, List<InvoiceInstalment> instalments)
        {
            using var db = new AppDbContext();
            var existing = await db.InvoiceInstalments
                .Where(i => i.CustomerInvoiceId == invoiceId)
                .ToListAsync();
            db.InvoiceInstalments.RemoveRange(existing);
            await db.SaveChangesAsync();

            foreach (var inst in instalments)
            {
                inst.CustomerInvoiceId = invoiceId;
                db.InvoiceInstalments.Add(inst);
            }
            await db.SaveChangesAsync();
        }

        // Mark instalment as paid — with cash/account split + account transaction
        public async Task MarkPaidAsync(
            int instalmentId,
            decimal cashAmount,
            decimal accountAmount,
            string? remarks,
            AccountService accountService)
        {
            using var db = new AppDbContext();

            var inst = await db.InvoiceInstalments.FindAsync(instalmentId);
            if (inst == null) return;

            decimal totalPaid = cashAmount + accountAmount;

            // ✅ Accumulate across possibly-multiple (partial) payments instead of overwriting
            decimal newPaidAmount = inst.PaidAmount + totalPaid;

            inst.PaidAmount = newPaidAmount;
            inst.PaidCash += cashAmount;
            inst.PaidAccount += accountAmount;
            inst.PaidOn = DateTime.Now;
            // ✅ Only mark fully "Paid" once the accumulated amount covers the instalment's
            // due amount — a partial payment used to silently mark the instalment as fully
            // paid, hiding the remaining shortfall from the due/overdue lists.
            inst.Status = newPaidAmount >= inst.Amount ? "Paid" : "Partially Paid";
            inst.PaymentSource = cashAmount > 0 && accountAmount > 0 ? "Cash + Account"
                               : accountAmount > 0 ? "Account"
                               : "Cash";
            if (!string.IsNullOrWhiteSpace(remarks)) inst.Remarks = remarks;

            await db.SaveChangesAsync();

            // Update invoice remaining balance
            var invoice = await db.CustomerInvoices
    .Include(i => i.Customer)
    .FirstOrDefaultAsync(i => i.CustomerInvoiceId == inst.CustomerInvoiceId);
            if (invoice != null)
            {
                invoice.AmountPaid += totalPaid;
                invoice.AmountPaidCash += cashAmount;
                invoice.AmountPaidAccount += accountAmount;
                invoice.RemainingBalance -= totalPaid;
                if (invoice.RemainingBalance < 0) invoice.RemainingBalance = 0;
                invoice.Status = invoice.RemainingBalance == 0 ? "Clear" : "Partially Paid";
                await db.SaveChangesAsync();
            }

            // Account transactions
            string invoiceRef = $"INV-{inst.CustomerInvoiceId:D4} — Instalment #{inst.InstalmentNumber}";
            string customerName = invoice?.Customer?.Name ?? "Customer";

            int? cashTxnId = null;
            int? accountTxnId = null;

            if (cashAmount > 0)
                cashTxnId = await accountService.RecordInvoicePaymentAsync(
                    cashAmount, customerName, invoiceRef, isCash: true);

            if (accountAmount > 0)
                accountTxnId = await accountService.RecordInvoicePaymentAsync(
                    accountAmount, customerName, invoiceRef, isCash: false);

            // ✅ Log into CustomerPaymentHistory so instalment collections show up in the
            // Payment History window and so its RemainingAfter recalculation (which infers
            // "paid at invoice creation" as AmountPaid minus history total) stays correct —
            // previously instalment payments were invisible to that table.
            if (invoice != null && totalPaid > 0)
            {
                using var historyDb = new AppDbContext();
                historyDb.CustomerPaymentHistories.Add(new CustomerPaymentHistory
                {
                    CustomerInvoiceId = inst.CustomerInvoiceId,
                    InstalmentId = inst.Id,
                    AmountPaid = totalPaid,
                    AmountPaidCash = cashAmount,
                    AmountPaidAccount = accountAmount,
                    PaymentDate = DateTime.Now,
                    ReceivedBy = customerName,
                    PaymentMethod = $"Instalment #{inst.InstalmentNumber} ({inst.PaymentSource ?? "Cash"})",
                    RemainingAfter = invoice.RemainingBalance,
                    CashTransactionId = cashTxnId,
                    AccountTransactionId = accountTxnId
                });
                await historyDb.SaveChangesAsync();
            }
        }

        // ✅ Adjust an instalment's paid amounts when a linked CustomerPaymentHistory row is
        // edited or deleted (cashDelta/accountDelta are signed — negative to reverse/reduce).
        // Recomputes Status consistently (Paid / Partially Paid / Pending / Overdue) instead
        // of leaving it in whatever state the last direct write left it in. Uses the caller's
        // own db context so it participates in the same save as the rest of the reversal.
        public async Task AdjustInstalmentPaidAmountAsync(
            AppDbContext db, int instalmentId, decimal cashDelta, decimal accountDelta)
        {
            var inst = await db.InvoiceInstalments.FindAsync(instalmentId);
            if (inst == null) return;

            inst.PaidCash = Math.Max(0, inst.PaidCash + cashDelta);
            inst.PaidAccount = Math.Max(0, inst.PaidAccount + accountDelta);
            inst.PaidAmount = Math.Max(0, inst.PaidCash + inst.PaidAccount);

            if (inst.PaidAmount <= 0)
            {
                inst.PaidAmount = 0;
                inst.PaidCash = 0;
                inst.PaidAccount = 0;
                inst.PaidOn = null;
                inst.PaymentSource = null;
                inst.Status = inst.DueDate.Date < DateTime.Today ? "Overdue" : "Pending";
            }
            else if (inst.PaidAmount >= inst.Amount)
            {
                inst.Status = "Paid";
            }
            else
            {
                inst.Status = "Partially Paid";
            }
        }

        // Update overdue statuses
        public async Task RefreshStatusesAsync()
        {
            using var db = new AppDbContext();
            var pending = await db.InvoiceInstalments
                .Where(i => (i.Status == "Pending" || i.Status == "Partially Paid")
                             && i.DueDate.Date < DateTime.Today)
                .ToListAsync();

            foreach (var inst in pending)
                inst.Status = "Overdue";

            await db.SaveChangesAsync();
        }

        // Delete all instalments for an invoice
        public async Task DeleteInstalmentsAsync(int invoiceId)
        {
            using var db = new AppDbContext();
            var existing = await db.InvoiceInstalments
                .Where(i => i.CustomerInvoiceId == invoiceId)
                .ToListAsync();
            db.InvoiceInstalments.RemoveRange(existing);
            await db.SaveChangesAsync();
        }
    }
}