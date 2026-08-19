using System;
using System.Collections.Generic;
using MyERP.Core;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Assets.Entities;

public class AssetMovement : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public string MovementNumber { get; set; } = null!;
    public Guid CompanyId { get; set; }
    public AssetMovementPurpose Purpose { get; set; } = AssetMovementPurpose.Transfer;
    public DateTime TransactionDate { get; set; }
    public string? ReferenceType { get; set; }
    public string? ReferenceId { get; set; }

    // Legacy/single-asset convenience properties
    public Guid? AssetId { get; set; }
    public string? MovementType { get; set; } = "Transfer";
    public DateTime MovementDate { get; set; }
    public string? SourceLocation { get; set; }
    public Guid? SourceLocationId { get; set; }
    public Guid? SourceEmployeeId { get; set; }
    public string? TargetLocation { get; set; }
    public Guid? TargetLocationId { get; set; }
    public Guid? TargetEmployeeId { get; set; }

    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;

    public List<AssetMovementItem> Items { get; private set; } = new();

    protected AssetMovement() { }

    public AssetMovement(
        Guid id,
        string movementNumber,
        Guid companyId,
        AssetMovementPurpose purpose,
        DateTime transactionDate,
        Guid? tenantId = null)
        : base(id)
    {
        MovementNumber = movementNumber;
        CompanyId = companyId;
        Purpose = purpose;
        TransactionDate = transactionDate;
        MovementDate = transactionDate;
        MovementType = purpose.ToString();
        TenantId = tenantId;
    }

    public AssetMovementItem AddItem(
        Guid id,
        Guid assetId,
        string? assetName = null,
        string? sourceLocation = null,
        string? targetLocation = null,
        Guid? fromEmployeeId = null,
        Guid? toEmployeeId = null,
        Guid? sourceLocationId = null,
        Guid? targetLocationId = null)
    {
        var item = new AssetMovementItem(
            id,
            Id,
            assetId,
            assetName,
            sourceLocation,
            targetLocation,
            fromEmployeeId,
            toEmployeeId,
            TenantId,
            sourceLocationId,
            targetLocationId);

        Items.Add(item);
        return item;
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
