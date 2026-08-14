using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Assets.Entities;

public class AssetActivity : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid AssetId { get; set; }
    public AssetActivityType ActivityType { get; set; }
    public string Subject { get; set; } = null!;
    public string? Details { get; set; }
    public DateTime TransactionDate { get; set; }
    public string? ReferenceType { get; set; }
    public string? ReferenceId { get; set; }

    protected AssetActivity() { }

    public AssetActivity(
        Guid id,
        Guid assetId,
        AssetActivityType activityType,
        string subject,
        DateTime transactionDate,
        string? details = null,
        string? referenceType = null,
        string? referenceId = null,
        Guid? tenantId = null)
        : base(id)
    {
        AssetId = assetId;
        ActivityType = activityType;
        Subject = subject;
        TransactionDate = transactionDate;
        Details = details;
        ReferenceType = referenceType;
        ReferenceId = referenceId;
        TenantId = tenantId;
    }
}
