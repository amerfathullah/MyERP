using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using MyERP.Inventory.Entities;
using MyERP.Permissions;

namespace MyERP.Inventory;

[Authorize(MyERPPermissions.Items.Default)]
public class PriceListAppService :
    CrudAppService<PriceList, PriceListDto, Guid, PagedAndSortedResultRequestDto, CreateUpdatePriceListDto>,
    IPriceListAppService
{
    public PriceListAppService(IRepository<PriceList, Guid> repository)
        : base(repository)
    {
    }

    public override async Task<PriceListDto> CreateAsync(CreateUpdatePriceListDto input)
    {
        if (!input.IsSelling && !input.IsBuying)
        {
            throw new Volo.Abp.BusinessException("MyERP:05018")
                .WithData("message", "A price list must apply to Buying, Selling, or both.");
        }

        var result = await base.CreateAsync(input);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "PriceList", result.Id,
            "Created", result.CompanyId ?? Guid.Empty,
            result.Name, "Draft", "Active", CurrentUser.Id,
            $"Price list '{result.Name}' created", CurrentTenant.Id));

        return result;
    }

    public override async Task<PriceListDto> UpdateAsync(Guid id, CreateUpdatePriceListDto input)
    {
        if (!input.IsSelling && !input.IsBuying)
        {
            throw new Volo.Abp.BusinessException("MyERP:05018")
                .WithData("message", "A price list must apply to Buying, Selling, or both.");
        }

        var result = await base.UpdateAsync(id, input);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "PriceList", result.Id,
            "Updated", result.CompanyId ?? Guid.Empty,
            result.Name, "Active", "Active", CurrentUser.Id,
            $"Price list '{result.Name}' updated", CurrentTenant.Id));

        return result;
    }

    protected override async Task<PriceList> MapToEntityAsync(CreateUpdatePriceListDto createInput)
    {
        var priceList = new PriceList(
            GuidGenerator.Create(),
            createInput.Name,
            createInput.CurrencyCode,
            createInput.IsSelling,
            createInput.IsBuying,
            CurrentTenant.Id)
        {
            IsDefault = createInput.IsDefault,
            CompanyId = createInput.CompanyId,
        };

        return await Task.FromResult(priceList);
    }

    protected override async Task MapToEntityAsync(CreateUpdatePriceListDto updateInput, PriceList entity)
    {
        entity.SetName(updateInput.Name);
        entity.CurrencyCode = updateInput.CurrencyCode;
        entity.IsSelling = updateInput.IsSelling;
        entity.IsBuying = updateInput.IsBuying;
        entity.IsDefault = updateInput.IsDefault;
        entity.CompanyId = updateInput.CompanyId;

        await Task.CompletedTask;
    }
}
