using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

[Authorize(MyERPPermissions.Accounts.Default)]
public class AccountingDimensionAppService : ApplicationService, IAccountingDimensionAppService
{
    private readonly IRepository<AccountingDimension, Guid> _repository;
    private readonly IRepository<AccountingDimensionFilter, Guid> _filterRepository;
    private readonly AccountingDimensionService _dimensionService;

    public AccountingDimensionAppService(
        IRepository<AccountingDimension, Guid> repository,
        IRepository<AccountingDimensionFilter, Guid> filterRepository,
        AccountingDimensionService dimensionService)
    {
        _repository = repository;
        _filterRepository = filterRepository;
        _dimensionService = dimensionService;
    }

    public async Task<List<AccountingDimensionDto>> GetEnabledDimensionsAsync(Guid? companyId = null)
    {
        var dimensions = await _dimensionService.GetEnabledDimensionsAsync(companyId);
        return dimensions.Select(ObjectMapper.Map<AccountingDimension, AccountingDimensionDto>).ToList();
    }

    public async Task<PagedResultDto<AccountingDimensionDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var totalCount = query.Count();
        var items = query.OrderBy(d => d.DocumentType)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<AccountingDimensionDto>(totalCount, items.Select(ObjectMapper.Map<AccountingDimension, AccountingDimensionDto>).ToList());
    }

    public async Task<AccountingDimensionDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return ObjectMapper.Map<AccountingDimension, AccountingDimensionDto>(entity);
    }

    [Authorize(MyERPPermissions.Accounts.Create)]
    public async Task<AccountingDimensionDto> CreateAsync(CreateAccountingDimensionDto input)
    {
        var dimension = new AccountingDimension(
            GuidGenerator.Create(),
            input.DocumentType,
            input.Label,
            CurrentTenant.Id);

        dimension.IsMandatory = input.IsMandatory;
        dimension.CompanyId = input.CompanyId;

        await _repository.InsertAsync(dimension);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "AccountingDimension", dimension.Id,
            "Created", dimension.CompanyId ?? Guid.Empty,
            dimension.Label, "Draft", "Active",
            CurrentUser.Id,
            $"Accounting dimension '{dimension.Label}' created for document type '{dimension.DocumentType}'", CurrentTenant.Id));

        return ObjectMapper.Map<AccountingDimension, AccountingDimensionDto>(dimension);
    }

    [Authorize(MyERPPermissions.Accounts.Edit)]
    public async Task<AccountingDimensionDto> UpdateAsync(Guid id, UpdateAccountingDimensionDto input)
    {
        var dimension = await _repository.GetAsync(id);
        dimension.Label = input.Label;
        dimension.IsMandatory = input.IsMandatory;
        dimension.CompanyId = input.CompanyId;
        dimension.HideDisabledValues = input.HideDisabledValues;
        await _repository.UpdateAsync(dimension);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "AccountingDimension", dimension.Id,
            "Updated", dimension.CompanyId ?? Guid.Empty,
            dimension.Label, "Active", "Active",
            CurrentUser.Id,
            $"Accounting dimension '{dimension.Label}' updated", CurrentTenant.Id));

        return ObjectMapper.Map<AccountingDimension, AccountingDimensionDto>(dimension);
    }

    [Authorize(MyERPPermissions.Accounts.Edit)]
    public async Task EnableAsync(Guid id)
    {
        var dimension = await _repository.GetAsync(id);
        dimension.Enable();
        await _repository.UpdateAsync(dimension);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "AccountingDimension", dimension.Id,
            "Enabled", dimension.CompanyId ?? Guid.Empty,
            dimension.Label, "Disabled", "Active",
            CurrentUser.Id,
            $"Accounting dimension '{dimension.Label}' enabled", CurrentTenant.Id));
    }

    [Authorize(MyERPPermissions.Accounts.Edit)]
    public async Task DisableAsync(Guid id)
    {
        var dimension = await _repository.GetAsync(id);
        dimension.Disable();
        await _repository.UpdateAsync(dimension);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "AccountingDimension", dimension.Id,
            "Disabled", dimension.CompanyId ?? Guid.Empty,
            dimension.Label, "Active", "Disabled",
            CurrentUser.Id,
            $"Accounting dimension '{dimension.Label}' disabled", CurrentTenant.Id));
    }

    [Authorize(MyERPPermissions.Accounts.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    #region Dimension Filters

    public async Task<List<AccountingDimensionFilterDto>> GetFiltersAsync(Guid dimensionId, Guid companyId)
    {
        var filters = await _filterRepository.GetListAsync(f =>
            f.AccountingDimensionId == dimensionId && f.CompanyId == companyId);
        return filters.Select(ObjectMapper.Map<AccountingDimensionFilter, AccountingDimensionFilterDto>).ToList();
    }

    [Authorize(MyERPPermissions.Accounts.Create)]
    public async Task<AccountingDimensionFilterDto> CreateFilterAsync(CreateDimensionFilterDto input)
    {
        var filter = new AccountingDimensionFilter(
            GuidGenerator.Create(),
            input.AccountingDimensionId,
            input.AccountId,
            input.CompanyId,
            input.IsAllowList);
        filter.DimensionValueIds = input.DimensionValueIds ?? string.Empty;
        filter.TenantId = CurrentTenant.Id;

        await _filterRepository.InsertAsync(filter);
        return ObjectMapper.Map<AccountingDimensionFilter, AccountingDimensionFilterDto>(filter);
    }

    [Authorize(MyERPPermissions.Accounts.Delete)]
    public async Task DeleteFilterAsync(Guid filterId)
    {
        await _filterRepository.DeleteAsync(filterId);
    }

    #endregion
}

