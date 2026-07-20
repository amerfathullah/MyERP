using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Shared;
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

public class PickAllocationResultDto
{
    public bool HasShortage { get; set; }
    public List<PickAllocationDto> Allocations { get; set; } = new();
}

public class PickAllocationDto
{
    public Guid ItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public decimal RequestedQty { get; set; }
    public decimal AllocatedQty { get; set; }
    public decimal ShortageQty { get; set; }
}

public class PendingTransferDto
{
    public Guid PickListItemId { get; set; }
    public Guid ItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public decimal PendingQty { get; set; }
    public Guid? BatchId { get; set; }
}

[Authorize(MyERPPermissions.StockEntries.Default)]
public class PickListAppService : ApplicationService
{
    private readonly IRepository<PickList, Guid> _repository;

    public PickListAppService(IRepository<PickList, Guid> repository) => _repository = repository;

    public async Task<PagedResultDto<PickListDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = (await _repository.WithDetailsAsync()).AsQueryable();
        if (input.CompanyId.HasValue)
            query = query.Where(p => p.CompanyId == input.CompanyId.Value);

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter;
            query = query.Where(p => p.PickListNumber != null && p.PickListNumber.Contains(filter));
        }

        if (!string.IsNullOrWhiteSpace(input.Status) && Enum.TryParse<DocumentStatus>(input.Status, true, out var status))
            query = query.Where(p => p.Status == status);

        var totalCount = query.Count();
        var items = query.OrderByDescending(p => p.CreationTime)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<PickListDto>(totalCount, items.Select(x => ObjectMapper.Map<PickList, PickListDto>(x)).ToList());
    }

    public async Task<PickListDto> GetAsync(Guid id)
    {
        var pl = (await _repository.WithDetailsAsync()).First(p => p.Id == id);
        return ObjectMapper.Map<PickList, PickListDto>(pl);
    }

    [Authorize(MyERPPermissions.StockEntries.Create)]
    public async Task<PickListDto> CreateAsync(CreatePickListDto input)
    {
        // Validate all items are active
        var itemValidation = LazyServiceProvider.LazyGetRequiredService<MyERP.Inventory.DomainServices.ItemTransactionValidationService>();
        await itemValidation.ValidateItemsForTransactionAsync(input.Items.Select(i => i.ItemId).ToArray());

        var pl = new PickList(GuidGenerator.Create(), input.CompanyId, input.Purpose, CurrentTenant.Id)
        {
            SalesOrderId = input.SalesOrderId,
            MaterialRequestId = input.MaterialRequestId,
            WorkOrderId = input.WorkOrderId,
        };
        foreach (var item in input.Items)
            pl.AddItem(item.ItemId, item.WarehouseId, item.Qty, itemName: item.ItemName, batchId: item.BatchId);
        await _repository.InsertAsync(pl);
        return ObjectMapper.Map<PickList, PickListDto>(pl);
    }

    [Authorize(MyERPPermissions.StockEntries.Submit)]
    public async Task<PickListDto> SubmitAsync(Guid id)
    {
        var pl = (await _repository.WithDetailsAsync()).First(p => p.Id == id);
        pl.Submit();
        await _repository.UpdateAsync(pl);
        return ObjectMapper.Map<PickList, PickListDto>(pl);
    }

    [Authorize(MyERPPermissions.StockEntries.Cancel)]
    public async Task<PickListDto> CancelAsync(Guid id)
    {
        var pl = (await _repository.WithDetailsAsync()).First(p => p.Id == id);
        pl.Cancel();
        await _repository.UpdateAsync(pl);
        return ObjectMapper.Map<PickList, PickListDto>(pl);
    }

    /// <summary>
    /// Allocates stock to pick list items, checking warehouse availability
    /// and deducting quantities already picked by other active pick lists.
    /// </summary>
    [Authorize(MyERPPermissions.StockEntries.Default)]
    public async Task<PickAllocationResultDto> AllocateStockAsync(Guid id)
    {
        var pl = (await _repository.WithDetailsAsync()).First(p => p.Id == id);
        var pickListManager = LazyServiceProvider.LazyGetRequiredService<PickListManager>();
        var result = await pickListManager.AllocateStockAsync(pl);

        return new PickAllocationResultDto
        {
            HasShortage = result.HasShortage,
            Allocations = result.Allocations.Select(a => new PickAllocationDto
            {
                ItemId = a.ItemId,
                WarehouseId = a.WarehouseId,
                RequestedQty = a.RequestedQty,
                AllocatedQty = a.AllocatedQty,
                ShortageQty = a.ShortageQty,
            }).ToList(),
        };
    }

    /// <summary>
    /// Gets pending transfer quantities for creating Stock Entries from this Pick List.
    /// Per DO-NOT: partial transfers are supported — map only pending qty per row.
    /// </summary>
    [Authorize(MyERPPermissions.StockEntries.Default)]
    public async Task<List<PendingTransferDto>> GetPendingTransfersAsync(Guid id)
    {
        var pl = (await _repository.WithDetailsAsync()).First(p => p.Id == id);
        var pickListManager = LazyServiceProvider.LazyGetRequiredService<PickListManager>();
        var pending = pickListManager.GetPendingTransfers(pl);

        return pending.Select(pt => new PendingTransferDto
        {
            PickListItemId = pt.PickListItemId,
            ItemId = pt.ItemId,
            WarehouseId = pt.WarehouseId,
            PendingQty = pt.PendingQty,
            BatchId = pt.BatchId,
        }).ToList();
    }
}

