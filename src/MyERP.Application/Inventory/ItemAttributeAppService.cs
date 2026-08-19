using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

/// <summary>
/// Manages Item Attributes for variant generation.
/// Attributes define configurable dimensions (Color, Size, Material, etc.)
/// that can be combined to create item variants from template items.
/// </summary>
[Authorize(MyERPPermissions.Items.Default)]
public class ItemAttributeAppService : ApplicationService, IItemAttributeAppService
{
    private readonly IRepository<ItemAttribute, Guid> _repository;

    public ItemAttributeAppService(IRepository<ItemAttribute, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<ItemAttributeDto> GetAsync(Guid id)
    {
        var attr = await _repository.GetAsync(id);
        return ObjectMapper.Map<ItemAttribute, ItemAttributeDto>(attr);
    }

    public async Task<List<ItemAttributeDto>> GetListAsync()
    {
        var query = await _repository.GetQueryableAsync();
        var list = query.OrderBy(a => a.AttributeName).ToList();
        return list.Select(x => ObjectMapper.Map<ItemAttribute, ItemAttributeDto>(x)).ToList();
    }

    [Authorize(MyERPPermissions.Items.Create)]
    public async Task<ItemAttributeDto> CreateAsync(CreateItemAttributeDto input)
    {
        if (input.IsNumeric)
        {
            if (input.ToRange < input.FromRange)
            {
                throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.InvalidDateRange)
                    .WithData("reason", "ToRange must be greater than or equal to FromRange");
            }

            if (input.Increment <= 0)
            {
                throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                    .WithData("field", "Increment");
            }
        }

        var attr = new ItemAttribute(GuidGenerator.Create(), input.Name, input.IsNumeric, CurrentTenant.Id);

        if (input.IsNumeric)
        {
            attr.SetNumericRange(input.FromRange, input.ToRange, input.Increment);
        }
        else
        {
            foreach (var value in input.Values)
            {
                attr.AddValue(value.Value, value.Abbreviation);
            }
        }

        await _repository.InsertAsync(attr);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "ItemAttribute", attr.Id,
            "Created", Guid.Empty,
            attr.AttributeName, "Draft", "Active", CurrentUser.Id,
            $"Item attribute '{attr.AttributeName}' created", CurrentTenant.Id));

        return ObjectMapper.Map<ItemAttribute, ItemAttributeDto>(attr);
    }

    [Authorize(MyERPPermissions.Items.Edit)]
    public async Task<ItemAttributeDto> AddValueAsync(Guid id, ItemAttributeValueDto input)
    {
        var attr = await _repository.GetAsync(id);
        attr.AddValue(input.Value, input.Abbreviation);
        await _repository.UpdateAsync(attr);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "ItemAttribute", attr.Id,
            "Updated", Guid.Empty,
            attr.AttributeName, "Active", "Active", CurrentUser.Id,
            $"Item attribute '{attr.AttributeName}' added value '{input.Value}'", CurrentTenant.Id));

        return ObjectMapper.Map<ItemAttribute, ItemAttributeDto>(attr);
    }

    [Authorize(MyERPPermissions.Items.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}
