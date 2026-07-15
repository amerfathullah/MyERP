using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Inventory.Entities;

/// <summary>
/// Putaway Rule — defines warehouse allocation strategy for incoming stock.
/// Allocates incoming quantities to warehouses based on priority and capacity.
/// Maps to ERPNext stock/doctype/putaway_rule.
/// Per DO-NOT: "Skip putaway rule capacity check on incoming stock"
/// </summary>
public class PutawayRule : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    /// <summary>Item this rule applies to (null = catch-all for item group).</summary>
    public Guid? ItemId { get; set; }

    /// <summary>Item group this rule applies to (used when ItemId is null).</summary>
    public Guid? ItemGroupId { get; set; }

    /// <summary>Target warehouse for stock allocation.</summary>
    public Guid WarehouseId { get; set; }

    /// <summary>Maximum stock capacity of this warehouse (0 = unlimited).</summary>
    public decimal StockCapacity { get; set; }

    /// <summary>
    /// Priority for allocation (lower = higher priority).
    /// When multiple rules match, sorted by priority ascending.
    /// </summary>
    public int Priority { get; set; } = 1;

    /// <summary>UOM for capacity (must match item's stock UOM).</summary>
    public string? Uom { get; set; }

    public bool IsEnabled { get; set; } = true;

    protected PutawayRule() { }

    public PutawayRule(Guid id, Guid companyId, Guid warehouseId, Guid? tenantId = null) : base(id)
    {
        CompanyId = companyId;
        WarehouseId = warehouseId;
        TenantId = tenantId;
    }

    /// <summary>
    /// Calculates available capacity = StockCapacity - currentBalance.
    /// Returns decimal.MaxValue when StockCapacity is 0 (unlimited).
    /// Per DO-NOT: "allocate qty beyond stock_capacity - current_balance_qty per warehouse"
    /// </summary>
    public decimal GetAvailableCapacity(decimal currentBalanceQty)
    {
        if (StockCapacity <= 0) return decimal.MaxValue;
        return Math.Max(0, StockCapacity - currentBalanceQty);
    }
}
