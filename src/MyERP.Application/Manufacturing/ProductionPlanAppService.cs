using System;
using System.Collections.Generic;
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
using MyERP.Inventory.DomainServices;

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
            query = query.Where(p => p.PlanNumber.Contains(f));
        }

        var totalCount = query.Count();
        var items = query.OrderByDescending(p => p.CreationTime)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<ProductionPlanDto>(totalCount, items.Select(x => ObjectMapper.Map<ProductionPlan, ProductionPlanDto>(x)).ToList());
    }

    [Authorize(MyERPPermissions.ProductionPlans.Create)]
    public async Task<ProductionPlanDto> CreateAsync(CreateProductionPlanDto input)
    {
        if (input.Items == null || !input.Items.Any())
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustHaveItems);

        foreach (var item in input.Items)
        {
            if (item.PlannedQty <= 0)
                throw new BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                    .WithData("field", "PlannedQty");
        }

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
            ReserveStock = input.ReserveStock,
            Notes = input.Notes,
        };

        // Validate Raw Material Group Warehouse hierarchy (ERPNext PR #56948)
        await ValidateRawMaterialGroupWarehouseAsync(input.CompanyId, input.RawMaterialGroupWarehouseId, input.ForWarehouseId);

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

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "ProductionPlan", plan.Id,
            "Submitted", plan.CompanyId,
            plan.PlanNumber, "Draft", "Submitted", CurrentUser.Id,
            $"Production Plan {plan.PlanNumber} submitted", CurrentTenant.Id));

        return ObjectMapper.Map<ProductionPlan, ProductionPlanDto>(plan);
    }

    [Authorize(MyERPPermissions.ProductionPlans.Cancel)]
    public async Task<ProductionPlanDto> CancelAsync(Guid id)
    {
        var plan = await _planRepository.GetAsync(id, includeDetails: true);

        // Check for submitted Work Orders generated from this plan; delete draft ones (gotchas #431, #552)
        var woIds = plan.PlannedItems.Where(i => i.WorkOrderId.HasValue).Select(i => i.WorkOrderId!.Value).ToList();
        if (woIds.Any())
        {
            var woRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<WorkOrder, Guid>>();
            var woQ = await woRepo.GetQueryableAsync();
            var hasSubmittedWo = woQ.Any(wo => woIds.Contains(wo.Id) && wo.Status != WorkOrderStatus.Draft && wo.Status != WorkOrderStatus.Cancelled);
            if (hasSubmittedWo)
            {
                throw new BusinessException(MyERPDomainErrorCodes.CannotCancelWithSubmittedDependents)
                    .WithData("documentType", "ProductionPlan")
                    .WithData("dependent", "WorkOrder");
            }

            var draftWos = woQ.Where(wo => woIds.Contains(wo.Id) && wo.Status == WorkOrderStatus.Draft).ToList();
            foreach (var draftWo in draftWos)
            {
                await woRepo.DeleteAsync(draftWo);
            }
        }

        // Check for submitted Material Requests generated from this plan; delete draft ones
        var mrIds = plan.MaterialRequirements.Where(i => i.MaterialRequestId.HasValue).Select(i => i.MaterialRequestId!.Value).ToList();
        if (mrIds.Any())
        {
            var mrRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Purchasing.Entities.MaterialRequest, Guid>>();
            var mrQ = await mrRepo.GetQueryableAsync();
            var hasSubmittedMr = mrQ.Any(mr => mrIds.Contains(mr.Id) && mr.Status != Core.DocumentStatus.Draft && mr.Status != Core.DocumentStatus.Cancelled);
            if (hasSubmittedMr)
            {
                throw new BusinessException(MyERPDomainErrorCodes.CannotCancelWithSubmittedDependents)
                    .WithData("documentType", "ProductionPlan")
                    .WithData("dependent", "MaterialRequest");
            }

            var draftMrs = mrQ.Where(mr => mrIds.Contains(mr.Id) && mr.Status == Core.DocumentStatus.Draft).ToList();
            foreach (var draftMr in draftMrs)
            {
                await mrRepo.DeleteAsync(draftMr);
            }
        }

        plan.Cancel();
        await _planRepository.UpdateAsync(plan);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "ProductionPlan", plan.Id,
            "Cancelled", plan.CompanyId,
            plan.PlanNumber, plan.Status.ToString(), "Cancelled", CurrentUser.Id,
            $"Production Plan {plan.PlanNumber} cancelled", CurrentTenant.Id));

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

        if (plan.Status is not (ProductionPlanStatus.Submitted or ProductionPlanStatus.MaterialRequested or ProductionPlanStatus.InProgress))
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        // Check that WOs haven't already been generated for all items
        // Per ERPNext PR #58249 (commit 1fa057b943): skip covered rows (PlannedQty <= 0) when ordering from MRP/plan
        var itemsNeedingWo = plan.PlannedItems
            .Where(i => !i.WorkOrderId.HasValue && Math.Round(i.PlannedQty, 4) > 0)
            .ToList();
        if (!itemsNeedingWo.Any())
            throw new BusinessException(MyERPDomainErrorCodes.ProductionPlanWorkOrdersAlreadyGenerated);

        var companyRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Core.Entities.Company, Guid>>();
        var company = await companyRepo.FindAsync(plan.CompanyId);

        // Batch load BOMs to prevent N+1 queries during bulk Work Order generation (ERPNext PR #57154)
        var bomIds = itemsNeedingWo.Select(i => i.BomId).Distinct().ToList();
        var bomQuery = await _bomRepository.WithDetailsAsync();
        var bomMap = bomQuery.Where(b => bomIds.Contains(b.Id)).ToList().ToDictionary(b => b.Id);

        foreach (var item in itemsNeedingWo)
        {
            if (!bomMap.TryGetValue(item.BomId, out var bom))
                continue;

            var woNumber = await _numberGenerator.GenerateAsync("WO", plan.CompanyId);
            var wo = new WorkOrder(
                GuidGenerator.Create(), plan.CompanyId, woNumber,
                item.ItemId, item.BomId, item.PlannedQty, CurrentTenant.Id)
            {
                SalesOrderId = item.SalesOrderId,
                SourceWarehouseId = bom.SourceWarehouseId,
                FgWarehouseId = item.WarehouseId ?? bom.TargetWarehouseId,
                WipWarehouseId = company?.DefaultWipWarehouseId,
                ScrapWarehouseId = bom.ScrapWarehouseId ?? company?.DefaultScrapWarehouseId,
                TrackSemiFinishedGoods = bom.TrackSemiFinishedGoods,
            };
            wo.SetPlannedDates(item.PlannedStartDate, null);

            // Populate required items from BOM
            var multiplier = item.PlannedQty / (bom.Quantity > 0 ? bom.Quantity : 1);
            foreach (var bi in bom.Items)
            {
                wo.RequiredItems.Add(new WorkOrderItem(
                    GuidGenerator.Create(), wo.Id, bi.ItemId, bi.ItemName, bi.Quantity * multiplier)
                { SourceWarehouseId = bi.SourceWarehouseId ?? bom.SourceWarehouseId });
            }

            await _workOrderRepository.InsertAsync(wo);
            item.WorkOrderId = wo.Id;

            // Transfer stock reservations from Production Plan to Work Order (ERPNext commit 0bc3cfe29d)
            var sreManager = LazyServiceProvider.LazyGetService<MyERP.Inventory.DomainServices.StockReservationManager>();
            if (sreManager != null)
            {
                foreach (var reqItem in wo.RequiredItems)
                {
                    if (reqItem.SourceWarehouseId.HasValue)
                    {
                        await sreManager.TransferReservationEntriesAsync(
                            "ProductionPlan", plan.Id,
                            "WorkOrder", wo.Id,
                            reqItem.ItemId, reqItem.SourceWarehouseId.Value,
                            reqItem.RequiredQuantity, reqItem.Id);
                    }
                }
            }
        }

        if (plan.Status is ProductionPlanStatus.Submitted or ProductionPlanStatus.MaterialRequested)
            plan.MarkInProgress();

        await _planRepository.UpdateAsync(plan);
        return ObjectMapper.Map<ProductionPlan, ProductionPlanDto>(plan);
    }

    [Authorize(MyERPPermissions.MaterialRequests.Create)]
    public async Task<ProductionPlanDto> GenerateMaterialRequestsAsync(Guid id)
    {
        var plan = await _planRepository.GetAsync(id, includeDetails: true);

        if (plan.Status is not (ProductionPlanStatus.Submitted or ProductionPlanStatus.MaterialRequested or ProductionPlanStatus.InProgress))
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        // Get items needing MRs (those with PlannedQty > 0 and no MR yet)
        // Filter out sub-assembly items that need Work Orders (InHouseManufacturing), not Purchase MRs
        // Per ERPNext PR #58249: skip covered items with PlannedQty <= 0
        var itemsNeedingMr = plan.MaterialRequirements
            .Where(m => Math.Round(m.PlannedQty, 4) > 0 && !m.MaterialRequestId.HasValue
                && m.ProcurementType != SubAssemblyType.InHouseManufacturing)
            .ToList();

        if (!itemsNeedingMr.Any())
            return ObjectMapper.Map<ProductionPlan, ProductionPlanDto>(plan);

        // Resolve default suppliers from ItemDefault for grouping
        // Per ERPNext: separate MRs per (supplier, warehouse) for procurement routing
        var itemIds = itemsNeedingMr.Select(m => m.ItemId).Distinct().ToList();
        var itemDefaultQuery = await LazyServiceProvider
            .LazyGetRequiredService<IRepository<Inventory.Entities.ItemDefault, Guid>>()
            .GetQueryableAsync();
        var supplierMap = itemDefaultQuery
            .Where(d => itemIds.Contains(d.ItemId) && d.DefaultSupplierId != null && d.CompanyId == plan.CompanyId)
            .Select(d => new { d.ItemId, d.DefaultSupplierId })
            .ToDictionary(d => d.ItemId, d => d.DefaultSupplierId);

        // Group items by (supplier, warehouse) — items without supplier go to a "general" MR
        var groups = itemsNeedingMr
            .GroupBy(m => new
            {
                SupplierId = supplierMap.ContainsKey(m.ItemId) ? supplierMap[m.ItemId] : (Guid?)null,
                WarehouseId = m.WarehouseId ?? plan.ForWarehouseId
            })
            .ToList();

        foreach (var group in groups)
        {
            var mrNumber = await _numberGenerator.GenerateAsync("MR", plan.CompanyId);
            var mr = new MaterialRequest(
                GuidGenerator.Create(), plan.CompanyId, mrNumber,
                MaterialRequestType.Purchase, plan.PostingDate, CurrentTenant.Id)
            {
                TargetWarehouseId = group.Key.WarehouseId,
            };

            foreach (var item in group)
            {
                // Fetch Stock UOM and Conversion Factor
                var itemRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Inventory.Entities.Item, Guid>>();
                var itemEntity = await itemRepo.GetAsync(item.ItemId);
                
                var uomService = LazyServiceProvider.LazyGetRequiredService<UomConversionService>();
                var conversionFactor = await uomService.GetConversionFactorAsync(item.ItemId, item.Uom ?? "Unit", itemEntity.Uom, itemEntity.VariantOfId);
                
                var requestedQty = UomConversionService.CalculatePurchaseUomQty(
                    item.PlannedQty,
                    conversionFactor,
                    item.MinOrderQty,
                    plan.ConsiderMinimumOrderQty);

                mr.AddItem(item.ItemId, item.ItemName, requestedQty, item.Uom ?? "Unit", item.WarehouseId);
                item.MaterialRequestId = mr.Id;
            }

            await _materialRequestRepository.InsertAsync(mr);
        }

        if (plan.Status == ProductionPlanStatus.Submitted)
            plan.MarkMaterialRequested();

        await _planRepository.UpdateAsync(plan);
        return ObjectMapper.Map<ProductionPlan, ProductionPlanDto>(plan);
    }

    /// <summary>
    /// Calculates the planned (to-order) qty for a material requirement.
    /// Per PR #57399: safety stock is added BEFORE min-order-qty and UOM rounding,
    /// and consumed available qty tracking uses (qty - required_qty) not min(qty, available).
    /// </summary>
    private static decimal CalculatePlannedQty(ProductionPlanMrItem item, ProductionPlan plan)
    {
        var safetyStock = plan.IncludeSafetyStock ? item.SafetyStock : 0m;
        var requiredQty = item.RequiredQty;

        if (!plan.IgnoreExistingOrderedQty || item.AvailableQty < 0)
        {
            // When not ignoring existing OR projected qty is negative: use full required + safety
            var qty = Math.Max(0, requiredQty + safetyStock);

            if (plan.ConsiderMinimumOrderQty && item.MinOrderQty > 0 && qty > 0 && qty < item.MinOrderQty)
                qty = item.MinOrderQty;

            return qty;
        }

        // Deduct available stock (minus safety buffer) from requirement
        var availableAfterSafety = item.AvailableQty - safetyStock;
        var plannedQty = Math.Max(0, requiredQty - availableAfterSafety);

        if (plan.ConsiderMinimumOrderQty && item.MinOrderQty > 0 && plannedQty > 0 && plannedQty < item.MinOrderQty)
            plannedQty = item.MinOrderQty;

        return plannedQty;
    }

    private async Task ValidateRawMaterialGroupWarehouseAsync(Guid companyId, Guid? rawMaterialGroupWarehouseId, Guid? forWarehouseId)
    {
        if (!rawMaterialGroupWarehouseId.HasValue) return;

        var whRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Inventory.Entities.Warehouse, Guid>>();
        var groupWh = await whRepo.FindAsync(rawMaterialGroupWarehouseId.Value);
        if (groupWh == null || groupWh.CompanyId != companyId)
        {
            throw new BusinessException(MyERPDomainErrorCodes.EntityNotFound)
                .WithData("reason", "Raw Material Group Warehouse not found for this company");
        }
        if (!groupWh.IsGroup)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Raw Material Group Warehouse must be a group warehouse");
        }

        if (forWarehouseId.HasValue)
        {
            var forWh = await whRepo.FindAsync(forWarehouseId.Value);
            if (forWh != null && forWh.ParentWarehouseId != groupWh.Id)
            {
                var allWhs = (await whRepo.GetQueryableAsync()).Where(w => w.CompanyId == companyId).ToList();
                var curr = forWh;
                bool isChild = false;
                while (curr?.ParentWarehouseId != null)
                {
                    if (curr.ParentWarehouseId == groupWh.Id)
                    {
                        isChild = true;
                        break;
                    }
                    curr = allWhs.FirstOrDefault(w => w.Id == curr.ParentWarehouseId);
                }
                if (!isChild)
                {
                    throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                        .WithData("detail", "For Warehouse must be a child of the Raw Material Group Warehouse");
                }
            }
        }
    }

    [Authorize(MyERPPermissions.ProductionPlans.Default)]
    public async Task<ProductionPlanVisualizerDto> GetVisualizerDataAsync(Guid id)
    {
        var plan = await _planRepository.GetAsync(id, includeDetails: true);

        var totalPlanned = plan.PlannedItems.Sum(i => i.PlannedQty);
        var totalProduced = plan.PlannedItems.Sum(i => i.ProducedQty);
        var completion = totalPlanned > 0 ? Math.Round(totalProduced / totalPlanned * 100m, 1) : 0m;

        // Query linked work orders
        var woQuery = await _workOrderRepository.GetQueryableAsync();
        var workOrders = woQuery
            .Where(w => plan.PlannedItems.Select(p => p.Id).Contains(w.SalesOrderItemId ?? Guid.Empty)
                     || plan.PlannedItems.Select(p => p.WorkOrderId).Contains(w.Id))
            .ToList();

        // Query linked material requests
        var mrQuery = await _materialRequestRepository.GetQueryableAsync();
        var materialRequests = mrQuery
            .Where(m => plan.MaterialRequirements.Select(mr => mr.MaterialRequestId).Contains(m.Id)
                     || plan.PlannedItems.Select(p => p.MaterialRequestId).Contains(m.Id))
            .ToList();

        var binRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Inventory.Entities.Bin, Guid>>();
        var binQuery = await binRepo.GetQueryableAsync();
        var bins = binQuery
            .Where(b => plan.MaterialRequirements.Select(mr => mr.ItemId).Contains(b.ItemId))
            .ToList();

        var finishedGoods = new List<VisualizerFinishedGoodDto>();
        foreach (var pi in plan.PlannedItems)
        {
            var linkedWos = workOrders
                .Where(w => w.Id == pi.WorkOrderId || w.ItemId == pi.ItemId)
                .Select(w => new VisualizerLinkedDocDto
                {
                    Id = w.Id,
                    DocumentNumber = w.WorkOrderNumber,
                    Status = w.Status.ToString(),
                    Qty = w.Quantity,
                    CompletedQty = w.ProducedQuantity,
                })
                .ToList();

            finishedGoods.Add(new VisualizerFinishedGoodDto
            {
                ItemId = pi.ItemId,
                ItemName = pi.ItemName,
                PlannedQty = pi.PlannedQty,
                ProducedQty = pi.ProducedQty,
                PendingQty = Math.Max(0, pi.PlannedQty - pi.ProducedQty),
                WarehouseId = pi.WarehouseId,
                PlannedStartDate = pi.PlannedStartDate,
                SalesOrderId = pi.SalesOrderId,
                WorkOrders = linkedWos,
            });
        }

        var rawMaterials = new List<VisualizerMaterialDto>();
        foreach (var mr in plan.MaterialRequirements)
        {
            var linkedMrs = materialRequests
                .Where(m => m.Id == mr.MaterialRequestId || m.Items.Any(i => i.ItemId == mr.ItemId))
                .Select(m => new VisualizerLinkedDocDto
                {
                    Id = m.Id,
                    DocumentNumber = m.RequestNumber,
                    Status = m.Status.ToString(),
                    Qty = m.Items.Where(i => i.ItemId == mr.ItemId).Sum(i => i.Quantity),
                    CompletedQty = m.Items.Where(i => i.ItemId == mr.ItemId).Sum(i => i.OrderedQuantity),
                })
                .ToList();

            var liveActualQty = bins
                .Where(b => b.ItemId == mr.ItemId && (!mr.WarehouseId.HasValue || b.WarehouseId == mr.WarehouseId.Value))
                .Sum(b => b.ActualQty);

            rawMaterials.Add(new VisualizerMaterialDto
            {
                ItemId = mr.ItemId,
                ItemName = mr.ItemName,
                RequiredQty = mr.RequiredQty,
                AvailableQty = liveActualQty,
                OrderedQty = mr.OrderedQty,
                ReceivedQty = mr.AvailableQty,
                WarehouseId = mr.WarehouseId,
                MaterialRequests = linkedMrs,
            });
        }

        return new ProductionPlanVisualizerDto
        {
            PlanId = plan.Id,
            PlanNumber = plan.PlanNumber,
            Status = plan.Status,
            TotalPlannedQty = totalPlanned,
            TotalProducedQty = totalProduced,
            CompletionPercentage = completion,
            FinishedGoods = finishedGoods,
            RawMaterials = rawMaterials,
        };
    }
}

