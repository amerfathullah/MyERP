using System;
using MyERP.Core;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Bank Guarantee — guarantees received from customers or provided to suppliers/institutions.
/// Maps to ERPNext accounts/doctype/bank_guarantee.
/// </summary>
public class BankGuarantee : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public BankGuaranteeType BgType { get; set; } = BankGuaranteeType.Receiving;

    public string? ReferenceDocType { get; set; }
    public Guid? ReferenceDocId { get; set; }
    public string? ReferenceDocName { get; set; }

    public Guid? CustomerId { get; set; }
    public string? CustomerName { get; set; }

    public Guid? SupplierId { get; set; }
    public string? SupplierName { get; set; }

    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }

    public decimal Amount { get; set; }
    public DateTime StartDate { get; set; } = DateTime.UtcNow.Date;
    public int ValidityDays { get; set; }
    public DateTime? EndDate { get; set; }

    public string? Bank { get; set; }
    public Guid? BankAccountId { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? Account { get; set; }
    public string? Iban { get; set; }
    public string? BranchCode { get; set; }
    public string? SwiftNumber { get; set; }

    public string? BankGuaranteeNumber { get; set; }
    public string? NameOfBeneficiary { get; set; }
    public decimal MarginMoney { get; set; }
    public decimal Charges { get; set; }
    public string? FixedDepositNumber { get; set; }
    public string? ClausesAndConditions { get; set; }

    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;

    protected BankGuarantee() { }

    public BankGuarantee(
        Guid id,
        Guid companyId,
        BankGuaranteeType bgType,
        decimal amount,
        DateTime startDate,
        int validityDays,
        Guid? customerId = null,
        Guid? supplierId = null,
        Guid? tenantId = null) : base(id)
    {
        CompanyId = companyId;
        BgType = bgType;
        Amount = amount;
        StartDate = startDate;
        ValidityDays = validityDays;
        EndDate = validityDays > 0 ? startDate.AddDays(validityDays) : null;
        CustomerId = customerId;
        SupplierId = supplierId;
        TenantId = tenantId;
        Status = DocumentStatus.Draft;

        ValidateParty();
    }

    public void RecalculateEndDate()
    {
        if (ValidityDays > 0)
        {
            EndDate = StartDate.AddDays(ValidityDays);
        }
    }

    public void ValidateParty()
    {
        if (!CustomerId.HasValue && !SupplierId.HasValue)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Select customer or supplier for Bank Guarantee");
        }

        if (CustomerId.HasValue && SupplierId.HasValue)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Bank Guarantee cannot be linked to both Customer and Supplier. Select only one party.");
        }

        if (BgType == BankGuaranteeType.Receiving && SupplierId.HasValue)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Receiving Bank Guarantee must be linked to a Customer, not a Supplier.");
        }

        if (BgType == BankGuaranteeType.Providing && CustomerId.HasValue)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Providing Bank Guarantee must be linked to a Supplier, not a Customer.");
        }
    }

    public void Submit()
    {
        if (Status != DocumentStatus.Draft)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        }

        ValidateParty();

        if (string.IsNullOrWhiteSpace(BankGuaranteeNumber))
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Enter Bank Guarantee Number before submitting");
        }

        if (string.IsNullOrWhiteSpace(NameOfBeneficiary))
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Enter Name of Beneficiary before submitting");
        }

        if (string.IsNullOrWhiteSpace(Bank))
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Enter Bank name before submitting");
        }

        if (Amount <= 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("detail", "Bank Guarantee amount must be positive");
        }

        RecalculateEndDate();
        Status = DocumentStatus.Submitted;
    }

    public void Cancel()
    {
        if (Status != DocumentStatus.Submitted)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        }

        Status = DocumentStatus.Cancelled;
    }
}
