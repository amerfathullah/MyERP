using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Inventory.Entities;

/// <summary>
/// Item Brand master. Maps to ERPNext setup/doctype/brand. Referenced by Item.Brand and
/// PricingRule/PromotionalScheme brand filters via its unique Name (ERPNext autonames Brand by name).
/// </summary>
public class Brand : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string Name { get; private set; } = null!;
    public string? Description { get; set; }

    /// <summary>Default warehouse for items of this brand.</summary>
    public Guid? DefaultWarehouseId { get; set; }

    /// <summary>Default income account for items of this brand.</summary>
    public Guid? DefaultIncomeAccountId { get; set; }

    /// <summary>Default expense account for items of this brand.</summary>
    public Guid? DefaultExpenseAccountId { get; set; }

    public bool IsActive { get; set; } = true;

    protected Brand() { }

    public Brand(Guid id, string name, Guid? tenantId = null) : base(id)
    {
        Rename(name);
        TenantId = tenantId;
    }

    public void Rename(string name)
        => Name = Check.NotNullOrWhiteSpace(name, nameof(name), 200);
}
