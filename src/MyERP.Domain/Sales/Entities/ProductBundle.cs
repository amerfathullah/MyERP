using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Core;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Sales.Entities;

/// <summary>
/// Product Bundle — a virtual item that decomposes into component items on transaction.
/// Used for kitting/packaging. Only the bundle item appears in the transaction;
/// components are tracked via Packed Items (packing list).
/// Internal transfers hard-block packed items.
/// Maps to ERPNext selling/doctype/product_bundle.
/// </summary>
public class ProductBundle : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    /// <summary>Parent item that represents the bundle.</summary>
    public Guid ItemId { get; set; }
    public string? ItemName { get; set; }

    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Whether this bundle is disabled (cannot be used in transactions, PR #55791).</summary>
    public bool IsDisabled { get; set; }

    private readonly List<ProductBundleItem> _items = new();
    public IReadOnlyList<ProductBundleItem> Items => _items.AsReadOnly();

    protected ProductBundle() { }

    public ProductBundle(Guid id, Guid itemId, Guid? tenantId = null) : base(id)
    {
        ItemId = itemId;
        TenantId = tenantId;
    }

    public void AddItem(Guid componentItemId, decimal qty, string? itemName = null, string? uom = null)
    {
        // Child item quantity must be positive (ERPNext PR #49163 / commit 711076d02d)
        if (qty <= 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", "Qty")
                .WithData("itemId", componentItemId);
        }

        _items.Add(new ProductBundleItem(Guid.NewGuid(), Id, componentItemId, qty, itemName, uom));
    }

    public void Activate()
    {
        IsActive = true;
    }

    /// <summary>Deactivate this version. Must deactivate old when activating new version.</summary>
    public void Deactivate()
    {
        IsActive = false;
    }

    public void Disable()
    {
        IsDisabled = true;
    }

    public void Enable()
    {
        IsDisabled = false;
    }

    /// <summary>
    /// Calculate bundle valuation from component valuation rates (for gross profit reporting).
    /// valuation = SUM(component_valuation_rate × component_qty)
    /// </summary>
    public decimal CalculateValuation(Func<Guid, decimal> getComponentRate)
    {
        return _items.Sum(i => getComponentRate(i.ComponentItemId) * i.Qty);
    }
}

public class ProductBundleItem : FullAuditedEntity<Guid>
{
    public Guid ProductBundleId { get; set; }
    public Guid ComponentItemId { get; set; }
    public string? ItemName { get; set; }
    public string? Uom { get; set; }
    public decimal Qty { get; set; }

    protected ProductBundleItem() { }

    public ProductBundleItem(Guid id, Guid bundleId, Guid componentItemId,
        decimal qty, string? itemName, string? uom) : base(id)
    {
        // Child item quantity must be positive (ERPNext PR #49163 / commit 711076d02d)
        if (qty <= 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", "Qty")
                .WithData("itemId", componentItemId);
        }

        ProductBundleId = bundleId;
        ComponentItemId = componentItemId;
        Qty = qty;
        ItemName = itemName;
        Uom = uom;
    }
}
