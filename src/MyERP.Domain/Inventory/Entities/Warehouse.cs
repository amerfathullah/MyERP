using System;
using MyERP.Inventory;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Inventory.Entities;

/// <summary>
/// Warehouse / storage location.
/// Maps to ERPNext stock/doctype/warehouse.
/// Supports tree hierarchy via ParentWarehouseId (NestedSet in ERPNext).
/// Per ERPNext: group warehouses cannot hold stock, only leaf warehouses.
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

    /// <summary>
    /// Warehouse type classification. Per ERPNext:
    /// Transit warehouses are used for inter-warehouse transfers (2-step: Source → Transit → Destination).
    /// Per Company.create_default_warehouses: "Goods In Transit" created with type=Transit.
    /// </summary>
    public WarehouseType WarehouseType { get; set; } = WarehouseType.Standard;

    /// <summary>
    /// Default GL account for stock in this warehouse (for perpetual inventory).
    /// Per ERPNext: each warehouse can have its own stock account via WarehouseAccount child table.
    /// Null = fallback to Company.DefaultInventoryAccountId.
    /// </summary>
    public Guid? DefaultAccountId { get; set; }

    /// <summary>
    /// Whether this warehouse is a transit warehouse (convenience computed property).
    /// Per DO-NOT: transit transfers must use 2-step process (Send → Receive).
    /// </summary>
    public bool IsTransitWarehouse => WarehouseType == WarehouseType.Transit;

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
