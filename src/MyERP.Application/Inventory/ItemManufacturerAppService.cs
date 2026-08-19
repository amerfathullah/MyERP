using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

[Authorize(MyERPPermissions.ItemManufacturers.Default)]
public class ItemManufacturerAppService :
    CrudAppService<
        ItemManufacturer,
        ItemManufacturerDto,
        Guid,
        PagedAndSortedResultRequestDto,
        CreateUpdateItemManufacturerDto>,
    IItemManufacturerAppService
{
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Manufacturer, Guid> _manufacturerRepository;

    public ItemManufacturerAppService(
        IRepository<ItemManufacturer, Guid> repository,
        IRepository<Item, Guid> itemRepository,
        IRepository<Manufacturer, Guid> manufacturerRepository)
        : base(repository)
    {
        _itemRepository = itemRepository;
        _manufacturerRepository = manufacturerRepository;

        GetPolicyName = MyERPPermissions.ItemManufacturers.Default;
        GetListPolicyName = MyERPPermissions.ItemManufacturers.Default;
        CreatePolicyName = MyERPPermissions.ItemManufacturers.Create;
        UpdatePolicyName = MyERPPermissions.ItemManufacturers.Edit;
        DeletePolicyName = MyERPPermissions.ItemManufacturers.Delete;
    }

    [Authorize(MyERPPermissions.ItemManufacturers.Create)]
    public override async Task<ItemManufacturerDto> CreateAsync(CreateUpdateItemManufacturerDto input)
    {
        var existing = await Repository.FindAsync(im =>
            im.CompanyId == input.CompanyId &&
            im.ItemId == input.ItemId &&
            im.ManufacturerId == input.ManufacturerId &&
            im.ManufacturerPartNo == input.ManufacturerPartNo.Trim());

        if (existing != null)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("reason", "An entry for this item, manufacturer, and part number already exists.");
        }

        var itemValidation = LazyServiceProvider.LazyGetRequiredService<DomainServices.ItemTransactionValidationService>();
        await itemValidation.ValidateItemAsync(input.ItemId);

        var entity = new ItemManufacturer(
            GuidGenerator.Create(),
            input.CompanyId,
            input.ItemId,
            input.ManufacturerId,
            input.ManufacturerPartNo,
            input.IsDefault)
        {
            Description = input.Description,
        };

        if (input.IsDefault)
        {
            await ClearOtherDefaultsAsync(input.CompanyId, input.ItemId);
            await UpdateItemDefaultManufacturerAsync(input.ItemId, input.ManufacturerId, input.ManufacturerPartNo);
        }

        await Repository.InsertAsync(entity, autoSave: true);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "ItemManufacturer", entity.Id,
            "Created", entity.CompanyId,
            entity.ManufacturerPartNo, "Draft", "Active", CurrentUser.Id,
            $"Item manufacturer part '{entity.ManufacturerPartNo}' created for item {entity.ItemId.ToString()[..8]}", CurrentTenant.Id));

        return await MapToDtoWithNamesAsync(entity);
    }

    [Authorize(MyERPPermissions.ItemManufacturers.Edit)]
    public override async Task<ItemManufacturerDto> UpdateAsync(Guid id, CreateUpdateItemManufacturerDto input)
    {
        var entity = await Repository.GetAsync(id);

        var existing = await Repository.FindAsync(im =>
            im.Id != id &&
            im.CompanyId == input.CompanyId &&
            im.ItemId == input.ItemId &&
            im.ManufacturerId == input.ManufacturerId &&
            im.ManufacturerPartNo == input.ManufacturerPartNo.Trim());

        if (existing != null)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("reason", "An entry for this item, manufacturer, and part number already exists.");
        }

        var itemValidation = LazyServiceProvider.LazyGetRequiredService<DomainServices.ItemTransactionValidationService>();
        await itemValidation.ValidateItemAsync(input.ItemId);

        entity.ItemId = input.ItemId;
        entity.ManufacturerId = input.ManufacturerId;
        entity.SetManufacturerPartNo(input.ManufacturerPartNo);
        entity.Description = input.Description;
        entity.IsDefault = input.IsDefault;

        if (input.IsDefault)
        {
            await ClearOtherDefaultsAsync(input.CompanyId, input.ItemId, id);
            await UpdateItemDefaultManufacturerAsync(input.ItemId, input.ManufacturerId, input.ManufacturerPartNo);
        }

        await Repository.UpdateAsync(entity, autoSave: true);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "ItemManufacturer", entity.Id,
            "Updated", entity.CompanyId,
            entity.ManufacturerPartNo, "Active", "Active", CurrentUser.Id,
            $"Item manufacturer part '{entity.ManufacturerPartNo}' updated for item {entity.ItemId.ToString()[..8]}", CurrentTenant.Id));

        return await MapToDtoWithNamesAsync(entity);
    }

    public async Task<List<ItemManufacturerDto>> GetListByItemAsync(Guid itemId)
    {
        var entities = await Repository.GetListAsync(im => im.ItemId == itemId);
        var result = new List<ItemManufacturerDto>();
        foreach (var entity in entities)
        {
            result.Add(await MapToDtoWithNamesAsync(entity));
        }
        return result;
    }

    private async Task ClearOtherDefaultsAsync(Guid companyId, Guid itemId, Guid? currentId = null)
    {
        var defaults = await Repository.GetListAsync(im =>
            im.CompanyId == companyId &&
            im.ItemId == itemId &&
            im.IsDefault &&
            (!currentId.HasValue || im.Id != currentId.Value));

        foreach (var item in defaults)
        {
            item.IsDefault = false;
            await Repository.UpdateAsync(item, autoSave: true);
        }
    }

    private async Task UpdateItemDefaultManufacturerAsync(Guid itemId, Guid manufacturerId, string partNo)
    {
        var item = await _itemRepository.FindAsync(itemId);
        if (item != null)
        {
            item.DefaultManufacturerId = manufacturerId;
            item.DefaultManufacturerPartNo = partNo;
            await _itemRepository.UpdateAsync(item, autoSave: true);
        }
    }

    private async Task<ItemManufacturerDto> MapToDtoWithNamesAsync(ItemManufacturer entity)
    {
        var dto = ObjectMapper.Map<ItemManufacturer, ItemManufacturerDto>(entity);
        var item = await _itemRepository.FindAsync(entity.ItemId);
        if (item != null)
        {
            dto.ItemCode = item.ItemCode;
            dto.ItemName = item.ItemName;
        }

        var mfr = await _manufacturerRepository.FindAsync(entity.ManufacturerId);
        if (mfr != null)
        {
            dto.ManufacturerShortName = mfr.ShortName;
        }

        return dto;
    }
}
