using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Assets.Entities;

public class AssetCategory : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public string CategoryName { get; set; } = null!;
    public bool IsDepreciable { get; set; } = true;

    // Default depreciation settings
    public DepreciationMethod DefaultDepreciationMethod { get; set; }
    public int DefaultUsefulLifeMonths { get; set; }
    public decimal? DefaultDepreciationRate { get; set; }

    // GL Accounts
    public Guid? AssetAccountId { get; set; }
    public Guid? DepreciationAccountId { get; set; }
    public Guid? AccumulatedDepreciationAccountId { get; set; }

    protected AssetCategory() { }

    public AssetCategory(Guid id, string categoryName, Guid? tenantId = null)
        : base(id)
    {
        CategoryName = categoryName;
        TenantId = tenantId;
        DefaultDepreciationMethod = DepreciationMethod.StraightLine;
        DefaultUsefulLifeMonths = 60; // 5 years
    }
}
