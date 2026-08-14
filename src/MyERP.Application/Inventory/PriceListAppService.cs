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
