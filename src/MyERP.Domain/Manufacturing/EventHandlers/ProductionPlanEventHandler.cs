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
}
