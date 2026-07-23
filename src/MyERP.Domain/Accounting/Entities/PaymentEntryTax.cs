using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Payment Entry tax/charge row — PE has its OWN tax engine separate from SI/PI.
/// Per ERPNext accounts/doctype/advance_taxes_and_charges:
/// - Charge types: "On Paid Amount" (instead of "On Net Total"), "Actual"
/// - PE taxes apply to the payment amount, not invoice net total
/// - Tax account currency MUST equal company currency (hard throw per DO-NOT)
/// - Included_in_paid_amount flag: if true, tax is deducted from paid amount (not added)
/// - Direction-dependent: add_deduct_tax × payment_type determines DR/CR sign
///   Pay+Add→debit, Pay+Deduct→credit, Receive+Add→credit, Receive+Deduct→debit
/// </summary>
public class PaymentEntryTax : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid PaymentEntryId { get; set; }

    /// <summary>GL account for this tax (must be in company currency).</summary>
    public Guid AccountId { get; set; }

    /// <summary>Charge calculation method: "On Paid Amount" or "Actual".</summary>
    public PaymentTaxChargeType ChargeType { get; set; } = PaymentTaxChargeType.OnPaidAmount;

    /// <summary>Tax rate percentage (for "On Paid Amount" type).</summary>
    public decimal Rate { get; set; }

    /// <summary>Calculated or fixed tax amount.</summary>
    public decimal TaxAmount { get; set; }

    /// <summary>Tax amount in base/company currency.</summary>
    public decimal BaseTaxAmount { get; set; }

    /// <summary>
    /// Whether this tax is included in (deducted from) the paid amount.
    /// If true: tax reduces what the party receives (not added on top).
    /// If false: tax is additional (contra entry posted separately).
    /// </summary>
    public bool IncludedInPaidAmount { get; set; }

    /// <summary>
    /// Whether to add or deduct this charge.
    /// Combined with PaymentType determines GL debit/credit direction.
    /// </summary>
    public TaxAddDeduct AddDeductTax { get; set; } = TaxAddDeduct.Add;

    /// <summary>Description/label for this tax row.</summary>
    public string? Description { get; set; }

    /// <summary>Cost center for this tax GL entry.</summary>
    public Guid? CostCenterId { get; set; }

    /// <summary>Reference for the tax row (e.g., Tax Withholding Category).</summary>
    public string? AccountHead { get; set; }

    /// <summary>
    /// Marks this row as exchange gain/loss (auto-managed, not user-editable).
    /// Excluded from unallocated_amount calculation (per gotcha #437).
    /// </summary>
    public bool IsExchangeGainLoss { get; set; }

    protected PaymentEntryTax() { }

    public PaymentEntryTax(Guid id, Guid paymentEntryId, Guid accountId, Guid? tenantId = null)
        : base(id)
    {
        PaymentEntryId = paymentEntryId;
        AccountId = accountId;
        TenantId = tenantId;
    }

    /// <summary>
    /// Calculates tax amount from rate and paid amount.
    /// Per ERPNext: "On Paid Amount" → amount = paidAmount × rate / 100
    /// </summary>
    public void Calculate(decimal paidAmount, decimal exchangeRate = 1m)
    {
        if (ChargeType == PaymentTaxChargeType.OnPaidAmount)
        {
            TaxAmount = paidAmount * Rate / 100m;
        }
        // Actual: TaxAmount is set directly (no calculation needed)

        BaseTaxAmount = TaxAmount * exchangeRate;
    }

    /// <summary>
    /// Determines if this tax creates a GL debit or credit based on payment direction.
    /// Per gotcha #624: Pay+Add→debit, Pay+Deduct→credit, Receive+Add→credit, Receive+Deduct→debit
    /// </summary>
    public bool IsDebit(string paymentType)
    {
        return paymentType switch
        {
            "Pay" => AddDeductTax == TaxAddDeduct.Add,     // Pay+Add = debit, Pay+Deduct = credit
            "Receive" => AddDeductTax == TaxAddDeduct.Deduct, // Receive+Deduct = debit, Receive+Add = credit
            _ => true
        };
    }
}

public enum PaymentTaxChargeType
{
    /// <summary>Calculated as percentage of paid amount.</summary>
    OnPaidAmount = 0,
    /// <summary>Fixed amount (entered directly).</summary>
    Actual = 1,
}

public enum TaxAddDeduct
{
    Add = 0,
    Deduct = 1,
}
