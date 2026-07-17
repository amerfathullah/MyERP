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
public class AccountingDimensionAppService : ApplicationService
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
        return ObjectMapper.Map<AccountingDimension, AccountingDimensionDto>(dimension);
    }

    [Authorize(MyERPPermissions.Accounts.Edit)]
    public async Task EnableAsync(Guid id)
    {
        var dimension = await _repository.GetAsync(id);
        dimension.Enable();
        await _repository.UpdateAsync(dimension);
    }

    [Authorize(MyERPPermissions.Accounts.Edit)]
    public async Task DisableAsync(Guid id)
    {
        var dimension = await _repository.GetAsync(id);
        dimension.Disable();
        await _repository.UpdateAsync(dimension);
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

#region DTOs

public class AccountingDimensionDto
{
    public Guid Id { get; set; }
    public string DocumentType { get; set; } = null!;
    public string Label { get; set; } = null!;
    public string FieldName { get; set; } = null!;
    public bool IsEnabled { get; set; }
    public bool IsMandatory { get; set; }
    public Guid? CompanyId { get; set; }
}

public class CreateAccountingDimensionDto
{
    [Required]
    [StringLength(100)]
    public string DocumentType { get; set; } = null!;

    [Required]
    [StringLength(200)]
    public string Label { get; set; } = null!;

    public bool IsMandatory { get; set; }
    public Guid? CompanyId { get; set; }
}

public class UpdateAccountingDimensionDto
{
    [Required]
    [StringLength(200)]
    public string Label { get; set; } = null!;

    public bool IsMandatory { get; set; }
    public bool HideDisabledValues { get; set; } = true;
    public Guid? CompanyId { get; set; }
}

public class AccountingDimensionFilterDto
{
    public Guid Id { get; set; }
    public Guid AccountingDimensionId { get; set; }
    public Guid AccountId { get; set; }
    public Guid CompanyId { get; set; }
    public bool IsAllowList { get; set; }
    public string DimensionValueIds { get; set; } = string.Empty;
}

public class CreateDimensionFilterDto
{
    public Guid AccountingDimensionId { get; set; }
    public Guid AccountId { get; set; }
    public Guid CompanyId { get; set; }
    public bool IsAllowList { get; set; } = true;
    public string? DimensionValueIds { get; set; }
}

#endregion
