using System;
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

    /// <summary>Default income account for sales.</summary>
    public Guid? DefaultIncomeAccountId { get; set; }

    /// <summary>Default expense account for purchases/COGS.</summary>
    public Guid? DefaultExpenseAccountId { get; set; }

    public bool IsActive { get; set; } = true;

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
}
