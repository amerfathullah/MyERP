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

        // Per ERPNext PR #58799 & #58847: calculate committed OrderedQty per planned item accounting for process loss
        var woQueryable = await _workOrderRepository.GetQueryableAsync();
        var existingWos = woQueryable
            .Where(w => w.ProductionPlanId == plan.Id && w.Status != WorkOrderStatus.Cancelled)
            .ToList();

        foreach (var item in plan.PlannedItems)
        {
            var committed = existingWos
                .Where(w => w.ProductionPlanItemId == item.Id || (!w.ProductionPlanItemId.HasValue && item.WorkOrderId == w.Id))
                .Sum(w => w.Quantity - w.ProcessLossQty);
            item.OrderedQty = committed;
        }

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

        // Validate Raw Material Group Warehouse hierarchy (ERPNext PR #56948)
        await ValidateRawMaterialGroupWarehouseAsync(input.CompanyId, input.RawMaterialGroupWarehouseId, input.ForWarehouseId);

        // Validate all planned items are active
        var itemValidation = LazyServiceProvider.LazyGetRequiredService<MyERP.Inventory.DomainServices.ItemTransactionValidationService>();
        await itemValidation.ValidateItemsForTransactionAsync(input.Items.Select(i => i.ItemId).ToArray());

        // Validate BOMs belong to company
        var bomIds = input.Items.Select(i => i.BomId).Distinct().ToList();
        var bomQuery = await _bomRepository.GetQueryableAsync();
        var boms = bomQuery.Where(b => bomIds.Contains(b.Id)).ToList();
        foreach (var bom in boms)
        {
            if (bom.CompanyId != input.CompanyId)
            {
                throw new BusinessException(MyERPDomainErrorCodes.CompanyMismatch)
                    .WithData("bomCompany", bom.CompanyId)
                    .WithData("productionPlanCompany", input.CompanyId);
            }
        }

        // Validate Sales Orders belong to company (if referenced)
        var soIds = input.Items.Where(i => i.SalesOrderId.HasValue).Select(i => i.SalesOrderId!.Value).Distinct().ToList();
        if (soIds.Count > 0)
        {
            var soRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.SalesOrder, Guid>>();
            var soQuery = await soRepo.GetQueryableAsync();
            var sos = soQuery.Where(s => soIds.Contains(s.Id)).ToList();
            foreach (var so in sos)
            {
                if (so.CompanyId != input.CompanyId)
                {
                    throw new BusinessException(MyERPDomainErrorCodes.CompanyMismatch)
                        .WithData("salesOrderCompany", so.CompanyId)
                        .WithData("productionPlanCompany", input.CompanyId);
                }
            }
        }

        // Validate Material Requests belong to company, are submitted, are of type Manufacture, and not stopped/closed (ERPNext PR #59584 / commit 6e24ef9cce)
        await ValidateMaterialRequestsAsync(input.CompanyId, input.Items);

        // Validate warehouses belong to company
        var warehouseIds = input.Items.Where(i => i.WarehouseId.HasValue).Select(i => i.WarehouseId!.Value)
            .Concat(new[] { input.RawMaterialGroupWarehouseId, input.ForWarehouseId, input.SubAssemblyWarehouseId }.Where(w => w.HasValue).Select(w => w!.Value))
            .Distinct()
            .ToList();
        if (warehouseIds.Count > 0)
        {
            var whRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Inventory.Entities.Warehouse, Guid>>();
            var whQuery = await whRepo.GetQueryableAsync();
            var warehouses = whQuery.Where(w => warehouseIds.Contains(w.Id)).ToList();
            if (warehouses.Count != warehouseIds.Count)
            {
                var foundIds = warehouses.Select(w => w.Id).ToHashSet();
                var missingId = warehouseIds.First(id => !foundIds.Contains(id));
                throw new BusinessException(MyERPDomainErrorCodes.CompanyMismatch)
                    .WithData("warehouseId", missingId)
                    .WithData("productionPlanCompany", input.CompanyId);
            }
            foreach (var wh in warehouses)
            {
                if (wh.CompanyId != input.CompanyId)
                {
                    throw new BusinessException(MyERPDomainErrorCodes.CompanyMismatch)
                        .WithData("warehouseCompany", wh.CompanyId)
                        .WithData("productionPlanCompany", input.CompanyId);
                }
            }
        }

        // Validate items and warehouses pass CompanyRestrictionValidationService
        var companyRestriction = LazyServiceProvider.LazyGetRequiredService<Core.DomainServices.CompanyRestrictionValidationService>();
        await companyRestriction.ValidateTransactionCompanyAsync(
            "ProductionPlan", input.CompanyId,
            itemIds: input.Items.Select(i => i.ItemId).Distinct().ToArray(),
            warehouseIds: warehouseIds.Count > 0 ? warehouseIds.ToArray() : null);

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
            SubAssemblyWarehouseId = input.SubAssemblyWarehouseId,
            ReserveStock = input.ReserveStock,
            Notes = input.Notes,
        };

        foreach (var item in input.Items)
        {
            plan.AddPlannedItem(new ProductionPlanItem(
                GuidGenerator.Create(), plan.Id,
                item.ItemId, item.ItemName, item.BomId, item.PlannedQty)
            {
                WarehouseId = item.WarehouseId,
                PlannedStartDate = item.PlannedStartDate,
                SalesOrderId = item.SalesOrderId,
                SalesOrderItemId = item.SalesOrderItemId,
                IsProductBundleItem = item.IsProductBundleItem,
                MaterialRequestId = item.MaterialRequestId,
            });
        }

        await _planRepository.InsertAsync(plan);
        return ObjectMapper.Map<ProductionPlan, ProductionPlanDto>(plan);
    }

    [Authorize(MyERPPermissions.ProductionPlans.Edit)]
    public async Task<ProductionPlanDto> UpdateAsync(Guid id, CreateProductionPlanDto input)
    {
        if (input.Items == null || !input.Items.Any())
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustHaveItems);

        foreach (var item in input.Items)
        {
            if (item.PlannedQty <= 0)
                throw new BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                    .WithData("field", "PlannedQty");
        }

        var plan = await _planRepository.GetAsync(id, includeDetails: true);

        if (plan.Status != ProductionPlanStatus.Draft)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("documentType", "ProductionPlan")
                .WithData("status", plan.Status.ToString());
        }

        if (plan.CompanyId != input.CompanyId)
        {
            throw new BusinessException(MyERPDomainErrorCodes.CompanyMismatch)
                .WithData("productionPlanCompany", plan.CompanyId)
                .WithData("inputCompany", input.CompanyId);
        }

        // Validate Raw Material Group Warehouse hierarchy (ERPNext PR #56948)
        await ValidateRawMaterialGroupWarehouseAsync(input.CompanyId, input.RawMaterialGroupWarehouseId, input.ForWarehouseId);

        // Validate all planned items are active
        var itemValidation = LazyServiceProvider.LazyGetRequiredService<MyERP.Inventory.DomainServices.ItemTransactionValidationService>();
        await itemValidation.ValidateItemsForTransactionAsync(input.Items.Select(i => i.ItemId).ToArray());

        // Validate BOMs belong to company
        var bomIds = input.Items.Select(i => i.BomId).Distinct().ToList();
        var bomQuery = await _bomRepository.GetQueryableAsync();
        var boms = bomQuery.Where(b => bomIds.Contains(b.Id)).ToList();
        foreach (var bom in boms)
        {
            if (bom.CompanyId != input.CompanyId)
            {
                throw new BusinessException(MyERPDomainErrorCodes.CompanyMismatch)
                    .WithData("bomCompany", bom.CompanyId)
                    .WithData("productionPlanCompany", input.CompanyId);
            }
        }

        // Validate Sales Orders belong to company (if referenced)
        var soIds = input.Items.Where(i => i.SalesOrderId.HasValue).Select(i => i.SalesOrderId!.Value).Distinct().ToList();
        if (soIds.Count > 0)
        {
            var soRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.SalesOrder, Guid>>();
            var soQuery = await soRepo.GetQueryableAsync();
            var sos = soQuery.Where(s => soIds.Contains(s.Id)).ToList();
            foreach (var so in sos)
            {
                if (so.CompanyId != input.CompanyId)
                {
                    throw new BusinessException(MyERPDomainErrorCodes.CompanyMismatch)
                        .WithData("salesOrderCompany", so.CompanyId)
                        .WithData("productionPlanCompany", input.CompanyId);
                }
            }
        }

        // Validate Material Requests belong to company, are submitted, are of type Manufacture, and not stopped/closed (ERPNext PR #59584 / commit 6e24ef9cce)
        await ValidateMaterialRequestsAsync(input.CompanyId, input.Items);

        // Validate warehouses belong to company
        var warehouseIds = input.Items.Where(i => i.WarehouseId.HasValue).Select(i => i.WarehouseId!.Value)
            .Concat(new[] { input.RawMaterialGroupWarehouseId, input.ForWarehouseId, input.SubAssemblyWarehouseId }.Where(w => w.HasValue).Select(w => w!.Value))
            .Distinct()
            .ToList();
        if (warehouseIds.Count > 0)
        {
            var whRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Inventory.Entities.Warehouse, Guid>>();
            var whQuery = await whRepo.GetQueryableAsync();
            var warehouses = whQuery.Where(w => warehouseIds.Contains(w.Id)).ToList();
            if (warehouses.Count != warehouseIds.Count)
            {
                var foundIds = warehouses.Select(w => w.Id).ToHashSet();
                var missingId = warehouseIds.First(id => !foundIds.Contains(id));
                throw new BusinessException(MyERPDomainErrorCodes.CompanyMismatch)
                    .WithData("warehouseId", missingId)
                    .WithData("productionPlanCompany", input.CompanyId);
            }
            foreach (var wh in warehouses)
            {
                if (wh.CompanyId != input.CompanyId)
                {
                    throw new BusinessException(MyERPDomainErrorCodes.CompanyMismatch)
                        .WithData("warehouseCompany", wh.CompanyId)
                        .WithData("productionPlanCompany", input.CompanyId);
                }
            }
        }

        // Validate items and warehouses pass CompanyRestrictionValidationService
        var companyRestriction = LazyServiceProvider.LazyGetRequiredService<Core.DomainServices.CompanyRestrictionValidationService>();
        await companyRestriction.ValidateTransactionCompanyAsync(
            "ProductionPlan", input.CompanyId,
            itemIds: input.Items.Select(i => i.ItemId).Distinct().ToArray(),
            warehouseIds: warehouseIds.Count > 0 ? warehouseIds.ToArray() : null);

        plan.PostingDate = input.PostingDate;
        plan.CombineItems = input.CombineItems;
        plan.IgnoreExistingOrderedQty = input.IgnoreExistingOrderedQty;
        plan.ConsiderMinimumOrderQty = input.ConsiderMinimumOrderQty;
        plan.IncludeSafetyStock = input.IncludeSafetyStock;
        plan.SkipAvailableSubAssemblyItem = input.SkipAvailableSubAssemblyItem;
        plan.RawMaterialGroupWarehouseId = input.RawMaterialGroupWarehouseId;
        plan.ForWarehouseId = input.ForWarehouseId;
        plan.SubAssemblyWarehouseId = input.SubAssemblyWarehouseId;
        plan.ReserveStock = input.ReserveStock;
        plan.Notes = input.Notes;

        plan.ClearPlannedItems();
        foreach (var item in input.Items)
        {
            plan.AddPlannedItem(new ProductionPlanItem(
                GuidGenerator.Create(), plan.Id,
                item.ItemId, item.ItemName, item.BomId, item.PlannedQty)
            {
                WarehouseId = item.WarehouseId,
                PlannedStartDate = item.PlannedStartDate,
                SalesOrderId = item.SalesOrderId,
                SalesOrderItemId = item.SalesOrderItemId,
                IsProductBundleItem = item.IsProductBundleItem,
                MaterialRequestId = item.MaterialRequestId,
            });
        }

        await _planRepository.UpdateAsync(plan);
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

        // Validate planned quantities against Sales Orders (ERPNext PR #60271 / commit f3ba7ca638)
        await ValidateSalesOrderPlannedQtyAsync(plan);

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
    public async Task<ProductionPlanDto> CloseAsync(Guid id)
    {
        var plan = await _planRepository.GetAsync(id, includeDetails: true);
        var oldStatus = plan.Status;
        plan.Close();
        await _planRepository.UpdateAsync(plan);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "ProductionPlan", plan.Id,
            "Closed", plan.CompanyId,
            plan.PlanNumber, oldStatus.ToString(), "Closed", CurrentUser.Id,
            $"Production Plan {plan.PlanNumber} closed", CurrentTenant.Id));

        return ObjectMapper.Map<ProductionPlan, ProductionPlanDto>(plan);
    }

    [Authorize(MyERPPermissions.ProductionPlans.Edit)]
    public async Task<ProductionPlanDto> ReopenAsync(Guid id)
    {
        var plan = await _planRepository.GetAsync(id, includeDetails: true);
        var oldStatus = plan.Status;
        plan.Reopen();
        await _planRepository.UpdateAsync(plan);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "ProductionPlan", plan.Id,
            "Reopened", plan.CompanyId,
            plan.PlanNumber, oldStatus.ToString(), plan.Status.ToString(), CurrentUser.Id,
            $"Production Plan {plan.PlanNumber} reopened", CurrentTenant.Id));

        return ObjectMapper.Map<ProductionPlan, ProductionPlanDto>(plan);
    }

    [Authorize(MyERPPermissions.ProductionPlans.Edit)]
    public async Task<ProductionPlanDto> CalculateMaterialRequirementsAsync(Guid id)
    {
        var plan = await _planRepository.GetAsync(id, includeDetails: true);

        if (plan.Status is not (ProductionPlanStatus.Draft or ProductionPlanStatus.Submitted))
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        // Per ERPNext PR #59759: A group warehouse without a For Warehouse is rejected when raw materials are fetched
        if (plan.RawMaterialGroupWarehouseId.HasValue && !plan.ForWarehouseId.HasValue)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "For Warehouse is required when Raw Material Group Warehouse is selected.");
        }

        // Clear existing material requirements for recalculation
        plan.MaterialRequirements.Clear();

        // Load all company warehouses to resolve group warehouse descendant hierarchies (PR #60210)
        var whRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Inventory.Entities.Warehouse, Guid>>();
        var allCompanyWarehouses = (await whRepo.GetQueryableAsync())
            .Where(w => w.CompanyId == plan.CompanyId)
            .ToList();

        var descendantMap = new Dictionary<Guid, HashSet<Guid>>();
        HashSet<Guid> GetDescendantWarehouseIds(Guid rootWhId)
        {
            if (descendantMap.TryGetValue(rootWhId, out var existing)) return existing;
            var descendants = new HashSet<Guid> { rootWhId };
            var queue = new Queue<Guid>();
            queue.Enqueue(rootWhId);
            while (queue.Count > 0)
            {
                var curr = queue.Dequeue();
                foreach (var child in allCompanyWarehouses.Where(w => w.ParentWarehouseId == curr))
                {
                    if (descendants.Add(child.Id))
                    {
                        queue.Enqueue(child.Id);
                    }
                }
            }
            descendantMap[rootWhId] = descendants;
            return descendants;
        }

        // Load all company bins for all company warehouses
        var binRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Inventory.Entities.Bin, Guid>>();
        var binQuery = await binRepo.GetQueryableAsync();
        var allCompanyWhIds = allCompanyWarehouses.Select(w => w.Id).ToHashSet();
        var bins = binQuery.Where(b => allCompanyWhIds.Contains(b.WarehouseId)).ToList();

        // Helper: aggregate bin stock across warehouse descendants (nets deficits per PR #60210)
        (decimal ActualQty, decimal ProjectedQty) GetAggregatedBin(Guid itemId, Guid? warehouseId)
        {
            if (!warehouseId.HasValue) return (0m, 0m);
            var descendantWhIds = GetDescendantWarehouseIds(warehouseId.Value);
            var matchingBins = bins.Where(b => b.ItemId == itemId && descendantWhIds.Contains(b.WarehouseId)).ToList();
            return (matchingBins.Sum(b => b.ActualQty), matchingBins.Sum(b => b.ProjectedQty));
        }

        // Batch load all active company BOMs
        var bomQuery = await _bomRepository.WithDetailsAsync();
        var allCompanyBoms = bomQuery
            .Where(b => b.CompanyId == plan.CompanyId && b.IsActive)
            .ToList();
        var bomsById = allCompanyBoms.ToDictionary(b => b.Id);
        var defaultBomByItemId = allCompanyBoms
            .GroupBy(b => b.ItemId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(b => b.IsDefault).First());

        // Batch load company items
        var itemRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Inventory.Entities.Item, Guid>>();
        var itemQuery = await itemRepo.GetQueryableAsync();
        var itemMap = itemQuery.Where(i => i.CompanyId == plan.CompanyId).ToList().ToDictionary(i => i.Id);

        var targetSubAssemblyWh = plan.SubAssemblyWarehouseId ?? plan.ForWarehouseId;
        var targetRawMaterialWh = plan.RawMaterialGroupWarehouseId ?? plan.ForWarehouseId;

        // Sub-assembly stock pool tracking: available projected stock consumed once per plan across branches/levels (PR #60210)
        var subAssemblyPool = new Dictionary<Guid, (decimal ActualQty, decimal ProjectedQty, decimal AvailableQty, bool Exhausted)>();

        var subAssemblyRows = new List<ProductionPlanMrItem>();
        var rawRows = new List<ProductionPlanMrItem>();

        async Task ExplodeBomLevelAsync(
            BillOfMaterials currentBom,
            decimal currentToProduceQty,
            int indent,
            HashSet<Guid> visitedBoms)
        {
            if (currentToProduceQty <= 0) return;
            if (!visitedBoms.Add(currentBom.Id)) return;

            var bomOutputQty = currentBom.Quantity > 0 ? currentBom.Quantity : 1m;

            foreach (var bomItem in currentBom.Items)
            {
                var multiplier = (bomItem.StockQty / bomOutputQty) * currentToProduceQty;

                if (!bomItem.DoNotExplode && bomItem.IsPhantom && bomItem.SubBomId.HasValue)
                {
                    // Phantom: explode sub-BOM directly into components
                    if (!bomsById.TryGetValue(bomItem.SubBomId.Value, out var phantomBom))
                    {
                        phantomBom = await _bomRepository.FindAsync(bomItem.SubBomId.Value);
                        if (phantomBom != null) bomsById[phantomBom.Id] = phantomBom;
                    }

                    if (phantomBom != null)
                    {
                        await ExplodeBomLevelAsync(phantomBom, multiplier, indent, visitedBoms);
                    }
                }
                else
                {
                    BillOfMaterials? subBom = null;
                    if (!bomItem.DoNotExplode)
                    {
                        if (bomItem.SubBomId.HasValue)
                        {
                            if (!bomsById.TryGetValue(bomItem.SubBomId.Value, out subBom))
                            {
                                subBom = await _bomRepository.FindAsync(bomItem.SubBomId.Value);
                                if (subBom != null) bomsById[subBom.Id] = subBom;
                            }
                        }
                        else if (defaultBomByItemId.TryGetValue(bomItem.ItemId, out var db))
                        {
                            subBom = db;
                        }
                    }

                    if (!itemMap.TryGetValue(bomItem.ItemId, out var itemMaster))
                    {
                        itemMaster = await itemRepo.FindAsync(bomItem.ItemId);
                        if (itemMaster != null) itemMap[itemMaster.Id] = itemMaster;
                    }

                    var isManufactureItem = itemMaster != null
                        && itemMaster.DefaultMaterialRequestType == Purchasing.MaterialRequestType.Manufacture;

                    var isSubAssembly = subBom != null || isManufactureItem;

                    if (isSubAssembly)
                    {
                        var targetWh = targetSubAssemblyWh ?? currentBom.SourceWarehouseId;
                        var requiredQty = multiplier;

                        if (!subAssemblyPool.TryGetValue(bomItem.ItemId, out var pool))
                        {
                            var (act, proj) = GetAggregatedBin(bomItem.ItemId, targetWh);
                            pool = (act, proj, Math.Max(0, proj), false);
                            subAssemblyPool[bomItem.ItemId] = pool;
                        }

                        decimal plannedQty;
                        if (plan.SkipAvailableSubAssemblyItem && !pool.Exhausted)
                        {
                            if (pool.AvailableQty <= 0)
                            {
                                plannedQty = requiredQty;
                                pool.Exhausted = true;
                            }
                            else if (pool.AvailableQty >= requiredQty)
                            {
                                pool.AvailableQty -= requiredQty;
                                plannedQty = 0;
                            }
                            else
                            {
                                plannedQty = requiredQty - pool.AvailableQty;
                                pool.AvailableQty = 0;
                                pool.Exhausted = true;
                            }
                            subAssemblyPool[bomItem.ItemId] = pool;
                        }
                        else
                        {
                            plannedQty = requiredQty;
                        }

                        var saRow = new ProductionPlanMrItem(
                            GuidGenerator.Create(), plan.Id,
                            bomItem.ItemId, bomItem.ItemName, requiredQty)
                        {
                            Uom = bomItem.Uom,
                            WarehouseId = targetWh,
                            ProcurementType = SubAssemblyType.InHouseManufacturing,
                            AvailableQty = pool.ActualQty,
                            PlannedQty = plannedQty,
                            MinOrderQty = itemMaster?.MinOrderQty ?? 0m,
                            SafetyStock = itemMaster?.SafetyStock ?? 0m,
                        };
                        subAssemblyRows.Add(saRow);

                        // Recurse down sub-BOM with plannedQty (PR #60210)
                        if (subBom != null && plannedQty > 0)
                        {
                            await ExplodeBomLevelAsync(subBom, plannedQty, indent + 1, visitedBoms);
                        }
                    }
                    else
                    {
                        // Raw material leaf
                        var targetWh = targetRawMaterialWh ?? currentBom.SourceWarehouseId;
                        var (actualQty, _) = GetAggregatedBin(bomItem.ItemId, targetWh);

                        var rmRow = new ProductionPlanMrItem(
                            GuidGenerator.Create(), plan.Id,
                            bomItem.ItemId, bomItem.ItemName, multiplier)
                        {
                            Uom = bomItem.Uom,
                            WarehouseId = targetWh,
                            ProcurementType = SubAssemblyType.MaterialRequest,
                            AvailableQty = actualQty,
                            MinOrderQty = itemMaster?.MinOrderQty ?? 0m,
                            SafetyStock = itemMaster?.SafetyStock ?? 0m,
                        };
                        rawRows.Add(rmRow);
                    }
                }
            }

            visitedBoms.Remove(currentBom.Id);
        }

        foreach (var plannedItem in plan.PlannedItems)
        {
            if (!bomsById.TryGetValue(plannedItem.BomId, out var rootBom))
            {
                rootBom = await _bomRepository.GetAsync(plannedItem.BomId);
                bomsById[rootBom.Id] = rootBom;
            }

            await ExplodeBomLevelAsync(rootBom, plannedItem.PlannedQty, indent: 0, new HashSet<Guid>());
        }

        List<ProductionPlanMrItem> finalSubAssemblyRows;
        List<ProductionPlanMrItem> finalRawRows;

        if (plan.CombineItems)
        {
            finalSubAssemblyRows = subAssemblyRows
                .GroupBy(r => (r.ItemId, r.WarehouseId, r.ProcurementType))
                .Select(g =>
                {
                    var first = g.First();
                    var combined = new ProductionPlanMrItem(
                        GuidGenerator.Create(), plan.Id,
                        first.ItemId, first.ItemName, g.Sum(r => r.RequiredQty))
                    {
                        Uom = first.Uom,
                        WarehouseId = first.WarehouseId,
                        ProcurementType = first.ProcurementType,
                        AvailableQty = first.AvailableQty,
                        PlannedQty = g.Sum(r => r.PlannedQty),
                        MinOrderQty = first.MinOrderQty,
                        SafetyStock = first.SafetyStock,
                    };
                    return combined;
                })
                .ToList();

            finalRawRows = rawRows
                .GroupBy(r => (r.ItemId, r.WarehouseId, r.ProcurementType))
                .Select(g =>
                {
                    var first = g.First();
                    var combined = new ProductionPlanMrItem(
                        GuidGenerator.Create(), plan.Id,
                        first.ItemId, first.ItemName, g.Sum(r => r.RequiredQty))
                    {
                        Uom = first.Uom,
                        WarehouseId = first.WarehouseId,
                        ProcurementType = first.ProcurementType,
                        AvailableQty = first.AvailableQty,
                        MinOrderQty = first.MinOrderQty,
                        SafetyStock = first.SafetyStock,
                    };
                    return combined;
                })
                .ToList();
        }
        else
        {
            finalSubAssemblyRows = subAssemblyRows;
            finalRawRows = rawRows;
        }

        // Per ERPNext PR #58806: apply safety stock ONCE across rows for the same item/warehouse
        var consumedStock = new Dictionary<(Guid ItemId, Guid? WarehouseId), decimal>();
        foreach (var mrItem in finalRawRows)
        {
            var key = (mrItem.ItemId, mrItem.WarehouseId);
            var (_, proj) = GetAggregatedBin(mrItem.ItemId, mrItem.WarehouseId);
            var projectedQty = plan.IgnoreExistingOrderedQty ? Math.Max(0, proj) : 0m;

            var alreadyConsumed = consumedStock.TryGetValue(key, out var c) ? c : 0m;
            var effectiveSafety = plan.IncludeSafetyStock ? mrItem.SafetyStock : 0m;
            var availablePool = Math.Max(0, projectedQty - alreadyConsumed);
            var netAvailableForDeduction = Math.Max(0, availablePool - effectiveSafety);

            var needed = Math.Max(0, mrItem.RequiredQty - netAvailableForDeduction);
            var consumedFromPool = Math.Min(netAvailableForDeduction, mrItem.RequiredQty);
            consumedStock[key] = alreadyConsumed + consumedFromPool;

            mrItem.PlannedQty = needed;
        }

        // Per ERPNext PR #58805: apply MOQ once across Production Plan rows
        if (plan.ConsiderMinimumOrderQty)
        {
            var groups = finalRawRows
                .Where(r => r.ProcurementType == SubAssemblyType.MaterialRequest && r.PlannedQty > 0)
                .GroupBy(r => (r.ItemId, r.WarehouseId, r.ProcurementType));

            foreach (var group in groups)
            {
                var surplus = 0m;
                foreach (var row in group)
                {
                    var demand = row.PlannedQty;
                    var covered = Math.Min(surplus, demand);
                    demand -= covered;
                    surplus -= covered;

                    if (row.MinOrderQty > 0 && demand > 0 && demand < row.MinOrderQty)
                    {
                        var extra = row.MinOrderQty - demand;
                        surplus += extra;
                        demand = row.MinOrderQty;
                    }
                    row.PlannedQty = demand;
                }
            }
        }

        foreach (var row in finalSubAssemblyRows.Concat(finalRawRows))
        {
            plan.AddMaterialRequirement(row);
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
        // Per ERPNext PR #58799 & #58847:
        // Query existing Work Orders for this plan to determine committed quantities:
        // committed = sum(wo.Quantity - wo.ProcessLossQty) for non-cancelled work orders
        // pending = max(0, planned_qty - committed)
        var woQueryable = await _workOrderRepository.GetQueryableAsync();
        var existingWos = woQueryable
            .Where(w => w.ProductionPlanId == plan.Id && w.Status != WorkOrderStatus.Cancelled)
            .ToList();

        var itemsNeedingWo = new List<(ProductionPlanItem Item, decimal QtyToOrder)>();
        foreach (var item in plan.PlannedItems)
        {
            if (Math.Round(item.PlannedQty, 4) <= 0) continue;
            var committed = existingWos
                .Where(w => w.ProductionPlanItemId == item.Id || (!w.ProductionPlanItemId.HasValue && item.WorkOrderId == w.Id))
                .Sum(w => w.Quantity - w.ProcessLossQty);
            item.OrderedQty = committed;
            var pending = Math.Max(0, Math.Round(item.PlannedQty - committed, 4));
            if (pending > 0)
            {
                itemsNeedingWo.Add((item, pending));
            }
        }

        var subAssembliesNeedingWo = new List<(ProductionPlanMrItem Item, decimal QtyToOrder)>();
        foreach (var mr in plan.MaterialRequirements.Where(m => m.ProcurementType == SubAssemblyType.InHouseManufacturing))
        {
            if (Math.Round(mr.PlannedQty, 4) <= 0) continue;
            var committed = existingWos
                .Where(w => w.ProductionPlanSubAssemblyItemId == mr.Id)
                .Sum(w => w.Quantity - w.ProcessLossQty);
            mr.OrderedQty = committed;
            var pending = Math.Max(0, Math.Round(mr.PlannedQty - committed, 4));
            if (pending > 0)
            {
                subAssembliesNeedingWo.Add((mr, pending));
            }
        }

        if (!itemsNeedingWo.Any() && !subAssembliesNeedingWo.Any())
            throw new BusinessException(MyERPDomainErrorCodes.ProductionPlanWorkOrdersAlreadyGenerated);

        var companyRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Core.Entities.Company, Guid>>();
        var company = await companyRepo.FindAsync(plan.CompanyId);

        // Batch load BOMs to prevent N+1 queries during bulk Work Order generation (ERPNext PR #57154)
        var bomIds = itemsNeedingWo.Select(i => i.Item.BomId).Where(id => id != Guid.Empty).Distinct().ToList();
        var saItemIds = subAssembliesNeedingWo.Select(s => s.Item.ItemId).Distinct().ToList();
        var bomQuery = await _bomRepository.WithDetailsAsync();
        var bomMap = bomQuery.Where(b => bomIds.Contains(b.Id) && b.IsActive).ToList().ToDictionary(b => b.Id);
        var saBoms = bomQuery.Where(b => saItemIds.Contains(b.ItemId) && b.IsActive).ToList();
        var saBomMap = saBoms.GroupBy(b => b.ItemId).ToDictionary(g => g.Key, g => g.OrderByDescending(b => b.IsDefault).First());

        // Per ERPNext PR #58510 & #58511: throw when manufactured items have no active BOM
        var missingBomItems = new List<string>();
        foreach (var (item, _) in itemsNeedingWo)
        {
            if (item.BomId == Guid.Empty || !bomMap.ContainsKey(item.BomId))
            {
                if (!missingBomItems.Contains(item.ItemName))
                    missingBomItems.Add(item.ItemName);
            }
        }
        foreach (var (saItem, _) in subAssembliesNeedingWo)
        {
            if (!saBomMap.ContainsKey(saItem.ItemId))
            {
                if (!missingBomItems.Contains(saItem.ItemName))
                    missingBomItems.Add(saItem.ItemName);
            }
        }

        if (missingBomItems.Count > 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.BomNotFound)
                .WithData("items", string.Join(", ", missingBomItems));
        }

        // Validate linked Material Requests are not stopped or cancelled (ERPNext PR #59584 / commit 6e24ef9cce)
        var linkedMrIds = itemsNeedingWo
            .Where(i => i.Item.MaterialRequestId.HasValue)
            .Select(i => i.Item.MaterialRequestId!.Value)
            .Distinct()
            .ToList();
        if (linkedMrIds.Count > 0)
        {
            var mrQuery = await _materialRequestRepository.GetQueryableAsync();
            var mrs = mrQuery.Where(m => linkedMrIds.Contains(m.Id)).ToList();
            var stoppedMr = mrs.FirstOrDefault(m => m.Status == Core.DocumentStatus.Closed || m.Status == Core.DocumentStatus.Cancelled);
            if (stoppedMr != null)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Cannot create Work Orders from a stopped or closed Material Request {stoppedMr.RequestNumber}.");
            }
        }

        var itemDefaultsService = LazyServiceProvider.LazyGetRequiredService<MyERP.Inventory.DomainServices.ItemDefaultsResolutionService>();
        var woManager = LazyServiceProvider.LazyGetRequiredService<MyERP.Manufacturing.DomainServices.WorkOrderManager>();
        var sreManager = LazyServiceProvider.LazyGetService<MyERP.Inventory.DomainServices.StockReservationManager>();

        foreach (var (item, qtyToOrder) in itemsNeedingWo)
        {
            var bom = bomMap[item.BomId];

            var woNumber = await _numberGenerator.GenerateAsync("WO", plan.CompanyId);
            var wo = new WorkOrder(
                GuidGenerator.Create(), plan.CompanyId, woNumber,
                item.ItemId, item.BomId, qtyToOrder, CurrentTenant.Id)
            {
                ProductionPlanId = plan.Id,
                ProductionPlanItemId = item.Id,
                SalesOrderId = item.SalesOrderId,
                SalesOrderItemId = item.SalesOrderItemId,
                SourceWarehouseId = bom.SourceWarehouseId,
                FgWarehouseId = item.WarehouseId ?? bom.TargetWarehouseId,
                WipWarehouseId = company?.DefaultWipWarehouseId,
                ScrapWarehouseId = bom.ScrapWarehouseId ?? company?.DefaultScrapWarehouseId,
                TrackSemiFinishedGoods = bom.TrackSemiFinishedGoods,
            };
            wo.SetPlannedDates(item.PlannedStartDate, null);

            // Populate required items from BOM (with recursive phantom explosion per PR #59445)
            var reqs = await woManager.CalculateMaterialRequirementsAsync(bom.Id, qtyToOrder);
            foreach (var req in reqs)
            {
                var rawWarehouseId = req.SourceWarehouseId
                    ?? bom.SourceWarehouseId
                    ?? await itemDefaultsService.ResolveWarehouseAsync(req.ItemId, plan.CompanyId);

                wo.RequiredItems.Add(new WorkOrderItem(
                    GuidGenerator.Create(), wo.Id, req.ItemId, req.ItemName, req.RequiredQty)
                { SourceWarehouseId = rawWarehouseId });
            }

            await _workOrderRepository.InsertAsync(wo);
            item.WorkOrderId = wo.Id;
            item.OrderedQty += qtyToOrder;

            // Transfer stock reservations from Production Plan to Work Order (ERPNext commit 0bc3cfe29d)
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

        foreach (var (subItem, qtyToOrder) in subAssembliesNeedingWo)
        {
            var bom = saBomMap[subItem.ItemId];

            var woNumber = await _numberGenerator.GenerateAsync("WO", plan.CompanyId);
            var wo = new WorkOrder(
                GuidGenerator.Create(), plan.CompanyId, woNumber,
                subItem.ItemId, bom.Id, qtyToOrder, CurrentTenant.Id)
            {
                ProductionPlanId = plan.Id,
                ProductionPlanSubAssemblyItemId = subItem.Id,
                SourceWarehouseId = bom.SourceWarehouseId,
                FgWarehouseId = subItem.WarehouseId ?? bom.TargetWarehouseId ?? plan.ForWarehouseId,
                WipWarehouseId = company?.DefaultWipWarehouseId,
                ScrapWarehouseId = bom.ScrapWarehouseId ?? company?.DefaultScrapWarehouseId,
                TrackSemiFinishedGoods = bom.TrackSemiFinishedGoods,
            };

            var reqs = await woManager.CalculateMaterialRequirementsAsync(bom.Id, qtyToOrder);
            foreach (var req in reqs)
            {
                var rawWarehouseId = req.SourceWarehouseId
                    ?? bom.SourceWarehouseId
                    ?? await itemDefaultsService.ResolveWarehouseAsync(req.ItemId, plan.CompanyId);

                wo.RequiredItems.Add(new WorkOrderItem(
                    GuidGenerator.Create(), wo.Id, req.ItemId, req.ItemName, req.RequiredQty)
                { SourceWarehouseId = rawWarehouseId });
            }

            await _workOrderRepository.InsertAsync(wo);
            subItem.OrderedQty += qtyToOrder;

            // Transfer stock reservations from Production Plan to Work Order
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

        // Resolve default suppliers with fallback chain: ItemDefault -> Item.Suppliers -> ItemGroup hierarchy -> null
        // Per ERPNext PR #59349 / commit 43913d5c2a
        var itemDefaultsService = LazyServiceProvider.LazyGetRequiredService<MyERP.Inventory.DomainServices.ItemDefaultsResolutionService>();
        var supplierMap = new Dictionary<Guid, Guid?>();
        foreach (var itemId in itemsNeedingMr.Select(m => m.ItemId).Distinct())
        {
            supplierMap[itemId] = await itemDefaultsService.ResolveDefaultSupplierAsync(itemId, plan.CompanyId);
        }

        // Group items by (supplier, warehouse) — items without supplier go to a "general" MR
        var groups = itemsNeedingMr
            .GroupBy(m => new
            {
                SupplierId = supplierMap.TryGetValue(m.ItemId, out var supId) ? supId : null,
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

                // Per ERPNext PR #58841: skip items where calculated qty to request <= 0
                if (Math.Round(requestedQty, 4) <= 0) continue;

                mr.AddItem(
                    item.ItemId, item.ItemName, requestedQty, item.Uom ?? "Unit", item.WarehouseId,
                    conversionFactor: conversionFactor,
                    productionPlanId: plan.Id,
                    productionPlanMrItemId: item.Id);
                item.MaterialRequestId = mr.Id;
            }

            if (mr.Items.Count > 0)
            {
                await _materialRequestRepository.InsertAsync(mr);
            }
        }

        if (plan.Status == ProductionPlanStatus.Submitted && plan.MaterialRequirements.Any(m => m.MaterialRequestId.HasValue))
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
        var whRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Inventory.Entities.Warehouse, Guid>>();

        // Per ERPNext PR #59759: For Warehouse must belong to the plan's company and must be a non-group warehouse
        Inventory.Entities.Warehouse? forWh = null;
        if (forWarehouseId.HasValue)
        {
            forWh = await whRepo.FindAsync(forWarehouseId.Value);
            if (forWh == null || forWh.CompanyId != companyId)
            {
                throw new BusinessException(MyERPDomainErrorCodes.CompanyMismatch)
                    .WithData("reason", "For Warehouse not found or belongs to another company");
            }
            if (forWh.IsGroup)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", "For Warehouse must be a non-group warehouse");
            }
        }

        if (!rawMaterialGroupWarehouseId.HasValue) return;

        var groupWh = await whRepo.FindAsync(rawMaterialGroupWarehouseId.Value);
        if (groupWh == null || groupWh.CompanyId != companyId)
        {
            throw new BusinessException(MyERPDomainErrorCodes.CompanyMismatch)
                .WithData("reason", "Raw Material Group Warehouse not found or belongs to another company");
        }
        if (!groupWh.IsGroup)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Raw Material Group Warehouse must be a group warehouse");
        }

        if (forWarehouseId.HasValue && forWh != null)
        {
            // Per ERPNext PR #59759: A group warehouse itself is rejected as For Warehouse
            if (forWarehouseId.Value == rawMaterialGroupWarehouseId.Value)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", "For Warehouse cannot be the Raw Material Group Warehouse itself");
            }

            if (forWh.ParentWarehouseId != groupWh.Id)
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

    private record ItemSummaryInfo(Guid Id, string ItemCode, string ItemName);

    [Authorize(MyERPPermissions.ProductionPlans.Default)]
    public async Task<ProductionPlanSummaryDto> GetSummaryReportAsync(Guid id)
    {
        var plan = await _planRepository.GetAsync(id, includeDetails: true);

        // Per ERPNext PR #58541: fetch submitted (non-draft, non-cancelled) work orders for this plan
        var woQuery = await _workOrderRepository.GetQueryableAsync();
        var workOrders = woQuery
            .Where(w => w.ProductionPlanId == plan.Id
                     && w.Status != WorkOrderStatus.Draft
                     && w.Status != WorkOrderStatus.Cancelled)
            .ToList();

        // Resolve item codes and sales order numbers
        var allItemIds = new HashSet<Guid>();
        foreach (var pi in plan.PlannedItems) allItemIds.Add(pi.ItemId);
        foreach (var mr in plan.MaterialRequirements) allItemIds.Add(mr.ItemId);
        foreach (var wo in workOrders) allItemIds.Add(wo.ItemId);

        var itemRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Inventory.Entities.Item, Guid>>();
        var itemQuery = await itemRepo.GetQueryableAsync();
        var itemMap = itemQuery
            .Where(i => allItemIds.Contains(i.Id))
            .Select(i => new ItemSummaryInfo(i.Id, i.ItemCode, i.ItemName))
            .ToDictionary(i => i.Id);

        var soIds = plan.PlannedItems
            .Select(p => p.SalesOrderId)
            .Concat(workOrders.Select(w => w.SalesOrderId))
            .Where(sid => sid.HasValue)
            .Select(sid => sid!.Value)
            .Distinct()
            .ToList();

        var soMap = new Dictionary<Guid, string>();
        if (soIds.Count > 0)
        {
            var soRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.SalesOrder, Guid>>();
            var soQuery = await soRepo.GetQueryableAsync();
            soMap = soQuery
                .Where(s => soIds.Contains(s.Id))
                .Select(s => new { s.Id, s.OrderNumber })
                .ToDictionary(s => s.Id, s => s.OrderNumber);
        }

        var result = new ProductionPlanSummaryDto
        {
            ProductionPlanId = plan.Id,
            PlanNumber = plan.PlanNumber,
            Rows = new List<ProductionPlanSummaryRowDto>()
        };

        // Sub-assembly requirements: InHouseManufacturing, Subcontracting, or has linked sub-assembly work orders
        var subAssemblyMrItems = plan.MaterialRequirements
            .Where(m => m.ProcurementType == SubAssemblyType.InHouseManufacturing
                     || m.ProcurementType == SubAssemblyType.Subcontracting
                     || workOrders.Any(w => w.ProductionPlanSubAssemblyItemId == m.Id))
            .ToList();

        var processedSubAssemblyIds = new HashSet<Guid>();

        foreach (var plannedItem in plan.PlannedItems)
        {
            var fgWorkOrders = workOrders
                .Where(w => w.ProductionPlanItemId == plannedItem.Id
                         || (!w.ProductionPlanItemId.HasValue && plannedItem.WorkOrderId == w.Id))
                .ToList();

            var fgProducedQty = fgWorkOrders.Sum(w => w.ProducedQuantity);
            var itemCode = itemMap.TryGetValue(plannedItem.ItemId, out var fgItem) ? fgItem.ItemCode : plannedItem.ItemName;
            var soNumber = plannedItem.SalesOrderId.HasValue && soMap.TryGetValue(plannedItem.SalesOrderId.Value, out var sNum)
                ? sNum
                : null;

            // FG summary row (indent = 0)
            result.Rows.Add(new ProductionPlanSummaryRowDto
            {
                Indent = 0,
                ItemCode = itemCode ?? string.Empty,
                ItemName = plannedItem.ItemName ?? string.Empty,
                SalesOrderNumber = soNumber,
                BomLevel = 0,
                Qty = plannedItem.PlannedQty,
                ProducedQty = fgProducedQty,
                PendingQty = Math.Max(0, plannedItem.PlannedQty - fgProducedQty),
                DocumentType = null,
                DocumentName = null,
                Status = null
            });

            // FG Work Order document rows (indent = 1)
            foreach (var wo in fgWorkOrders)
            {
                itemMap.TryGetValue(wo.ItemId, out var wItem);
                var woCode = wItem?.ItemCode ?? string.Empty;
                var woName = wItem?.ItemName ?? string.Empty;
                var woSoNumber = wo.SalesOrderId.HasValue && soMap.TryGetValue(wo.SalesOrderId.Value, out var wSo)
                    ? wSo
                    : soNumber;

                result.Rows.Add(new ProductionPlanSummaryRowDto
                {
                    Indent = 1,
                    ItemCode = woCode,
                    ItemName = woName,
                    SalesOrderNumber = woSoNumber,
                    DocumentType = "Work Order",
                    DocumentName = wo.WorkOrderNumber,
                    Status = wo.Status.ToString(),
                    BomLevel = 0,
                    Qty = wo.Quantity,
                    ProducedQty = wo.ProducedQuantity,
                    PendingQty = Math.Max(0, wo.Quantity - wo.ProducedQuantity)
                });
            }

            // Sub-assemblies for this FG: if single planned item, all sub-assemblies belong here
            var subItemsForFg = new List<ProductionPlanMrItem>();
            if (plan.PlannedItems.Count == 1)
            {
                subItemsForFg = subAssemblyMrItems;
            }
            else
            {
                var bomRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<BillOfMaterials, Guid>>();
                var bom = await bomRepo.FindAsync(plannedItem.BomId);
                if (bom != null)
                {
                    var bomItemIds = bom.Items.Select(bi => bi.ItemId).ToHashSet();
                    subItemsForFg = subAssemblyMrItems.Where(s => bomItemIds.Contains(s.ItemId)).ToList();
                }
            }

            foreach (var subItem in subItemsForFg)
            {
                processedSubAssemblyIds.Add(subItem.Id);
                AddSubAssemblyRows(subItem, result.Rows, workOrders, itemMap, soNumber, indent: 1);
            }
        }

        // Add orphan sub-assembly items not mapped to any FG row (per ERPNext production_plan_summary.py)
        var orphanItems = subAssemblyMrItems.Where(s => !processedSubAssemblyIds.Contains(s.Id)).ToList();
        foreach (var orphan in orphanItems)
        {
            AddSubAssemblyRows(orphan, result.Rows, workOrders, itemMap, salesOrderNumber: null, indent: 1);
        }

        return result;
    }

    private static void AddSubAssemblyRows(
        ProductionPlanMrItem subItem,
        List<ProductionPlanSummaryRowDto> rows,
        List<WorkOrder> workOrders,
        Dictionary<Guid, ItemSummaryInfo> itemMap,
        string? salesOrderNumber,
        int indent)
    {
        var linkedWos = workOrders
            .Where(w => w.ProductionPlanSubAssemblyItemId == subItem.Id)
            .ToList();

        var producedQty = linkedWos.Sum(w => w.ProducedQuantity);
        var subQty = subItem.PlannedQty > 0 ? subItem.PlannedQty : subItem.RequiredQty;
        var subItemCode = itemMap.TryGetValue(subItem.ItemId, out var sItem) ? sItem.ItemCode : subItem.ItemName;

        // Sub-assembly summary row
        rows.Add(new ProductionPlanSummaryRowDto
        {
            Indent = indent,
            ItemCode = subItemCode ?? string.Empty,
            ItemName = subItem.ItemName ?? string.Empty,
            SalesOrderNumber = salesOrderNumber,
            BomLevel = 1,
            Qty = subQty,
            ProducedQty = producedQty,
            PendingQty = Math.Max(0, subQty - producedQty),
            DocumentType = null,
            DocumentName = null,
            Status = null
        });

        // Sub-assembly linked Work Order rows (indent + 1)
        foreach (var wo in linkedWos)
        {
            itemMap.TryGetValue(wo.ItemId, out var wItem);
            var woCode = wItem?.ItemCode ?? string.Empty;
            var woName = wItem?.ItemName ?? string.Empty;
            rows.Add(new ProductionPlanSummaryRowDto
            {
                Indent = indent + 1,
                ItemCode = woCode,
                ItemName = woName,
                SalesOrderNumber = salesOrderNumber,
                DocumentType = "Work Order",
                DocumentName = wo.WorkOrderNumber,
                Status = wo.Status.ToString(),
                BomLevel = 1,
                Qty = wo.Quantity,
                ProducedQty = wo.ProducedQuantity,
                PendingQty = Math.Max(0, wo.Quantity - wo.ProducedQuantity)
            });
        }
    }

    /// <summary>
    /// Validates linked Material Requests: must belong to same company, must be submitted,
    /// must be of type Manufacture, and must not be stopped or closed (ERPNext PR #59584 / commit 6e24ef9cce).
    /// </summary>
    private async Task ValidateMaterialRequestsAsync(Guid companyId, IEnumerable<CreateProductionPlanItemDto> items)
    {
        var mrIds = items
            .Where(i => i.MaterialRequestId.HasValue)
            .Select(i => i.MaterialRequestId!.Value)
            .Distinct()
            .ToList();

        if (mrIds.Count == 0) return;

        var mrQuery = await _materialRequestRepository.GetQueryableAsync();
        var mrs = mrQuery.Where(m => mrIds.Contains(m.Id)).ToList();

        foreach (var mr in mrs)
        {
            if (mr.CompanyId != companyId)
            {
                throw new BusinessException(MyERPDomainErrorCodes.CompanyMismatch)
                    .WithData("materialRequestCompany", mr.CompanyId)
                    .WithData("productionPlanCompany", companyId);
            }

            if (mr.Status != Core.DocumentStatus.Submitted)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Material Request {mr.RequestNumber} must be submitted to create Work Orders.");
            }

            if (mr.RequestType != Purchasing.MaterialRequestType.Manufacture)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Material Request {mr.RequestNumber} must be of type Manufacture.");
            }

            if (mr.Status == Core.DocumentStatus.Closed)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Cannot create Work Orders from a stopped or closed Material Request {mr.RequestNumber}.");
            }
        }
    }

    /// <summary>
    /// Validates planned quantity against Sales Order lines, accounting for overproduction allowance
    /// and already planned quantities from other submitted Production Plans (ERPNext PR #60271 / commit f3ba7ca638).
    /// </summary>
    private async Task ValidateSalesOrderPlannedQtyAsync(ProductionPlan plan)
    {
        var plannedMap = new Dictionary<(Guid SalesOrderId, Guid SalesOrderItemId), decimal>();
        foreach (var row in plan.PlannedItems)
        {
            if (row.SalesOrderId.HasValue && row.SalesOrderItemId.HasValue && !row.IsProductBundleItem)
            {
                var key = (row.SalesOrderId.Value, row.SalesOrderItemId.Value);
                plannedMap[key] = (plannedMap.TryGetValue(key, out var val) ? val : 0m) + row.PlannedQty;
            }
        }

        if (!plannedMap.Any())
            return;

        var soIds = plannedMap.Keys.Select(k => k.SalesOrderId).Distinct().ToList();
        var planQuery = await _planRepository.GetQueryableAsync();
        var otherPlans = planQuery
            .Where(p => p.Id != plan.Id &&
                        (p.Status == ProductionPlanStatus.Submitted ||
                         p.Status == ProductionPlanStatus.MaterialRequested ||
                         p.Status == ProductionPlanStatus.InProgress ||
                         p.Status == ProductionPlanStatus.Completed))
            .SelectMany(p => p.PlannedItems)
            .Where(pi => pi.SalesOrderId.HasValue && soIds.Contains(pi.SalesOrderId.Value) && pi.SalesOrderItemId.HasValue && !pi.IsProductBundleItem)
            .ToList();

        var alreadyPlanned = otherPlans
            .GroupBy(pi => (pi.SalesOrderId!.Value, pi.SalesOrderItemId!.Value))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.PlannedQty));

        var settingsRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<ManufacturingSettings, Guid>>();
        var settingsQuery = await settingsRepo.GetQueryableAsync();
        var settings = settingsQuery.FirstOrDefault(s => s.CompanyId == plan.CompanyId);
        var allowance = settings?.OverproductionPercentageForSalesOrder ?? 0m;

        var soItemIds = plannedMap.Keys.Select(k => k.SalesOrderItemId).Distinct().ToList();
        var soRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.SalesOrder, Guid>>();
        var soQuery = await soRepo.WithDetailsAsync(s => s.Items);
        var sos = soQuery.Where(s => soIds.Contains(s.Id)).ToList();
        var soItemMap = sos.SelectMany(s => s.Items).Where(i => soItemIds.Contains(i.Id)).ToDictionary(i => i.Id);

        var itemIds = soItemMap.Values.Select(i => i.ItemId).Distinct().ToList();
        var itemRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Inventory.Entities.Item, Guid>>();
        var itemQuery = await itemRepo.GetQueryableAsync();
        var itemMap = itemQuery.Where(i => itemIds.Contains(i.Id)).ToDictionary(i => i.Id);

        foreach (var (key, plannedQty) in plannedMap)
        {
            if (!soItemMap.TryGetValue(key.SalesOrderItemId, out var soItem))
                continue;

            var alreadyPlannedQty = alreadyPlanned.TryGetValue(key, out var ap) ? ap : 0m;
            var maxAllowed = Math.Round(soItem.StockQty * (1m + allowance / 100m), 4);
            var unplannedQty = Math.Max(0m, Math.Round(maxAllowed - alreadyPlannedQty, 4));

            if (Math.Round(plannedQty, 4) > unplannedQty)
            {
                var so = sos.FirstOrDefault(s => s.Id == key.SalesOrderId);
                itemMap.TryGetValue(soItem.ItemId, out var invItem);
                var itemCode = invItem?.ItemCode ?? soItem.ItemId.ToString();

                throw new BusinessException(MyERPDomainErrorCodes.SalesOrderQtyExceeded)
                    .WithData("plannedQty", plannedQty)
                    .WithData("uom", soItem.StockUom)
                    .WithData("itemCode", itemCode)
                    .WithData("salesOrderNumber", so?.OrderNumber ?? key.SalesOrderId.ToString())
                    .WithData("unplannedQty", unplannedQty);
            }
        }
    }

    /// <summary>
    /// Pulls open Sales Order items with remaining unplanned quantity for planning (ERPNext PR #60271 / commit a58b88b468).
    /// </summary>
    [Authorize(MyERPPermissions.ProductionPlans.Default)]
    public async Task<List<OpenSalesOrderItemDto>> GetOpenSalesOrderItemsAsync(Guid companyId, List<Guid>? salesOrderIds = null)
    {
        var soRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.SalesOrder, Guid>>();
        var soQuery = await soRepo.WithDetailsAsync(s => s.Items);

        var query = soQuery.Where(s => s.CompanyId == companyId &&
                                       s.Status != Core.DocumentStatus.Draft &&
                                       s.Status != Core.DocumentStatus.Cancelled &&
                                       s.Status != Core.DocumentStatus.Closed);

        if (salesOrderIds != null && salesOrderIds.Any())
        {
            query = query.Where(s => salesOrderIds.Contains(s.Id));
        }

        var sos = query.ToList();
        if (!sos.Any())
            return new List<OpenSalesOrderItemDto>();

        var allSoIds = sos.Select(s => s.Id).ToList();

        // Get already planned quantities from active/submitted production plans
        var planQuery = await _planRepository.WithDetailsAsync(p => p.PlannedItems);
        var existingPlans = planQuery
            .Where(p => p.CompanyId == companyId &&
                        (p.Status == ProductionPlanStatus.Submitted ||
                         p.Status == ProductionPlanStatus.MaterialRequested ||
                         p.Status == ProductionPlanStatus.InProgress ||
                         p.Status == ProductionPlanStatus.Completed))
            .SelectMany(p => p.PlannedItems)
            .Where(pi => pi.SalesOrderId.HasValue && allSoIds.Contains(pi.SalesOrderId.Value) && pi.SalesOrderItemId.HasValue && !pi.IsProductBundleItem)
            .ToList();

        var alreadyPlannedMap = existingPlans
            .GroupBy(pi => (pi.SalesOrderId!.Value, pi.SalesOrderItemId!.Value))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.PlannedQty));

        // Get default active BOMs for company
        var bomQuery = await _bomRepository.GetQueryableAsync();
        var activeBoms = bomQuery
            .Where(b => b.CompanyId == companyId && b.IsActive)
            .ToList();
        var defaultBomMap = activeBoms
            .GroupBy(b => b.ItemId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(b => b.IsDefault).First());

        // Get items for code & name lookup
        var allItemIds = sos.SelectMany(s => s.Items).Select(i => i.ItemId).Distinct().ToList();
        var itemRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Inventory.Entities.Item, Guid>>();
        var itemQuery = await itemRepo.GetQueryableAsync();
        var itemMap = itemQuery.Where(i => allItemIds.Contains(i.Id)).ToDictionary(i => i.Id);

        // Get customers for name lookup
        var customerIds = sos.Select(s => s.CustomerId).Distinct().ToList();
        var custRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.Customer, Guid>>();
        var custQuery = await custRepo.GetQueryableAsync();
        var custMap = custQuery.Where(c => customerIds.Contains(c.Id)).ToDictionary(c => c.Id);

        var result = new List<OpenSalesOrderItemDto>();
        foreach (var so in sos)
        {
            custMap.TryGetValue(so.CustomerId, out var customer);

            foreach (var item in so.Items)
            {
                if (item.IsClosed) continue;
                if (item.StockQty <= 0) continue;

                var key = (so.Id, item.Id);
                var alreadyPlannedQty = alreadyPlannedMap.TryGetValue(key, out var ap) ? ap : 0m;
                var unplannedQty = Math.Max(0m, Math.Round(item.StockQty - alreadyPlannedQty, 4));

                if (unplannedQty <= 0m) continue;

                itemMap.TryGetValue(item.ItemId, out var invItem);
                defaultBomMap.TryGetValue(item.ItemId, out var bom);

                result.Add(new OpenSalesOrderItemDto
                {
                    SalesOrderId = so.Id,
                    SalesOrderItemId = item.Id,
                    SalesOrderNumber = so.OrderNumber,
                    CustomerId = so.CustomerId,
                    CustomerName = customer?.Name,
                    TransactionDate = so.OrderDate,
                    DeliveryDate = item.DeliveryDate ?? so.DeliveryDate,
                    ItemId = item.ItemId,
                    ItemCode = invItem?.ItemCode ?? string.Empty,
                    ItemName = invItem?.ItemName ?? item.Description,
                    Description = item.Description,
                    StockUom = item.StockUom,
                    StockQty = item.StockQty,
                    OrderedQty = item.Quantity,
                    AlreadyPlannedQty = alreadyPlannedQty,
                    UnplannedQty = unplannedQty,
                    WarehouseId = item.WarehouseId,
                    BomId = bom?.Id,
                });
            }
        }

        return result;
    }
}


