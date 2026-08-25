using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

[Authorize(MyERPPermissions.ItemLeadTimes.Default)]
public class ItemLeadTimeAppService : MyERPAppService, IItemLeadTimeAppService
{
    private readonly IRepository<ItemLeadTime, Guid> _repository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Supplier, Guid> _supplierRepository;

    public ItemLeadTimeAppService(
        IRepository<ItemLeadTime, Guid> repository,
        IRepository<Item, Guid> itemRepository,
        IRepository<Supplier, Guid> supplierRepository)
    {
        _repository = repository;
        _itemRepository = itemRepository;
        _supplierRepository = supplierRepository;
    }

    public async Task<ItemLeadTimeDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return await MapToDtoAsync(entity);
    }

    public async Task<ItemLeadTimeDto?> GetByItemIdAsync(Guid itemId)
    {
        var query = await _repository.GetQueryableAsync();
        var entity = await AsyncExecuter.FirstOrDefaultAsync(query.Where(x => x.ItemId == itemId));
        return entity == null ? null : await MapToDtoAsync(entity);
    }

    public async Task<PagedResultDto<ItemLeadTimeDto>> GetListAsync(GetItemLeadTimeListDto input)
    {
        var query = await _repository.GetQueryableAsync();
        if (input.ItemId.HasValue)
        {
            query = query.Where(x => x.ItemId == input.ItemId.Value);
        }

        var totalCount = await AsyncExecuter.CountAsync(query);
        var entities = await AsyncExecuter.ToListAsync(
            query.OrderByDescending(x => x.CreationTime)
                 .Skip(input.SkipCount)
                 .Take(input.MaxResultCount));

        var dtos = new List<ItemLeadTimeDto>();
        foreach (var entity in entities)
        {
            dtos.Add(await MapToDtoAsync(entity));
        }

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter.Trim().ToLower();
            dtos = dtos.Where(x => (x.ItemCode != null && x.ItemCode.ToLower().Contains(filter)) ||
                                   (x.ItemName != null && x.ItemName.ToLower().Contains(filter))).ToList();
        }

        return new PagedResultDto<ItemLeadTimeDto>(totalCount, dtos);
    }

    [Authorize(MyERPPermissions.ItemLeadTimes.Create)]
    public async Task<ItemLeadTimeDto> CreateAsync(CreateUpdateItemLeadTimeDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var exists = await AsyncExecuter.AnyAsync(query.Where(x => x.ItemId == input.ItemId));
        if (exists)
        {
            throw new UserFriendlyException("Lead time configuration already exists for this item.");
        }

        var entity = new ItemLeadTime(
            GuidGenerator.Create(),
            input.ItemId,
            input.ShiftTimeInHours,
            input.NoOfWorkstations,
            input.NoOfShifts,
            input.ManufacturingTimeInMins,
            input.DailyYield,
            input.PurchaseTimeDays,
            input.BufferTimeDays,
            CurrentTenant.Id);

        if (input.Suppliers != null)
        {
            foreach (var sup in input.Suppliers)
            {
                entity.AddSupplier(sup.SupplierId, sup.PurchaseTimeDays, sup.BufferTimeDays, sup.IsDefault);
            }
        }

        await _repository.InsertAsync(entity);
        return await MapToDtoAsync(entity);
    }

    [Authorize(MyERPPermissions.ItemLeadTimes.Edit)]
    public async Task<ItemLeadTimeDto> UpdateAsync(Guid id, CreateUpdateItemLeadTimeDto input)
    {
        var entity = await _repository.GetAsync(id);

        entity.ShiftTimeInHours = input.ShiftTimeInHours;
        entity.NoOfWorkstations = input.NoOfWorkstations;
        entity.NoOfShifts = input.NoOfShifts;
        entity.ManufacturingTimeInMins = input.ManufacturingTimeInMins;
        entity.DailyYield = input.DailyYield;
        entity.PurchaseTimeDays = input.PurchaseTimeDays;
        entity.BufferTimeDays = input.BufferTimeDays;

        entity.Recalculate();

        entity.ClearSuppliers();
        if (input.Suppliers != null)
        {
            foreach (var sup in input.Suppliers)
            {
                entity.AddSupplier(sup.SupplierId, sup.PurchaseTimeDays, sup.BufferTimeDays, sup.IsDefault);
            }
        }

        await _repository.UpdateAsync(entity);
        return await MapToDtoAsync(entity);
    }

    [Authorize(MyERPPermissions.ItemLeadTimes.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    private async Task<ItemLeadTimeDto> MapToDtoAsync(ItemLeadTime entity)
    {
        var dto = new ItemLeadTimeMapper().Map(entity);

        var item = await _itemRepository.FindAsync(entity.ItemId);
        if (item != null)
        {
            dto.ItemCode = item.ItemCode;
            dto.ItemName = item.ItemName;
            dto.StockUom = item.Uom;
        }

        var supplierMapper = new ItemLeadTimeSupplierMapper();
        dto.Suppliers = new List<ItemLeadTimeSupplierDto>();

        foreach (var sup in entity.Suppliers)
        {
            var supDto = supplierMapper.Map(sup);
            var supplier = await _supplierRepository.FindAsync(sup.SupplierId);
            if (supplier != null)
            {
                supDto.SupplierName = supplier.Name;
            }
            dto.Suppliers.Add(supDto);
        }

        return dto;
    }
}
