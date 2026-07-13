using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

public class PickListDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string? PickListNumber { get; set; }
    public string Purpose { get; set; } = null!;
    public Guid? SalesOrderId { get; set; }
    public int Status { get; set; }
    public bool IsFullyTransferred { get; set; }
    public bool IsPartiallyTransferred { get; set; }
    public PickListItemDto[] Items { get; set; } = [];
    public DateTime CreationTime { get; set; }
}

public class PickListItemDto
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public string? ItemName { get; set; }
    public Guid WarehouseId { get; set; }
    public decimal Qty { get; set; }
    public decimal TransferredQty { get; set; }
    public decimal PendingQty { get; set; }
}

public class CreatePickListDto
{
    public Guid CompanyId { get; set; }
    public string Purpose { get; set; } = "Delivery";
    public Guid? SalesOrderId { get; set; }
    public Guid? MaterialRequestId { get; set; }
    public Guid? WorkOrderId { get; set; }
    public CreatePickListItemDto[] Items { get; set; } = [];
}

public class CreatePickListItemDto
{
    public Guid ItemId { get; set; }
    public string? ItemName { get; set; }
    public Guid WarehouseId { get; set; }
    public decimal Qty { get; set; }
    public Guid? BatchId { get; set; }
}

[Authorize(MyERPPermissions.StockEntries.Default)]
public class PickListAppService : ApplicationService
{
    private readonly IRepository<PickList, Guid> _repository;

    public PickListAppService(IRepository<PickList, Guid> repository) => _repository = repository;

    public async Task<PagedResultDto<PickListDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = (await _repository.WithDetailsAsync()).AsQueryable();
        var totalCount = query.Count();
        var items = query.OrderByDescending(p => p.CreationTime)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<PickListDto>(totalCount, items.Select(MapToDto).ToList());
    }

    public async Task<PickListDto> GetAsync(Guid id)
    {
        var pl = (await _repository.WithDetailsAsync()).First(p => p.Id == id);
        return MapToDto(pl);
    }

    [Authorize(MyERPPermissions.StockEntries.Create)]
    public async Task<PickListDto> CreateAsync(CreatePickListDto input)
    {
        var pl = new PickList(GuidGenerator.Create(), input.CompanyId, input.Purpose, CurrentTenant.Id)
        {
            SalesOrderId = input.SalesOrderId,
            MaterialRequestId = input.MaterialRequestId,
            WorkOrderId = input.WorkOrderId,
        };
        foreach (var item in input.Items)
            pl.AddItem(item.ItemId, item.WarehouseId, item.Qty, itemName: item.ItemName, batchId: item.BatchId);
        await _repository.InsertAsync(pl);
        return MapToDto(pl);
    }

    [Authorize(MyERPPermissions.StockEntries.Submit)]
    public async Task<PickListDto> SubmitAsync(Guid id)
    {
        var pl = (await _repository.WithDetailsAsync()).First(p => p.Id == id);
        pl.Submit();
        await _repository.UpdateAsync(pl);
        return MapToDto(pl);
    }

    [Authorize(MyERPPermissions.StockEntries.Cancel)]
    public async Task<PickListDto> CancelAsync(Guid id)
    {
        var pl = (await _repository.WithDetailsAsync()).First(p => p.Id == id);
        pl.Cancel();
        await _repository.UpdateAsync(pl);
        return MapToDto(pl);
    }

    private static PickListDto MapToDto(PickList p) => new()
    {
        Id = p.Id, CompanyId = p.CompanyId, PickListNumber = p.PickListNumber,
        Purpose = p.Purpose, SalesOrderId = p.SalesOrderId, Status = (int)p.Status,
        IsFullyTransferred = p.IsFullyTransferred, IsPartiallyTransferred = p.IsPartiallyTransferred,
        CreationTime = p.CreationTime,
        Items = p.Items.Select(i => new PickListItemDto
        {
            Id = i.Id, ItemId = i.ItemId, ItemName = i.ItemName,
            WarehouseId = i.WarehouseId, Qty = i.Qty,
            TransferredQty = i.TransferredQty, PendingQty = i.PendingQty,
        }).ToArray(),
    };
}
