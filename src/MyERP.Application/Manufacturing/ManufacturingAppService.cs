using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.DomainServices;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Manufacturing.Entities;
using MyERP.Manufacturing.Services;
using MyERP.Permissions;
using MyERP.Purchasing;
using MyERP.Purchasing.DTOs;
using MyERP.Purchasing.Entities;
using MyERP.Shared;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Manufacturing;

[Authorize(MyERPPermissions.Manufacturing.Default)]
[RemoteService(false)] // Explicit controller in HttpApi project handles routing
public class ManufacturingAppService : ApplicationService, IManufacturingAppService
{
    private readonly IRepository<BillOfMaterials, Guid> _bomRepository;
    private readonly IRepository<WorkOrder, Guid> _workOrderRepository;
    private readonly IRepository<MaterialRequest, Guid> _materialRequestRepository;
    private readonly IDocumentNumberGenerator _numberGenerator;
    private readonly StockValuationService _valuationService;
    private readonly BinService _binService;

    public ManufacturingAppService(
        IRepository<BillOfMaterials, Guid> bomRepository,
        IRepository<WorkOrder, Guid> workOrderRepository,
        IRepository<MaterialRequest, Guid> materialRequestRepository,
        IDocumentNumberGenerator numberGenerator,
        StockValuationService valuationService,
        BinService binService)
    {
        _bomRepository = bomRepository;
        _workOrderRepository = workOrderRepository;
        _materialRequestRepository = materialRequestRepository;
        _numberGenerator = numberGenerator;
        _valuationService = valuationService;
        _binService = binService;
    }

    // === BOM ===

    public async Task<BomDto> GetBomAsync(Guid id)
    {
        var bom = await _bomRepository.GetAsync(id, includeDetails: true);
        var dto = ObjectMapper.Map<BillOfMaterials, BomDto>(bom);

        // Resolve finished good item name (entity only stores ItemId)
        var itemRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Inventory.Entities.Item, Guid>>();
        var fgItem = await itemRepo.FindAsync(bom.ItemId);
        if (fgItem != null)
            dto.ItemName = fgItem.ItemName;

        return dto;
    }

    public async Task<PagedResultDto<BomDto>> GetBomListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = await _bomRepository.GetQueryableAsync();
        if (input.CompanyId.HasValue)
            query = query.Where(b => b.CompanyId == input.CompanyId.Value);
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var f = input.Filter;
            query = query.Where(b => b.BomNumber.Contains(f));
        }
        var totalCount = query.Count();
        var items = query.OrderByDescending(b => b.CreationTime)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<BomDto>(totalCount, items.Select(ObjectMapper.Map<BillOfMaterials, BomDto>).ToList());
    }

    [Authorize(MyERPPermissions.Manufacturing.Create)]
    public async Task<BomDto> CreateBomAsync(CreateBomDto input)
    {
        Check.NotDefaultOrNull<Guid>(input.CompanyId, nameof(input.CompanyId));
        Check.NotDefaultOrNull<Guid>(input.ItemId, nameof(input.ItemId));
        if (input.Items == null || input.Items.Count == 0)
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustHaveItems);

        var number = await _numberGenerator.GenerateAsync("BOM", input.CompanyId);
        var bom = new BillOfMaterials(GuidGenerator.Create(), input.CompanyId, number, input.ItemId, CurrentTenant.Id)
        {
            Quantity = input.Quantity,
            Uom = input.Uom,
            IsDefault = input.IsDefault,
            SourceWarehouseId = input.SourceWarehouseId,
            TargetWarehouseId = input.TargetWarehouseId,
            RoutingId = input.RoutingId,
            ScrapWarehouseId = input.ScrapWarehouseId,
            ProcessLossPercentage = input.ProcessLossPercentage,
        };

        foreach (var item in input.Items)
        {
            bom.Items.Add(new BomItem(
                GuidGenerator.Create(), bom.Id, item.ItemId, item.ItemName, item.Quantity, item.Rate)
            { Uom = item.Uom });
        }

        // Add operations (sorted by SequenceId to enforce monotonic insertion)
        foreach (var op in input.Operations.OrderBy(o => o.SequenceId))
        {
            var bomOp = new BomOperation(GuidGenerator.Create(), bom.Id, op.OperationId,
                op.SequenceId, op.TimeInMins, op.WorkstationId, CurrentTenant.Id)
            {
                BatchSize = op.BatchSize,
                FixedTime = op.FixedTime,
                Description = op.Description,
                IsSubcontracted = op.IsSubcontracted,
            };
            if (op.WorkstationHourRate > 0)
                bomOp.CalculateCost(op.WorkstationHourRate);
            bom.AddOperation(bomOp);
        }

        // Add secondary items (co-products, by-products, scrap)
        foreach (var si in input.SecondaryItems ?? Enumerable.Empty<CreateBomSecondaryItemDto>())
        {
            var secondaryItem = new BomSecondaryItem(
                GuidGenerator.Create(), bom.Id, si.ItemId, si.SecondaryItemType, si.Quantity, CurrentTenant.Id)
            {
                ItemName = si.ItemName,
                StockUom = si.StockUom,
                Rate = si.Rate,
                CostAllocationPercentage = si.CostAllocationPercentage,
                ProcessLossPercentage = si.ProcessLossPercentage,
                WarehouseId = si.WarehouseId,
            };
            bom.AddSecondaryItem(secondaryItem);
        }

        // Validate cost allocation totals 100%
        if (bom.SecondaryItems.Any(s => s.CostAllocationPercentage > 0) && !bom.ValidateCostAllocation())
            throw new BusinessException(MyERPDomainErrorCodes.SecondaryItemCostAllocationInvalid);

        bom.RecalculateCost();

        await _bomRepository.InsertAsync(bom);
        return ObjectMapper.Map<BillOfMaterials, BomDto>(bom);
    }

    [Authorize(MyERPPermissions.Manufacturing.Edit)]
    public async Task<BomDto> UpdateBomAsync(Guid id, CreateBomDto input)
    {
        var bom = await _bomRepository.GetAsync(id, includeDetails: true);

        bom.Quantity = input.Quantity;
        bom.Uom = input.Uom;
        bom.IsDefault = input.IsDefault;
        bom.SourceWarehouseId = input.SourceWarehouseId;
        bom.TargetWarehouseId = input.TargetWarehouseId;
        bom.RoutingId = input.RoutingId;
        bom.ScrapWarehouseId = input.ScrapWarehouseId;
        bom.ProcessLossPercentage = input.ProcessLossPercentage;

        bom.Items.Clear();
        foreach (var item in input.Items)
        {
            bom.Items.Add(new BomItem(
                GuidGenerator.Create(), bom.Id, item.ItemId, item.ItemName, item.Quantity, item.Rate)
            { Uom = item.Uom });
        }

        bom.Operations.Clear();
        foreach (var op in input.Operations.OrderBy(o => o.SequenceId))
        {
            var bomOp = new BomOperation(GuidGenerator.Create(), bom.Id, op.OperationId,
                op.SequenceId, op.TimeInMins, op.WorkstationId, CurrentTenant.Id)
            {
                BatchSize = op.BatchSize,
                FixedTime = op.FixedTime,
                Description = op.Description,
                IsSubcontracted = op.IsSubcontracted,
            };
            if (op.WorkstationHourRate > 0)
                bomOp.CalculateCost(op.WorkstationHourRate);
            bom.AddOperation(bomOp);
        }

        bom.SecondaryItems.Clear();
        foreach (var si in input.SecondaryItems ?? Enumerable.Empty<CreateBomSecondaryItemDto>())
        {
            var secondaryItem = new BomSecondaryItem(
                GuidGenerator.Create(), bom.Id, si.ItemId, si.SecondaryItemType, si.Quantity, CurrentTenant.Id)
            {
                ItemName = si.ItemName,
                StockUom = si.StockUom,
                Rate = si.Rate,
                CostAllocationPercentage = si.CostAllocationPercentage,
                ProcessLossPercentage = si.ProcessLossPercentage,
                WarehouseId = si.WarehouseId,
            };
            bom.AddSecondaryItem(secondaryItem);
        }

        bom.RecalculateCost();
        await _bomRepository.UpdateAsync(bom);
        return ObjectMapper.Map<BillOfMaterials, BomDto>(bom);
    }

    [Authorize(MyERPPermissions.Manufacturing.Delete)]
    public async Task DeleteBomAsync(Guid id)
    {
        // Guard: cannot delete BOM used by active Work Orders
        var woQuery = await _workOrderRepository.GetQueryableAsync();
        var hasActiveWO = woQuery.Any(wo =>
            wo.BomId == id
            && wo.Status != WorkOrderStatus.Draft
            && wo.Status != WorkOrderStatus.Cancelled
            && wo.Status != WorkOrderStatus.Completed);

        if (hasActiveWO)
        {
            throw new Volo.Abp.BusinessException("MyERP:10009")
                .WithData("reason", "BOM is used by active Work Orders. Cancel or complete them first.");
        }

        await _bomRepository.DeleteAsync(id);
    }

    /// <summary>
    /// Recalculate BOM cost and propagate to all parent BOMs that use this as a sub-assembly.
    /// Per ERPNext: when Item Price changes or sub-assembly cost changes, all referencing BOMs
    /// must update their costs bottom-up (leaf BOMs first, then parents).
    /// Per DO-NOT: concurrency=1 for BOM Update Log.
    /// </summary>
    [Authorize(MyERPPermissions.Manufacturing.Edit)]
    public async Task<BomDto> UpdateBomCostAsync(Guid bomId)
    {
        var bom = await _bomRepository.GetAsync(bomId, includeDetails: true);
        bom.RecalculateCost();
        await _bomRepository.UpdateAsync(bom);

        // Propagate cost change to all parent BOMs that reference this BOM
        var propagationService = LazyServiceProvider.LazyGetRequiredService<MyERP.Manufacturing.DomainServices.BomCostPropagationService>();
        await propagationService.UpdateCostAndPropagateAsync(bomId);

        return ObjectMapper.Map<BillOfMaterials, BomDto>(bom);
    }

    // === Work Order ===

    public async Task<WorkOrderDto> GetWorkOrderAsync(Guid id)
    {
        var wo = await _workOrderRepository.GetAsync(id, includeDetails: true);
        return ObjectMapper.Map<WorkOrder, WorkOrderDto>(wo);
    }

    public async Task<PagedResultDto<WorkOrderDto>> GetWorkOrderListAsync(GetWorkOrderListDto input)
    {
        var query = await _workOrderRepository.GetQueryableAsync();
        if (input.Status.HasValue)
            query = query.Where(w => w.Status == input.Status.Value);
        if (input.CompanyId.HasValue)
            query = query.Where(w => w.CompanyId == input.CompanyId.Value);
        if (input.FromDate.HasValue)
            query = query.Where(w => w.PlannedStartDate >= input.FromDate.Value);
        if (input.ToDate.HasValue)
            query = query.Where(w => w.PlannedStartDate <= input.ToDate.Value);
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var f = input.Filter;
            query = query.Where(w => w.WorkOrderNumber.Contains(f));
        }

        var totalCount = query.Count();
        query = SortingHelper.ApplySorting(query, input.Sorting,
            q => q.OrderByDescending(w => w.CreationTime),
            ("workOrderNumber", w => w.WorkOrderNumber),
            ("quantity", w => w.Quantity),
            ("producedQuantity", w => w.ProducedQuantity));
        var items = query.Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        var dtos = items.Select(ObjectMapper.Map<WorkOrder, WorkOrderDto>).ToList();

        // Resolve item names
        var itemIds = dtos.Select(d => d.ItemId).Distinct().ToList();
        if (itemIds.Count > 0)
        {
            var itemRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Inventory.Entities.Item, Guid>>();
            var itemQuery = await itemRepo.GetQueryableAsync();
            var itemNames = itemQuery.Where(i => itemIds.Contains(i.Id))
                .Select(i => new { i.Id, i.ItemName }).ToList()
                .ToDictionary(i => i.Id, i => i.ItemName);
            foreach (var dto in dtos)
            {
                if (itemNames.TryGetValue(dto.ItemId, out var name))
                    dto.ItemName = name;
            }
        }

        return new PagedResultDto<WorkOrderDto>(totalCount, dtos);
    }

    [Authorize(MyERPPermissions.Manufacturing.Create)]
    public async Task<WorkOrderDto> CreateWorkOrderAsync(CreateWorkOrderDto input)
    {
        Check.NotDefaultOrNull<Guid>(input.CompanyId, nameof(input.CompanyId));
        Check.NotDefaultOrNull<Guid>(input.ItemId, nameof(input.ItemId));
        Check.NotDefaultOrNull<Guid>(input.BomId, nameof(input.BomId));
        if (input.Quantity <= 0)
            throw new BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", "Quantity");

        // Validate item eligibility and BOM (per WorkOrderManager domain logic)
        var woManager = LazyServiceProvider.LazyGetRequiredService<Manufacturing.DomainServices.WorkOrderManager>();
        await woManager.ValidateProductionItemAsync(input.ItemId);
        await woManager.ValidateBomAsync(input.BomId, input.ItemId);

        var number = await _numberGenerator.GenerateAsync("WO", input.CompanyId);
        var wo = new WorkOrder(GuidGenerator.Create(), input.CompanyId, number, input.ItemId, input.BomId, input.Quantity, CurrentTenant.Id)
        {
            SalesOrderId = input.SalesOrderId,
            SourceWarehouseId = input.SourceWarehouseId,
            WipWarehouseId = input.WipWarehouseId,
            FgWarehouseId = input.FgWarehouseId,
            Notes = input.Notes,
        };
        wo.SetPlannedDates(input.PlannedStartDate, input.PlannedEndDate);

        // Populate required items from BOM
        var bom = await _bomRepository.GetAsync(input.BomId, includeDetails: true);
        var multiplier = input.Quantity / (bom.Quantity > 0 ? bom.Quantity : 1);
        foreach (var bi in bom.Items)
        {
            wo.RequiredItems.Add(new WorkOrderItem(
                GuidGenerator.Create(), wo.Id, bi.ItemId, bi.ItemName, bi.Quantity * multiplier)
            { SourceWarehouseId = bi.SourceWarehouseId ?? bom.SourceWarehouseId });
        }

        await _workOrderRepository.InsertAsync(wo);
        return ObjectMapper.Map<WorkOrder, WorkOrderDto>(wo);
    }

    /// <summary>
    /// Creates a Work Order from a Sales Order (make-to-order manufacturing).
    /// Auto-resolves the default BOM for the item.
    /// </summary>
    [Authorize(MyERPPermissions.Manufacturing.Create)]
    public async Task<WorkOrderDto> CreateWorkOrderFromSalesOrderAsync(
        Guid salesOrderId, Guid itemId, decimal quantity, Guid companyId)
    {
        // Find the default active BOM for this item
        var bomQuery = await _bomRepository.GetQueryableAsync();
        var bom = bomQuery.FirstOrDefault(b =>
            b.ItemId == itemId && b.IsActive && b.IsDefault && b.CompanyId == companyId)
            ?? bomQuery.FirstOrDefault(b => b.ItemId == itemId && b.IsActive && b.CompanyId == companyId);

        if (bom == null)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("message", $"No active BOM found for item {itemId}");
        }

        var input = new CreateWorkOrderDto
        {
            ItemId = itemId,
            BomId = bom.Id,
            Quantity = quantity,
            CompanyId = companyId,
            SalesOrderId = salesOrderId,
            SourceWarehouseId = bom.SourceWarehouseId,
            FgWarehouseId = bom.TargetWarehouseId,
            PlannedStartDate = DateTime.UtcNow,
        };

        return await CreateWorkOrderAsync(input);
    }

    [Authorize(MyERPPermissions.Manufacturing.Delete)]
    public async Task DeleteWorkOrderAsync(Guid id)
    {
        await _workOrderRepository.DeleteAsync(id);
    }

    [Authorize(MyERPPermissions.Manufacturing.Edit)]
    public async Task<WorkOrderDto> SubmitWorkOrderAsync(Guid id)
    {
        var wo = await _workOrderRepository.GetAsync(id, includeDetails: true);
        wo.Submit();
        await _workOrderRepository.UpdateAsync(wo);
        return ObjectMapper.Map<WorkOrder, WorkOrderDto>(wo);
    }

    [Authorize(MyERPPermissions.Manufacturing.Edit)]
    public async Task<WorkOrderDto> StartWorkOrderAsync(Guid id)
    {
        var wo = await _workOrderRepository.GetAsync(id, includeDetails: true);
        wo.Start();
        await _workOrderRepository.UpdateAsync(wo);
        return ObjectMapper.Map<WorkOrder, WorkOrderDto>(wo);
    }

    [Authorize(MyERPPermissions.Manufacturing.Edit)]
    public async Task<WorkOrderDto> RecordProductionAsync(Guid id, decimal quantity, decimal processLossQty = 0)
    {
        var wo = await _workOrderRepository.GetAsync(id, includeDetails: true);

        // Validate posting period is not frozen/closed before creating SLE entries
        var postingOrchestrator = LazyServiceProvider
            .LazyGetRequiredService<Accounting.DomainServices.DocumentPostingOrchestrator>();
        await postingOrchestrator.ValidatePostingPeriodAsync(wo.CompanyId, DateTime.UtcNow, "WorkOrder");

        // Read overproduction percentage + backflush method from ManufacturingSettings
        var settingsRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<ManufacturingSettings, Guid>>();
        var settingsQuery = await settingsRepo.GetQueryableAsync();
        var settings = settingsQuery.FirstOrDefault(s => s.CompanyId == wo.CompanyId);
        var overproductionPct = settings?.OverproductionPercentage ?? 5m;
        var backflushMethod = settings?.BackflushRawMaterialsBasedOn ?? "BOM";

        // Use WorkOrderProductionService for validated production parameters
        // Per gotcha #524: fg_completed_qty = produce_qty + process_loss_qty (exact balance)
        var productionService = LazyServiceProvider
            .LazyGetRequiredService<Manufacturing.Services.WorkOrderProductionService>();
        var productionParams = productionService.ValidateAndGetProductionParams(
            wo, quantity, processLossQty, overproductionPct);

        // Record production on the entity (uses produce_qty only for ProducedQuantity tracking)
        wo.RecordProduction(quantity, overproductionPercentage: overproductionPct);

        // Calculate RM consumption using domain service (proper DDD delegation)
        // Per gotcha #453: MIN-capped formula for BOM mode
        // Per gotcha #491: caps at available (transferred - consumed) for MaterialTransferred mode
        var consumptionItems = productionService.CalculateRawMaterialConsumption(
            wo, productionParams.TotalFgQty, backflushMethod);

        // Validate sufficient stock for all raw materials BEFORE consuming any
        foreach (var rmItem in consumptionItems)
        {
            var warehouseId = rmItem.SourceWarehouseId ?? productionParams.SourceWarehouseId;
            if (warehouseId.HasValue)
            {
                var balance = await _valuationService.GetCurrentBalanceAsync(rmItem.ItemId, warehouseId.Value);
                if (balance.Quantity < rmItem.Quantity)
                {
                    throw new BusinessException("MyERP:10008")
                        .WithData("itemId", rmItem.ItemId)
                        .WithData("warehouseId", warehouseId.Value)
                        .WithData("required", rmItem.Quantity)
                        .WithData("available", balance.Quantity);
                }
            }
        }

        // Issue raw materials and track total cost for FG valuation
        decimal totalRmCost = 0;
        foreach (var rmItem in consumptionItems)
        {
            var warehouseId = rmItem.SourceWarehouseId ?? productionParams.SourceWarehouseId;
            if (warehouseId.HasValue)
            {
                var rmBalance = await _valuationService.GetCurrentBalanceAsync(rmItem.ItemId, warehouseId.Value);
                var rmRate = rmBalance.ValuationRate;
                totalRmCost += rmItem.Quantity * rmRate;

                await _valuationService.CreateLedgerEntryAsync(
                    wo.CompanyId, rmItem.ItemId, warehouseId.Value,
                    DateTime.UtcNow, -rmItem.Quantity, rmRate,
                    voucherType: "WorkOrder", voucherId: wo.Id,
                    tenantId: wo.TenantId);

                await _binService.ApplyStockMovementAsync(
                    rmItem.ItemId, warehouseId.Value, -rmItem.Quantity, -(rmItem.Quantity * rmRate), wo.TenantId);

                await _binService.UpdateReservedQtyForProductionAsync(
                    rmItem.ItemId, warehouseId.Value, -rmItem.Quantity, wo.TenantId);
            }
        }

        // Receive finished goods (excluding process loss qty — only good items enter stock)
        // FG rate = total consumed RM cost / produced qty (per ERPNext absorption costing)
        if (productionParams.TargetWarehouseId.HasValue && quantity > 0)
        {
            var fgRate = totalRmCost / quantity;

            await _valuationService.CreateLedgerEntryAsync(
                wo.CompanyId, wo.ItemId, productionParams.TargetWarehouseId.Value,
                DateTime.UtcNow, quantity, fgRate,
                voucherType: "WorkOrder", voucherId: wo.Id,
                tenantId: wo.TenantId);

            await _binService.ApplyStockMovementAsync(
                wo.ItemId, productionParams.TargetWarehouseId.Value, quantity, totalRmCost, wo.TenantId);

            await _binService.UpdatePlannedQtyAsync(
                wo.ItemId, productionParams.TargetWarehouseId.Value, -quantity, wo.TenantId);
        }

        // Process loss: consumed materials but no FG output for the loss portion
        // The cost of process loss is absorbed into the FG rate (already included in totalRmCost)
        // Per gotcha #442: process_loss_qty = fg_completed_qty × (process_loss_percentage / 100)

        await _workOrderRepository.UpdateAsync(wo);
        return ObjectMapper.Map<WorkOrder, WorkOrderDto>(wo);
    }

    [Authorize(MyERPPermissions.Manufacturing.Edit)]
    public async Task<WorkOrderDto> StopWorkOrderAsync(Guid id)
    {
        var wo = await _workOrderRepository.GetAsync(id, includeDetails: true);
        wo.Stop();
        await _workOrderRepository.UpdateAsync(wo);
        return ObjectMapper.Map<WorkOrder, WorkOrderDto>(wo);
    }

    [Authorize(MyERPPermissions.Manufacturing.Edit)]
    public async Task<WorkOrderDto> UnstopWorkOrderAsync(Guid id)
    {
        var wo = await _workOrderRepository.GetAsync(id, includeDetails: true);
        wo.Unstop();
        await _workOrderRepository.UpdateAsync(wo);
        return ObjectMapper.Map<WorkOrder, WorkOrderDto>(wo);
    }

    [Authorize(MyERPPermissions.Manufacturing.Edit)]
    public async Task<WorkOrderDto> CancelWorkOrderAsync(Guid id)
    {
        var wo = await _workOrderRepository.GetAsync(id, includeDetails: true);

        // Per DO-NOT: "Cancel Work Order when submitted Stock Entries exist (must cancel all SEs first)"
        var seRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Inventory.Entities.StockEntry, Guid>>();
        var queryable = await seRepo.GetQueryableAsync();
        var hasSubmittedSE = queryable.Any(se =>
            se.WorkOrderId == wo.Id &&
            se.Status != Core.DocumentStatus.Draft &&
            se.Status != Core.DocumentStatus.Cancelled);
        if (hasSubmittedSE)
        {
            throw new BusinessException(MyERPDomainErrorCodes.CannotCancelWithSubmittedDependents)
                .WithData("documentType", "WorkOrder")
                .WithData("dependent", "StockEntry");
        }

        // Reverse stock entries: return consumed RM and remove produced FG
        if (wo.ProducedQuantity > 0)
        {
            var productionRatio = wo.Quantity > 0 ? wo.ProducedQuantity / wo.Quantity : 0m;

            // Return raw materials to source warehouse
            foreach (var item in wo.RequiredItems)
            {
                var issueQty = Math.Round(item.RequiredQuantity * productionRatio, 4);
                var warehouseId = item.SourceWarehouseId ?? wo.SourceWarehouseId;
                if (issueQty > 0 && warehouseId.HasValue)
                {
                    await _valuationService.CreateLedgerEntryAsync(
                        wo.CompanyId, item.ItemId, warehouseId.Value,
                        DateTime.UtcNow, issueQty, 0, // Positive = stock back in
                        voucherType: "WorkOrder", voucherId: wo.Id,
                        tenantId: wo.TenantId);

                    await _binService.ApplyStockMovementAsync(
                        item.ItemId, warehouseId.Value, issueQty, 0, wo.TenantId);
                }
            }

            // Remove finished goods from FG warehouse
            if (wo.FgWarehouseId.HasValue)
            {
                await _valuationService.CreateLedgerEntryAsync(
                    wo.CompanyId, wo.ItemId, wo.FgWarehouseId.Value,
                    DateTime.UtcNow, -wo.ProducedQuantity, 0, // Negative = stock out
                    voucherType: "WorkOrder", voucherId: wo.Id,
                    tenantId: wo.TenantId);

                await _binService.ApplyStockMovementAsync(
                    wo.ItemId, wo.FgWarehouseId.Value, -wo.ProducedQuantity, 0, wo.TenantId);
            }
        }

        wo.Cancel();
        await _workOrderRepository.UpdateAsync(wo);
        return ObjectMapper.Map<WorkOrder, WorkOrderDto>(wo);
    }

    /// <summary>
    /// Creates a Material Consumption for Manufacture stock entry — records actual RM usage
    /// separately from the Manufacture SE (which only produces FG).
    /// Per DO-NOT: "Consume raw materials twice when material_consumption ON"
    /// Per DO-NOT: "Skip Material Consumption separation when get_rm_cost_from_consumption_entry is enabled"
    /// </summary>
    [Authorize(MyERPPermissions.Manufacturing.Edit)]
    public async Task<MaterialConsumptionResultDto> CreateMaterialConsumptionAsync(CreateMaterialConsumptionDto input)
    {
        var wo = await _workOrderRepository.GetAsync(input.WorkOrderId, includeDetails: true);

        // WO must be in process
        if (wo.Status != WorkOrderStatus.InProcess)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("detail", "Work Order must be In Process to record material consumption");

        // Check MaterialConsumption setting is enabled for the company
        var settingsRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<ManufacturingSettings, Guid>>();
        var settingsQuery = await settingsRepo.GetQueryableAsync();
        var settings = settingsQuery.FirstOrDefault(s => s.CompanyId == wo.CompanyId);
        if (settings == null || !settings.MaterialConsumption)
            throw new BusinessException("MyERP:10014")
                .WithData("reason", "Material Consumption setting is not enabled for this company");

        // Per DO-NOT: check for existing submitted Material Consumption SE (no double consumption)
        var seRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Inventory.Entities.StockEntry, Guid>>();
        var seQuery = await seRepo.GetQueryableAsync();
        var existingConsumption = seQuery.Any(se =>
            se.WorkOrderId == wo.Id &&
            se.EntryType == StockEntryType.MaterialConsumptionForManufacture &&
            se.Status != Core.DocumentStatus.Draft &&
            se.Status != Core.DocumentStatus.Cancelled);
        if (existingConsumption)
            throw new BusinessException("MyERP:10015")
                .WithData("reason", "A submitted Material Consumption entry already exists for this Work Order. Cancel it before creating a new one.");

        // Validate items are non-empty
        if (input.Items == null || !input.Items.Any())
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustHaveItems);

        // Create the consumption Stock Entry
        var numberGenerator = LazyServiceProvider.LazyGetRequiredService<IDocumentNumberGenerator>();
        var entry = new Inventory.Entities.StockEntry(
            GuidGenerator.Create(), wo.CompanyId,
            StockEntryType.MaterialConsumptionForManufacture,
            DateTime.UtcNow.Date, CurrentTenant.Id);

        entry.WorkOrderId = wo.Id;
        entry.EntryNumber = await numberGenerator.GenerateAsync("SE", wo.CompanyId);
        entry.Notes = $"Material Consumption for Work Order {wo.WorkOrderNumber ?? wo.Id.ToString()}";

        // Validate and add items
        decimal totalConsumedValue = 0;
        var wipWarehouseId = wo.WipWarehouseId ?? wo.SourceWarehouseId;

        foreach (var item in input.Items)
        {
            // Validate consumption qty does not exceed transferred qty per gotcha #491
            var woItem = wo.RequiredItems.FirstOrDefault(ri => ri.ItemId == item.ItemId);
            if (woItem != null && item.Quantity > woItem.TransferredQuantity)
            {
                throw new BusinessException("MyERP:10016")
                    .WithData("itemId", item.ItemId)
                    .WithData("consumed", item.Quantity)
                    .WithData("transferred", woItem.TransferredQuantity);
            }

            var warehouseId = item.WarehouseId ?? wipWarehouseId;
            if (!warehouseId.HasValue)
                throw new BusinessException(MyERPDomainErrorCodes.MissingWarehouse)
                    .WithData("field", "Consumption Warehouse");

            // Get current valuation rate for the item
            var balance = await _valuationService.GetCurrentBalanceAsync(item.ItemId, warehouseId.Value);
            var rate = balance.ValuationRate;
            totalConsumedValue += item.Quantity * rate;

            entry.AddItem(
                itemId: item.ItemId,
                quantity: item.Quantity,
                sourceWarehouseId: warehouseId.Value,
                targetWarehouseId: null,
                valuationRate: rate);
        }

        // Submit and post immediately (consumption is a direct stock-out)
        entry.Submit();
        entry.Post();

        // Create SLE entries for each consumed item (stock-out from WIP/source)
        foreach (var seItem in entry.Items)
        {
            if (seItem.SourceWarehouseId.HasValue)
            {
                var rate = seItem.ValuationRate ?? 0m;
                await _valuationService.CreateLedgerEntryAsync(
                    wo.CompanyId, seItem.ItemId, seItem.SourceWarehouseId.Value,
                    DateTime.UtcNow, -seItem.Quantity, rate,
                    voucherType: "StockEntry", voucherId: entry.Id,
                    tenantId: wo.TenantId);

                await _binService.ApplyStockMovementAsync(
                    seItem.ItemId, seItem.SourceWarehouseId.Value,
                    -seItem.Quantity, -(seItem.Quantity * rate), wo.TenantId);
            }
        }

        await seRepo.InsertAsync(entry, autoSave: true);

        return new MaterialConsumptionResultDto
        {
            StockEntryId = entry.Id,
            EntryNumber = entry.EntryNumber,
            TotalConsumedValue = totalConsumedValue,
            ItemCount = input.Items.Count
        };
    }

    /// <summary>
    /// Creates a Material Request for raw materials not yet transferred to the work order.
    /// Maps to ERPNext's "Create Material Request" button on Work Order.
    /// </summary>
    [Authorize(MyERPPermissions.MaterialRequests.Create)]
    public async Task<MaterialRequestDto> CreateMaterialRequestFromWorkOrderAsync(Guid workOrderId)
    {
        var wo = await _workOrderRepository.GetAsync(workOrderId, includeDetails: true);

        if (wo.Status is not (WorkOrderStatus.Submitted or WorkOrderStatus.NotStarted or WorkOrderStatus.InProcess))
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        // Only include items with outstanding quantity
        var pendingItems = wo.RequiredItems
            .Where(i => i.RequiredQuantity - i.TransferredQuantity > 0)
            .ToList();

        if (!pendingItems.Any())
            throw new BusinessException(MyERPDomainErrorCodes.MaterialRequestAlreadyExists);

        var number = await _numberGenerator.GenerateAsync("MR", wo.CompanyId);
        var mr = new MaterialRequest(
            GuidGenerator.Create(), wo.CompanyId, number,
            MaterialRequestType.MaterialTransfer, DateTime.UtcNow, CurrentTenant.Id)
        {
            WorkOrderId = wo.Id,
            SourceWarehouseId = wo.SourceWarehouseId,
            TargetWarehouseId = wo.WipWarehouseId,
        };

        foreach (var item in pendingItems)
        {
            var pendingQty = item.RequiredQuantity - item.TransferredQuantity;
            mr.AddItem(item.ItemId, item.ItemName, pendingQty, "Unit", item.SourceWarehouseId);
        }

        await _materialRequestRepository.InsertAsync(mr);

        return ObjectMapper.Map<MaterialRequest, MaterialRequestDto>(mr);
    }
}

