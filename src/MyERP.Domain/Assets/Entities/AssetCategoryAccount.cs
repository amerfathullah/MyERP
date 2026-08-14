using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Assets.Entities;

public class AssetCategoryAccount : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid AssetCategoryId { get; set; }
    public Guid CompanyId { get; set; }

    public Guid FixedAssetAccountId { get; set; }
    public Guid? AccumulatedDepreciationAccountId { get; set; }
    public Guid? DepreciationExpenseAccountId { get; set; }
    public Guid? CapitalWorkInProgressAccountId { get; set; }

    protected AssetCategoryAccount() { }

    public AssetCategoryAccount(
        Guid id,
        Guid assetCategoryId,
        Guid companyId,
        Guid fixedAssetAccountId,
        Guid? accumulatedDepreciationAccountId = null,
        Guid? depreciationExpenseAccountId = null,
        Guid? capitalWorkInProgressAccountId = null,
        Guid? tenantId = null)
        : base(id)
    {
        AssetCategoryId = assetCategoryId;
        CompanyId = companyId;
        FixedAssetAccountId = fixedAssetAccountId;
        AccumulatedDepreciationAccountId = accumulatedDepreciationAccountId;
        DepreciationExpenseAccountId = depreciationExpenseAccountId;
        CapitalWorkInProgressAccountId = capitalWorkInProgressAccountId;
        TenantId = tenantId;
    }
}
