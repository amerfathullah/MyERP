using System;
using System.Collections.Generic;
using MyERP.Inventory;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Inventory.Entities;

/// <summary>
/// Item (Product/Service) master data.
/// Maps to ERPNext stock/doctype/item.
/// </summary>
public class Item : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string ItemCode { get; private set; } = null!;
    public string ItemName { get; private set; } = null!;
    public string? Barcode { get; set; }
    public string? Description { get; set; }

    public ItemType ItemType { get; set; }
    public string? ItemGroup { get; set; }

    /// <summary>Reference to ItemGroup entity (structured hierarchy).</summary>
    public Guid? ItemGroupId { get; set; }

    public string? Brand { get; set; }

    /// <summary>Default unit of measure (e.g., "Unit", "Kg", "Box").</summary>
    public string Uom { get; set; } = "Unit";

    public ValuationMethod ValuationMethod { get; set; } = ValuationMethod.FIFO;

    /// <summary>Standard selling price (default, can be overridden by price lists).</summary>
    public decimal? StandardSellingPrice { get; set; }

    /// <summary>Standard buying price.</summary>
    public decimal? StandardBuyingPrice { get; set; }

    /// <summary>Tax category for SST calculation.</summary>
    public Guid? TaxCategoryId { get; set; }

    /// <summary>Track stock for this item (false for services).</summary>
    public bool MaintainStock { get; set; } = true;

    /// <summary>Item requires serial number tracking (per-unit identification).</summary>
    public bool HasSerialNo { get; set; }

    /// <summary>Item requires batch/lot number tracking.</summary>
    public bool HasBatchNo { get; set; }

    /// <summary>Allow negative stock for this specific item (overrides global setting).</summary>
    public bool AllowNegativeStock { get; set; }

    /// <summary>Default income account for sales.</summary>
    public Guid? DefaultIncomeAccountId { get; set; }

    /// <summary>Default expense account for purchases/COGS.</summary>
    public Guid? DefaultExpenseAccountId { get; set; }

    public bool IsActive { get; set; } = true;

    // Reorder settings
    /// <summary>Minimum stock level that triggers reorder alert/MR creation.</summary>
    public decimal ReorderLevel { get; set; }

    /// <summary>Quantity to order when reorder is triggered.</summary>
    public decimal ReorderQty { get; set; }

    /// <summary>Safety stock buffer (kept above reorder level).</summary>
    public decimal SafetyStock { get; set; }

    /// <summary>Default warehouse for reorder (used in auto-MR creation).</summary>
    public Guid? DefaultWarehouseId { get; set; }

    /// <summary>
    /// Type of Material Request to create when auto-reorder triggers.
    /// Per ERPNext: Purchase (buy from supplier), Transfer (move from another warehouse),
    /// Manufacture (create work order to produce). Default: Purchase.
    /// </summary>
    public MyERP.Purchasing.MaterialRequestType DefaultMaterialRequestType { get; set; }
        = MyERP.Purchasing.MaterialRequestType.Purchase;

    /// <summary>Minimum order quantity for purchasing (hard error if PO qty below this).</summary>
    public decimal MinOrderQty { get; set; }

    /// <summary>Require submitted+accepted Quality Inspection before Purchase Receipt can be submitted.</summary>
    public bool InspectionRequiredBeforePurchase { get; set; }

    /// <summary>Require submitted+accepted Quality Inspection before Delivery Note can be submitted.</summary>
    public bool InspectionRequiredBeforeDelivery { get; set; }

    /// <summary>
    /// When true, this item is restricted to specific companies (per PR #57258/#57352).
    /// Transactions in companies not in the AllowedCompanies list will be blocked.
    /// </summary>
    public bool RestrictToCompanies { get; set; }

    // Variant system
    /// <summary>True if this is a template item that has variants (cannot be used directly in transactions).</summary>
    public bool HasVariants { get; set; }

    /// <summary>For variants: the template item this was created from.</summary>
    public Guid? VariantOfId { get; set; }

    /// <summary>Variant attribute values (only populated for variant items).</summary>
    public ICollection<ItemVariantAttribute> VariantAttributes { get; private set; }
        = new List<ItemVariantAttribute>();

    protected Item() { }

    public Item(Guid id, Guid companyId, string itemCode, string itemName, ItemType itemType, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        SetItemCode(itemCode);
        SetItemName(itemName);
        ItemType = itemType;
        MaintainStock = itemType == ItemType.Goods;
        TenantId = tenantId;
    }

    public void SetItemCode(string itemCode)
    {
        ItemCode = Check.NotNullOrWhiteSpace(itemCode, nameof(itemCode), ItemConsts.MaxCodeLength);
    }

    public void SetItemName(string itemName)
    {
        ItemName = Check.NotNullOrWhiteSpace(itemName, nameof(itemName), ItemConsts.MaxNameLength);
    }

    /// <summary>
    /// Safely sets the valuation method with SLE-existence guard.
    /// Per DO-NOT: "Allow valuation_method change from Moving Average → FIFO after SLE exists"
    /// Per DO-NOT: "Switch item to/from Standard Cost valuation method after SLE exists (always blocked both directions)"
    /// Exception: FIFO → Moving Average is permitted.
    /// </summary>
    public void SetValuationMethod(ValuationMethod newMethod, bool hasStockLedgerEntries)
    {
        if (newMethod == ValuationMethod) return; // No change

        if (hasStockLedgerEntries)
        {
            // Standard Cost: blocked in both directions when SLE exists
            if (ValuationMethod == ValuationMethod.StandardCost || newMethod == ValuationMethod.StandardCost)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValuationMethodChangeLocked)
                    .WithData("item", ItemCode)
                    .WithData("currentMethod", ValuationMethod.ToString())
                    .WithData("newMethod", newMethod.ToString());
            }

            // MA → FIFO: blocked after SLE exists
            if (ValuationMethod == ValuationMethod.WeightedAverage && newMethod == ValuationMethod.FIFO)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValuationMethodChangeLocked)
                    .WithData("item", ItemCode)
                    .WithData("currentMethod", ValuationMethod.ToString())
                    .WithData("newMethod", newMethod.ToString());
            }

            // FIFO → MA: explicitly allowed (only permitted direction when SLE exists)
        }

        ValuationMethod = newMethod;
    }
}
