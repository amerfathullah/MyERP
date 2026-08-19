using System;
using Volo.Abp.Domain.Entities;

namespace MyERP.Inventory.Entities;

/// <summary>
/// Per-warehouse reorder override for an Item. When present for a warehouse, this row's
/// level/qty/request-type replace the Item's single global ReorderLevel/ReorderQty/DefaultMaterialRequestType
/// for that warehouse. Maps to ERPNext's Item Reorder child table (stock/doctype/item_reorder).
/// </summary>
public class ItemReorder : Entity<Guid>
{
    public Guid ItemId { get; set; }

    /// <summary>The warehouse a Material Request is raised for ("Request for" in ERPNext).</summary>
    public Guid WarehouseId { get; set; }

    /// <summary>
    /// Optional warehouse (often a group warehouse) whose stock is checked instead of WarehouseId's own.
    /// Per ERPNext "Check Availability in Warehouse": null means check WarehouseId itself.
    /// </summary>
    public Guid? WarehouseGroupId { get; set; }

    public decimal WarehouseReorderLevel { get; set; }
    public decimal WarehouseReorderQty { get; set; }

    public MyERP.Purchasing.MaterialRequestType MaterialRequestType { get; set; }

    protected ItemReorder() { }

    public ItemReorder(
        Guid id, Guid itemId, Guid warehouseId,
        decimal warehouseReorderLevel, decimal warehouseReorderQty,
        MyERP.Purchasing.MaterialRequestType materialRequestType,
        Guid? warehouseGroupId = null)
        : base(id)
    {
        ItemId = itemId;
        WarehouseId = warehouseId;
        WarehouseReorderLevel = warehouseReorderLevel;
        WarehouseReorderQty = warehouseReorderQty;
        MaterialRequestType = materialRequestType;
        WarehouseGroupId = warehouseGroupId;
    }
}
