using System;
using MyERP.Inventory;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Inventory.Entities;

/// <summary>
/// Warehouse / storage location.
/// Maps to ERPNext stock/doctype/warehouse.
/// </summary>
public class Warehouse : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid? BranchId { get; set; }

    public string Name { get; private set; } = null!;
    public string? WarehouseCode { get; set; }

    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }

    /// <summary>Parent warehouse for tree hierarchy.</summary>
    public Guid? ParentWarehouseId { get; set; }

    public bool IsGroup { get; set; }
    public bool IsActive { get; set; } = true;

    protected Warehouse() { }

    public Warehouse(Guid id, Guid companyId, string name, Guid? tenantId = null) : base(id)
    {
        CompanyId = companyId;
        SetName(name);
        TenantId = tenantId;
    }

    public void SetName(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), WarehouseConsts.MaxNameLength);
    }
}
