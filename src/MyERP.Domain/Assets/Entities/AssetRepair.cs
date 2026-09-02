using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Assets.Entities;

/// <summary>
/// Asset Repair — tracks repair costs with optional capitalization.
/// Per gotcha #35: fully depreciated assets CAN be repaired but
/// capitalize_repair_cost and increase_in_asset_life are forced to 0.
/// Maps to ERPNext assets/doctype/asset_repair.
/// </summary>
public class AssetRepair : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public string RepairNumber { get; set; } = null!;
    public Guid CompanyId { get; set; }
    public Guid AssetId { get; set; }

    public string? RepairDescription { get; set; }
    public string? ActionsPerformed { get; set; }
    public string? Downtime { get; set; }
    public DateTime? FailureDate { get; set; }
    public DateTime? CompletionDate { get; set; }

    public Guid? CostCenterId { get; set; }
    public Guid? ProjectId { get; set; }

    /// <summary>Direct repair cost (parts + labor) or invoice total.</summary>
    public decimal RepairCost { get; set; }

    /// <summary>Cost of stock items consumed during repair.</summary>
    public decimal ConsumedItemsCost { get; set; }

    /// <summary>Total repair cost = RepairCost + ConsumedItemsCost.</summary>
    public decimal TotalRepairCost { get; set; }

    /// <summary>When true, total repair cost is added to asset value (increases book value).</summary>
    public bool CapitalizeRepairCost { get; set; }

    /// <summary>Additional months added to useful life due to repair.</summary>
    public int IncreaseInAssetLife { get; set; }

    public AssetRepairStatus Status { get; private set; } = AssetRepairStatus.Pending;

    /// <summary>Linked Stock Entry created for consumed stock items (PR #50793 / commit da7f28a3c3).</summary>
    public Guid? StockEntryId { get; set; }

    public List<AssetRepairConsumedItem> StockItems { get; private set; } = new();
    public List<AssetRepairPurchaseInvoice> Invoices { get; private set; } = new();

    protected AssetRepair() { }

    public AssetRepair(
        Guid id,
        string repairNumber,
        Guid companyId,
        Guid assetId,
        Guid? tenantId = null)
        : base(id)
    {
        RepairNumber = repairNumber;
        CompanyId = companyId;
        AssetId = assetId;
        TenantId = tenantId;
    }

    public AssetRepairConsumedItem AddStockItem(
        Guid id,
        Guid itemId,
        decimal qty,
        decimal valuationRate,
        Guid? warehouseId = null,
        string? itemName = null,
        string? serialAndBatchBundleId = null)
    {
        var item = new AssetRepairConsumedItem(
            id,
            Id,
            itemId,
            qty,
            valuationRate,
            warehouseId,
            itemName,
            serialAndBatchBundleId,
            TenantId);

        StockItems.Add(item);
        CalculateTotals();
        return item;
    }

    public AssetRepairPurchaseInvoice AddInvoice(
        Guid id,
        Guid purchaseInvoiceId,
        decimal repairCost,
        string? purchaseInvoiceNumber = null,
        Guid? expenseAccountId = null)
    {
        var invoice = new AssetRepairPurchaseInvoice(
            id,
            Id,
            purchaseInvoiceId,
            repairCost,
            purchaseInvoiceNumber,
            expenseAccountId,
            TenantId);

        Invoices.Add(invoice);
        CalculateTotals();
        return invoice;
    }

    public void CalculateTotals()
    {
        ConsumedItemsCost = StockItems.Sum(i => i.TotalValue);
        if (Invoices.Count > 0)
        {
            RepairCost = Invoices.Sum(i => i.RepairCost);
        }
        TotalRepairCost = RepairCost + ConsumedItemsCost;
    }

    /// <summary>
    /// Applies fully-depreciated asset rules:
    /// forces CapitalizeRepairCost=false and IncreaseInAssetLife=0.
    /// Per gotcha #35.
    /// </summary>
    public void ApplyFullyDepreciatedRules(bool isFullyDepreciated)
    {
        if (isFullyDepreciated)
        {
            CapitalizeRepairCost = false;
            IncreaseInAssetLife = 0;
        }
    }

    public void SetDowntime()
    {
        if (Status == AssetRepairStatus.Completed && FailureDate.HasValue && CompletionDate.HasValue)
        {
            var hours = (decimal)(CompletionDate.Value - FailureDate.Value).TotalHours;
            Downtime = $"{Math.Round(hours, 1):0.0} Hrs";
        }
        else
        {
            Downtime = null;
        }
    }

    public void Complete()
    {
        if (Status != AssetRepairStatus.Pending)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        CalculateTotals();
        CompletionDate ??= DateTime.UtcNow;
        Status = AssetRepairStatus.Completed;
        SetDowntime();
    }

    public void Cancel()
    {
        if (Status == AssetRepairStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = AssetRepairStatus.Cancelled;
    }
}
