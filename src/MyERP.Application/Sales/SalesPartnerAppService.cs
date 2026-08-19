using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

[Authorize(MyERPPermissions.SalesPartners.Default)]
public class SalesPartnerAppService : ApplicationService, ISalesPartnerAppService
{
    private readonly IRepository<SalesPartner, Guid> _repository;

    public SalesPartnerAppService(IRepository<SalesPartner, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<PagedResultDto<SalesPartnerDto>> GetListAsync(GetSalesPartnerListDto input)
    {
        var queryable = await _repository.GetQueryableAsync();

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            queryable = queryable.Where(x =>
                x.Name.Contains(input.Filter) ||
                (x.ReferralCode != null && x.ReferralCode.Contains(input.Filter)));
        }

        var totalCount = queryable.Count();
        var items = queryable
            .OrderBy(x => x.Name)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        return new PagedResultDto<SalesPartnerDto>(
            totalCount,
            items.Select(MapToDto).ToList());
    }

    public async Task<SalesPartnerDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return MapToDto(entity);
    }

    [Authorize(MyERPPermissions.SalesPartners.Create)]
    public async Task<SalesPartnerDto> CreateAsync(CreateSalesPartnerDto input)
    {
        if (input.CommissionRate < 0 || input.CommissionRate > 100)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.InvalidDiscountPercentage);
        }

        var entity = new SalesPartner(
            GuidGenerator.Create(),
            input.Name,
            (PartnerType)input.PartnerType,
            input.CommissionRate,
            CurrentTenant.Id);

        entity.TerritoryId = input.TerritoryId;
        entity.Website = input.Website;
        entity.Description = input.Description;
        entity.ReferralCode = input.ReferralCode;

        await _repository.InsertAsync(entity);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "SalesPartner", entity.Id,
            "Created", Guid.Empty,
            entity.Name, "Draft", "Active", CurrentUser.Id,
            $"Sales partner '{entity.Name}' created with commission rate {entity.CommissionRate}%", CurrentTenant.Id));

        return MapToDto(entity);
    }

    [Authorize(MyERPPermissions.SalesPartners.Edit)]
    public async Task<SalesPartnerDto> UpdateAsync(Guid id, CreateSalesPartnerDto input)
    {
        if (input.CommissionRate < 0 || input.CommissionRate > 100)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.InvalidDiscountPercentage);
        }

        var entity = await _repository.GetAsync(id);
        entity.SetName(input.Name);
        entity.PartnerType = (PartnerType)input.PartnerType;
        entity.SetCommissionRate(input.CommissionRate);
        entity.TerritoryId = input.TerritoryId;
        entity.Website = input.Website;
        entity.Description = input.Description;
        entity.ReferralCode = input.ReferralCode;

        await _repository.UpdateAsync(entity);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "SalesPartner", entity.Id,
            "Updated", Guid.Empty,
            entity.Name, "Active", "Active", CurrentUser.Id,
            $"Sales partner '{entity.Name}' updated", CurrentTenant.Id));

        return MapToDto(entity);
    }

    [Authorize(MyERPPermissions.SalesPartners.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    [Authorize(MyERPPermissions.SalesPartners.Edit)]
    public async Task ToggleAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        if (entity.IsEnabled) entity.Disable();
        else entity.Enable();
        await _repository.UpdateAsync(entity);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "SalesPartner", entity.Id,
            entity.IsEnabled ? "Enabled" : "Disabled", Guid.Empty,
            entity.Name, "Active", entity.IsEnabled ? "Active" : "Disabled", CurrentUser.Id,
            $"Sales partner '{entity.Name}' {(entity.IsEnabled ? "enabled" : "disabled")}", CurrentTenant.Id));
    }

    private static SalesPartnerDto MapToDto(SalesPartner e) => new()
    {
        Id = e.Id,
        Name = e.Name,
        PartnerType = (int)e.PartnerType,
        CommissionRate = e.CommissionRate,
        TerritoryId = e.TerritoryId,
        Website = e.Website,
        Description = e.Description,
        IsEnabled = e.IsEnabled,
        ReferralCode = e.ReferralCode
    };
}
