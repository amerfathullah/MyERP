using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Manufacturing;

[Authorize(MyERPPermissions.SalesForecasts.Default)]
public class SalesForecastAppService : ApplicationService, ISalesForecastAppService
{
    private readonly IRepository<SalesForecast, Guid> _repository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<MasterProductionSchedule, Guid> _mpsRepository;
    private readonly IDocumentNumberGenerator _numberGenerator;

    public SalesForecastAppService(
        IRepository<SalesForecast, Guid> repository,
        IRepository<Item, Guid> itemRepository,
        IRepository<MasterProductionSchedule, Guid> mpsRepository,
        IDocumentNumberGenerator numberGenerator)
    {
        _repository = repository;
        _itemRepository = itemRepository;
        _mpsRepository = mpsRepository;
        _numberGenerator = numberGenerator;
    }

    public async Task<SalesForecastDto> GetAsync(Guid id)
    {
        var forecast = await _repository.GetAsync(id, includeDetails: true);
        return ToDto(forecast);
    }

    public async Task<PagedResultDto<SalesForecastDto>> GetListAsync(MyERP.Shared.CompanyFilteredPagedRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        if (input.CompanyId.HasValue)
            query = query.Where(f => f.CompanyId == input.CompanyId.Value);

        var totalCount = query.Count();
        var items = query.OrderByDescending(f => f.CreationTime).Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<SalesForecastDto>(totalCount, items.Select(ToDto).ToList());
    }

    [Authorize(MyERPPermissions.SalesForecasts.Create)]
    public async Task<SalesForecastDto> CreateAsync(CreateSalesForecastDto input)
    {
        var number = await _numberGenerator.GenerateAsync("SF", input.CompanyId);
        var forecast = new SalesForecast(GuidGenerator.Create(), input.CompanyId, number, input.FromDate, input.ParentWarehouseId, CurrentTenant.Id)
        {
            Frequency = input.Frequency,
            DemandNumber = input.DemandNumber,
        };
        forecast.SetSelectedItems(input.SelectedItemIds);
        await _repository.InsertAsync(forecast);
        return ToDto(forecast);
    }

    [Authorize(MyERPPermissions.SalesForecasts.Edit)]
    public async Task<SalesForecastDto> UpdateAsync(Guid id, UpdateSalesForecastDto input)
    {
        var forecast = await _repository.GetAsync(id, includeDetails: true);
        forecast.FromDate = input.FromDate;
        forecast.ParentWarehouseId = input.ParentWarehouseId;
        forecast.Frequency = input.Frequency;
        forecast.DemandNumber = input.DemandNumber;
        forecast.SetSelectedItems(input.SelectedItemIds);
        await _repository.UpdateAsync(forecast);
        return ToDto(forecast);
    }

    [Authorize(MyERPPermissions.SalesForecasts.Delete)]
    public async Task DeleteAsync(Guid id) => await _repository.DeleteAsync(id);

    /// <summary>Projects one demand row per selected item per period. Per ERPNext generate_demand().</summary>
    [Authorize(MyERPPermissions.SalesForecasts.Edit)]
    public async Task<SalesForecastDto> GenerateDemandAsync(Guid id)
    {
        var forecast = await _repository.GetAsync(id, includeDetails: true);

        var itemIds = forecast.SelectedItems.Select(s => s.ItemId).ToList();
        var itemQuery = await _itemRepository.GetQueryableAsync();
        var itemDetails = itemQuery.Where(i => itemIds.Contains(i.Id))
            .ToDictionary(i => i.Id, i => (i.ItemName, i.Uom));

        forecast.GenerateDemand(itemDetails);
        await _repository.UpdateAsync(forecast);
        return ToDto(forecast);
    }

    [Authorize(MyERPPermissions.SalesForecasts.Submit)]
    public async Task<SalesForecastDto> SubmitAsync(Guid id)
    {
        var forecast = await _repository.GetAsync(id, includeDetails: true);
        forecast.Submit();
        await _repository.UpdateAsync(forecast);
        return ToDto(forecast);
    }

    [Authorize(MyERPPermissions.SalesForecasts.Cancel)]
    public async Task<SalesForecastDto> CancelAsync(Guid id)
    {
        var forecast = await _repository.GetAsync(id, includeDetails: true);
        forecast.Cancel();
        await _repository.UpdateAsync(forecast);
        return ToDto(forecast);
    }

    /// <summary>Creates a Master Production Schedule from this forecast. Per ERPNext create_mps (get_mapped_doc).</summary>
    [Authorize(MyERPPermissions.MasterProductionSchedules.Create)]
    public async Task<Guid> CreateMpsAsync(Guid id)
    {
        var forecast = await _repository.GetAsync(id, includeDetails: true);
        if (forecast.Status != DocumentStatus.Submitted)
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustBeSubmittedForConversion);

        var number = await _numberGenerator.GenerateAsync("MPS", forecast.CompanyId);
        var mps = new MasterProductionSchedule(GuidGenerator.Create(), forecast.CompanyId, number,
            Clock.Now, forecast.FromDate, CurrentTenant.Id)
        {
            ParentWarehouseId = forecast.ParentWarehouseId,
            SalesForecastId = forecast.Id,
        };
        await _mpsRepository.InsertAsync(mps);

        forecast.MarkMpsGenerated();
        await _repository.UpdateAsync(forecast);

        return mps.Id;
    }

    private SalesForecastDto ToDto(SalesForecast forecast)
    {
        var dto = ObjectMapper.Map<SalesForecast, SalesForecastDto>(forecast);
        dto.SelectedItemIds = forecast.SelectedItems.Select(s => s.ItemId).ToList();
        return dto;
    }
}
