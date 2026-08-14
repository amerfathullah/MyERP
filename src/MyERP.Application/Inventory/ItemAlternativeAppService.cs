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

[Authorize(MyERPPermissions.ItemAlternatives.Default)]
public class ItemAlternativeAppService :
    CrudAppService<
        ItemAlternative,
        ItemAlternativeDto,
        Guid,
        PagedAndSortedResultRequestDto,
        CreateUpdateItemAlternativeDto>,
    IItemAlternativeAppService
{
    private readonly IRepository<Item, Guid> _itemRepository;

    public ItemAlternativeAppService(
        IRepository<ItemAlternative, Guid> repository,
        IRepository<Item, Guid> itemRepository)
        : base(repository)
    {
        _itemRepository = itemRepository;

        GetPolicyName = MyERPPermissions.ItemAlternatives.Default;
        GetListPolicyName = MyERPPermissions.ItemAlternatives.Default;
        CreatePolicyName = MyERPPermissions.ItemAlternatives.Create;
        UpdatePolicyName = MyERPPermissions.ItemAlternatives.Edit;
        DeletePolicyName = MyERPPermissions.ItemAlternatives.Delete;
    }

    [Authorize(MyERPPermissions.ItemAlternatives.Create)]
    public override async Task<ItemAlternativeDto> CreateAsync(CreateUpdateItemAlternativeDto input)
    {
        var existing = await Repository.FindAsync(ia =>
            ia.CompanyId == input.CompanyId &&
            ia.ItemId == input.ItemId &&
            ia.AlternativeItemId == input.AlternativeItemId);

        if (existing != null)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("reason", "An alternative entry for this item already exists.");
        }

        var entity = new ItemAlternative(
            GuidGenerator.Create(),
            input.CompanyId,
            input.ItemId,
            input.AlternativeItemId,
            input.TwoWay);

        await Repository.InsertAsync(entity, autoSave: true);
        return await MapToDtoWithNamesAsync(entity);
    }

    [Authorize(MyERPPermissions.ItemAlternatives.Edit)]
    public override async Task<ItemAlternativeDto> UpdateAsync(Guid id, CreateUpdateItemAlternativeDto input)
    {
        var entity = await Repository.GetAsync(id);

        var existing = await Repository.FindAsync(ia =>
            ia.Id != id &&
            ia.CompanyId == input.CompanyId &&
            ia.ItemId == input.ItemId &&
            ia.AlternativeItemId == input.AlternativeItemId);

        if (existing != null)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("reason", "An alternative entry for this item already exists.");
        }

        entity.SetItems(input.ItemId, input.AlternativeItemId);
        entity.TwoWay = input.TwoWay;

        await Repository.UpdateAsync(entity, autoSave: true);
        return await MapToDtoWithNamesAsync(entity);
    }

    public async Task<List<ItemAlternativeDto>> GetAlternativesAsync(Guid itemId)
    {
        var direct = await Repository.GetListAsync(ia => ia.ItemId == itemId);
        var reverseTwoWay = await Repository.GetListAsync(ia => ia.AlternativeItemId == itemId && ia.TwoWay);

        var allEntities = direct.Concat(reverseTwoWay).DistinctBy(ia => ia.Id).ToList();
        var result = new List<ItemAlternativeDto>();
        foreach (var entity in allEntities)
        {
            result.Add(await MapToDtoWithNamesAsync(entity));
        }
        return result;
    }

    private async Task<ItemAlternativeDto> MapToDtoWithNamesAsync(ItemAlternative entity)
    {
        var dto = ObjectMapper.Map<ItemAlternative, ItemAlternativeDto>(entity);

        var item = await _itemRepository.FindAsync(entity.ItemId);
        if (item != null)
        {
            dto.ItemCode = item.ItemCode;
            dto.ItemName = item.ItemName;
        }

        var altItem = await _itemRepository.FindAsync(entity.AlternativeItemId);
        if (altItem != null)
        {
            dto.AlternativeItemCode = altItem.ItemCode;
            dto.AlternativeItemName = altItem.ItemName;
        }

        return dto;
    }
}
