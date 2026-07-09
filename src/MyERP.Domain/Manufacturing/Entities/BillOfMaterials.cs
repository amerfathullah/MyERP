using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Manufacturing.Entities;

/// <summary>
/// Bill of Materials — defines raw materials needed to manufacture a finished item.
/// </summary>
public class BillOfMaterials : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string BomNumber { get; set; } = null!;
    public Guid ItemId { get; set; }
    public decimal Quantity { get; set; } = 1;
    public string? Uom { get; set; }

    public Guid CompanyId { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; }

    public Guid? SourceWarehouseId { get; set; }
    public Guid? TargetWarehouseId { get; set; }

    public decimal TotalMaterialCost { get; set; }
    public decimal OperatingCost { get; set; }
    public decimal TotalCost => TotalMaterialCost + OperatingCost;

    public string? Notes { get; set; }

    public List<BomItem> Items { get; set; } = new();

    protected BillOfMaterials() { }

    public BillOfMaterials(Guid id, Guid companyId, string bomNumber, Guid itemId, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        BomNumber = bomNumber;
        ItemId = itemId;
        TenantId = tenantId;
    }

    public void RecalculateCost()
    {
        TotalMaterialCost = 0;
        foreach (var item in Items)
        {
            item.Recalculate();
            TotalMaterialCost += item.Amount;
        }
    }
}
