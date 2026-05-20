// Services/ZakatService.cs
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Ramza_EBike_Swabi.Data;
using Ramza_EBike_Swabi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ramza_EBike_Swabi.Services
{
    public class ZakatService
    {
        // ═══════════════════════════════════════════════════════════
        // GET / LIST
        // ═══════════════════════════════════════════════════════════
        public async Task<List<ZakatRecord>> GetAllAsync()
        {
            using var db = new AppDbContext();
            return await db.ZakatRecords
                .Include(r => r.Payments)
                .OrderByDescending(r => r.CalculationDate)
                .ToListAsync();
        }

        public async Task<ZakatRecord?> GetByIdAsync(int id)
        {
            using var db = new AppDbContext();
            return await db.ZakatRecords
                .Include(r => r.Payments)
                .FirstOrDefaultAsync(r => r.Id == id);
        }

        // ═══════════════════════════════════════════════════════════
        // SAVE  (create or update — only when not locked)
        // ═══════════════════════════════════════════════════════════
        public async Task<ZakatRecord> SaveAsync(ZakatRecord record)
        {
            using var db = new AppDbContext();

            if (record.Id == 0)
            {
                // ── New record ───────────────────────────────────
                record.CalculationDate = DateTime.Now;
                record.TotalPaid = 0;
                record.IsLocked = false;
                db.ZakatRecords.Add(record);
                await db.SaveChangesAsync();
                // Return with the new Id populated
                return record;
            }
            else
            {
                // ── Update existing (not locked) ─────────────────
                var existing = await db.ZakatRecords.FindAsync(record.Id);
                if (existing == null)
                    throw new Exception("Zakat record not found.");
                if (existing.IsLocked)
                    throw new InvalidOperationException("This Zakat record is locked and cannot be edited.");

                existing.ZakatYear = record.ZakatYear;
                existing.CalculationDate = record.CalculationDate;
                existing.CashOnHand = record.CashOnHand;
                existing.BankBalance = record.BankBalance;
                existing.VendorCash = record.VendorCash;
                existing.InventoryWorth = record.InventoryWorth;
                existing.CustomerReceivables = record.CustomerReceivables;
                existing.VendorDues = record.VendorDues;
                existing.NisabThreshold = record.NisabThreshold;
                existing.NetZakatableWealth = record.NetZakatableWealth;
                existing.TotalZakatDue = record.TotalZakatDue;
                existing.Remarks = record.Remarks;

                await db.SaveChangesAsync();
                return existing;
            }
        }

        // ═══════════════════════════════════════════════════════════
        // ADD PAYMENT
        // ═══════════════════════════════════════════════════════════
        /// <summary>
        /// Records a Zakat payment instalment.
        /// allowVoluntary = true  → payment recorded even if RemainingDue = 0,
        ///                         and TotalPaid is NOT incremented (it's sadaqah).
        /// allowVoluntary = false → normal obligatory payment, validates remaining.
        /// </summary>
        public async Task<(bool Success, string Message)> AddPaymentAsync(
            ZakatPayment payment,
            AccountService accountService,
            bool allowVoluntary = false)
        {
            using var db = new AppDbContext();

            // ── Reload record fresh from DB with payments ────────
            var record = await db.ZakatRecords
                .Include(r => r.Payments)
                .FirstOrDefaultAsync(r => r.Id == payment.ZakatRecordId);

            if (record == null) return (false, "Zakat record not found.");
            if (record.IsLocked) return (false, "This Zakat year is already locked.");
            if (payment.Amount <= 0)
                return (false, "Amount must be greater than zero.");

            // ── Remaining check (obligatory payments only) ───────
            if (!allowVoluntary)
            {
                // Use DB-fresh TotalPaid — not in-memory sum — to avoid stale state
                decimal remaining = record.TotalZakatDue - record.TotalPaid;
                if (payment.Amount > remaining)
                    return (false,
                        $"Amount exceeds remaining due.\n" +
                        $"Remaining: PKR {remaining:N2}");
            }

            // ── Load account balance ─────────────────────────────
            var balance = await db.AccountBalances.FirstOrDefaultAsync();
            if (balance == null)
                return (false, "Account balance record not found.");

            // Normalise source string
            string src = (payment.PaymentSource?.Trim().ToLower() == "account")
                         ? "Account" : "Cash";

            // ── Deduct from correct balance ───────────────────────
            if (src == "Cash")
            {
                if (balance.CashBalance < payment.Amount)
                    return (false,
                        $"Insufficient Cash balance.\n" +
                        $"Available: PKR {balance.CashBalance:N2}");
                balance.CashBalance -= payment.Amount;
            }
            else // Account / Bank
            {
                if (balance.BankBalance < payment.Amount)
                    return (false,
                        $"Insufficient Account balance.\n" +
                        $"Available: PKR {balance.BankBalance:N2}");
                balance.BankBalance -= payment.Amount;
            }

            // ── Write AccountTransaction ─────────────────────────
            // For Cash   : WithdrawFromCash  (money leaves cash box)
            // For Account: DepositToAccount opposite = we use a new clear remark
            //              but the correct type for "money leaves account" is
            //              NOT CashWithdraw (that adds to cash).
            //              We store it as WithdrawFromCash with a clear remark,
            //              OR define a dedicated label via Remarks.
            //              Using WithdrawFromCash for both and differentiating by Remarks.
            var txn = new AccountTransaction
            {
                Type = TransactionType.WithdrawFromCash,  // generic "money out"
                ByWhom = string.IsNullOrWhiteSpace(payment.PaidByName)
                         ? "Zakat Payment" : payment.PaidByName,
                Amount = payment.Amount,
                TransactionDate = payment.PaymentDate,
                Remarks = $"Zakat Payment ({src}) — {record.ZakatYear} | " +
                          $"To: {payment.RecipientName}" +
                          (string.IsNullOrWhiteSpace(payment.Remarks)
                              ? "" : $" | {payment.Remarks}"),
                CashBalanceAfter = balance.CashBalance,
                BankBalanceAfter = balance.BankBalance
            };

            db.AccountTransactions.Add(txn);
            await db.SaveChangesAsync();           // txn.Id is now set

            // ── Store payment ────────────────────────────────────
            payment.PaymentSource = src;     // normalised
            payment.AccountTransactionId = txn.Id;

            db.ZakatPayments.Add(payment);

            // ── Update TotalPaid on record (obligatory only) ─────
            if (!allowVoluntary)
                record.TotalPaid += payment.Amount;

            await db.SaveChangesAsync();

            string msg = allowVoluntary
                ? "Voluntary charity (Sadaqah) payment recorded successfully."
                : "Zakat payment recorded successfully.";

            return (true, msg);
        }

        // ═══════════════════════════════════════════════════════════
        // DELETE PAYMENT  (reverses the account transaction)
        // ═══════════════════════════════════════════════════════════
        public async Task<(bool Success, string Message)> DeletePaymentAsync(int paymentId)
        {
            using var db = new AppDbContext();

            var payment = await db.ZakatPayments
                .Include(p => p.ZakatRecord)
                .FirstOrDefaultAsync(p => p.Id == paymentId);

            if (payment == null)
                return (false, "Payment not found.");
            if (payment.ZakatRecord?.IsLocked == true)
                return (false, "Cannot delete a payment from a locked Zakat year.");

            var balance = await db.AccountBalances.FirstOrDefaultAsync();
            if (balance != null)
            {
                // Return money to correct balance
                if (payment.PaymentSource?.Trim().ToLower() == "account")
                    balance.BankBalance += payment.Amount;
                else
                    balance.CashBalance += payment.Amount;

                // Reversal transaction for audit trail
                db.AccountTransactions.Add(new AccountTransaction
                {
                    Type = payment.PaymentSource?.Trim().ToLower() == "account"
                           ? TransactionType.DepositToAccount
                           : TransactionType.CashDeposit,
                    ByWhom = "Zakat Reversal",
                    Amount = payment.Amount,
                    TransactionDate = DateTime.Now,
                    Remarks = $"REVERSAL — Zakat payment #{payment.Id} deleted | " +
                              $"{payment.ZakatRecord?.ZakatYear} | " +
                              $"Was paid to: {payment.RecipientName}",
                    CashBalanceAfter = balance.CashBalance,
                    BankBalanceAfter = balance.BankBalance
                });
            }

            // Remove original linked transaction
            if (payment.AccountTransactionId.HasValue)
            {
                var orig = await db.AccountTransactions
                    .FindAsync(payment.AccountTransactionId.Value);
                if (orig != null)
                    db.AccountTransactions.Remove(orig);
            }

            // Roll back TotalPaid on parent record
            if (payment.ZakatRecord != null)
            {
                decimal newPaid = payment.ZakatRecord.TotalPaid - payment.Amount;
                payment.ZakatRecord.TotalPaid = Math.Max(0, newPaid);
            }

            db.ZakatPayments.Remove(payment);
            await db.SaveChangesAsync();

            return (true, "Payment deleted and balance restored.");
        }

        // ═══════════════════════════════════════════════════════════
        // LOCK YEAR
        // ═══════════════════════════════════════════════════════════
        public async Task<(bool Success, string Message)> LockYearAsync(int recordId)
        {
            using var db = new AppDbContext();

            var record = await db.ZakatRecords
                .Include(r => r.Payments)
                .FirstOrDefaultAsync(r => r.Id == recordId);

            if (record == null) return (false, "Record not found.");
            if (record.IsLocked) return (false, "Already locked.");

            record.IsLocked = true;
            record.LockedDate = DateTime.Now;
            await db.SaveChangesAsync();

            return (true, "Zakat year locked successfully.");
        }

        // ═══════════════════════════════════════════════════════════
        // EXPORT TO EXCEL
        // ═══════════════════════════════════════════════════════════
        public void ExportToExcel(ZakatRecord record, string filePath)
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Zakat Report");

            var titleBg = XLColor.FromHtml("#1E3A6E");
            var headerBg = XLColor.FromHtml("#2B579A");
            var sectionBg = XLColor.FromHtml("#D6E4F0");
            var altBg = XLColor.FromHtml("#F2F7FB");
            var totalBg = XLColor.FromHtml("#E8F0FE");
            var lockBg = XLColor.FromHtml("#E8F5E9");
            var white = XLColor.White;
            var bdrColor = XLColor.FromHtml("#B0BEC5");
            const int COL = 12;
            int row = 1;

            // Title
            Merge(ws, row, 1, row, COL,
                $"Zakat Report — {record.ZakatYear}",
                titleBg, white, 16, true);
            ws.Row(row++).Height = 30;

            Merge(ws, row, 1, row, COL,
                $"Calculated on: {record.CalculationDate:dd-MMM-yyyy}",
                sectionBg, XLColor.FromHtml("#1E3A6E"), 11, false, italic: true);
            ws.Row(row++).Height = 18;
            row++; // spacer

            // Wealth breakdown
            Merge(ws, row, 1, row, COL, "ZAKATABLE WEALTH BREAKDOWN", headerBg, white, 11, true);
            ws.Row(row++).Height = 20;

            Row2Col(ws, row++, "Cash on Hand", $"PKR {record.CashOnHand:N2}", altBg, bdrColor);
            Row2Col(ws, row++, "Bank / Account Balance", $"PKR {record.BankBalance:N2}", white, bdrColor);
            Row2Col(ws, row++, "Vendor Cash (Advance)", $"PKR {record.VendorCash:N2}", altBg, bdrColor);
            Row2Col(ws, row++, "Inventory Worth", $"PKR {record.InventoryWorth:N2}", white, bdrColor);
            Row2Col(ws, row++, "Customer Receivables", $"PKR {record.CustomerReceivables:N2}", altBg, bdrColor);
            Row2Col(ws, row++, "Less: Vendor Dues", $"- PKR {record.VendorDues:N2}", white, bdrColor,
                valueColor: XLColor.FromHtml("#A32D2D"));
            row++; // spacer

            bool applicable = record.NetZakatableWealth >= record.NisabThreshold;
            Row2Col(ws, row++, "Net Zakatable Wealth", $"PKR {record.NetZakatableWealth:N2}", totalBg, bdrColor, bold: true);
            Row2Col(ws, row++, "Nisab Threshold", $"PKR {record.NisabThreshold:N2}", totalBg, bdrColor);
            Row2Col(ws, row++, "Zakat Applicable?",
                applicable ? "Yes" : "No (Below Nisab)",
                totalBg, bdrColor,
                valueColor: applicable
                    ? XLColor.FromHtml("#0F6E56")
                    : XLColor.FromHtml("#A32D2D"));
            Row2Col(ws, row++, "Total Zakat Due (2.5%)", $"PKR {record.TotalZakatDue:N2}", totalBg, bdrColor,
                bold: true, valueColor: XLColor.FromHtml("#3C3489"));
            row += 2;

            // Payments header
            Merge(ws, row, 1, row, COL, "PAYMENT HISTORY", headerBg, white, 11, true);
            ws.Row(row++).Height = 20;

            string[] heads = { "#", "Recipient", "Amount (PKR)", "Date", "Paid By", "Source", "Remarks" };
            int[] widths = { 6, 22, 18, 16, 18, 10, 30 };
            for (int c = 0; c < heads.Length; c++)
            {
                var cell = ws.Cell(row, c + 1);
                cell.Value = heads[c];
                cell.Style
                    .Font.SetBold(true).Font.SetFontColor(white)
                    .Fill.SetBackgroundColor(headerBg)
                    .Border.SetOutsideBorder(XLBorderStyleValues.Thin)
                    .Border.SetOutsideBorderColor(bdrColor)
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                ws.Column(c + 1).Width = widths[c];
            }
            ws.Row(row++).Height = 20;

            bool alt = false;
            int idx = 1;
            var payments = record.Payments?.OrderBy(p => p.PaymentDate).ToList()
                           ?? new List<ZakatPayment>();

            foreach (var p in payments)
            {
                var bg = alt ? altBg : white;
                object[] vals =
                {
                    idx++,
                    p.RecipientName,
                    $"PKR {p.Amount:N2}",
                    p.PaymentDate.ToString("dd-MMM-yyyy"),
                    p.PaidByName,
                    p.PaymentSource,
                    p.Remarks ?? ""
                };
                for (int c = 0; c < vals.Length; c++)
                {
                    var cell = ws.Cell(row, c + 1);
                    cell.Value = XLCellValue.FromObject(vals[c]);
                    cell.Style
                        .Fill.SetBackgroundColor(bg)
                        .Border.SetOutsideBorder(XLBorderStyleValues.Thin)
                        .Border.SetOutsideBorderColor(bdrColor)
                        .Font.SetFontSize(10);
                }
                ws.Row(row++).Height = 18;
                alt = !alt;
            }

            Row2Col(ws, row++, "Total Paid",
                $"PKR {record.TotalPaid:N2}", totalBg, bdrColor, bold: true,
                valueColor: XLColor.FromHtml("#0F6E56"));
            Row2Col(ws, row++, "Remaining Due",
                $"PKR {record.RemainingDue:N2}", totalBg, bdrColor, bold: true,
                valueColor: record.RemainingDue > 0
                    ? XLColor.FromHtml("#A32D2D")
                    : XLColor.FromHtml("#0F6E56"));
            row++;

            // Lock info
            if (record.IsLocked)
            {
                Merge(ws, row, 1, row, COL,
                    $"LOCKED on {record.LockedDate:dd-MMM-yyyy HH:mm} — This Zakat year is fully closed.",
                    lockBg, XLColor.FromHtml("#1B5E20"), 11, true);
                ws.Row(row++).Height = 22;
            }

            if (!string.IsNullOrWhiteSpace(record.Remarks))
            {
                row++;
                Merge(ws, row, 1, row, COL,
                    $"Remarks: {record.Remarks}",
                    XLColor.FromHtml("#FAFAFA"), XLColor.Gray, 10, false, italic: true);
                ws.Row(row).Height = 16;
            }

            wb.SaveAs(filePath);
        }

        // ── Excel helpers ────────────────────────────────────────
        private static void Merge(IXLWorksheet ws, int row, int c1, int r2, int c2,
            string text, XLColor bg, XLColor fg, int fontSize, bool bold, bool italic = false)
        {
            var r = ws.Range(row, c1, r2, c2);
            r.Merge();
            r.Value = text;
            r.Style
                .Font.SetBold(bold).Font.SetItalic(italic)
                .Font.SetFontSize(fontSize).Font.SetFontColor(fg)
                .Fill.SetBackgroundColor(bg)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                .Alignment.SetVertical(XLAlignmentVerticalValues.Center);
        }

        private static void Row2Col(IXLWorksheet ws, int row, string label, string value,
            XLColor bg, XLColor bdrColor, bool bold = false, XLColor? valueColor = null)
        {
            var lc = ws.Cell(row, 1);
            lc.Value = label;
            lc.Style
                .Font.SetBold(bold).Font.SetFontSize(10)
                .Fill.SetBackgroundColor(bg)
                .Border.SetOutsideBorder(XLBorderStyleValues.Thin)
                .Border.SetOutsideBorderColor(bdrColor)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Left);
            ws.Range(row, 1, row, 6).Merge();

            var vc = ws.Cell(row, 7);
            vc.Value = value;
            vc.Style
                .Font.SetBold(bold).Font.SetFontSize(10)
                .Font.SetFontColor(valueColor ?? XLColor.Black)
                .Fill.SetBackgroundColor(bg)
                .Border.SetOutsideBorder(XLBorderStyleValues.Thin)
                .Border.SetOutsideBorderColor(bdrColor)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
            ws.Range(row, 7, row, 12).Merge();

            ws.Row(row).Height = 18;
        }
    }
}