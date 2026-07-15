using System;
using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Assets.Entities;

/// <summary>
/// Asset Capitalization — converts expense/stock items into a fixed asset.
/// Handles CWIP (Capital Work in Progress) to Asset conversion.
/// Maps to ERPNext assets/doctype/asset_capitalization.
/// </summary>
public class AssetCapitalization : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string CapitalizationNumber { get; set; } = null!;
    public DateTime PostingDate { get; set; }

    /// <summary>Target asset receiving the capitalized value.</summary>
    public Guid TargetAssetId { get; set; }

    /// <summary>Target asset name (denormalized).</summary>
    public string? TargetAssetName { get; set; }

    /// <summary>Total value being capitalized (sum of all consumed items/expenses).</summary>
    public decimal TotalCapitalizedAmount { get; set; }

    /// <summary>
    /// Consumed stock items (reduces inventory, adds to asset value).
    /// </summary>
    private readonly List<AssetCapitalizationItem> _stockItems = new();
    public IReadOnlyList<AssetCapitalizationItem> StockItems => _stockItems.AsReadOnly();

    /// <summary>
    /// Consumed service/expense items (reduces expense, adds to asset value).
    /// </summary>
    private readonly List<AssetCapitalizationItem> _serviceItems = new();
    public IReadOnlyList<AssetCapitalizationItem> ServiceItems => _serviceItems.AsReadOnly();

    /// <summary>
    /// Consumed existing assets (derecognition, adds remaining value to target).
    /// </summary>
    private readonly List<AssetCapitalizationAsset> _consumedAssets = new();
    public IReadOnlyList<AssetCapitalizationAsset> ConsumedAssets => _consumedAssets.AsReadOnly();

    public AssetCapitalizationStatus Status { get; private set; } = AssetCapitalizationStatus.Draft;

    protected AssetCapitalization() { }

    public AssetCapitalization(Guid id, Guid companyId, string capitalizationNumber,
        DateTime postingDate, Guid targetAssetId, Guid? tenantId = null) : base(id)
    {
        CompanyId = companyId;
        CapitalizationNumber = capitalizationNumber;
        PostingDate = postingDate;
        TargetAssetId = targetAssetId;
        TenantId = tenantId;
    }

    public void AddStockItem(Guid itemId, string itemName, decimal qty, decimal rate, Guid? warehouseId = null)
    {
        if (Status != AssetCapitalizationStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        _stockItems.Add(new AssetCapitalizationItem
        {
            ItemId = itemId, ItemName = itemName,
            Qty = qty, Rate = rate, Amount = qty * rate,
            WarehouseId = warehouseId
        });
        RecalculateTotal();
    }

    public void AddServiceItem(Guid itemId, string itemName, decimal amount, Guid? expenseAccountId = null)
    {
        if (Status != AssetCapitalizationStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        _serviceItems.Add(new AssetCapitalizationItem
        {
            ItemId = itemId, ItemName = itemName,
            Qty = 1, Rate = amount, Amount = amount,
            ExpenseAccountId = expenseAccountId
        });
        RecalculateTotal();
    }

    public void AddConsumedAsset(Guid assetId, string assetName, decimal valueAfterDepreciation)
    {
        if (Status != AssetCapitalizationStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        _consumedAssets.Add(new AssetCapitalizationAsset
        {
            AssetId = assetId, AssetName = assetName,
            CurrentValue = valueAfterDepreciation
        });
        RecalculateTotal();
    }

    public void Submit()
    {
        if (Status != AssetCapitalizationStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = AssetCapitalizationStatus.Submitted;
    }

    public void Cancel()
    {
        if (Status != AssetCapitalizationStatus.Submitted)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = AssetCapitalizationStatus.Cancelled;
    }

    private void RecalculateTotal()
    {
        TotalCapitalizedAmount = 0;
        foreach (var item in _stockItems) TotalCapitalizedAmount += item.Amount;
        foreach (var item in _serviceItems) TotalCapitalizedAmount += item.Amount;
        foreach (var asset in _consumedAssets) TotalCapitalizedAmount += asset.CurrentValue;
    }
}

public class AssetCapitalizationItem : Volo.Abp.Domain.Entities.Entity<Guid>
{
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = null!;
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? ExpenseAccountId { get; set; }
}

public class AssetCapitalizationAsset : Volo.Abp.Domain.Entities.Entity<Guid>
{
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = null!;
    public decimal CurrentValue { get; set; }
}

public enum AssetCapitalizationStatus
{
    Draft = 0,
    Submitted = 1,
    Cancelled = 2
}
