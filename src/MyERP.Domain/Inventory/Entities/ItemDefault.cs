using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Inventory.Entities;

/// <summary>
/// Per-company default settings for an Item.
/// Maps to ERPNext Item Default child table (one row per company).
/// Allows different default warehouses, accounts, and settings per company.
/// </summary>
public class ItemDefault : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid ItemId { get; set; }
    public Guid CompanyId { get; set; }

    /// <summary>Default warehouse for stock receipts.</summary>
    public Guid? DefaultWarehouseId { get; set; }

    /// <summary>Default income account for sales (overrides Item.DefaultIncomeAccountId).</summary>
    public Guid? IncomeAccountId { get; set; }

    /// <summary>Default expense account for purchases/COGS (overrides Item.DefaultExpenseAccountId).</summary>
    public Guid? ExpenseAccountId { get; set; }

    /// <summary>Default buying cost center.</summary>
    public Guid? BuyingCostCenterId { get; set; }

    /// <summary>Default selling cost center.</summary>
    public Guid? SellingCostCenterId { get; set; }

    /// <summary>Default supplier for this item at this company.</summary>
    public Guid? DefaultSupplierId { get; set; }

    /// <summary>Default price list for this item at this company.</summary>
    public Guid? DefaultPriceListId { get; set; }

    /// <summary>Default discount percentage for this item at this company.</summary>
    public decimal DefaultDiscountPercentage { get; set; }

    protected ItemDefault() { }

    public ItemDefault(Guid id, Guid itemId, Guid companyId, Guid? tenantId = null) : base(id)
    {
        ItemId = itemId;
        CompanyId = companyId;
        TenantId = tenantId;
    }
}
