using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.CRM.Entities;

/// <summary>
/// Contract — customer/supplier agreement lifecycle management.
/// Per ERPNext: Contract entity tracks legal agreements with parties.
/// Lifecycle: Draft → Active → InactiveByExpiry → InactiveByAutoRenewFailure
/// Supports auto-renewal, fulfilment tracking, and document linking.
/// </summary>
public class Contract : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string ContractNumber { get; set; } = null!;
    public string? ContractName { get; set; }

    /// <summary>"Customer" or "Supplier".</summary>
    public string PartyType { get; set; } = null!;
    public Guid PartyId { get; set; }
    public string? PartyName { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    /// <summary>Date when both parties signed the contract.</summary>
    public DateTime? SigningDate { get; set; }

    public ContractStatus Status { get; private set; } = ContractStatus.Unsigned;

    /// <summary>Linked contract template.</summary>
    public Guid? ContractTemplateId { get; set; }

    /// <summary>Contract terms and conditions (Jinja-rendered from template).</summary>
    public string? ContractTerms { get; set; }

    /// <summary>Fulfilment tracking.</summary>
    public bool RequiresFulfilment { get; set; }
    public bool IsFulfilmentSatisfied { get; set; }

    /// <summary>Auto-renewal settings.</summary>
    public bool IsAutoRenewal { get; set; }
    public int? RenewalReminderDays { get; set; }

    /// <summary>IP/document ownership.</summary>
    public string? IpOwnership { get; set; }

    /// <summary>Contract value (for financial tracking).</summary>
    public decimal? ContractValue { get; set; }
    public string? CurrencyCode { get; set; }

    public string? Notes { get; set; }

    protected Contract() { }

    public Contract(Guid id, Guid companyId, string contractNumber, string partyType,
        Guid partyId, DateTime startDate, Guid? tenantId = null)
        : base(id)
    {
        Check.NotNullOrWhiteSpace(contractNumber, nameof(contractNumber));
        Check.NotNullOrWhiteSpace(partyType, nameof(partyType));
        CompanyId = companyId;
        ContractNumber = contractNumber;
        PartyType = partyType;
        PartyId = partyId;
        StartDate = startDate;
        TenantId = tenantId;
    }

    /// <summary>Signs the contract — transitions from Unsigned to Active.</summary>
    public void Sign(DateTime signingDate)
    {
        if (Status != ContractStatus.Unsigned)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("documentType", "Contract")
                .WithData("status", Status.ToString());
        SigningDate = signingDate;
        Status = ContractStatus.Active;
    }

    /// <summary>Checks if contract is expired based on end date.</summary>
    public bool IsExpired(DateTime asOfDate) =>
        EndDate.HasValue && EndDate.Value < asOfDate;

    /// <summary>Auto-renew the contract by extending end date.</summary>
    public void Renew(DateTime newEndDate)
    {
        if (Status != ContractStatus.Active)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("documentType", "Contract")
                .WithData("status", Status.ToString());
        EndDate = newEndDate;
    }

    /// <summary>Mark as inactive (expired or failed renewal).</summary>
    public void MarkInactive(bool failedRenewal = false)
    {
        Status = failedRenewal
            ? ContractStatus.InactiveByAutoRenewFailure
            : ContractStatus.InactiveByExpiry;
    }

    /// <summary>Cancel the contract.</summary>
    public void Cancel()
    {
        if (Status == ContractStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("documentType", "Contract")
                .WithData("status", Status.ToString());
        Status = ContractStatus.Cancelled;
    }
}

