using System;
using System.Collections.Generic;
using System.Linq;
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

    /// <summary>Target warehouse for sub-assemblies; pools child warehouse stock if group warehouse (ERPNext PR #60210).</summary>
    public Guid? SubAssemblyWarehouseId { get; set; }

    /// <summary>If true, automatically reserves stock for work orders generated from this plan.</summary>
    public bool ReserveStock { get; set; }

    public string? Notes { get; set; }

    public List<ProductionPlanItem> PlannedItems { get; private set; } = new();
    public List<ProductionPlanMrItem> MaterialRequirements { get; private set; } = new();

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
        // Per ERPNext PR #59555: Planned Qty must be strictly greater than 0
        if (PlannedItems.Any(i => i.PlannedQty <= 0))
            throw new BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", "PlannedQty");
        Status = ProductionPlanStatus.Submitted;
        AddLocalEvent(new MyERP.Manufacturing.Events.ProductionPlanSubmittedEvent(Id, TenantId));
    }

    public void MarkMaterialRequested()
    {
        if (Status == ProductionPlanStatus.Submitted)
            Status = ProductionPlanStatus.MaterialRequested;
    }

    public void MarkInProgress()
    {
        if (Status is not (ProductionPlanStatus.Submitted or ProductionPlanStatus.MaterialRequested))
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = ProductionPlanStatus.InProgress;
    }

    public void Complete()
    {
        if (Status is not (ProductionPlanStatus.Submitted or ProductionPlanStatus.InProgress or ProductionPlanStatus.MaterialRequested))
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = ProductionPlanStatus.Completed;
        AddLocalEvent(new MyERP.Manufacturing.Events.ProductionPlanCompletedEvent(Id, TenantId));
    }

    public void RevertCompletion(ProductionPlanStatus fallbackStatus = ProductionPlanStatus.InProgress)
    {
        if (Status != ProductionPlanStatus.Completed)
            return;
        Status = fallbackStatus;
        AddLocalEvent(new MyERP.Manufacturing.Events.ProductionPlanCompletionRevertedEvent(Id, TenantId));
    }

    public void Close()
    {
        if (Status is ProductionPlanStatus.Draft or ProductionPlanStatus.Cancelled or ProductionPlanStatus.Closed)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = ProductionPlanStatus.Closed;
        AddLocalEvent(new MyERP.Manufacturing.Events.ProductionPlanClosedEvent(Id, TenantId));
    }

    public void Reopen()
    {
        if (Status != ProductionPlanStatus.Closed)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = MaterialRequirements.Any(m => m.MaterialRequestId.HasValue)
            ? ProductionPlanStatus.MaterialRequested
            : ProductionPlanStatus.Submitted;
        AddLocalEvent(new MyERP.Manufacturing.Events.ProductionPlanReopenedEvent(Id, TenantId));
    }

    /// <summary>
    /// Updates status based on production progress and releases/restores reservations.
    /// Per PR #59454: closed plans stay closed until reopened.
    /// Per PR #59449: plan reservations are released when completed and restored if reverted.
    /// </summary>
    public void UpdateProducedStatus(bool allCompleted, bool hasProduction)
    {
        if (Status is ProductionPlanStatus.Closed or ProductionPlanStatus.Cancelled or ProductionPlanStatus.Draft)
            return;

        if (allCompleted)
        {
            if (Status != ProductionPlanStatus.Completed)
            {
                Complete();
            }
        }
        else
        {
            if (Status == ProductionPlanStatus.Completed)
            {
                var fallback = hasProduction
                    ? ProductionPlanStatus.InProgress
                    : (MaterialRequirements.Any(m => m.MaterialRequestId.HasValue) ? ProductionPlanStatus.MaterialRequested : ProductionPlanStatus.Submitted);
                RevertCompletion(fallback);
            }
            else if (hasProduction)
            {
                if (Status is ProductionPlanStatus.Submitted or ProductionPlanStatus.MaterialRequested)
                {
                    Status = ProductionPlanStatus.InProgress;
                }
            }
        }
    }


    public void Cancel()
    {
        if (Status is ProductionPlanStatus.Cancelled or ProductionPlanStatus.Completed or ProductionPlanStatus.Closed)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = ProductionPlanStatus.Cancelled;
        MaterialRequirements.Clear();
        AddLocalEvent(new MyERP.Manufacturing.Events.ProductionPlanCancelledEvent(Id, TenantId));
    }

    public void ClearPlannedItems()
    {
        if (Status != ProductionPlanStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        PlannedItems.Clear();
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

    /// <summary>
    /// Updates the plan status to <see cref="ProductionPlanStatus.MaterialRequested"/> only when
    /// at least one material requirement row has a positive quantity to request (<c>PlannedQty &gt; 0</c>)
    /// and every such row has been requested in full (<c>RequestedQty &gt;= PlannedQty</c> or <c>MaterialRequestId.HasValue</c>).
    /// Per ERPNext commit e00b16f781 (update_requested_status):
    /// A plan where all raw materials are already in stock (so no row has a positive quantity to request)
    /// must not transition to Material Requested.
    /// </summary>
    public void UpdateRequestedStatus()
    {
        if (Status is ProductionPlanStatus.Cancelled or ProductionPlanStatus.Closed or ProductionPlanStatus.Completed or ProductionPlanStatus.Draft)
        {
            return;
        }

        var rowsToRequest = MaterialRequirements
            .Where(d => d.PlannedQty > 0m)
            .ToList();

        if (rowsToRequest.Count > 0 &&
            rowsToRequest.All(d => d.RequestedQty >= d.PlannedQty || (d.MaterialRequestId.HasValue && d.RequestedQty <= 0m)))
        {
            Status = ProductionPlanStatus.MaterialRequested;
        }
    }

    /// <summary>
    /// Validates before submitting a Production Plan that none of the Sales Order rows it actually plans
    /// (directly on <paramref name="poItems"/> or through combined <paramref name="planReferences"/> with a non-zero
    /// rounded allocated quantity) is closed or belongs to a Closed Sales Order
    /// per ERPNext commits bf675b5480, d6c1294e97, and 0b4b095b8d (<c>validate_closed_sales_order_items</c> in <c>production_plan.py</c>).
    /// </summary>
    public static void ValidateNoClosedPlannedSalesOrderRows(
        IEnumerable<(Guid PlanItemId, Guid? SalesOrderItemId, decimal PlannedQty)>? poItems,
        IEnumerable<(Guid PlanItemReferenceId, Guid SalesOrderItemId, decimal ReferenceQty)>? planReferences,
        IReadOnlyDictionary<Guid, (string SalesOrderNumber, int RowNumber, string ItemCode, bool IsRowClosed, bool IsOrderClosed)>? salesOrderRowsById,
        int referenceQtyPrecision = 4)
    {
        if (salesOrderRowsById == null)
        {
            return;
        }

        var plannedSoItems = new HashSet<Guid>();
        var remainingPlannedByPlanItem = new Dictionary<Guid, decimal>();

        if (poItems != null)
        {
            foreach (var row in poItems)
            {
                if (row.SalesOrderItemId.HasValue && row.SalesOrderItemId.Value != Guid.Empty)
                {
                    plannedSoItems.Add(row.SalesOrderItemId.Value);
                }
                remainingPlannedByPlanItem[row.PlanItemId] = remainingPlannedByPlanItem.GetValueOrDefault(row.PlanItemId, 0m) + row.PlannedQty;
            }
        }

        if (planReferences != null)
        {
            foreach (var r in planReferences)
            {
                var available = remainingPlannedByPlanItem.GetValueOrDefault(r.PlanItemReferenceId, 0m);
                var allocated = Math.Min(available, r.ReferenceQty);
                remainingPlannedByPlanItem[r.PlanItemReferenceId] = Math.Max(0m, available - allocated);

                if (Math.Round(allocated, referenceQtyPrecision, MidpointRounding.AwayFromZero) != 0m)
                {
                    plannedSoItems.Add(r.SalesOrderItemId);
                }
            }
        }

        if (plannedSoItems.Count == 0)
        {
            return;
        }

        var closedRow = plannedSoItems
            .Where(id => salesOrderRowsById.TryGetValue(id, out var info) && (info.IsRowClosed || info.IsOrderClosed))
            .Select(id => salesOrderRowsById[id])
            .OrderBy(info => info.SalesOrderNumber, StringComparer.Ordinal)
            .ThenBy(info => info.RowNumber)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(closedRow.SalesOrderNumber))
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Row #{closedRow.RowNumber} ({closedRow.ItemCode}) of Sales Order {closedRow.SalesOrderNumber} is closed, so it cannot be planned.");
        }
    }
}
