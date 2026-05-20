// Services/InvoiceDraftService.cs
using Microsoft.EntityFrameworkCore;
using Ramza_EBike_Swabi.Data;
using Ramza_EBike_Swabi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Ramza_EBike_Swabi.Services
{
    public class InvoiceDraftService
    {
        private static readonly JsonSerializerOptions _json =
            new() { WriteIndented = false };

        // ===========================
        // SAVE / UPDATE ONE DRAFT
        // ===========================
        public async Task SaveDraftAsync(int draftId, string tabTitle,
            InvoiceDraftData data, int tabOrder)
        {
            using var db = new AppDbContext();

            string json = JsonSerializer.Serialize(data, _json);

            if (draftId <= 0)
            {
                // New draft — caller should store the returned Id
                throw new InvalidOperationException(
                    "Use CreateDraftAsync to create, then SaveDraftAsync to update.");
            }

            var draft = await db.InvoiceDrafts.FindAsync(draftId);
            if (draft == null) return;

            draft.TabTitle = tabTitle;
            draft.DraftJson = json;
            draft.LastModified = DateTime.Now;
            draft.TabOrder = tabOrder;

            await db.SaveChangesAsync();
        }

        // ===========================
        // CREATE NEW DRAFT
        // ===========================
        public async Task<int> CreateDraftAsync(string tabTitle, int tabOrder)
        {
            using var db = new AppDbContext();

            var emptyData = new InvoiceDraftData();
            var draft = new InvoiceDraft
            {
                TabTitle = tabTitle,
                DraftJson = JsonSerializer.Serialize(emptyData, _json),
                LastModified = DateTime.Now,
                TabOrder = tabOrder
            };

            db.InvoiceDrafts.Add(draft);
            await db.SaveChangesAsync();
            return draft.Id;
        }

        // ===========================
        // DELETE DRAFT (after save or close)
        // ===========================
        public async Task DeleteDraftAsync(int draftId)
        {
            using var db = new AppDbContext();
            var draft = await db.InvoiceDrafts.FindAsync(draftId);
            if (draft != null)
            {
                db.InvoiceDrafts.Remove(draft);
                await db.SaveChangesAsync();
            }
        }

        // ===========================
        // LOAD ALL DRAFTS (on startup / navigation back)
        // ===========================
        public async Task<List<InvoiceDraft>> GetAllDraftsAsync()
        {
            using var db = new AppDbContext();
            return await db.InvoiceDrafts
                .OrderBy(d => d.TabOrder)
                .ToListAsync();
        }

        // ===========================
        // DESERIALIZE
        // ===========================
        public static InvoiceDraftData? Deserialize(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<InvoiceDraftData>(json);
            }
            catch
            {
                return null;
            }
        }
    }
}