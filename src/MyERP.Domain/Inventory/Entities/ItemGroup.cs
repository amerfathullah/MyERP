using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Inventory.Entities;

/// <summary>
/// Item Group — hierarchical category for items (e.g., "Raw Material", "Products", "Services").
/// Supports tree structure with group/leaf distinction.
/// Only leaf groups can be assigned to items.
/// </summary>
public class ItemGroup : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string Name { get; set; } = null!;

    /// <summary>Parent group (null = root).</summary>
    public Guid? ParentId { get; set; }

    /// <summary>If true, this is a category group (cannot be assigned to items directly).</summary>
    public bool IsGroup { get; set; }

    /// <summary>Default warehouse for items in this group.</summary>
    public Guid? DefaultWarehouseId { get; set; }

    /// <summary>Default expense account for items in this group.</summary>
    public Guid? DefaultExpenseAccountId { get; set; }

    /// <summary>Default income account for items in this group.</summary>
    public Guid? DefaultIncomeAccountId { get; set; }

    public bool IsActive { get; set; } = true;

    protected ItemGroup() { }

    public ItemGroup(Guid id, string name, bool isGroup = false, Guid? parentId = null, Guid? tenantId = null)
        : base(id)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), 200);
        IsGroup = isGroup;
        ParentId = parentId;
        TenantId = tenantId;
    }
}
