using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.DomainServices;
using MyERP.Inventory.DomainServices;
using MyERP.Manufacturing.Entities;
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
        return MapBomToDto(bom);
    }

    public async Task<PagedResultDto<BomDto>> GetBomListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = await _bomRepository.GetQueryableAsync();
        if (input.CompanyId.HasValue)
            query = query.Where(b => b.CompanyId == input.CompanyId.Value);
        var totalCount = query.Count();
        var items = query.OrderByDescending(b => b.CreationTime)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<BomDto>(totalCount, items.Select(MapBomToDto).ToList());
    }

    [Authorize(MyERPPermissions.Manufacturing.Create)]
    public async Task<BomDto> CreateBomAsync(CreateBomDto input)
    {
        var number = await _numberGenerator.GenerateAsync("BOM", input.CompanyId);
        var bom = new BillOfMaterials(GuidGenerator.Create(), input.CompanyId, number, input.ItemId, CurrentTenant.Id)
        {
            Quantity = input.Quantity,
            Uom = input.Uom,
            IsDefault = input.IsDefault,
            SourceWarehouseId = input.SourceWarehouseId,
            TargetWarehouseId = input.TargetWarehouseId,
        };

        foreach (var item in input.Items)
        {
            bom.Items.Add(new BomItem(
                GuidGenerator.Create(), bom.Id, item.ItemId, item.ItemName, item.Quantity, item.Rate)
            { Uom = item.Uom });
        }
        bom.RecalculateCost();

        await _bomRepository.InsertAsync(bom);
        return MapBomToDto(bom);
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

    // === Work Order ===

    public async Task<WorkOrderDto> GetWorkOrderAsync(Guid id)
    {
        var wo = await _workOrderRepository.GetAsync(id, includeDetails: true);
        return MapWoToDto(wo);
    }

    public async Task<PagedResultDto<WorkOrderDto>> GetWorkOrderListAsync(GetWorkOrderListDto input)
    {
        var query = await _workOrderRepository.GetQueryableAsync();
        if (input.Status.HasValue)
            query = query.Where(w => w.Status == input.Status.Value);
        if (input.CompanyId.HasValue)
            query = query.Where(w => w.CompanyId == input.CompanyId.Value);
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var f = input.Filter.ToLower();
            query = query.Where(w => w.WorkOrderNumber.ToLower().Contains(f));
        }

        var totalCount = query.Count();
        var items = query.OrderByDescending(w => w.CreationTime)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<WorkOrderDto>(totalCount, items.Select(MapWoToDto).ToList());
    }

    [Authorize(MyERPPermissions.Manufacturing.Create)]
    public async Task<WorkOrderDto> CreateWorkOrderAsync(CreateWorkOrderDto input)
    {
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
        return MapWoToDto(wo);
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
        return MapWoToDto(wo);
    }

    [Authorize(MyERPPermissions.Manufacturing.Edit)]
    public async Task<WorkOrderDto> StartWorkOrderAsync(Guid id)
    {
        var wo = await _workOrderRepository.GetAsync(id, includeDetails: true);
        wo.Start();
        await _workOrderRepository.UpdateAsync(wo);
        return MapWoToDto(wo);
    }

    [Authorize(MyERPPermissions.Manufacturing.Edit)]
    public async Task<WorkOrderDto> RecordProductionAsync(Guid id, decimal quantity)
    {
        var wo = await _workOrderRepository.GetAsync(id, includeDetails: true);

        // Validate posting period is not frozen/closed before creating SLE entries
        var postingOrchestrator = LazyServiceProvider
            .LazyGetRequiredService<Accounting.DomainServices.DocumentPostingOrchestrator>();
        await postingOrchestrator.ValidatePostingPeriodAsync(wo.CompanyId, DateTime.UtcNow, "WorkOrder");

        // Read overproduction percentage from ManufacturingSettings (per-company)
        var settingsRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<ManufacturingSettings, Guid>>();
        var settingsQuery = await settingsRepo.GetQueryableAsync();
        var settings = settingsQuery.FirstOrDefault(s => s.CompanyId == wo.CompanyId);
        var overproductionPct = settings?.OverproductionPercentage ?? 5m; // Default 5% per ERPNext
        wo.RecordProduction(quantity, overproductionPercentage: overproductionPct);

        // Validate sufficient stock for raw materials before consuming
        var productionRatio = quantity / wo.Quantity;
        foreach (var item in wo.RequiredItems)
        {
            var issueQty = Math.Round(item.RequiredQuantity * productionRatio, 4);
            var warehouseId = item.SourceWarehouseId ?? wo.SourceWarehouseId;
            if (issueQty > 0 && warehouseId.HasValue)
            {
                var balance = await _valuationService.GetCurrentBalanceAsync(item.ItemId, warehouseId.Value);
                if (balance.Quantity < issueQty)
                {
                    throw new Volo.Abp.BusinessException("MyERP:10008")
                        .WithData("itemId", item.ItemId)
                        .WithData("warehouseId", warehouseId.Value)
                        .WithData("required", issueQty)
                        .WithData("available", balance.Quantity);
                }
            }
        }

        // Issue raw materials from source warehouse (proportional to production qty)
        foreach (var item in wo.RequiredItems)
        {
            var issueQty = Math.Round(item.RequiredQuantity * productionRatio, 4);
            var warehouseId = item.SourceWarehouseId ?? wo.SourceWarehouseId;
            if (issueQty > 0 && warehouseId.HasValue)
            {
                // Create SLE for raw material consumption (stock-out from source warehouse)
                await _valuationService.CreateLedgerEntryAsync(
                    wo.CompanyId, item.ItemId, warehouseId.Value,
                    DateTime.UtcNow, -issueQty, 0,
                    voucherType: "WorkOrder", voucherId: wo.Id,
                    tenantId: wo.TenantId);

                await _binService.ApplyStockMovementAsync(
                    item.ItemId, warehouseId.Value, -issueQty, 0, wo.TenantId);

                // Release production reservation
                await _binService.UpdateReservedQtyForProductionAsync(
                    item.ItemId, warehouseId.Value, -issueQty, wo.TenantId);
            }
        }

        // Receive finished goods into FG warehouse
        if (wo.FgWarehouseId.HasValue)
        {
            await _valuationService.CreateLedgerEntryAsync(
                wo.CompanyId, wo.ItemId, wo.FgWarehouseId.Value,
                DateTime.UtcNow, quantity, 0,
                voucherType: "WorkOrder", voucherId: wo.Id,
                tenantId: wo.TenantId);

            await _binService.ApplyStockMovementAsync(
                wo.ItemId, wo.FgWarehouseId.Value, quantity, 0, wo.TenantId);

            // Reduce planned qty (FG was planned, now produced)
            await _binService.UpdatePlannedQtyAsync(
                wo.ItemId, wo.FgWarehouseId.Value, -quantity, wo.TenantId);
        }

        await _workOrderRepository.UpdateAsync(wo);
        return MapWoToDto(wo);
    }

    [Authorize(MyERPPermissions.Manufacturing.Edit)]
    public async Task<WorkOrderDto> StopWorkOrderAsync(Guid id)
    {
        var wo = await _workOrderRepository.GetAsync(id, includeDetails: true);
        wo.Stop();
        await _workOrderRepository.UpdateAsync(wo);
        return MapWoToDto(wo);
    }

    [Authorize(MyERPPermissions.Manufacturing.Edit)]
    public async Task<WorkOrderDto> CancelWorkOrderAsync(Guid id)
    {
        var wo = await _workOrderRepository.GetAsync(id, includeDetails: true);

        // Reverse stock entries: return consumed RM and remove produced FG
        if (wo.ProducedQuantity > 0)
        {
            var productionRatio = wo.ProducedQuantity / wo.Quantity;

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
        return MapWoToDto(wo);
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

        return new MaterialRequestDto
        {
            Id = mr.Id,
            RequestNumber = mr.RequestNumber,
            RequestType = mr.RequestType,
            Status = mr.Status,
            RequestDate = mr.RequestDate,
            CompanyId = mr.CompanyId,
            WorkOrderId = mr.WorkOrderId,
            CreationTime = mr.CreationTime,
            Items = mr.Items.Select(i => new MaterialRequestItemDto
            {
                Id = i.Id, ItemId = i.ItemId, ItemName = i.ItemName,
                Quantity = i.Quantity, Uom = i.Uom, WarehouseId = i.WarehouseId,
            }).ToList(),
        };
    }

    private static BomDto MapBomToDto(BillOfMaterials b) => new()
    {
        Id = b.Id,
        BomNumber = b.BomNumber,
        ItemId = b.ItemId,
        Quantity = b.Quantity,
        Uom = b.Uom,
        CompanyId = b.CompanyId,
        IsActive = b.IsActive,
        IsDefault = b.IsDefault,
        TotalMaterialCost = b.TotalMaterialCost,
        TotalCost = b.TotalCost,
        CreationTime = b.CreationTime,
        Items = b.Items.Select(i => new BomItemDto
        {
            Id = i.Id, ItemId = i.ItemId, ItemName = i.ItemName,
            Quantity = i.Quantity, Uom = i.Uom, Rate = i.Rate, Amount = i.Amount,
        }).ToList(),
    };

    private static WorkOrderDto MapWoToDto(WorkOrder w) => new()
    {
        Id = w.Id,
        WorkOrderNumber = w.WorkOrderNumber,
        Status = w.Status,
        ItemId = w.ItemId,
        BomId = w.BomId,
        Quantity = w.Quantity,
        ProducedQuantity = w.ProducedQuantity,
        MaterialTransferred = w.MaterialTransferred,
        PercentComplete = w.PercentComplete,
        CompanyId = w.CompanyId,
        SalesOrderId = w.SalesOrderId,
        PlannedStartDate = w.PlannedStartDate,
        PlannedEndDate = w.PlannedEndDate,
        ActualStartDate = w.ActualStartDate,
        ActualEndDate = w.ActualEndDate,
        Notes = w.Notes,
        CreationTime = w.CreationTime,
        RequiredItems = w.RequiredItems.Select(i => new WorkOrderItemDto
        {
            Id = i.Id, ItemId = i.ItemId, ItemName = i.ItemName,
            RequiredQuantity = i.RequiredQuantity,
            TransferredQuantity = i.TransferredQuantity,
            ConsumedQuantity = i.ConsumedQuantity,
        }).ToList(),
    };
}
