using System;
using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Manufacturing.Entities;

/// <summary>
/// Production Plan — MRP planning document that generates Work Orders and Material Requests.
/// </summary>
public class ProductionPlan : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string PlanNumber { get; set; } = null!;
    public ProductionPlanStatus Status { get; private set; }

    public Guid CompanyId { get; set; }
    public DateTime PostingDate { get; set; }

    /// <summary>If true, groups items by (item_code, warehouse, planned_start_date).</summary>
    public bool CombineItems { get; set; }

    /// <summary>If true, deducts existing ordered qty from required qty.</summary>
    public bool IgnoreExistingOrderedQty { get; set; }

    /// <summary>If true, applies MIN_ORDER_QTY rounding to material requirements.</summary>
    public bool ConsiderMinimumOrderQty { get; set; }

    /// <summary>If true, includes item safety stock in material requirement calculation.</summary>
    public bool IncludeSafetyStock { get; set; }

    /// <summary>If true, skips WO creation for sub-assemblies with sufficient stock.</summary>
    public bool SkipAvailableSubAssemblyItem { get; set; }

    /// <summary>Optional group warehouse to widen material availability scope.</summary>
    public Guid? RawMaterialGroupWarehouseId { get; set; }

    /// <summary>Target warehouse for material receipt.</summary>
    public Guid? ForWarehouseId { get; set; }

    public string? Notes { get; set; }

    public List<ProductionPlanItem> PlannedItems { get; set; } = new();
    public List<ProductionPlanMrItem> MaterialRequirements { get; set; } = new();

    protected ProductionPlan() { }

    public ProductionPlan(Guid id, Guid companyId, string planNumber, DateTime postingDate, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        PlanNumber = planNumber;
        PostingDate = postingDate;
        Status = ProductionPlanStatus.Draft;
        TenantId = tenantId;
    }

    public void Submit()
    {
        if (Status != ProductionPlanStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        if (PlannedItems.Count == 0)
            throw new BusinessException(MyERPDomainErrorCodes.ProductionPlanHasNoItems);
        Status = ProductionPlanStatus.Submitted;
    }

    public void MarkInProgress()
    {
        if (Status != ProductionPlanStatus.Submitted)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = ProductionPlanStatus.InProgress;
    }

    public void Complete()
    {
        if (Status is not (ProductionPlanStatus.Submitted or ProductionPlanStatus.InProgress))
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = ProductionPlanStatus.Completed;
    }

    public void Cancel()
    {
        if (Status is ProductionPlanStatus.Cancelled or ProductionPlanStatus.Completed)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = ProductionPlanStatus.Cancelled;
    }

    public void AddPlannedItem(ProductionPlanItem item)
    {
        if (Status != ProductionPlanStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        PlannedItems.Add(item);
    }

    public void AddMaterialRequirement(ProductionPlanMrItem item)
    {
        MaterialRequirements.Add(item);
    }
}
