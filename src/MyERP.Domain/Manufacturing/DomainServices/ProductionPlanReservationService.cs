using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Manufacturing.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Manufacturing.DomainServices;

/// <summary>
/// Domain service for Production Plan stock reservations and offsets across warehouses.
/// Per ERPNext PR #59429 / commit a710111db9: offset plan reservations across warehouses.
/// </summary>
public class ProductionPlanReservationService : DomainService
{
    private readonly IRepository<ProductionPlan, Guid> _planRepository;
    private readonly IRepository<WorkOrder, Guid> _workOrderRepository;

    public ProductionPlanReservationService(
        IRepository<ProductionPlan, Guid> planRepository,
        IRepository<WorkOrder, Guid> workOrderRepository)
    {
        _planRepository = planRepository;
        _workOrderRepository = workOrderRepository;
    }

    /// <summary>
    /// Calculates remaining reserved quantity for a warehouse, distributing offsets across warehouses.
    /// Formula matches ERPNext PR #59429:
    /// 1. Subtract direct matching work order demand from each warehouse.
    /// 2. If work order demand exists in other warehouses or exceeds a planned warehouse, distribute
    ///    the unmatched demand proportionally across all warehouses with remaining reservations.
    /// </summary>
    public static decimal CalculateRemainingReservedQty(
        IReadOnlyDictionary<Guid, decimal> planQtyByWarehouse,
        IReadOnlyDictionary<Guid, decimal> workOrderQtyByWarehouse,
        Guid warehouseId)
    {
        var remainingQtyByWarehouse = new Dictionary<Guid, decimal>();
        foreach (var (wh, qty) in planQtyByWarehouse)
        {
            var woQty = workOrderQtyByWarehouse.TryGetValue(wh, out var q) ? q : 0m;
            remainingQtyByWarehouse[wh] = Math.Max(0m, qty - woQty);
        }

        var totalRemainingQty = remainingQtyByWarehouse.Values.Sum();
        if (totalRemainingQty <= 0m)
        {
            return 0m;
        }

        var totalPlanQty = planQtyByWarehouse.Values.Sum();
        var matchedQty = totalPlanQty - totalRemainingQty;
        var totalWoQty = workOrderQtyByWarehouse.Values.Sum();
        var unmatchedQty = Math.Min(Math.Max(0m, totalWoQty - matchedQty), totalRemainingQty);
        var remainingQty = remainingQtyByWarehouse.TryGetValue(warehouseId, out var rem) ? rem : 0m;

        if (remainingQty <= 0m)
        {
            return 0m;
        }

        return Math.Max(0m, remainingQty - (remainingQty * unmatchedQty / totalRemainingQty));
    }

    /// <summary>
    /// Gets the effective reserved quantity for a raw material item in a specific warehouse
    /// across all open production plans, accounting for work order offsets.
    /// </summary>
    public async Task<decimal> GetReservedQtyForProductionPlanAsync(Guid itemId, Guid warehouseId)
    {
        var planQuery = await _planRepository.GetQueryableAsync();
        var openPlans = planQuery
            .Where(p => p.Status != ProductionPlanStatus.Draft
                && p.Status != ProductionPlanStatus.Closed
                && p.Status != ProductionPlanStatus.Completed)
            .ToList();

        if (openPlans.Count == 0)
        {
            return 0m;
        }

        var planIds = openPlans.Select(p => p.Id).ToList();
        var woQuery = await _workOrderRepository.GetQueryableAsync();
        var openWos = woQuery
            .Where(w => w.ProductionPlanId.HasValue
                && planIds.Contains(w.ProductionPlanId.Value)
                && w.Status != WorkOrderStatus.Draft
                && w.Status != WorkOrderStatus.Cancelled
                && w.Status != WorkOrderStatus.Closed
                && w.Status != WorkOrderStatus.Completed)
            .ToList();

        decimal totalReserved = 0m;

        foreach (var plan in openPlans)
        {
            var planQtyByWarehouse = plan.MaterialRequirements
                .Where(mr => mr.ItemId == itemId && mr.WarehouseId.HasValue && mr.RequiredQty > 0)
                .GroupBy(mr => mr.WarehouseId!.Value)
                .ToDictionary(g => g.Key, g => g.Sum(mr => mr.RequiredQty));

            if (planQtyByWarehouse.Count == 0)
            {
                continue;
            }

            var wosForPlan = openWos.Where(w => w.ProductionPlanId == plan.Id).ToList();
            var woQtyByWarehouse = wosForPlan
                .SelectMany(w => w.RequiredItems)
                .Where(ri => ri.ItemId == itemId && ri.SourceWarehouseId.HasValue)
                .GroupBy(ri => ri.SourceWarehouseId!.Value)
                .ToDictionary(g => g.Key, g => g.Sum(ri => ri.RequiredQuantity));

            totalReserved += CalculateRemainingReservedQty(planQtyByWarehouse, woQtyByWarehouse, warehouseId);
        }

        return Math.Round(totalReserved, 4);
    }
}
