using System;
using System.Threading.Tasks;
using MyERP.Inventory.DomainServices;
using MyERP.Manufacturing.Entities;
using MyERP.Manufacturing.Events;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus;
using Volo.Abp.Uow;

namespace MyERP.Manufacturing.EventHandlers;

public class ProductionPlanEventHandler :
    ILocalEventHandler<ProductionPlanSubmittedEvent>,
    ILocalEventHandler<ProductionPlanCancelledEvent>,
    ILocalEventHandler<ProductionPlanClosedEvent>,
    ILocalEventHandler<ProductionPlanReopenedEvent>,
    ILocalEventHandler<ProductionPlanCompletedEvent>,
    ILocalEventHandler<ProductionPlanCompletionRevertedEvent>,
    ITransientDependency
{
    private readonly IRepository<ProductionPlan, Guid> _planRepository;
    private readonly BinService _binService;

    public ProductionPlanEventHandler(
        IRepository<ProductionPlan, Guid> planRepository,
        BinService binService)
    {
        _planRepository = planRepository;
        _binService = binService;
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(ProductionPlanSubmittedEvent eventData)
    {
        var plan = await _planRepository.GetAsync(eventData.ProductionPlanId, includeDetails: true);

        // Reserve BOM quantities for material requirements
        foreach (var mr in plan.MaterialRequirements)
        {
            if (mr.WarehouseId.HasValue && mr.RequiredQty > 0)
            {
                // ERPNext PR #57399: Use required_bom_qty (RequiredQty) instead of quantity
                await _binService.UpdateReservedQtyForProductionPlanAsync(
                    mr.ItemId,
                    mr.WarehouseId.Value,
                    mr.RequiredQty,
                    eventData.TenantId);
            }
        }
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(ProductionPlanCancelledEvent eventData)
    {
        var plan = await _planRepository.GetAsync(eventData.ProductionPlanId, includeDetails: true);

        // Release BOM quantities for material requirements
        foreach (var mr in plan.MaterialRequirements)
        {
            if (mr.WarehouseId.HasValue && mr.RequiredQty > 0)
            {
                // ERPNext PR #57399: Use required_bom_qty (RequiredQty) instead of quantity
                await _binService.UpdateReservedQtyForProductionPlanAsync(
                    mr.ItemId,
                    mr.WarehouseId.Value,
                    -mr.RequiredQty,
                    eventData.TenantId);
            }
        }
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(ProductionPlanClosedEvent eventData)
    {
        var plan = await _planRepository.GetAsync(eventData.ProductionPlanId, includeDetails: true);

        // Release BOM quantities on close (PR #59449 / PR #59454)
        foreach (var mr in plan.MaterialRequirements)
        {
            if (mr.WarehouseId.HasValue && mr.RequiredQty > 0)
            {
                await _binService.UpdateReservedQtyForProductionPlanAsync(
                    mr.ItemId,
                    mr.WarehouseId.Value,
                    -mr.RequiredQty,
                    eventData.TenantId);
            }
        }
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(ProductionPlanReopenedEvent eventData)
    {
        var plan = await _planRepository.GetAsync(eventData.ProductionPlanId, includeDetails: true);

        // Restore BOM quantities on reopen
        foreach (var mr in plan.MaterialRequirements)
        {
            if (mr.WarehouseId.HasValue && mr.RequiredQty > 0)
            {
                await _binService.UpdateReservedQtyForProductionPlanAsync(
                    mr.ItemId,
                    mr.WarehouseId.Value,
                    mr.RequiredQty,
                    eventData.TenantId);
            }
        }
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(ProductionPlanCompletedEvent eventData)
    {
        var plan = await _planRepository.GetAsync(eventData.ProductionPlanId, includeDetails: true);

        // Release BOM quantities when plan completes (ERPNext PR #59449)
        foreach (var mr in plan.MaterialRequirements)
        {
            if (mr.WarehouseId.HasValue && mr.RequiredQty > 0)
            {
                await _binService.UpdateReservedQtyForProductionPlanAsync(
                    mr.ItemId,
                    mr.WarehouseId.Value,
                    -mr.RequiredQty,
                    eventData.TenantId);
            }
        }
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(ProductionPlanCompletionRevertedEvent eventData)
    {
        var plan = await _planRepository.GetAsync(eventData.ProductionPlanId, includeDetails: true);

        // Restore BOM quantities if completion is reverted
        foreach (var mr in plan.MaterialRequirements)
        {
            if (mr.WarehouseId.HasValue && mr.RequiredQty > 0)
            {
                await _binService.UpdateReservedQtyForProductionPlanAsync(
                    mr.ItemId,
                    mr.WarehouseId.Value,
                    mr.RequiredQty,
                    eventData.TenantId);
            }
        }
    }
}
