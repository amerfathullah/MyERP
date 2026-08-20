using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Tax.Entities;

/// <summary>
/// Lower Deduction Certificate (LDC) — a supplier-held certificate entitling them to a reduced
/// withholding tax rate, up to a limit, for a specific Tax Withholding Category within a validity
/// window. Maps to ERPNext's "Lower Deduction Certificate" doctype (referenced by
/// tax_withholding_category.py's get_ldc_details(), no dedicated JSON present in this repo's
/// mirror — field list reconstructed from that method's query).
/// </summary>
public class LowerDeductionCertificate : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; private set; }
    public Guid SupplierId { get; private set; }
    public Guid TaxWithholdingCategoryId { get; private set; }

    public string CertificateNumber { get; private set; } = null!;

    /// <summary>Reduced withholding rate (percentage) this certificate grants.</summary>
    public decimal Rate { get; private set; }

    /// <summary>Maximum taxable amount this certificate covers before reverting to the standard rate.</summary>
    public decimal CertificateLimit { get; private set; }

    public DateTime ValidFrom { get; private set; }
    public DateTime ValidUpto { get; private set; }

    protected LowerDeductionCertificate() { }

    public LowerDeductionCertificate(
        Guid id, Guid companyId, Guid supplierId, Guid taxWithholdingCategoryId,
        string certificateNumber, decimal rate, decimal certificateLimit,
        DateTime validFrom, DateTime validUpto, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = Check.NotDefaultOrNull<Guid>(companyId, nameof(companyId));
        SupplierId = Check.NotDefaultOrNull<Guid>(supplierId, nameof(supplierId));
        TaxWithholdingCategoryId = Check.NotDefaultOrNull<Guid>(taxWithholdingCategoryId, nameof(taxWithholdingCategoryId));
        SetCertificateNumber(certificateNumber);
        SetValidity(validFrom, validUpto);
        Rate = rate;
        CertificateLimit = certificateLimit;
        TenantId = tenantId;
    }

    public void SetCertificateNumber(string certificateNumber)
        => CertificateNumber = Check.NotNullOrWhiteSpace(certificateNumber, nameof(certificateNumber), LowerDeductionCertificateConsts.MaxCertificateNumberLength);

    public void SetValidity(DateTime validFrom, DateTime validUpto)
    {
        if (validFrom >= validUpto)
            throw new BusinessException(MyERPDomainErrorCodes.TaxWithholdingRateDateRangeInvalid)
                .WithData("reason", "Lower Deduction Certificate valid_from must be before valid_upto");

        ValidFrom = validFrom;
        ValidUpto = validUpto;
    }

    public void SetTerms(decimal rate, decimal certificateLimit)
    {
        Rate = rate;
        CertificateLimit = certificateLimit;
    }

    public bool CoversDate(DateTime date) => ValidFrom <= date && date <= ValidUpto;
}
