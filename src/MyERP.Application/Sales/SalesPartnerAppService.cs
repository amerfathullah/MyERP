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
        return MapToDto(entity);
    }

    [Authorize(MyERPPermissions.SalesPartners.Edit)]
    public async Task<SalesPartnerDto> UpdateAsync(Guid id, CreateSalesPartnerDto input)
    {
        var entity = await _repository.GetAsync(id);
        entity.SetName(input.Name);
        entity.PartnerType = (PartnerType)input.PartnerType;
        entity.SetCommissionRate(input.CommissionRate);
        entity.TerritoryId = input.TerritoryId;
        entity.Website = input.Website;
        entity.Description = input.Description;
        entity.ReferralCode = input.ReferralCode;

        await _repository.UpdateAsync(entity);
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
