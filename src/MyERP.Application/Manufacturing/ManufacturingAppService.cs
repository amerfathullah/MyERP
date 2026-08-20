using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
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
    /// <summary>
    /// Gets BOM raw materials for a given FG item (used by subcontracting PO form to auto-populate supplied items).
    /// Returns the default active BOM items for the specified item, or empty if no BOM exists.
    /// Per ERPNext: subcontracting PO auto-loads BOM components as raw materials to supply.
    /// </summary>
    public async Task<SubcontractingBomItemsDto> GetBomItemsForSubcontractingAsync(Guid itemId, Guid companyId, decimal fgQty = 1)
    {
        var query = await _bomRepository.GetQueryableAsync();
        var bom = query
            .Where(b => b.ItemId == itemId && b.CompanyId == companyId && b.IsActive)
            .OrderByDescending(b => b.IsDefault)
            .ThenByDescending(b => b.CreationTime)
            .FirstOrDefault();

        if (bom == null)
            return new SubcontractingBomItemsDto { Items = new(), BomId = null, BomNumber = null };

        // BOM items are auto-included via EF
        var bomQty = bom.Quantity > 0 ? bom.Quantity : 1m;
        var ratio = fgQty / bomQty;

        var itemRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Inventory.Entities.Item, Guid>>();
        var itemIds = bom.Items.Select(i => i.ItemId).Distinct().ToList();
        var itemQuery = await itemRepo.GetQueryableAsync();
        var itemLookup = itemQuery.Where(i => itemIds.Contains(i.Id))
            .Select(i => new { i.Id, i.ItemName, i.ItemCode, i.Uom })
            .ToList();
        var itemNames = itemLookup.ToDictionary(i => i.Id, i => (ItemName: i.ItemName, ItemCode: i.ItemCode, Uom: i.Uom));

        var items = bom.Items.Select(bi =>
        {
            itemNames.TryGetValue(bi.ItemId, out var resolved);
            return new SubcontractingBomItemLineDto
            {
                ItemId = bi.ItemId,
                ItemName = resolved.ItemName ?? bi.ItemName,
                ItemCode = resolved.ItemCode ?? "",
                RequiredQty = Math.Round(bi.Quantity * ratio, 4),
                Rate = bi.Rate,
                Uom = bi.Uom ?? resolved.Uom ?? "Unit",
                SourceWarehouseId = bom.SourceWarehouseId,
            };
        }).ToList();

        return new SubcontractingBomItemsDto
        {
            BomId = bom.Id,
            BomNumber = bom.BomNumber,
            FgItemId = bom.ItemId,
            FgQty = fgQty,
            Items = items,
            SourceWarehouseId = bom.SourceWarehouseId,
        };
    }

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

        if (input.PlannedEndDate.HasValue && input.PlannedEndDate.Value < input.PlannedStartDate)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidDateRange);
        }

        // Validate item eligibility and BOM (per WorkOrderManager domain logic)
        var woManager = LazyServiceProvider.LazyGetRequiredService<Manufacturing.DomainServices.WorkOrderManager>();
        await woManager.ValidateProductionItemAsync(input.ItemId);
        await woManager.ValidateBomAsync(input.BomId, input.ItemId);

        var bom = await _bomRepository.GetAsync(input.BomId, includeDetails: true);

        // Validate active items for FG and all BOM raw materials
        var itemValidation = LazyServiceProvider.LazyGetRequiredService<MyERP.Inventory.DomainServices.ItemTransactionValidationService>();
        await itemValidation.ValidateItemsForTransactionAsync(bom.Items.Select(i => i.ItemId).Concat(new[] { input.ItemId }).Distinct().ToArray());

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

    /// <summary>
    /// Creates Work Orders for ALL SO items that have active BOMs.
    /// Per ERPNext SO "Make Work Orders": one WO per item with pending production qty.
    /// </summary>
    [Authorize(MyERPPermissions.Manufacturing.Create)]
    public async Task<BatchCreateWorkOrdersResultDto> CreateWorkOrdersFromSalesOrderAsync(Guid salesOrderId)
    {
        var soRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.SalesOrder, Guid>>();
        var so = await soRepo.GetAsync(salesOrderId, includeDetails: true);
        var companyId = so.CompanyId;
        var bomQuery = await _bomRepository.GetQueryableAsync();
        var activeBoms = bomQuery.Where(b => b.IsActive && b.CompanyId == companyId).ToList();

        var created = new List<CreatedWorkOrderInfo>();
        var skipped = 0;

        foreach (var item in so.Items)
        {
            var pendingQty = item.Quantity - item.DeliveredQty;
            if (pendingQty <= 0) { skipped++; continue; }

            var bom = activeBoms.FirstOrDefault(b => b.ItemId == item.ItemId && b.IsDefault)
                      ?? activeBoms.FirstOrDefault(b => b.ItemId == item.ItemId);
            if (bom == null) { skipped++; continue; }

            try
            {
                var wo = await CreateWorkOrderFromSalesOrderAsync(salesOrderId, item.ItemId, pendingQty, companyId);
                created.Add(new CreatedWorkOrderInfo
                {
                    WorkOrderId = wo.Id,
                    WorkOrderNumber = wo.WorkOrderNumber,
                    ItemName = item.Description,
                    Quantity = pendingQty
                });
            }
            catch
            {
                skipped++;
            }
        }

        return new BatchCreateWorkOrdersResultDto
        {
            CreatedCount = created.Count,
            SkippedCount = skipped,
            WorkOrders = created
        };
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

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "WorkOrder", wo.Id,
            "Submitted", wo.CompanyId,
            wo.WorkOrderNumber, "Draft", "Submitted", CurrentUser.Id,
            $"Work Order {wo.WorkOrderNumber} submitted", CurrentTenant.Id));

        return ObjectMapper.Map<WorkOrder, WorkOrderDto>(wo);
    }

    /// <summary>
    /// Creates Job Cards for a Work Order from its BOM operations.
    /// Splits by batch_size per operation. Per ERPNext: WO detail → "Create Job Cards" button.
    /// </summary>
    [Authorize(MyERPPermissions.Manufacturing.Create)]
    public async Task<List<WorkOrderJobCardDto>> CreateJobCardsForWorkOrderAsync(Guid workOrderId)
    {
        var wo = await _workOrderRepository.GetAsync(workOrderId, includeDetails: true);
        if (wo.Status == WorkOrderStatus.Draft) throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
            .WithData("documentType", "WorkOrder").WithData("status", "Draft");

        var bom = await _bomRepository.GetAsync(wo.BomId, includeDetails: true);
        if (bom.RoutingId == null)
            throw new BusinessException(MyERPDomainErrorCodes.BomHasNoRouting).WithData("reason", "BOM has no routing configured");

        var routingRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Routing, Guid>>();
        var routing = await routingRepo.GetAsync(bom.RoutingId.Value, includeDetails: true);

        var jobCardManager = LazyServiceProvider.LazyGetRequiredService<Manufacturing.DomainServices.JobCardManager>();
        var jobCards = await jobCardManager.CreateJobCardsFromWorkOrderAsync(wo, routing, CurrentTenant.Id);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "WorkOrder", wo.Id,
            "JobCardsCreated", wo.CompanyId,
            wo.WorkOrderNumber, null, null, CurrentUser.Id,
            $"{jobCards.Length} job cards created", CurrentTenant.Id));

        return jobCards.Select(jc => new WorkOrderJobCardDto
        {
            Id = jc.Id,
            SequenceId = jc.SequenceId,
            OperationName = routing.Operations.FirstOrDefault(o => o.Id == jc.BomOperationId)?.Description,
            ForQuantity = jc.ForQuantity,
            CompletedQty = 0,
            TotalTimeInMins = 0,
            Status = 0,
            PlannedTimeInMins = jc.PlannedTimeInMins
        }).ToList();
    }

    /// <summary>
    /// Batch material readiness check for all active WOs (dashboard use).
    /// Per ERPNext Shop Floor: shows which WOs can start production immediately.
    /// </summary>
    [Authorize(MyERPPermissions.Manufacturing.Default)]
    public async Task<List<WorkOrderMaterialReadinessDto>> GetBatchMaterialReadinessAsync(Guid? companyId)
    {
        var query = await _workOrderRepository.GetQueryableAsync();
        var activeOrders = query
            .Where(wo => wo.Status == WorkOrderStatus.NotStarted || wo.Status == WorkOrderStatus.InProcess)
            .Where(wo => companyId == null || wo.CompanyId == companyId)
            .OrderBy(wo => wo.PlannedStartDate)
            .Take(50)
            .ToList();

        if (!activeOrders.Any())
            return new List<WorkOrderMaterialReadinessDto>();

        var binService = LazyServiceProvider.LazyGetRequiredService<BinService>();
        var result = new List<WorkOrderMaterialReadinessDto>();

        foreach (var wo in activeOrders)
        {
            int totalMaterials = wo.RequiredItems.Count;
            int available = 0;
            int shortage = 0;
            decimal totalShortageValue = 0;

            foreach (var item in wo.RequiredItems)
            {
                var pending = Math.Max(0, item.RequiredQuantity - item.TransferredQuantity);
                if (pending <= 0) { available++; continue; }

                var warehouseId = item.SourceWarehouseId ?? wo.SourceWarehouseId ?? Guid.Empty;
                decimal stockQty = 0;
                try
                {
                    var bin = await binService.GetBalanceAsync(item.ItemId, warehouseId);
                    stockQty = bin.ActualQty;
                }
                catch { /* no bin = zero stock */ }

                if (stockQty >= pending) { available++; }
                else
                {
                    shortage++;
                    totalShortageValue += (pending - stockQty);
                }
            }

            result.Add(new WorkOrderMaterialReadinessDto
            {
                WorkOrderId = wo.Id,
                WorkOrderNumber = wo.WorkOrderNumber ?? "—",
                ItemName = wo.RequiredItems.FirstOrDefault()?.ItemName ?? "—",
                TotalMaterials = totalMaterials,
                MaterialsAvailable = available,
                MaterialsShort = shortage,
                TotalShortageValue = totalShortageValue,
                IsReady = totalMaterials > 0 && shortage == 0,
                IsPartial = available > 0 && shortage > 0,
                HasShortage = shortage > 0,
            });
        }

        return result;
    }

    /// <summary>
    /// Pre-flight material availability check before starting production.
    /// Per ERPNext: shows per-item required vs available qty with shortage highlighting.
    /// Returns list of materials with stock status — enables informed production decisions.
    /// </summary>
    [Authorize(MyERPPermissions.Manufacturing.Default)]
    public async Task<List<MaterialAvailabilityDto>> GetMaterialAvailabilityAsync(Guid workOrderId)
    {
        var wo = await _workOrderRepository.GetAsync(workOrderId, includeDetails: true);
        var binService = LazyServiceProvider.LazyGetRequiredService<BinService>();
        var itemRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Inventory.Entities.Item, Guid>>();

        var result = new List<MaterialAvailabilityDto>();
        foreach (var item in wo.RequiredItems)
        {
            var itemEntity = await itemRepo.FindAsync(item.ItemId);
            var warehouseId = item.SourceWarehouseId ?? wo.SourceWarehouseId ?? Guid.Empty;

            decimal available = 0;
            try
            {
                var bin = await binService.GetBalanceAsync(item.ItemId, warehouseId);
                available = bin.ActualQty;
            }
            catch { /* warehouse may not have bin yet */ }

            var required = item.RequiredQuantity;
            var transferred = item.TransferredQuantity;
            var pending = Math.Max(0, required - transferred);

            result.Add(new MaterialAvailabilityDto
            {
                ItemId = item.ItemId,
                ItemName = itemEntity?.ItemName ?? item.ItemName ?? "—",
                ItemCode = itemEntity?.ItemCode ?? "—",
                RequiredQty = required,
                TransferredQty = transferred,
                PendingQty = pending,
                AvailableQty = available,
                Shortage = Math.Max(0, pending - available),
                HasSufficientStock = available >= pending,
                WarehouseId = warehouseId,
            });
        }

        return result;
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

            // Track consumed qty on Work Order item (per ERPNext work_order.py update_consumed_qty)
            var woItem = wo.RequiredItems.FirstOrDefault(i => i.ItemId == rmItem.ItemId);
            if (woItem != null)
            {
                woItem.ConsumedQuantity += rmItem.Quantity;
            }
        }

        // Receive finished goods (excluding process loss qty — only good items enter stock)
        // When BOM has secondary items with cost allocation: FG gets only its allocated share
        // Per DO-NOT: "Skip FG cost_allocation_per validation (FG + all secondary items MUST total exactly 100%)"
        var bom = await _bomRepository.GetAsync(wo.BomId, includeDetails: true);
        var fgCostAllocationPct = bom.FgCostAllocationPercentage;
        var fgAllocatedCost = totalRmCost * (fgCostAllocationPct / 100m);

        // Per PR #57334: when consumed RM cost is known to be zero (free inputs),
        // the FG rate should also be zero — don't fall back to BOM cost/valuation rate.
        // has_consumption_basis = true when any RM was consumed (even at zero rate)
        var hasConsumptionBasis = consumptionItems.Any();

        // Quality Inspection gate: validate FG item has passed QI before entering stock
        var qiEnforcement = LazyServiceProvider
            .LazyGetRequiredService<QualityInspectionEnforcementService>();
        await qiEnforcement.ValidateForManufactureAsync(wo.Id, wo.ItemId, wo.TenantId);

        if (productionParams.TargetWarehouseId.HasValue && quantity > 0)
        {
            var fgRate = fgAllocatedCost / quantity;

            await _valuationService.CreateLedgerEntryAsync(
                wo.CompanyId, wo.ItemId, productionParams.TargetWarehouseId.Value,
                DateTime.UtcNow, quantity, fgRate,
                voucherType: "WorkOrder", voucherId: wo.Id,
                tenantId: wo.TenantId);

            await _binService.ApplyStockMovementAsync(
                wo.ItemId, productionParams.TargetWarehouseId.Value, quantity, fgAllocatedCost, wo.TenantId);

            await _binService.UpdatePlannedQtyAsync(
                wo.ItemId, productionParams.TargetWarehouseId.Value, -quantity, wo.TenantId);
        }

        // Produce secondary items (co-products, by-products, scrap) when BOM defines them
        // Per gotcha #85: v16 secondary items replace v15 scrap items
        // Per gotcha #518: cost distributed by CostAllocationPercentage
        foreach (var secItem in bom.SecondaryItems)
        {
            var secQty = secItem.EffectiveQuantity * (productionParams.TotalFgQty / bom.Quantity);
            if (secQty <= 0) continue;

            var secCost = totalRmCost * (secItem.CostAllocationPercentage / 100m);
            var secRate = secCost / secQty;

            // Scrap goes to scrap warehouse; co-products/by-products go to FG warehouse
            var secWarehouseId = secItem.SecondaryItemType == SecondaryItemType.Scrap
                ? (wo.ScrapWarehouseId ?? bom.ScrapWarehouseId ?? productionParams.TargetWarehouseId)
                : productionParams.TargetWarehouseId;

            if (secWarehouseId.HasValue)
            {
                await _valuationService.CreateLedgerEntryAsync(
                    wo.CompanyId, secItem.ItemId, secWarehouseId.Value,
                    DateTime.UtcNow, secQty, secRate,
                    voucherType: "WorkOrder", voucherId: wo.Id,
                    tenantId: wo.TenantId);

                await _binService.ApplyStockMovementAsync(
                    secItem.ItemId, secWarehouseId.Value, secQty, secCost, wo.TenantId);
            }
        }

        // Process loss: consumed materials but no FG output for the loss portion
        // The cost of process loss is absorbed into the FG rate (already included in totalRmCost)
        // Per gotcha #442: process_loss_qty = fg_completed_qty × (process_loss_percentage / 100)

        // Notify production managers when WO completes
        if (wo.Status == WorkOrderStatus.Completed)
        {
            try
            {
                var notificationService = LazyServiceProvider
                    .LazyGetRequiredService<Notification.DomainServices.BusinessNotificationService>();
                await notificationService.NotifyWorkOrderCompletedAsync(
                    wo.CompanyId, wo.WorkOrderNumber, wo.ProducedQuantity, wo.TenantId);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to send WO completion notification for {WoNumber}", wo.WorkOrderNumber);
            }
        }

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

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "WorkOrder", wo.Id,
            "Cancelled", wo.CompanyId,
            wo.WorkOrderNumber, wo.Status.ToString(), "Cancelled", CurrentUser.Id,
            $"Work Order {wo.WorkOrderNumber} cancelled", CurrentTenant.Id));

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

    /// <summary>
    /// Creates a Material Transfer for Manufacture Stock Entry from a Work Order.
    /// Transfers raw materials from source warehouse to WIP warehouse.
    /// 
    /// Per ERPNext WO → SE mapper:
    /// - Purpose = "Material Transfer for Manufacture"
    /// - Items come from WO.RequiredItems with pending qty (required - already_transferred)
    /// - Source warehouse from WO item or WO.SourceWarehouse
    /// - Target warehouse = WO.WipWarehouse
    /// - Per DO-NOT: "Allow excess material transfer beyond required_qty - already_transferred_qty"
    ///   (exceptions: returns and "Material Transferred" backflush mode)
    /// </summary>
    [Authorize(MyERPPermissions.Manufacturing.Edit)]
    public async Task<StockEntryResultDto> CreateMaterialTransferForManufactureAsync(Guid workOrderId)
    {
        var wo = await _workOrderRepository.GetAsync(workOrderId, includeDetails: true);

        if (wo.Status is not (WorkOrderStatus.Submitted or WorkOrderStatus.NotStarted or WorkOrderStatus.InProcess))
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("detail", "Work Order must be Submitted or In Process to transfer materials");

        var wipWarehouseId = wo.WipWarehouseId ?? wo.SourceWarehouseId;
        if (!wipWarehouseId.HasValue)
            throw new BusinessException(MyERPDomainErrorCodes.MissingWarehouse)
                .WithData("field", "WIP Warehouse");

        // Calculate pending qty per item: required - already_transferred
        var pendingItems = wo.RequiredItems
            .Where(i => i.RequiredQuantity - i.TransferredQuantity > 0)
            .ToList();

        if (!pendingItems.Any())
            throw new BusinessException(MyERPDomainErrorCodes.AllMaterialsAlreadyTransferred)
                .WithData("reason", "All required materials have already been transferred");

        // Create Stock Entry
        var seRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Inventory.Entities.StockEntry, Guid>>();
        var entry = new Inventory.Entities.StockEntry(
            GuidGenerator.Create(), wo.CompanyId,
            StockEntryType.MaterialTransferForManufacture,
            DateTime.UtcNow.Date, CurrentTenant.Id)
        {
            WorkOrderId = wo.Id,
            EntryNumber = await _numberGenerator.GenerateAsync("SE", wo.CompanyId),
            Notes = $"Material Transfer for Manufacture — WO {wo.WorkOrderNumber}",
        };

        foreach (var item in pendingItems)
        {
            var sourceWarehouseId = item.SourceWarehouseId ?? wo.SourceWarehouseId;
            if (!sourceWarehouseId.HasValue)
                continue;

            var pendingQty = item.RequiredQuantity - item.TransferredQuantity;

            // Validate stock availability before adding to entry
            var balance = await _valuationService.GetCurrentBalanceAsync(item.ItemId, sourceWarehouseId.Value);
            var transferQty = Math.Min(pendingQty, balance.Quantity); // Cap at available
            if (transferQty <= 0) continue;

            entry.AddItem(
                itemId: item.ItemId,
                quantity: transferQty,
                sourceWarehouseId: sourceWarehouseId.Value,
                targetWarehouseId: wipWarehouseId.Value,
                valuationRate: balance.ValuationRate);
        }

        if (!entry.Items.Any())
            throw new BusinessException("MyERP:10018")
                .WithData("reason", "No materials available for transfer (insufficient stock)");

        await seRepo.InsertAsync(entry);

        // Update WO item transferred quantities (per ERPNext update_transferred_qty)
        foreach (var seItem in entry.Items)
        {
            var woItem = wo.RequiredItems.FirstOrDefault(i => i.ItemId == seItem.ItemId);
            if (woItem != null)
            {
                woItem.TransferredQuantity += seItem.Quantity;
            }
        }
        await _workOrderRepository.UpdateAsync(wo);

        return new StockEntryResultDto
        {
            StockEntryId = entry.Id,
            EntryNumber = entry.EntryNumber,
            EntryType = StockEntryType.MaterialTransferForManufacture.ToString(),
            ItemCount = entry.Items.Count,
            TotalValue = entry.TotalIncomingValue,
        };
    }

    /// <summary>
    /// Creates a Manufacture Stock Entry from a Work Order.
    /// Consumes raw materials from WIP warehouse and produces finished goods.
    /// 
    /// Per ERPNext WO → SE mapper (purpose = "Manufacture"):
    /// - Outgoing items (RM): from WIP warehouse, quantities from WO.RequiredItems × ratio
    /// - Incoming item (FG): to FG warehouse, qty = fg_completed_qty
    /// - FG rate = sum(RM consumed value) / fg_qty (cost rollup)
    /// - Process loss absorbed into FG rate (no separate entry)
    /// Per DO-NOT: "Allow multiple different FG items in Manufacture stock entry (only ONE unique FG allowed)"
    /// </summary>
    [Authorize(MyERPPermissions.Manufacturing.Edit)]
    public async Task<StockEntryResultDto> CreateManufactureStockEntryAsync(CreateManufactureStockEntryDto input)
    {
        var wo = await _workOrderRepository.GetAsync(input.WorkOrderId, includeDetails: true);

        if (wo.Status != WorkOrderStatus.InProcess)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("detail", "Work Order must be In Process to record manufacture");

        if (input.FgQuantity <= 0)
            throw new BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", "FG Quantity");

        // Overproduction check
        var settings = await GetManufacturingSettingsAsync(wo.CompanyId);
        var overproductionPct = settings?.OverproductionPercentage ?? 5m;
        var maxAllowed = wo.Quantity * (1 + overproductionPct / 100m);
        if (wo.ProducedQuantity + input.FgQuantity > maxAllowed)
        {
            throw new BusinessException(MyERPDomainErrorCodes.WorkOrderOverproduction)
                .WithData("maxAllowed", maxAllowed)
                .WithData("produced", wo.ProducedQuantity)
                .WithData("attempted", input.FgQuantity);
        }

        var fgWarehouseId = wo.FgWarehouseId ?? wo.WipWarehouseId;
        var wipWarehouseId = wo.WipWarehouseId ?? wo.SourceWarehouseId;
        if (!fgWarehouseId.HasValue)
            throw new BusinessException(MyERPDomainErrorCodes.MissingWarehouse)
                .WithData("field", "FG Warehouse");

        // Create Manufacture Stock Entry
        var seRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Inventory.Entities.StockEntry, Guid>>();
        var entry = new Inventory.Entities.StockEntry(
            GuidGenerator.Create(), wo.CompanyId,
            StockEntryType.Manufacture,
            DateTime.UtcNow.Date, CurrentTenant.Id)
        {
            WorkOrderId = wo.Id,
            EntryNumber = await _numberGenerator.GenerateAsync("SE", wo.CompanyId),
            FgCompletedQty = input.FgQuantity,
            ProcessLossQty = input.ProcessLossQty,
            Notes = $"Manufacture — WO {wo.WorkOrderNumber}",
        };

        // Add RM consumption items (outgoing from WIP)
        decimal totalRmCost = 0;
        var ratio = input.FgQuantity / (wo.Quantity > 0 ? wo.Quantity : 1m);

        foreach (var woItem in wo.RequiredItems)
        {
            var consumeQty = Math.Round(woItem.RequiredQuantity * ratio, 4);
            if (consumeQty <= 0) continue;

            var sourceWh = wipWarehouseId ?? woItem.SourceWarehouseId;
            if (!sourceWh.HasValue) continue;

            var balance = await _valuationService.GetCurrentBalanceAsync(woItem.ItemId, sourceWh.Value);
            var rate = balance.ValuationRate;
            totalRmCost += consumeQty * rate;

            entry.AddItem(
                itemId: woItem.ItemId,
                quantity: consumeQty,
                sourceWarehouseId: sourceWh.Value,
                targetWarehouseId: null,
                valuationRate: rate);
        }

        // Add FG production item (incoming to FG warehouse)
        // FG rate = total RM cost / fg_qty (absorbed costing)
        // Per PR #57334: when consumed cost is known (RM rows present) but zero (free inputs),
        // preserve zero rate — do NOT fall back to BOM cost or existing valuation rate.
        // has_consumption_basis = true when any RM row has a source warehouse
        var hasConsumptionBasis = entry.Items.Any(i => i.SourceWarehouseId.HasValue);
        var fgRate = input.FgQuantity > 0 ? totalRmCost / input.FgQuantity : 0m;
        // If rate is zero but consumption basis exists, it's a REAL zero cost — don't override
        entry.AddItem(
            itemId: wo.ItemId,
            quantity: input.FgQuantity,
            sourceWarehouseId: null,
            targetWarehouseId: fgWarehouseId.Value,
            valuationRate: fgRate);

        await seRepo.InsertAsync(entry);

        return new StockEntryResultDto
        {
            StockEntryId = entry.Id,
            EntryNumber = entry.EntryNumber,
            EntryType = StockEntryType.Manufacture.ToString(),
            ItemCount = entry.Items.Count,
            TotalValue = totalRmCost,
        };
    }

    private async Task<ManufacturingSettings?> GetManufacturingSettingsAsync(Guid companyId)
    {
        var settingsRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<ManufacturingSettings, Guid>>();
        var q = await settingsRepo.GetQueryableAsync();
        return q.FirstOrDefault(s => s.CompanyId == companyId);
    }

    /// <summary>
    /// Returns production cost breakdown for a completed/in-process Work Order.
    /// Per ERPNext BOM Costing: compares actual consumed cost vs BOM standard cost.
    /// </summary>
    public async Task<ProductionCostBreakdownDto> GetProductionCostBreakdownAsync(Guid workOrderId)
    {
        var wo = await _workOrderRepository.GetAsync(workOrderId, includeDetails: true);
        var bom = await _bomRepository.GetAsync(wo.BomId, includeDetails: true);

        // Actual RM cost from SLE entries for this WO (outgoing = consumed)
        var sleRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Inventory.Entities.StockLedgerEntry, Guid>>();
        var sleQuery = await sleRepo.GetQueryableAsync();
        var consumedEntries = sleQuery
            .Where(s => s.VoucherType == "WorkOrder" && s.VoucherId == workOrderId && s.QuantityChange < 0)
            .ToList();
        var totalRmCost = consumedEntries.Sum(s => Math.Abs(s.QuantityChange * s.ValuationRate));

        // Process loss
        var processLossQty = wo.ProcessLossQty;
        var processLossValue = wo.ProducedQuantity > 0 && processLossQty > 0
            ? totalRmCost * (processLossQty / (wo.ProducedQuantity + processLossQty))
            : 0m;

        // Additional costs (operations from BOM)
        var additionalCosts = bom.OperatingCost * (wo.ProducedQuantity / (bom.Quantity > 0 ? bom.Quantity : 1m));

        var totalProductionCost = totalRmCost + additionalCosts;
        var fgUnitCost = wo.ProducedQuantity > 0 ? totalProductionCost / wo.ProducedQuantity : 0m;
        var bomStandardCost = bom.TotalCost / (bom.Quantity > 0 ? bom.Quantity : 1m);
        var costVariance = fgUnitCost - bomStandardCost;
        var costVariancePercent = bomStandardCost > 0 ? costVariance / bomStandardCost * 100 : 0m;

        // Resolve item name
        var itemRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Inventory.Entities.Item, Guid>>();
        var item = await itemRepo.FindAsync(wo.ItemId);

        return new ProductionCostBreakdownDto
        {
            WorkOrderId = wo.Id,
            WorkOrderNumber = wo.WorkOrderNumber,
            ItemId = wo.ItemId,
            ItemName = item?.ItemName,
            ProducedQty = wo.ProducedQuantity,
            TotalRmCost = Math.Round(totalRmCost, 4),
            ProcessLossQty = processLossQty,
            ProcessLossValue = Math.Round(processLossValue, 4),
            AdditionalCosts = Math.Round(additionalCosts, 4),
            TotalProductionCost = Math.Round(totalProductionCost, 4),
            FgUnitCost = Math.Round(fgUnitCost, 4),
            BomStandardCost = Math.Round(bomStandardCost, 4),
            CostVariance = Math.Round(costVariance, 4),
            CostVariancePercent = Math.Round(costVariancePercent, 2),
        };
    }

    /// <summary>
    /// Returns Job Cards linked to a Work Order for operations progress display.
    /// Per ERPNext: WO detail shows per-operation Job Card status with completed qty and time.
    /// </summary>
    public async Task<PagedResultDto<WorkOrderJobCardDto>> GetWorkOrderJobCardsAsync(Guid workOrderId)
    {
        var jobCardRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<JobCard, Guid>>();
        var query = (await jobCardRepo.GetQueryableAsync())
            .Where(j => j.WorkOrderId == workOrderId)
            .OrderBy(j => j.SequenceId);

        var totalCount = query.Count();
        var items = query.Take(50).ToList();

        var result = items.Select(jc => new WorkOrderJobCardDto
        {
            Id = jc.Id,
            SequenceId = jc.SequenceId,
            OperationId = jc.OperationId,
            Status = (int)jc.Status,
            ForQuantity = jc.ForQuantity,
            CompletedQty = jc.CompletedQty,
            TotalTimeInMins = jc.TotalTimeInMins,
            PlannedTimeInMins = jc.PlannedTimeInMins,
        }).ToList();

        return new PagedResultDto<WorkOrderJobCardDto>(totalCount, result);
    }

    /// <summary>
    /// Creates a Disassemble Stock Entry that reverses a prior Manufacture Stock Entry.
    /// Breaks finished goods back into raw material components.
    /// Per ERPNext stock_entry.py: disassembly reverses production — FG goes out, RM comes back in.
    /// Per DO-NOT: "Use source_stock_entry from a different Work Order for Disassembly (cross-WO guard)"
    /// </summary>
    [Authorize(MyERPPermissions.Manufacturing.Edit)]
    public async Task<DisassemblyResultDto> CreateDisassemblyStockEntryAsync(CreateDisassemblyDto input)
    {
        var wo = await _workOrderRepository.GetAsync(input.WorkOrderId, includeDetails: true);

        // WO must be Completed or InProcess (has produced qty to disassemble)
        if (wo.ProducedQuantity <= 0)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("documentType", "WorkOrder")
                .WithData("reason", "No production to disassemble");

        // Per DO-NOT: "Allow Disassemble qty to exceed source manufacture qty minus already-disassembled"
        var availableForDisassembly = wo.ProducedQuantity - wo.DisassembledQuantity;
        if (input.Quantity > availableForDisassembly)
            throw new BusinessException(MyERPDomainErrorCodes.WorkOrderOverproduction)
                .WithData("maxAllowed", availableForDisassembly)
                .WithData("attempted", input.Quantity);

        if (input.Quantity <= 0)
            throw new BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", "quantity");

        // Create Disassemble Stock Entry
        var seRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Inventory.Entities.StockEntry, Guid>>();
        var numberGen = LazyServiceProvider.LazyGetRequiredService<IDocumentNumberGenerator>();
        var entryNumber = await numberGen.GenerateAsync("SE", wo.CompanyId);

        var entry = new Inventory.Entities.StockEntry(
            GuidGenerator.Create(), wo.CompanyId, Inventory.StockEntryType.Disassemble, DateTime.UtcNow, wo.TenantId);
        entry.EntryNumber = entryNumber;
        entry.WorkOrderId = wo.Id;
        entry.SourceStockEntryId = input.SourceStockEntryId;
        entry.FgCompletedQty = input.Quantity;

        // Scale factor = disassemble_qty / source_fg_qty (proportional RM return)
        var scaleFactor = wo.Quantity > 0 ? input.Quantity / wo.Quantity : 0m;

        // FG item goes OUT (source warehouse = FG warehouse) — finished goods consumed
        var fgWarehouse = wo.FgWarehouseId ?? wo.SourceWarehouseId;
        if (fgWarehouse.HasValue)
        {
            entry.AddItem(wo.ItemId, input.Quantity, sourceWarehouseId: fgWarehouse.Value, targetWarehouseId: null);
            var lastFgItem = entry.Items.Last();
            lastFgItem.IsFinishedItem = true;
        }

        // RM items come back IN (target warehouse = source warehouse) — proportional to scale factor
        foreach (var rmItem in wo.RequiredItems)
        {
            var returnQty = Math.Round(rmItem.RequiredQuantity * scaleFactor, 4);
            if (returnQty <= 0) continue;

            var rmSourceWarehouse = rmItem.SourceWarehouseId ?? wo.SourceWarehouseId;
            if (!rmSourceWarehouse.HasValue) continue;

            entry.AddItem(rmItem.ItemId, returnQty, sourceWarehouseId: null, targetWarehouseId: rmSourceWarehouse.Value);
        }

        // Validate using domain service
        var seManager = LazyServiceProvider.LazyGetRequiredService<Inventory.DomainServices.StockEntryManager>();
        seManager.ValidateDisassembleItems(entry, null); // Source entry validation deferred

        // Submit + Post atomically
        entry.Submit();
        entry.Post();

        // Create SLE entries: FG stock-out, RM stock-in
        foreach (var seItem in entry.Items)
        {
            if (seItem.IsFinishedItem && seItem.SourceWarehouseId.HasValue)
            {
                // FG goes out
                await _valuationService.CreateLedgerEntryAsync(
                    wo.CompanyId, seItem.ItemId, seItem.SourceWarehouseId.Value,
                    DateTime.UtcNow, -seItem.Quantity, 0,
                    voucherType: "StockEntry", voucherId: entry.Id, tenantId: wo.TenantId);
                await _binService.ApplyStockMovementAsync(
                    seItem.ItemId, seItem.SourceWarehouseId.Value, -seItem.Quantity, 0, wo.TenantId);
            }
            else if (!seItem.IsFinishedItem && seItem.TargetWarehouseId.HasValue)
            {
                // RM comes back in
                await _valuationService.CreateLedgerEntryAsync(
                    wo.CompanyId, seItem.ItemId, seItem.TargetWarehouseId.Value,
                    DateTime.UtcNow, seItem.Quantity, 0,
                    voucherType: "StockEntry", voucherId: entry.Id, tenantId: wo.TenantId);
                await _binService.ApplyStockMovementAsync(
                    seItem.ItemId, seItem.TargetWarehouseId.Value, seItem.Quantity, 0, wo.TenantId);
            }
        }

        // Update WO disassembled quantity
        wo.RecordDisassembly(input.Quantity);
        await _workOrderRepository.UpdateAsync(wo);

        await seRepo.InsertAsync(entry, autoSave: true);

        return new DisassemblyResultDto
        {
            StockEntryId = entry.Id,
            EntryNumber = entryNumber,
            DisassembledQty = input.Quantity,
            ItemCount = entry.Items.Count,
            RemainingDisassemblable = wo.ProducedQuantity - wo.DisassembledQuantity
        };
    }

    public async Task<ProductionScheduleDto> GetProductionScheduleAsync(Guid companyId)
    {
        var queryable = await _workOrderRepository.GetQueryableAsync();
        var orders = queryable
            .Where(wo => wo.CompanyId == companyId && wo.Status >= WorkOrderStatus.Submitted && wo.Status <= WorkOrderStatus.Stopped)
            .OrderBy(wo => wo.PlannedStartDate)
            .Take(100)
            .ToList();

        var itemIds = orders.Select(wo => wo.ItemId).Distinct().ToList();
        var itemRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Inventory.Entities.Item, Guid>>();
        var itemQueryable = await itemRepo.GetQueryableAsync();
        var itemNames = itemQueryable.Where(i => itemIds.Contains(i.Id))
            .Select(i => new { i.Id, i.ItemName }).ToList()
            .ToDictionary(x => x.Id, x => x.ItemName);

        var today = DateTime.UtcNow.Date;
        var items = orders.Select(wo =>
        {
            var isOverdue = wo.PlannedEndDate.HasValue && wo.PlannedEndDate.Value.Date < today && wo.Status < WorkOrderStatus.Completed;
            var daysOverdue = isOverdue ? (int)(today - wo.PlannedEndDate!.Value.Date).TotalDays : 0;

            return new ProductionScheduleItemDto
            {
                WorkOrderId = wo.Id,
                WorkOrderNumber = wo.WorkOrderNumber ?? wo.Id.ToString()[..8],
                ItemName = itemNames.GetValueOrDefault(wo.ItemId, wo.ItemId.ToString()[..8]),
                Quantity = wo.Quantity,
                ProducedQuantity = wo.ProducedQuantity,
                PercentComplete = wo.Quantity > 0 ? Math.Min(100, wo.ProducedQuantity / wo.Quantity * 100) : 0,
                Status = (int)wo.Status,
                StatusLabel = GetWoStatusLabel((int)wo.Status),
                PlannedStartDate = wo.PlannedStartDate,
                PlannedEndDate = wo.PlannedEndDate,
                ActualStartDate = wo.ActualStartDate,
                ActualEndDate = wo.ActualEndDate,
                IsOverdue = isOverdue,
                DaysOverdue = daysOverdue,
                StatusColor = GetWoStatusColor((int)wo.Status)
            };
        }).ToList();

        return new ProductionScheduleDto
        {
            Items = items,
            TotalOrders = items.Count,
            NotStarted = items.Count(i => i.Status == (int)WorkOrderStatus.NotStarted),
            InProcess = items.Count(i => i.Status == (int)WorkOrderStatus.InProcess),
            Completed = items.Count(i => i.Status == (int)WorkOrderStatus.Completed),
            Overdue = items.Count(i => i.IsOverdue),
            OverallCompletionRate = items.Count > 0 ? items.Average(i => i.PercentComplete) : 0
        };
    }

    public async Task<MaterialShortageAcrossOrdersDto> GetMaterialShortageAcrossOrdersAsync(Guid companyId)
    {
        var queryable = await _workOrderRepository.GetQueryableAsync();
        var activeOrders = queryable
            .Where(wo => wo.CompanyId == companyId && wo.Status >= WorkOrderStatus.Submitted && wo.Status <= WorkOrderStatus.InProcess)
            .ToList();

        if (!activeOrders.Any())
            return new MaterialShortageAcrossOrdersDto();

        var allRequiredItems = activeOrders
            .SelectMany(wo => wo.RequiredItems.Select(ri => new
            {
                ri.ItemId,
                PendingQty = Math.Max(0, ri.RequiredQuantity - ri.TransferredQuantity),
                WoNumber = wo.WorkOrderNumber ?? wo.Id.ToString()[..8]
            }))
            .Where(x => x.PendingQty > 0)
            .ToList();

        if (!allRequiredItems.Any())
            return new MaterialShortageAcrossOrdersDto();

        var itemIds = allRequiredItems.Select(x => x.ItemId).Distinct().ToList();

        var itemRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Inventory.Entities.Item, Guid>>();
        var binRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Inventory.Entities.Bin, Guid>>();

        var itemQ = await itemRepo.GetQueryableAsync();
        var itemLookup = itemQ.Where(i => itemIds.Contains(i.Id))
            .Select(i => new { i.Id, i.ItemCode, i.ItemName, i.StandardBuyingPrice })
            .ToList().ToDictionary(x => x.Id);

        var binQ = await binRepo.GetQueryableAsync();
        var stockByItem = binQ.Where(b => itemIds.Contains(b.ItemId))
            .GroupBy(b => b.ItemId)
            .Select(g => new { ItemId = g.Key, Available = g.Sum(b => b.ActualQty) })
            .ToList().ToDictionary(x => x.ItemId, x => x.Available);

        var grouped = allRequiredItems
            .GroupBy(x => x.ItemId)
            .Select(g =>
            {
                var totalRequired = g.Sum(x => x.PendingQty);
                var available = stockByItem.GetValueOrDefault(g.Key, 0);
                var shortage = Math.Max(0, totalRequired - available);
                var item = itemLookup.GetValueOrDefault(g.Key);
                return new MaterialShortageItemDto
                {
                    ItemId = g.Key,
                    ItemCode = item?.ItemCode ?? "—",
                    ItemName = item?.ItemName ?? "—",
                    TotalRequired = totalRequired,
                    TotalAvailable = available,
                    ShortageQty = shortage,
                    AffectedWorkOrders = g.Select(x => x.WoNumber).Distinct().Count(),
                    MostUrgentWO = g.First().WoNumber
                };
            })
            .Where(x => x.ShortageQty > 0)
            .OrderByDescending(x => x.ShortageQty)
            .ToList();

        return new MaterialShortageAcrossOrdersDto
        {
            Items = grouped,
            TotalItemsShort = grouped.Count,
            TotalAffectedOrders = grouped.Sum(x => x.AffectedWorkOrders),
            TotalShortageValue = grouped.Sum(x =>
                x.ShortageQty * (itemLookup.GetValueOrDefault(x.ItemId)?.StandardBuyingPrice ?? 0))
        };
    }

    private static string GetWoStatusLabel(int status) => status switch
    {
        0 => "Draft", 1 => "Submitted", 2 => "Not Started",
        3 => "In Process", 4 => "Completed", 5 => "Stopped",
        6 => "Cancelled", _ => "Unknown"
    };

    private static string GetWoStatusColor(int status) => status switch
    {
        0 => "secondary", 1 => "info", 2 => "warning",
        3 => "primary", 4 => "success", 5 => "danger",
        6 => "dark", _ => "secondary"
    };
}

