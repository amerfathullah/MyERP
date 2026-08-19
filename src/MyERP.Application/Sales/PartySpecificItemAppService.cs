using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

/// <summary>
/// CRUD for item-visibility restrictions per Customer/Customer Group/Supplier/Supplier Group.
/// Enforcement lives in ItemAppService.GetListAsync via PartySpecificItemFilterService.
/// Maps to ERPNext selling/doctype/party_specific_item.
/// </summary>
[Authorize(MyERPPermissions.PartySpecificItems.Default)]
public class PartySpecificItemAppService :
    CrudAppService<
        PartySpecificItem,
        PartySpecificItemDto,
        Guid,
        GetPartySpecificItemListDto,
        CreateUpdatePartySpecificItemDto>,
    IPartySpecificItemAppService
{
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IRepository<CustomerGroup, Guid> _customerGroupRepository;
    private readonly IRepository<Supplier, Guid> _supplierRepository;
    private readonly IRepository<SupplierGroup, Guid> _supplierGroupRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<ItemGroup, Guid> _itemGroupRepository;
    private readonly IRepository<Brand, Guid> _brandRepository;

    public PartySpecificItemAppService(
        IRepository<PartySpecificItem, Guid> repository,
        IRepository<Customer, Guid> customerRepository,
        IRepository<CustomerGroup, Guid> customerGroupRepository,
        IRepository<Supplier, Guid> supplierRepository,
        IRepository<SupplierGroup, Guid> supplierGroupRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<ItemGroup, Guid> itemGroupRepository,
        IRepository<Brand, Guid> brandRepository)
        : base(repository)
    {
        _customerRepository = customerRepository;
        _customerGroupRepository = customerGroupRepository;
        _supplierRepository = supplierRepository;
        _supplierGroupRepository = supplierGroupRepository;
        _itemRepository = itemRepository;
        _itemGroupRepository = itemGroupRepository;
        _brandRepository = brandRepository;

        GetPolicyName = MyERPPermissions.PartySpecificItems.Default;
        GetListPolicyName = MyERPPermissions.PartySpecificItems.Default;
        CreatePolicyName = MyERPPermissions.PartySpecificItems.Create;
        UpdatePolicyName = MyERPPermissions.PartySpecificItems.Edit;
        DeletePolicyName = MyERPPermissions.PartySpecificItems.Delete;
    }

    public override async Task<PagedResultDto<PartySpecificItemDto>> GetListAsync(GetPartySpecificItemListDto input)
    {
        var queryable = await Repository.GetQueryableAsync();

        if (input.PartyType.HasValue)
        {
            queryable = queryable.Where(r => r.PartyType == input.PartyType.Value);
        }
        if (input.PartyId.HasValue)
        {
            queryable = queryable.Where(r => r.PartyId == input.PartyId.Value);
        }

        var totalCount = queryable.Count();
        var entities = queryable
            .OrderBy(r => r.PartyType).ThenBy(r => r.RestrictBasedOn)
            .Skip(input.SkipCount).Take(input.MaxResultCount)
            .ToList();

        var dtos = new PartySpecificItemDto[entities.Count];
        for (var i = 0; i < entities.Count; i++)
        {
            dtos[i] = await MapToDtoWithNamesAsync(entities[i]);
        }

        return new PagedResultDto<PartySpecificItemDto>(totalCount, dtos.ToList());
    }

    [Authorize(MyERPPermissions.PartySpecificItems.Create)]
    public override async Task<PartySpecificItemDto> CreateAsync(CreateUpdatePartySpecificItemDto input)
    {
        var existing = await Repository.FindAsync(r =>
            r.PartyType == input.PartyType &&
            r.PartyId == input.PartyId &&
            r.RestrictBasedOn == input.RestrictBasedOn &&
            r.BasedOnValueId == input.BasedOnValueId);

        if (existing != null)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("reason", "This item filter has already been applied for the party.");
        }

        if (input.RestrictBasedOn == PartySpecificItemRestrictBasedOn.Item)
        {
            var itemValidation = LazyServiceProvider.LazyGetRequiredService<Inventory.DomainServices.ItemTransactionValidationService>();
            await itemValidation.ValidateItemAsync(input.BasedOnValueId);
        }

        var entity = new PartySpecificItem(
            GuidGenerator.Create(),
            input.PartyType, input.PartyId,
            input.RestrictBasedOn, input.BasedOnValueId,
            CurrentTenant.Id);

        await Repository.InsertAsync(entity, autoSave: true);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "PartySpecificItem", entity.Id,
            "Created", Guid.Empty,
            entity.PartyId.ToString()[..8], "Draft", "Active", CurrentUser.Id,
            $"Party-specific item rule created for {entity.PartyType} {entity.PartyId.ToString()[..8]} restricting {entity.RestrictBasedOn}", CurrentTenant.Id));

        return await MapToDtoWithNamesAsync(entity);
    }

    [Authorize(MyERPPermissions.PartySpecificItems.Edit)]
    public override async Task<PartySpecificItemDto> UpdateAsync(Guid id, CreateUpdatePartySpecificItemDto input)
    {
        var entity = await Repository.GetAsync(id);

        var existing = await Repository.FindAsync(r =>
            r.Id != id &&
            r.PartyType == input.PartyType &&
            r.PartyId == input.PartyId &&
            r.RestrictBasedOn == input.RestrictBasedOn &&
            r.BasedOnValueId == input.BasedOnValueId);

        if (existing != null)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("reason", "This item filter has already been applied for the party.");
        }

        if (input.RestrictBasedOn == PartySpecificItemRestrictBasedOn.Item)
        {
            var itemValidation = LazyServiceProvider.LazyGetRequiredService<Inventory.DomainServices.ItemTransactionValidationService>();
            await itemValidation.ValidateItemAsync(input.BasedOnValueId);
        }

        entity.PartyType = input.PartyType;
        entity.PartyId = input.PartyId;
        entity.RestrictBasedOn = input.RestrictBasedOn;
        entity.BasedOnValueId = input.BasedOnValueId;

        await Repository.UpdateAsync(entity, autoSave: true);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "PartySpecificItem", entity.Id,
            "Updated", Guid.Empty,
            entity.PartyId.ToString()[..8], "Active", "Active", CurrentUser.Id,
            $"Party-specific item rule updated for {entity.PartyType} {entity.PartyId.ToString()[..8]}", CurrentTenant.Id));

        return await MapToDtoWithNamesAsync(entity);
    }

    private async Task<PartySpecificItemDto> MapToDtoWithNamesAsync(PartySpecificItem entity)
    {
        var dto = ObjectMapper.Map<PartySpecificItem, PartySpecificItemDto>(entity);

        dto.PartyName = entity.PartyType switch
        {
            PartySpecificItemPartyType.Customer => (await _customerRepository.FindAsync(entity.PartyId))?.Name,
            PartySpecificItemPartyType.CustomerGroup => (await _customerGroupRepository.FindAsync(entity.PartyId))?.Name,
            PartySpecificItemPartyType.Supplier => (await _supplierRepository.FindAsync(entity.PartyId))?.Name,
            PartySpecificItemPartyType.SupplierGroup => (await _supplierGroupRepository.FindAsync(entity.PartyId))?.Name,
            _ => null,
        };

        dto.BasedOnValueName = entity.RestrictBasedOn switch
        {
            PartySpecificItemRestrictBasedOn.Item => (await _itemRepository.FindAsync(entity.BasedOnValueId))?.ItemName,
            PartySpecificItemRestrictBasedOn.ItemGroup => (await _itemGroupRepository.FindAsync(entity.BasedOnValueId))?.Name,
            PartySpecificItemRestrictBasedOn.Brand => (await _brandRepository.FindAsync(entity.BasedOnValueId))?.Name,
            _ => null,
        };

        return dto;
    }
}
