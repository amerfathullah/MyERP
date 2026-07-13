using System;
using MyERP.Core;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Tax.Entities;

/// <summary>
/// Tax Withholding Entry — records tax withheld on a transaction.
/// Per Malaysia: Section 107A withholding tax on payments to non-resident suppliers.
/// Per ERPNext: Tax Withholding Certificate matching, FIFO deque, LDC support.
/// </summary>
public class TaxWithholdingEntry : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    /// <summary>Party (supplier) this withholding applies to.</summary>
    public Guid PartyId { get; set; }
    public string PartyType { get; set; } = "Supplier";

    /// <summary>Source document (e.g., PurchaseInvoice, PaymentEntry).</summary>
    public string VoucherType { get; set; } = null!;
    public Guid VoucherId { get; set; }

    /// <summary>Tax account to post the withheld amount.</summary>
    public Guid TaxAccountId { get; set; }

    /// <summary>Withholding tax category (e.g., "Section 107A", "Royalty", "Technical Fee").</summary>
    public string? TaxCategory { get; set; }

    /// <summary>Withholding rate (percentage).</summary>
    public decimal WithholdingRate { get; set; }

    /// <summary>Taxable amount (base on which withholding is calculated).</summary>
    public decimal TaxableAmount { get; set; }

    /// <summary>Withheld amount = TaxableAmount × WithholdingRate / 100.</summary>
    public decimal WithheldAmount { get; set; }

    /// <summary>Posting date of the withholding.</summary>
    public DateTime PostingDate { get; set; }

    /// <summary>Whether a Lower Deduction Certificate (LDC) applies.</summary>
    public bool HasLDC { get; set; }

    /// <summary>Reduced rate from LDC (if applicable).</summary>
    public decimal? LdcRate { get; set; }

    /// <summary>Certificate number for audit trail.</summary>
    public string? CertificateNumber { get; set; }

    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;

    protected TaxWithholdingEntry() { }

    public TaxWithholdingEntry(
        Guid id, Guid companyId, Guid partyId, string voucherType, Guid voucherId,
        Guid taxAccountId, decimal withholdingRate, decimal taxableAmount,
        DateTime postingDate, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        PartyId = partyId;
        VoucherType = voucherType;
        VoucherId = voucherId;
        TaxAccountId = taxAccountId;
        WithholdingRate = withholdingRate;
        TaxableAmount = taxableAmount;
        WithheldAmount = Math.Round(taxableAmount * withholdingRate / 100m, 2);
        PostingDate = postingDate;
        TenantId = tenantId;
    }

    /// <summary>Apply Lower Deduction Certificate — reduces withholding rate.</summary>
    public void ApplyLDC(decimal ldcRate, string certificateNumber)
    {
        HasLDC = true;
        LdcRate = ldcRate;
        CertificateNumber = certificateNumber;
        WithheldAmount = Math.Round(TaxableAmount * ldcRate / 100m, 2);
    }

    public void Submit()
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Submitted;
    }

    public void Cancel()
    {
        if (Status != DocumentStatus.Submitted)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Cancelled;
    }
}
