using System;
using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace MyERP.Accounting.DomainServices;

/// <summary>
/// Domain service for Invoice Discounting calculations and eligibility rules.
/// Per ERPNext: Invoice Discounting = selling receivables to a bank at a discount.
///
/// GL pattern (see <see cref="Entities.InvoiceDiscounting"/> for the full lifecycle):
/// - On submit (Draft -&gt; Sanctioned): DR AccountsReceivableCredit / CR each invoice's own AR account
///   (moves the receivable off the normal AR ledger onto a dedicated "pledged" holding account)
/// - On disburse (Sanctioned -&gt; Disbursed): DR Bank + DR Bank Charges / CR Short-Term Loan
/// - On settle (Disbursed -&gt; Settled): DR Short-Term Loan / CR Bank
///
/// Per DO-NOT rules:
/// - Allow invoice discounting on already-discounted invoices (blocked)
/// - Allow the pledged outstanding amount to exceed the invoice's actual outstanding amount
/// </summary>
public class InvoiceDiscountingService : DomainService
{
    /// <summary>
    /// Calculate the discount charge (bank's fee) for discounting invoices.
    /// Typically: total_outstanding × discount_rate × remaining_days / 365
    /// </summary>
    public decimal CalculateDiscountCharge(
        decimal totalOutstanding,
        decimal annualDiscountRate,
        int daysToMaturity)
    {
        if (daysToMaturity <= 0 || annualDiscountRate <= 0)
            return 0;

        return Math.Round(totalOutstanding * annualDiscountRate / 100m * daysToMaturity / 365m, 2);
    }

    /// <summary>
    /// Calculate the disbursement amount (what the bank pays).
    /// = Total Outstanding - Discount Charge
    /// </summary>
    public decimal CalculateDisbursementAmount(
        decimal totalOutstanding,
        decimal discountCharge)
    {
        return totalOutstanding - discountCharge;
    }

    /// <summary>
    /// Validate that invoices are eligible for discounting.
    /// Per DO-NOT: cannot discount already-discounted invoices, cannot pledge more than the invoice's
    /// actual current outstanding amount. Callers must resolve <see cref="InvoiceForDiscounting.IsAlreadyDiscounted"/>
    /// and the real <see cref="InvoiceForDiscounting.ActualOutstandingAmount"/> server-side from persisted
    /// records — never trust a client-supplied flag for either, matching ERPNext's own
    /// validate_invoices() which re-reads both from the database.
    /// </summary>
    public static void ValidateInvoicesForDiscounting(
        IReadOnlyList<InvoiceForDiscounting> invoices)
    {
        foreach (var inv in invoices)
        {
            if (inv.IsAlreadyDiscounted)
                throw new BusinessException(MyERPDomainErrorCodes.InvoiceAlreadyDiscounted)
                    .WithData("invoiceNumber", inv.InvoiceNumber);

            if (inv.OutstandingAmount <= 0 || inv.OutstandingAmount > inv.ActualOutstandingAmount)
                throw new BusinessException(MyERPDomainErrorCodes.InvoiceDiscountingOutstandingExceeded)
                    .WithData("invoiceNumber", inv.InvoiceNumber)
                    .WithData("actualOutstanding", inv.ActualOutstandingAmount);
        }
    }
}

/// <summary>Invoice details for discounting eligibility validation.</summary>
public class InvoiceForDiscounting
{
    public Guid InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = null!;

    /// <summary>Amount being pledged (must not exceed <see cref="ActualOutstandingAmount"/>).</summary>
    public decimal OutstandingAmount { get; set; }

    /// <summary>The invoice's real, current outstanding amount, resolved server-side.</summary>
    public decimal ActualOutstandingAmount { get; set; }

    /// <summary>Resolved server-side: true if any other non-cancelled Invoice Discounting already pledges this invoice.</summary>
    public bool IsAlreadyDiscounted { get; set; }
}
