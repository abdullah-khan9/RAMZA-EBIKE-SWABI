// Models/AccountTransaction.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ramza_EBike_Swabi.Models
{
    public enum TransactionType
    {
        CashDeposit,            // External cash → Cash Balance
        DepositToAccount,       // External transfer → Account/Bank Balance
        CashWithdraw,           // Account Balance → Cash Balance
        ConvertCashToAccount,   // Cash Balance → Account Balance
        WithdrawFromCash,       // Spend / remove from Cash Balance
        WithdrawFromAccount     // ✅ NEW — Spend / remove from Account Balance
    }

    public class AccountTransaction
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public TransactionType Type { get; set; }
        [Required]
        public string ByWhom { get; set; } = string.Empty;
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }
        public DateTime TransactionDate { get; set; } = DateTime.Now;
        public string? Remarks { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal CashBalanceAfter { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal BankBalanceAfter { get; set; }
    }
}