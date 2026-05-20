// Models/InvoiceDraft.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace Ramza_EBike_Swabi.Models
{
    /// <summary>
    /// Stores a serialized in-progress invoice (JSON) so it survives
    /// navigation away and app restarts.
    /// </summary>
    public class InvoiceDraft
    {
        [Key]
        public int Id { get; set; }

        /// Tab display title e.g. "New Invoice 1"
        public string TabTitle { get; set; } = string.Empty;

        /// Full invoice state serialized as JSON
        public string DraftJson { get; set; } = string.Empty;

        public DateTime LastModified { get; set; } = DateTime.Now;

        /// Display order in tab strip
        public int TabOrder { get; set; } = 0;
    }
}