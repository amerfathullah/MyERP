using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.DomainServices;
using MyERP.Manufacturing.DomainServices;
using MyERP.Manufacturing.Entities;
using MyERP.Permissions;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Manufacturing;

[Authorize(MyERPPermissions.ProductionPlans.Default)]
public class ProductionPlanAppService : ApplicationService, IProductionPlanAppService
{
    private readonly IRepository<ProductionPlan, Guid> _planRepository;
    private readonly IRepository<BillOfMaterials, Guid> _bomRepository;
    private readonly IRepository<WorkOrder, Guid> _workOrderRepository;
    private readonly IRepository<MaterialRequest, Guid> _materialRequestRepository;
    private readonly IDocumentNumberGenerator _numberGenerator;
    private readonly BomValidationService _bomValidationService;

    public ProductionPlanAppService(
        IRepository<ProductionPlan, Guid> planRepository,
        IRepository<BillOfMaterials, Guid> bomRepository,
        IRepository<WorkOrder, Guid> workOrderRepository,
        IRepository<MaterialRequest, Guid> materialRequestRepository,
        IDocumentNumberGenerator numberGenerator,
        BomValidationService bomValidationService)
    {
        _planRepository = planRepository;
        _bomRepository = bomRepository;
        _workOrderRepository = workOrderRepository;
        _materialRequestRepository = materialRequestRepository;
        _numberGenerator = numberGenerator;
        _bomValidationService = bomValidationService;
    }

    public async Task<ProductionPlanDto> GetAsync(Guid id)
    {
        var plan = await _planRepository.GetAsync(id, includeDetails: true);
        return ObjectMapper.Map<ProductionPlan, ProductionPlanDto>(plan);
    }

    public async Task<PagedResultDto<ProductionPlanDto>> GetListAsync(GetProductionPlanListDto input)
    {
        var query = await _planRepository.GetQueryableAsync();

        if (input.Status.HasValue)
            query = query.Where(p => p.Status == input.Status.Value);
        if (input.CompanyId.HasValue)
            query = query.Where(p => p.CompanyId == input.CompanyId.Value);
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var f = input.Filter;
            query = query.Where(p => p.PlanNumber.ToLower().Contains(f));
        }

        var totalCount = query.Count();
        var items = query.OrderByDescending(p => p.CreationTime)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<ProductionPlanDto>(totalCount, items.Select(x => ObjectMapper.Map<ProductionPlan, ProductionPlanDto>(x)).ToList());
    }

    [Authorize(MyERPPermissions.ProductionPlans.Create)]
    public async Task<ProductionPlanDto> CreateAsync(CreateProductionPlanDto input)
    {
        var number = await _numberGenerator.GenerateAsync("PP", input.CompanyId);
        var plan = new ProductionPlan(
            GuidGenerator.Create(), input.CompanyId, number, input.PostingDate, CurrentTenant.Id)
        {
            CombineItems = input.CombineItems,
            IgnoreExistingOrderedQty = input.IgnoreExistingOrderedQty,
            ConsiderMinimumOrderQty = input.ConsiderMinimumOrderQty,
            IncludeSafetyStock = input.IncludeSafetyStock,
            SkipAvailableSubAssemblyItem = input.SkipAvailableSubAssemblyItem,
            RawMaterialGroupWarehouseId = input.RawMaterialGroupWarehouseId,
            ForWarehouseId = input.ForWarehouseId,
            Notes = input.Notes,
        };

        // Validate all planned items are active
        var itemValidation = LazyServiceProvider.LazyGetRequiredService<MyERP.Inventory.DomainServices.ItemTransactionValidationService>();
        await itemValidation.ValidateItemsForTransactionAsync(input.Items.Select(i => i.ItemId).ToArray());

        foreach (var item in input.Items)
        {
            plan.AddPlannedItem(new ProductionPlanItem(
                GuidGenerator.Create(), plan.Id,
                item.ItemId, item.ItemName, item.BomId, item.PlannedQty)
            {
                WarehouseId = item.WarehouseId,
                PlannedStartDate = item.PlannedStartDate,
                SalesOrderId = item.SalesOrderId,
                MaterialRequestId = item.MaterialRequestId,
            });
        }

        await _planRepository.InsertAsync(plan);
        return ObjectMapper.Map<ProductionPlan, ProductionPlanDto>(plan);
    }

    [Authorize(MyERPPermissions.ProductionPlans.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _planRepository.DeleteAsync(id);
    }

    [Authorize(MyERPPermissions.ProductionPlans.Submit)]
    public async Task<ProductionPlanDto> SubmitAsync(Guid id)
    {
        var plan = await _planRepository.GetAsync(id, includeDetails: true);
        plan.Submit();
        await _planRepository.UpdateAsync(plan);
        return ObjectMapper.Map<ProductionPlan, ProductionPlanDto>(plan);
    }

    [Authorize(MyERPPermissions.ProductionPlans.Cancel)]
    public async Task<ProductionPlanDto> CancelAsync(Guid id)
    {
        var plan = await _planRepository.GetAsync(id, includeDetails: true);
        plan.Cancel();
        await _planRepository.UpdateAsync(plan);
        return ObjectMapper.Map<ProductionPlan, ProductionPlanDto>(plan);
    }

    [Authorize(MyERPPermissions.ProductionPlans.Edit)]
    public async Task<ProductionPlanDto> CalculateMaterialRequirementsAsync(Guid id)
    {
        var plan = await _planRepository.GetAsync(id, includeDetails: true);

        if (plan.Status is not (ProductionPlanStatus.Draft or ProductionPlanStatus.Submitted))
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        // Clear existing material requirements for recalculation
        plan.MaterialRequirements.Clear();

        // Batch-load all BOMs for planned items to avoid N+1
        var bomIds = plan.PlannedItems.Select(pi => pi.BomId).Distinct().ToArray();
        var bomQuery = await _bomRepository.GetQueryableAsync();
        var boms = bomQuery.Where(b => bomIds.Contains(b.Id)).ToDictionary(b => b.Id);

        // Explode BOMs for each planned item (phantom-aware recursive explosion)
        foreach (var plannedItem in plan.PlannedItems)
        {
            var bom = boms.TryGetValue(plannedItem.BomId, out var cachedBom)
                ? cachedBom
                : await _bomRepository.GetAsync(plannedItem.BomId); // fallback if not in batch
            var multiplier = plannedItem.PlannedQty / (bom.Quantity > 0 ? bom.Quantity : 1);

            // Use BomValidationService for phantom-aware explosion
            var explodedItems = await _bomValidationService.ExplodeBomAsync(plannedItem.BomId, multiplier);

            foreach (var explodedItem in explodedItems)
            {
                // Check if material already exists in requirements (for combining)
                var existing = plan.MaterialRequirements
                    .FirstOrDefault(mr => mr.ItemId == explodedItem.ItemId
                        && mr.WarehouseId == (plan.ForWarehouseId ?? bom.SourceWarehouseId));

                if (existing != null && plan.CombineItems)
                {
                    existing.RequiredQty += explodedItem.Quantity;
                    existing.PlannedQty = CalculatePlannedQty(existing, plan);
                }
                else
                {
                    var mrItem = new ProductionPlanMrItem(
                        GuidGenerator.Create(), plan.Id,
                        explodedItem.ItemId, explodedItem.ItemName, explodedItem.Quantity)
                    {
                        Uom = explodedItem.Uom,
                        WarehouseId = plan.ForWarehouseId ?? bom.SourceWarehouseId,
                        ProcurementType = explodedItem.SubBomId.HasValue
                            ? SubAssemblyType.InHouseManufacturing
                            : SubAssemblyType.MaterialRequest,
                    };
                    mrItem.PlannedQty = CalculatePlannedQty(mrItem, plan);
                    plan.AddMaterialRequirement(mrItem);
                }
            }
        }

        await _planRepository.UpdateAsync(plan);
        return ObjectMapper.Map<ProductionPlan, ProductionPlanDto>(plan);
    }

    [Authorize(MyERPPermissions.ProductionPlans.Edit)]
    public async Task<ProductionPlanDto> GenerateWorkOrdersAsync(Guid id)
    {
        var plan = await _planRepository.GetAsync(id, includeDetails: true);

        if (plan.Status is not (ProductionPlanStatus.Submitted or ProductionPlanStatus.InProgress))
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        // Check that WOs haven't already been generated for all items
        var itemsNeedingWo = plan.PlannedItems.Where(i => !i.WorkOrderId.HasValue).ToList();
        if (!itemsNeedingWo.Any())
            throw new BusinessException(MyERPDomainErrorCodes.ProductionPlanWorkOrdersAlreadyGenerated);

        foreach (var item in itemsNeedingWo)
        {
            var woNumber = await _numberGenerator.GenerateAsync("WO", plan.CompanyId);
            var wo = new WorkOrder(
                GuidGenerator.Create(), plan.CompanyId, woNumber,
                item.ItemId, item.BomId, item.PlannedQty, CurrentTenant.Id)
            {
                SalesOrderId = item.SalesOrderId,
                FgWarehouseId = item.WarehouseId,
            };
            wo.SetPlannedDates(item.PlannedStartDate, null);

            // Populate required items from BOM
            var bom = await _bomRepository.GetAsync(item.BomId, includeDetails: true);
            var multiplier = item.PlannedQty / (bom.Quantity > 0 ? bom.Quantity : 1);
            foreach (var bi in bom.Items)
            {
                wo.RequiredItems.Add(new WorkOrderItem(
                    GuidGenerator.Create(), wo.Id, bi.ItemId, bi.ItemName, bi.Quantity * multiplier)
                { SourceWarehouseId = bi.SourceWarehouseId ?? bom.SourceWarehouseId });
            }

            await _workOrderRepository.InsertAsync(wo);
            item.WorkOrderId = wo.Id;
        }

        if (plan.Status == ProductionPlanStatus.Submitted)
            plan.MarkInProgress();

        await _planRepository.UpdateAsync(plan);
        return ObjectMapper.Map<ProductionPlan, ProductionPlanDto>(plan);
    }

    [Authorize(MyERPPermissions.MaterialRequests.Create)]
    public async Task<ProductionPlanDto> GenerateMaterialRequestsAsync(Guid id)
    {
        var plan = await _planRepository.GetAsync(id, includeDetails: true);

        if (plan.Status is not (ProductionPlanStatus.Submitted or ProductionPlanStatus.InProgress))
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        // Get items needing MRs (those with PlannedQty > 0 and no MR yet)
        var itemsNeedingMr = plan.MaterialRequirements
            .Where(m => m.PlannedQty > 0 && !m.MaterialRequestId.HasValue)
            .ToList();

        if (!itemsNeedingMr.Any())
            return ObjectMapper.Map<ProductionPlan, ProductionPlanDto>(plan);

        var mrNumber = await _numberGenerator.GenerateAsync("MR", plan.CompanyId);
        var mr = new MaterialRequest(
            GuidGenerator.Create(), plan.CompanyId, mrNumber,
            MaterialRequestType.Purchase, plan.PostingDate, CurrentTenant.Id)
        {
            TargetWarehouseId = plan.ForWarehouseId,
        };

        foreach (var item in itemsNeedingMr)
        {
            mr.AddItem(item.ItemId, item.ItemName, item.PlannedQty, item.Uom ?? "Unit", item.WarehouseId);
            item.MaterialRequestId = mr.Id;
        }

        await _materialRequestRepository.InsertAsync(mr);

        if (plan.Status == ProductionPlanStatus.Submitted)
            plan.MarkInProgress();

        await _planRepository.UpdateAsync(plan);
        return ObjectMapper.Map<ProductionPlan, ProductionPlanDto>(plan);
    }

    private static decimal CalculatePlannedQty(ProductionPlanMrItem item, ProductionPlan plan)
    {
        var qty = item.RequiredQty;

        if (!plan.IgnoreExistingOrderedQty)
            qty -= item.OrderedQty;

        qty -= item.AvailableQty;

        if (plan.IncludeSafetyStock)
            qty += item.SafetyStock;

        qty = Math.Max(0, qty);

        if (plan.ConsiderMinimumOrderQty && item.MinOrderQty > 0)
            qty = Math.Ceiling(qty / item.MinOrderQty) * item.MinOrderQty;

        return qty;
    }
}

