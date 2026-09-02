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
    public bool IsFixedAsset => ItemType == ItemType.FixedAsset;
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

    /// <summary>
    /// When true, this item is eligible for sales commission calculations.
    /// Per ERPNext: grant_commission field on Item master (gotcha #6156). Default: true.
    /// </summary>
    public bool GrantCommission { get; set; } = true;

    /// <summary>
    /// Maximum discount percentage allowed for this item in sales transactions.
    /// Per ERPNext: max_discount field on Item master (gotcha #3222). Null = no limit.
    /// </summary>
    public decimal? MaxDiscount { get; set; }

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

    // UOM settings per ERPNext Item master
    /// <summary>Sales UOM (defaults to stock UOM if null). Per ERPNext: sales_uom field.</summary>
    public string? SalesUom { get; set; }

    /// <summary>Purchase UOM (defaults to stock UOM if null). Per ERPNext: purchase_uom field.</summary>
    public string? PurchaseUom { get; set; }

    /// <summary>Weight per unit in weight UOM. Per ERPNext: weight_per_unit field.</summary>
    public decimal WeightPerUnit { get; set; }

    /// <summary>Weight UOM (e.g., "Kg", "Gram"). Per ERPNext: weight_uom field.</summary>
    public string? WeightUom { get; set; }

    /// <summary>Default BOM for manufacturing. Per ERPNext: default_bom field.</summary>
    public Guid? DefaultBomId { get; set; }

    /// <summary>Lead time in days for procurement planning. Per ERPNext: lead_time_days.</summary>
    public int LeadTimeDays { get; set; }

    /// <summary>Customs Tariff Number (HS Code) for export/import declaration.</summary>
    public Guid? CustomsTariffNumberId { get; set; }

    /// <summary>Allow alternative/substitute item in transactions.</summary>
    public bool AllowAlternativeItem { get; set; }

    /// <summary>Default item manufacturer.</summary>
    public Guid? DefaultManufacturerId { get; set; }

    /// <summary>Default manufacturer part number.</summary>
    public string? DefaultManufacturerPartNo { get; set; }

    // Variant system
    /// <summary>True if this is a template item that has variants (cannot be used directly in transactions).</summary>
    public bool HasVariants { get; set; }

    /// <summary>For variants: the template item this was created from.</summary>
    public Guid? VariantOfId { get; set; }

    /// <summary>Variant attribute values (only populated for variant items).</summary>
    public ICollection<ItemVariantAttribute> VariantAttributes { get; private set; }
        = new List<ItemVariantAttribute>();

    /// <summary>
    /// Additional barcodes for this item (EAN/UPC/case codes, etc).
    /// The legacy single Barcode field above remains as the primary/default scan code;
    /// this collection supports items with multiple package sizes or barcode standards.
    /// Per ERPNext stock/doctype/item_barcode (child table).
    /// </summary>
    public ICollection<ItemBarcode> Barcodes { get; private set; }
        = new List<ItemBarcode>();

    /// <summary>Preferred/approved suppliers for this item. Per ERPNext stock/doctype/item_supplier (child table).</summary>
    public ICollection<ItemSupplier> Suppliers { get; private set; }
        = new List<ItemSupplier>();

    /// <summary>Customer-specific item codes for this item. Per ERPNext stock/doctype/item_customer_detail (child table).</summary>
    public ICollection<ItemCustomerDetail> CustomerDetails { get; private set; }
        = new List<ItemCustomerDetail>();

    /// <summary>
    /// Per-warehouse reorder level/qty overrides. When a warehouse has a row here, it replaces
    /// the global ReorderLevel/ReorderQty/DefaultMaterialRequestType above for that warehouse.
    /// Per ERPNext stock/doctype/item_reorder (child table).
    /// </summary>
    public ICollection<ItemReorder> Reorders { get; private set; }
        = new List<ItemReorder>();

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
