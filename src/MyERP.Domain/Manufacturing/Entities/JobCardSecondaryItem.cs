using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Manufacturing.Entities;

/// <summary>
/// Job Card Secondary Item — records co-products, by-products, and scrap produced during Job Card operations.
/// Maps to ERPNext manufacturing/doctype/job_card_secondary_item.
/// </summary>
public class JobCardSecondaryItem : CreationAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid JobCardId { get; set; }
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Output quantity in stock UOM.</summary>
    public decimal StockQty { get; set; }

    /// <summary>Stock UOM snapshot from item master.</summary>
    public string StockUom { get; set; } = string.Empty;

    /// <summary>Classification: CoProduct, ByProduct, Scrap, or AdditionalFinishedGood.</summary>
    public SecondaryItemType SecondaryItemType { get; set; }

    /// <summary>Link to parent BOM Secondary Item (if scheduled from BOM).</summary>
    public Guid? BomSecondaryItemId { get; set; }

    /// <summary>Row index within Job Card.</summary>
    public int Idx { get; set; }

    protected JobCardSecondaryItem() { }

    public JobCardSecondaryItem(
        Guid id,
        Guid jobCardId,
        Guid itemId,
        string itemName,
        decimal stockQty,
        string stockUom,
        SecondaryItemType secondaryItemType,
        string? description = null,
        Guid? bomSecondaryItemId = null,
        int idx = 0,
        Guid? tenantId = null)
        : base(id)
    {
        JobCardId = jobCardId;
        ItemId = itemId;
        ItemName = itemName;
        StockQty = stockQty;
        StockUom = stockUom;
        SecondaryItemType = secondaryItemType;
        Description = description;
        BomSecondaryItemId = bomSecondaryItemId;
        Idx = idx;
        TenantId = tenantId;
    }
}
