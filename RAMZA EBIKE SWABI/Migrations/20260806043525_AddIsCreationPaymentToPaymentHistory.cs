using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RAMZA_EBIKE_SWABI.Migrations
{
    /// <inheritdoc />
    public partial class AddIsCreationPaymentToPaymentHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsCreationPayment",
                table: "CustomerPaymentHistories",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // ✅ Data backfill — for every existing invoice that has any AmountPaidCash /
            // AmountPaidAccount but no history row marked as its "Invoice Creation" payment
            // yet, insert one. The amount backfilled is whatever isn't already accounted for
            // by existing Due/Instalment history rows (AmountPaidCash/Account minus the sum
            // already in CustomerPaymentHistories) — mirroring the same safe formula the app
            // already uses elsewhere (RecalculateRemainingAfterAsync) to infer the
            // creation-time portion. This does not touch any existing row or invoice field —
            // it only adds the missing history entries so old data lines up with the new
            // Payment History behavior.
            migrationBuilder.Sql(@"
;WITH HistTotals AS (
    SELECT CustomerInvoiceId,
           SUM(AmountPaidCash) AS HistCash,
           SUM(AmountPaidAccount) AS HistAccount
    FROM CustomerPaymentHistories
    GROUP BY CustomerInvoiceId
),
CreationCalc AS (
    SELECT
        ci.CustomerInvoiceId,
        ci.InvoiceDate,
        ci.NetBill,
        ci.CustomerId,
        -- ✅ Legacy bills (created before the Cash/Account split existed) only have the
        -- old single AmountPaid field populated — treat that as all-cash, exactly like
        -- GenerateInvoicePage.LoadInvoiceForEdit already does for such invoices.
        CASE WHEN ci.AmountPaidCash = 0 AND ci.AmountPaidAccount = 0 AND ci.AmountPaid > 0
             THEN ci.AmountPaid ELSE ci.AmountPaidCash END AS EffectiveCash,
        CASE WHEN ci.AmountPaidCash = 0 AND ci.AmountPaidAccount = 0 AND ci.AmountPaid > 0
             THEN 0 ELSE ci.AmountPaidAccount END AS EffectiveAccount
    FROM CustomerInvoices ci
    WHERE NOT EXISTS (
        SELECT 1 FROM CustomerPaymentHistories cph
        WHERE cph.CustomerInvoiceId = ci.CustomerInvoiceId AND cph.IsCreationPayment = 1
    )
),
CreationCalc2 AS (
    SELECT
        cc.CustomerInvoiceId,
        cc.InvoiceDate,
        cc.NetBill,
        cc.CustomerId,
        CASE WHEN (cc.EffectiveCash - ISNULL(ht.HistCash, 0)) < 0 THEN 0
             ELSE (cc.EffectiveCash - ISNULL(ht.HistCash, 0)) END AS CreationCash,
        CASE WHEN (cc.EffectiveAccount - ISNULL(ht.HistAccount, 0)) < 0 THEN 0
             ELSE (cc.EffectiveAccount - ISNULL(ht.HistAccount, 0)) END AS CreationAccount
    FROM CreationCalc cc
    LEFT JOIN HistTotals ht ON ht.CustomerInvoiceId = cc.CustomerInvoiceId
)
INSERT INTO CustomerPaymentHistories
    (CustomerInvoiceId, AmountPaid, AmountPaidCash, AmountPaidAccount,
     PaymentDate, ReceivedBy, PaymentMethod, RemainingAfter,
     CashTransactionId, AccountTransactionId, InstalmentId, IsCreationPayment)
SELECT
    cc.CustomerInvoiceId,
    cc.CreationCash + cc.CreationAccount,
    cc.CreationCash,
    cc.CreationAccount,
    cc.InvoiceDate,
    ISNULL(cu.Name, N'Customer'),
    N'Invoice Creation',
    cc.NetBill - (cc.CreationCash + cc.CreationAccount),
    NULL,
    NULL,
    NULL,
    1
FROM CreationCalc2 cc
LEFT JOIN Customers cu ON cu.CustomerId = cc.CustomerId
WHERE (cc.CreationCash + cc.CreationAccount) > 0;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsCreationPayment",
                table: "CustomerPaymentHistories");
        }
    }
}
