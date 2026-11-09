using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Permissions;
using MyERP.Shared;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

[Authorize(MyERPPermissions.Accounts.Default)]
public class CostCenterAllocationAppService : ApplicationService, ICostCenterAllocationAppService
{
    private readonly IRepository<CostCenterAllocation, Guid> _repository;
    private readonly CostCenterAllocationService _allocationService;

    public CostCenterAllocationAppService(
        IRepository<CostCenterAllocation, Guid> repository,
        CostCenterAllocationService allocationService)
    {
        _repository = repository;
        _allocationService = allocationService;
    }

    public async Task<PagedResultDto<CostCenterAllocationDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var queryable = await _repository.GetQueryableAsync();

        if (input.CompanyId.HasValue)
            queryable = queryable.Where(a => a.CompanyId == input.CompanyId.Value);

        var totalCount = queryable.Count();
        var items = queryable
            .OrderByDescending(a => a.ValidFrom)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        return new PagedResultDto<CostCenterAllocationDto>(
            totalCount,
            items.Select(MapToDto).ToList());
    }

    public async Task<CostCenterAllocationDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return MapToDto(entity);
    }

    [Authorize(MyERPPermissions.Accounts.Create)]
    public async Task<CostCenterAllocationDto> CreateAsync(CreateCostCenterAllocationDto input)
    {
        var childIds = input.Entries.Select(e => e.ChildCostCenterId).ToList();

        // Validate company, non-group, and active state for main and child cost centers (ERPNext 674a548db7)
        await _allocationService.ValidateCostCentersAsync(input.CompanyId, input.MainCostCenterId, childIds);

        // Validate no duplicate active allocation for same main cost center and valid_from date (ERPNext 2683d487a6)
        await _allocationService.ValidateNoDuplicateAllocationAsync(input.MainCostCenterId, input.ValidFrom);

        // DAG cycle validation
        await _allocationService.ValidateNoCycleAsync(input.MainCostCenterId, childIds);

        // valid_from date validation
        await _allocationService.ValidateValidFromAsync(input.MainCostCenterId, input.ValidFrom, input.CompanyId);

        var allocation = new CostCenterAllocation(
            GuidGenerator.Create(),
            input.CompanyId,
            input.MainCostCenterId,
            input.ValidFrom,
            CurrentTenant.Id);

        foreach (var entry in input.Entries)
            allocation.AddEntry(entry.ChildCostCenterId, entry.Percentage);

        allocation.ValidatePercentages();

        await _repository.InsertAsync(allocation);
        return MapToDto(allocation);
    }

    [Authorize(MyERPPermissions.Accounts.Edit)]
    public async Task ToggleActiveAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        if (!entity.IsActive)
        {
            var childIds = entity.Entries.Select(e => e.ChildCostCenterId).ToList();
            await _allocationService.ValidateCostCentersAsync(entity.CompanyId, entity.MainCostCenterId, childIds);
            await _allocationService.ValidateNoDuplicateAllocationAsync(entity.MainCostCenterId, entity.ValidFrom, entity.Id);
            await _allocationService.ValidateNoCycleAsync(entity.MainCostCenterId, childIds, entity.Id);
        }
        entity.IsActive = !entity.IsActive;
        await _repository.UpdateAsync(entity);
    }

    [Authorize(MyERPPermissions.Accounts.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    private static CostCenterAllocationDto MapToDto(CostCenterAllocation entity)
    {
        return new CostCenterAllocationDto
        {
            Id = entity.Id,
            CompanyId = entity.CompanyId,
            MainCostCenterId = entity.MainCostCenterId,
            ValidFrom = entity.ValidFrom,
            IsActive = entity.IsActive,
            Entries = entity.Entries.Select(e => new CostCenterAllocationEntryDto
            {
                Id = e.Id,
                ChildCostCenterId = e.ChildCostCenterId,
                Percentage = e.Percentage
            }).ToList()
        };
    }
}

