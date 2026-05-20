// Services/DocumentIssuanceService.cs
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Ramza_EBike_Swabi.Data;
using Ramza_EBike_Swabi.Models;

namespace Ramza_EBike_Swabi.Services
{
    public class DocumentIssuanceService
    {
        /// <summary>Persist a new issuance record.</summary>
        public async Task AddIssuanceAsync(DocumentIssuanceRecord record)
        {
            using var db = new AppDbContext();
            db.DocumentIssuanceRecords.Add(record);
            await db.SaveChangesAsync();
        }

        /// <summary>Retrieve full history for one invoice, newest first.</summary>
        public async Task<List<DocumentIssuanceRecord>> GetHistoryAsync(int invoiceId)
        {
            using var db = new AppDbContext();
            return await db.DocumentIssuanceRecords
                .Where(r => r.CustomerInvoiceId == invoiceId)
                .OrderByDescending(r => r.IssuanceDate)
                .ToListAsync();
        }

        /// <summary>Update an existing issuance record including document flags.</summary>
        public async Task UpdateIssuanceAsync(
            int recordId,
            bool warrantyCardIssued, bool voucherCustomerIssued, bool voucherCompanyIssued,
            string issuedBy, string receivedBy,
            DateTime issuanceDate, string notes)
        {
            using var db = new AppDbContext();
            var record = await db.DocumentIssuanceRecords.FindAsync(recordId);
            if (record == null) return;

            record.WarrantyCardIssued = warrantyCardIssued;
            record.VoucherCustomerIssued = voucherCustomerIssued;
            record.VoucherCompanyIssued = voucherCompanyIssued;
            record.IssuedBy = issuedBy;
            record.ReceivedBy = receivedBy;
            record.IssuanceDate = issuanceDate;
            record.Notes = notes;

            await db.SaveChangesAsync();
        }

        /// <summary>Delete all issuance history for an invoice.</summary>
        public async Task DeleteAllHistoryAsync(int invoiceId)
        {
            using var db = new AppDbContext();
            var records = await db.DocumentIssuanceRecords
                .Where(r => r.CustomerInvoiceId == invoiceId)
                .ToListAsync();
            db.DocumentIssuanceRecords.RemoveRange(records);
            await db.SaveChangesAsync();
        }
    }
}