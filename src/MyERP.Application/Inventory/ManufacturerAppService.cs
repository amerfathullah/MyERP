using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

[Authorize(MyERPPermissions.Manufacturers.Default)]
public class ManufacturerAppService :
    CrudAppService<
        Manufacturer,
        ManufacturerDto,
        Guid,
        PagedAndSortedResultRequestDto,
        CreateUpdateManufacturerDto>,
    IManufacturerAppService
{
    public ManufacturerAppService(IRepository<Manufacturer, Guid> repository)
        : base(repository)
    {
        GetPolicyName = MyERPPermissions.Manufacturers.Default;
        GetListPolicyName = MyERPPermissions.Manufacturers.Default;
        CreatePolicyName = MyERPPermissions.Manufacturers.Create;
        UpdatePolicyName = MyERPPermissions.Manufacturers.Edit;
        DeletePolicyName = MyERPPermissions.Manufacturers.Delete;
    }

    [Authorize(MyERPPermissions.Manufacturers.Create)]
    public override async Task<ManufacturerDto> CreateAsync(CreateUpdateManufacturerDto input)
    {
        var existing = await Repository.FindAsync(m => m.CompanyId == input.CompanyId && m.ShortName == input.ShortName.Trim());
        if (existing != null)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("reason", $"Manufacturer with short name '{input.ShortName}' already exists for this company.");
        }

        var entity = new Manufacturer(GuidGenerator.Create(), input.CompanyId, input.ShortName, input.FullName)
        {
            Website = input.Website,
            Country = input.Country,
            LogoUrl = input.LogoUrl,
            Notes = input.Notes,
        };

        await Repository.InsertAsync(entity, autoSave: true);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "Manufacturer", entity.Id,
            "Created", entity.CompanyId,
            entity.ShortName, "Draft", "Active", CurrentUser.Id,
            $"Manufacturer '{entity.ShortName}' created", CurrentTenant.Id));

        return ObjectMapper.Map<Manufacturer, ManufacturerDto>(entity);
    }

    [Authorize(MyERPPermissions.Manufacturers.Edit)]
    public override async Task<ManufacturerDto> UpdateAsync(Guid id, CreateUpdateManufacturerDto input)
    {
        var entity = await Repository.GetAsync(id);
        var existing = await Repository.FindAsync(m => m.Id != id && m.CompanyId == input.CompanyId && m.ShortName == input.ShortName.Trim());
        if (existing != null)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("reason", $"Manufacturer with short name '{input.ShortName}' already exists for this company.");
        }

        entity.SetShortName(input.ShortName);
        entity.FullName = input.FullName;
        entity.Website = input.Website;
        entity.Country = input.Country;
        entity.LogoUrl = input.LogoUrl;
        entity.Notes = input.Notes;

        await Repository.UpdateAsync(entity, autoSave: true);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "Manufacturer", entity.Id,
            "Updated", entity.CompanyId,
            entity.ShortName, "Active", "Active", CurrentUser.Id,
            $"Manufacturer '{entity.ShortName}' updated", CurrentTenant.Id));

        return ObjectMapper.Map<Manufacturer, ManufacturerDto>(entity);
    }
}
