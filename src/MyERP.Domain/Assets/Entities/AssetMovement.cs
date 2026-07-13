using System;
using MyERP.Core;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Assets.Entities;

/// <summary>
/// Asset Movement — transfers asset location or custodian.
/// Tracks movement of assets between locations, employees, and companies.
/// Maps to ERPNext assets/doctype/asset_movement.
/// </summary>
public class AssetMovement : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid AssetId { get; set; }

    /// <summary>Movement type: Transfer, Issue, Receipt.</summary>
    public string MovementType { get; set; } = "Transfer";

    public DateTime MovementDate { get; set; }

    /// <summary>Source location/custodian.</summary>
    public string? SourceLocation { get; set; }
    public Guid? SourceEmployeeId { get; set; }

    /// <summary>Target location/custodian.</summary>
    public string? TargetLocation { get; set; }
    public Guid? TargetEmployeeId { get; set; }

    public string? Purpose { get; set; }
    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;

    protected AssetMovement() { }

    public AssetMovement(Guid id, Guid companyId, Guid assetId,
        string movementType, DateTime movementDate, Guid? tenantId = null) : base(id)
    {
        CompanyId = companyId;
        AssetId = assetId;
        MovementType = movementType;
        MovementDate = movementDate;
        TenantId = tenantId;
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
