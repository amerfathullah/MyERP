using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Payment Ledger Entry (PLE) — tracks all payment-related movements.
/// This is the authoritative source for outstanding amounts on invoices.
/// Outstanding = SUM(PLE.AmountInAccountCurrency) WHERE against_voucher matches AND delinked = false.
/// </summary>
public class PaymentLedgerEntry : CreationAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public DateTime PostingDate { get; set; }

    /// <summary>The account (Receivable/Payable) this entry belongs to.</summary>
    public Guid AccountId { get; set; }

    /// <summary>Party type: Customer, Supplier, Employee.</summary>
    public string PartyType { get; set; } = null!;

    /// <summary>Party ID (Customer/Supplier/Employee ID).</summary>
    public Guid PartyId { get; set; }

    /// <summary>Source document type that created this PLE.</summary>
    public string VoucherType { get; set; } = null!;

    /// <summary>Source document ID.</summary>
    public Guid VoucherId { get; set; }

    /// <summary>The voucher type this PLE is against (for payment matching).</summary>
    public string AgainstVoucherType { get; set; } = null!;

    /// <summary>The voucher ID this PLE is against.</summary>
    public Guid AgainstVoucherId { get; set; }

    /// <summary>Amount in company currency.</summary>
    public decimal Amount { get; set; }

    /// <summary>Amount in the account's currency (for multi-currency).</summary>
    public decimal AmountInAccountCurrency { get; set; }

    /// <summary>Account currency code.</summary>
    public string AccountCurrency { get; set; } = "MYR";

    /// <summary>Due date for this outstanding amount.</summary>
    public DateTime? DueDate { get; set; }

    /// <summary>If true, this PLE has been delinked (excluded from outstanding calculation).</summary>
    public bool Delinked { get; set; }

    /// <summary>If true, this is a reversal entry created during cancellation.</summary>
    public bool IsReversal { get; set; }

    /// <summary>Cost center for dimension tracking.</summary>
    public Guid? CostCenterId { get; set; }

    /// <summary>Remarks/reference.</summary>
    public string? Remarks { get; set; }

    protected PaymentLedgerEntry() { }

    public PaymentLedgerEntry(
        Guid id, Guid companyId, DateTime postingDate,
        Guid accountId, string partyType, Guid partyId,
        string voucherType, Guid voucherId,
        string againstVoucherType, Guid againstVoucherId,
        decimal amount, decimal amountInAccountCurrency,
        string accountCurrency, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        PostingDate = postingDate;
        AccountId = accountId;
        PartyType = partyType;
        PartyId = partyId;
        VoucherType = voucherType;
        VoucherId = voucherId;
        AgainstVoucherType = againstVoucherType;
        AgainstVoucherId = againstVoucherId;
        Amount = amount;
        AmountInAccountCurrency = amountInAccountCurrency;
        AccountCurrency = accountCurrency;
        TenantId = tenantId;
    }
}
