using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Sales.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

public class BlanketOrderDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string OrderNumber { get; set; } = null!;
    public string OrderType { get; set; } = null!;
    public Guid PartyId { get; set; }
    public string? PartyName { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int Status { get; set; }
    public BlanketOrderItemDto[] Items { get; set; } = [];
    public DateTime CreationTime { get; set; }
}

public class BlanketOrderItemDto
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public string? ItemName { get; set; }
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
    public decimal OrderedQty { get; set; }
    public decimal RemainingQty { get; set; }
}

public class CreateBlanketOrderDto
{
    public Guid CompanyId { get; set; }
    public string OrderType { get; set; } = "Selling";
    public Guid PartyId { get; set; }
    public string? PartyName { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public CreateBlanketOrderItemDto[] Items { get; set; } = [];
}

public class CreateBlanketOrderItemDto
{
    public Guid ItemId { get; set; }
    public string? ItemName { get; set; }
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
}

[Authorize(MyERPPermissions.SalesOrders.Default)]
public class BlanketOrderAppService : ApplicationService
{
    private readonly IRepository<BlanketOrder, Guid> _repository;

    public BlanketOrderAppService(IRepository<BlanketOrder, Guid> repository) => _repository = repository;

    public async Task<PagedResultDto<BlanketOrderDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = (await _repository.WithDetailsAsync()).AsQueryable();
        var totalCount = query.Count();
        var items = query.OrderByDescending(b => b.CreationTime)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<BlanketOrderDto>(totalCount, items.Select(MapToDto).ToList());
    }

    public async Task<BlanketOrderDto> GetAsync(Guid id)
    {
        var bo = (await _repository.WithDetailsAsync()).First(b => b.Id == id);
        return MapToDto(bo);
    }

    [Authorize(MyERPPermissions.SalesOrders.Create)]
    public async Task<BlanketOrderDto> CreateAsync(CreateBlanketOrderDto input)
    {
        var bo = new BlanketOrder(GuidGenerator.Create(), input.CompanyId,
            $"BO-{DateTime.UtcNow:yyyyMMdd-HHmmss}", input.OrderType,
            input.PartyId, input.FromDate, input.ToDate, CurrentTenant.Id)
        { PartyName = input.PartyName };
        foreach (var item in input.Items)
            bo.AddItem(item.ItemId, item.Qty, item.Rate, item.ItemName);
        await _repository.InsertAsync(bo);
        return MapToDto(bo);
    }

    [Authorize(MyERPPermissions.SalesOrders.Submit)]
    public async Task<BlanketOrderDto> SubmitAsync(Guid id)
    {
        var bo = (await _repository.WithDetailsAsync()).First(b => b.Id == id);
        bo.Submit();
        await _repository.UpdateAsync(bo);
        return MapToDto(bo);
    }

    [Authorize(MyERPPermissions.SalesOrders.Cancel)]
    public async Task<BlanketOrderDto> CancelAsync(Guid id)
    {
        var bo = await _repository.GetAsync(id);
        bo.Cancel();
        await _repository.UpdateAsync(bo);
        return MapToDto(bo);
    }

    private static BlanketOrderDto MapToDto(BlanketOrder b) => new()
    {
        Id = b.Id, CompanyId = b.CompanyId, OrderNumber = b.OrderNumber,
        OrderType = b.OrderType, PartyId = b.PartyId, PartyName = b.PartyName,
        FromDate = b.FromDate, ToDate = b.ToDate, Status = (int)b.Status,
        CreationTime = b.CreationTime,
        Items = b.Items.Select(i => new BlanketOrderItemDto
        {
            Id = i.Id, ItemId = i.ItemId, ItemName = i.ItemName,
            Qty = i.Qty, Rate = i.Rate, OrderedQty = i.OrderedQty,
            RemainingQty = i.RemainingQty,
        }).ToArray(),
    };
}
