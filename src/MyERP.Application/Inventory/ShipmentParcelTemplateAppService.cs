using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

[Authorize(MyERPPermissions.ShipmentParcelTemplates.Default)]
public class ShipmentParcelTemplateAppService : MyERPAppService, IShipmentParcelTemplateAppService
{
    private readonly IRepository<ShipmentParcelTemplate, Guid> _repository;

    public ShipmentParcelTemplateAppService(IRepository<ShipmentParcelTemplate, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<ShipmentParcelTemplateDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return new ShipmentParcelTemplateMapper().Map(entity);
    }

    public async Task<PagedResultDto<ShipmentParcelTemplateDto>> GetListAsync(GetShipmentParcelTemplateListDto input)
    {
        var query = await _repository.GetQueryableAsync();
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter.Trim().ToLower();
            query = query.Where(x => x.ParcelTemplateName.ToLower().Contains(filter));
        }

        var totalCount = await AsyncExecuter.CountAsync(query);
        var entities = await AsyncExecuter.ToListAsync(
            query.OrderBy(x => x.ParcelTemplateName)
                 .Skip(input.SkipCount)
                 .Take(input.MaxResultCount));

        var dtos = entities.Select(e => new ShipmentParcelTemplateMapper().Map(e)).ToList();
        return new PagedResultDto<ShipmentParcelTemplateDto>(totalCount, dtos);
    }

    public async Task<List<ShipmentParcelTemplateDto>> GetAllListAsync()
    {
        var query = await _repository.GetQueryableAsync();
        var entities = await AsyncExecuter.ToListAsync(
            query.Where(x => x.IsActive)
                 .OrderBy(x => x.ParcelTemplateName));

        return entities.Select(e => new ShipmentParcelTemplateMapper().Map(e)).ToList();
    }

    [Authorize(MyERPPermissions.ShipmentParcelTemplates.Create)]
    public async Task<ShipmentParcelTemplateDto> CreateAsync(CreateUpdateShipmentParcelTemplateDto input)
    {
        var entity = new ShipmentParcelTemplate(
            GuidGenerator.Create(),
            input.ParcelTemplateName,
            input.Length,
            input.Width,
            input.Height,
            input.Weight,
            input.Description,
            CurrentTenant.Id)
        {
            IsActive = input.IsActive
        };

        await _repository.InsertAsync(entity);
        return new ShipmentParcelTemplateMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.ShipmentParcelTemplates.Edit)]
    public async Task<ShipmentParcelTemplateDto> UpdateAsync(Guid id, CreateUpdateShipmentParcelTemplateDto input)
    {
        var entity = await _repository.GetAsync(id);
        entity.ParcelTemplateName = input.ParcelTemplateName;
        entity.Length = input.Length;
        entity.Width = input.Width;
        entity.Height = input.Height;
        entity.Weight = input.Weight;
        entity.Description = input.Description;
        entity.IsActive = input.IsActive;

        await _repository.UpdateAsync(entity);
        return new ShipmentParcelTemplateMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.ShipmentParcelTemplates.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}
