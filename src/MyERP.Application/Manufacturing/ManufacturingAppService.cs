using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.DomainServices;
using MyERP.Manufacturing.Entities;
using MyERP.Permissions;
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
    private readonly IDocumentNumberGenerator _numberGenerator;

    public ManufacturingAppService(
        IRepository<BillOfMaterials, Guid> bomRepository,
        IRepository<WorkOrder, Guid> workOrderRepository,
        IDocumentNumberGenerator numberGenerator)
    {
        _bomRepository = bomRepository;
        _workOrderRepository = workOrderRepository;
        _numberGenerator = numberGenerator;
    }

    // === BOM ===

    public async Task<BomDto> GetBomAsync(Guid id)
    {
        var bom = await _bomRepository.GetAsync(id, includeDetails: true);
        return MapBomToDto(bom);
    }

    public async Task<PagedResultDto<BomDto>> GetBomListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _bomRepository.GetQueryableAsync();
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
            PlannedStartDate = input.PlannedStartDate,
            PlannedEndDate = input.PlannedEndDate,
            Notes = input.Notes,
        };

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
        wo.RecordProduction(quantity);
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
        wo.Cancel();
        await _workOrderRepository.UpdateAsync(wo);
        return MapWoToDto(wo);
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
