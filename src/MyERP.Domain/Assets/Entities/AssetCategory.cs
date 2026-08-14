using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Assets.Entities;

public class AssetCategory : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public string CategoryName { get; set; } = null!;
    public bool IsDepreciable { get; set; } = true;
    public bool EnableCwipAccounting { get; set; }
    public bool NonDepreciableCategory { get; set; }

    // Default depreciation settings
    public DepreciationMethod DefaultDepreciationMethod { get; set; }
    public int DefaultUsefulLifeMonths { get; set; }
    public decimal? DefaultDepreciationRate { get; set; }
    public int DefaultFrequencyMonths { get; set; } = 12;

    // Fallback GL Accounts (single-company or legacy)
    public Guid? AssetAccountId { get; set; }
    public Guid? DepreciationAccountId { get; set; }
    public Guid? AccumulatedDepreciationAccountId { get; set; }

    public List<AssetCategoryAccount> Accounts { get; private set; } = new();

    protected AssetCategory() { }

    public AssetCategory(Guid id, string categoryName, Guid? tenantId = null)
        : base(id)
    {
        CategoryName = categoryName;
        TenantId = tenantId;
        DefaultDepreciationMethod = DepreciationMethod.StraightLine;
        DefaultUsefulLifeMonths = 60; // 5 years
        DefaultFrequencyMonths = 12;
        IsDepreciable = true;
    }

    public AssetCategoryAccount AddAccount(
        Guid id,
        Guid companyId,
        Guid fixedAssetAccountId,
        Guid? accumulatedDepreciationAccountId = null,
        Guid? depreciationExpenseAccountId = null,
        Guid? capitalWorkInProgressAccountId = null)
    {
        var existing = Accounts.FirstOrDefault(a => a.CompanyId == companyId);
        if (existing != null)
        {
            existing.FixedAssetAccountId = fixedAssetAccountId;
            existing.AccumulatedDepreciationAccountId = accumulatedDepreciationAccountId;
            existing.DepreciationExpenseAccountId = depreciationExpenseAccountId;
            existing.CapitalWorkInProgressAccountId = capitalWorkInProgressAccountId;
            return existing;
        }

        var account = new AssetCategoryAccount(
            id,
            Id,
            companyId,
            fixedAssetAccountId,
            accumulatedDepreciationAccountId,
            depreciationExpenseAccountId,
            capitalWorkInProgressAccountId,
            TenantId);

        Accounts.Add(account);
        return account;
    }

    public AssetCategoryAccount? GetAccountForCompany(Guid companyId)
    {
        return Accounts.FirstOrDefault(a => a.CompanyId == companyId);
    }
}
