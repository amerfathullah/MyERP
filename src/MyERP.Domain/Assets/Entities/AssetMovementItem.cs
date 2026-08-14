using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Assets.Entities;

public class AssetMovementItem : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid AssetMovementId { get; set; }
    public Guid AssetId { get; set; }
    public string? AssetName { get; set; }

    public string? SourceLocation { get; set; }
    public string? TargetLocation { get; set; }
    public Guid? FromEmployeeId { get; set; }
    public Guid? ToEmployeeId { get; set; }

    protected AssetMovementItem() { }

    public AssetMovementItem(
        Guid id,
        Guid assetMovementId,
        Guid assetId,
        string? assetName = null,
        string? sourceLocation = null,
        string? targetLocation = null,
        Guid? fromEmployeeId = null,
        Guid? toEmployeeId = null,
        Guid? tenantId = null)
        : base(id)
    {
        AssetMovementId = assetMovementId;
        AssetId = assetId;
        AssetName = assetName;
        SourceLocation = sourceLocation;
        TargetLocation = targetLocation;
        FromEmployeeId = fromEmployeeId;
        ToEmployeeId = toEmployeeId;
        TenantId = tenantId;
    }
}
