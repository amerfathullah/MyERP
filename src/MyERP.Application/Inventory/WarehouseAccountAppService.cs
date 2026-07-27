using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

/// <summary>
/// Manages warehouse-to-GL-account mappings for perpetual inventory.
/// Per ERPNext: each warehouse can have specific GL accounts for stock balance,
/// SRBNB (stock received but not billed), SDBNB (stock delivered but not billed),
/// and stock adjustment. Falls back to company defaults when not configured.
/// </summary>
[Authorize(MyERPPermissions.WarehouseAccounts.Default)]
public class WarehouseAccountAppService : ApplicationService
{
    private readonly IRepository<WarehouseAccount, Guid> _repository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;

    public WarehouseAccountAppService(
        IRepository<WarehouseAccount, Guid> repository,
        IRepository<Warehouse, Guid> warehouseRepository)
    {
        _repository = repository;
        _warehouseRepository = warehouseRepository;
    }

    public async Task<ListResultDto<WarehouseAccountDto>> GetListAsync(Guid companyId)
    {
        var queryable = await _repository.GetQueryableAsync();
        var items = queryable
            .Where(x => x.CompanyId == companyId)
            .OrderBy(x => x.WarehouseId)
            .ToList();

        // Resolve warehouse names
        var warehouseIds = items.Select(x => x.WarehouseId).Distinct().ToList();
        var whQ = await _warehouseRepository.GetQueryableAsync();
        var warehouseNames = whQ.Where(w => warehouseIds.Contains(w.Id))
            .Select(w => new { w.Id, w.Name }).ToList()
            .ToDictionary(w => w.Id, w => w.Name);

        return new ListResultDto<WarehouseAccountDto>(
            items.Select(e => new WarehouseAccountDto
            {
                Id = e.Id,
                WarehouseId = e.WarehouseId,
                WarehouseName = warehouseNames.TryGetValue(e.WarehouseId, out var wn) ? wn : null,
                CompanyId = e.CompanyId,
                AccountId = e.AccountId,
                StockReceivedButNotBilledAccountId = e.StockReceivedButNotBilledAccountId,
                StockDeliveredButNotBilledAccountId = e.StockDeliveredButNotBilledAccountId,
                StockAdjustmentAccountId = e.StockAdjustmentAccountId
            }).ToList());
    }

    [Authorize(MyERPPermissions.WarehouseAccounts.Create)]
    public async Task<WarehouseAccountDto> SaveAsync(CreateWarehouseAccountDto input)
    {
        // Upsert: find existing by warehouse+company, update or create
        var queryable = await _repository.GetQueryableAsync();
        var existing = queryable.FirstOrDefault(x =>
            x.WarehouseId == input.WarehouseId && x.CompanyId == input.CompanyId);

        if (existing != null)
        {
            existing.AccountId = input.AccountId;
            existing.StockReceivedButNotBilledAccountId = input.StockReceivedButNotBilledAccountId;
            existing.StockDeliveredButNotBilledAccountId = input.StockDeliveredButNotBilledAccountId;
            existing.StockAdjustmentAccountId = input.StockAdjustmentAccountId;
            await _repository.UpdateAsync(existing);
            return MapToDto(existing);
        }

        var entity = new WarehouseAccount(
            GuidGenerator.Create(),
            input.WarehouseId,
            input.CompanyId,
            input.AccountId,
            CurrentTenant.Id)
        {
            StockReceivedButNotBilledAccountId = input.StockReceivedButNotBilledAccountId,
            StockDeliveredButNotBilledAccountId = input.StockDeliveredButNotBilledAccountId,
            StockAdjustmentAccountId = input.StockAdjustmentAccountId
        };

        await _repository.InsertAsync(entity);
        return MapToDto(entity);
    }

    [Authorize(MyERPPermissions.WarehouseAccounts.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    private static WarehouseAccountDto MapToDto(WarehouseAccount e) => new()
    {
        Id = e.Id,
        WarehouseId = e.WarehouseId,
        CompanyId = e.CompanyId,
        AccountId = e.AccountId,
        StockReceivedButNotBilledAccountId = e.StockReceivedButNotBilledAccountId,
        StockDeliveredButNotBilledAccountId = e.StockDeliveredButNotBilledAccountId,
        StockAdjustmentAccountId = e.StockAdjustmentAccountId
    };
}
