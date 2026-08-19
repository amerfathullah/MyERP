using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

[Authorize(MyERPPermissions.SalesPersons.Default)]
public class SalesPersonAppService : ApplicationService, ISalesPersonAppService
{
    private readonly IRepository<SalesPerson, Guid> _repository;

    public SalesPersonAppService(IRepository<SalesPerson, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<SalesPersonDto> GetAsync(Guid id)
    {
        var sp = await _repository.GetAsync(id);
        return ObjectMapper.Map<SalesPerson, SalesPersonDto>(sp);
    }

    public async Task<PagedResultDto<SalesPersonDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var count = query.Count();
        var list = query.OrderBy(x => x.Name)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<SalesPersonDto>(count, list.Select(x => ObjectMapper.Map<SalesPerson, SalesPersonDto>(x)).ToList());
    }

    /// <summary>
    /// Get sales persons hierarchy (tree structure).
    /// </summary>
    public async Task<List<SalesPersonDto>> GetTreeAsync()
    {
        var query = await _repository.GetQueryableAsync();
        var all = query.OrderBy(x => x.Name).ToList();
        return all.Select(x => ObjectMapper.Map<SalesPerson, SalesPersonDto>(x)).ToList();
    }

    [Authorize(MyERPPermissions.SalesPersons.Create)]
    public async Task<SalesPersonDto> CreateAsync(CreateSalesPersonDto input)
    {
        if (input.CommissionRate < 0 || input.CommissionRate > 100)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidDiscountPercentage);
        }

        var sp = new SalesPerson(
            GuidGenerator.Create(),
            input.Name,
            input.ParentSalesPersonId,
            CurrentTenant.Id);

        sp.IsGroup = input.IsGroup;
        sp.EmployeeId = input.EmployeeId;
        sp.SetCommissionRate(input.CommissionRate);

        await _repository.InsertAsync(sp);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "SalesPerson", sp.Id,
            "Created", Guid.Empty,
            sp.Name, "Draft", "Active",
            CurrentUser.Id,
            $"Sales person '{sp.Name}' created with commission rate {sp.CommissionRate}%", CurrentTenant.Id));

        return ObjectMapper.Map<SalesPerson, SalesPersonDto>(sp);
    }

    [Authorize(MyERPPermissions.SalesPersons.Edit)]
    public async Task<SalesPersonDto> UpdateAsync(Guid id, UpdateSalesPersonDto input)
    {
        if (input.CommissionRate < 0 || input.CommissionRate > 100)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidDiscountPercentage);
        }

        if (input.ParentSalesPersonId == id)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("reason", "A sales person cannot be their own parent.");
        }

        var sp = await _repository.GetAsync(id);
        sp.SetCommissionRate(input.CommissionRate);
        sp.IsGroup = input.IsGroup;
        sp.EmployeeId = input.EmployeeId;
        sp.ParentSalesPersonId = input.ParentSalesPersonId;
        await _repository.UpdateAsync(sp);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "SalesPerson", sp.Id,
            "Updated", Guid.Empty,
            sp.Name, "Active", "Active",
            CurrentUser.Id,
            $"Sales person '{sp.Name}' updated", CurrentTenant.Id));

        return ObjectMapper.Map<SalesPerson, SalesPersonDto>(sp);
    }

    /// <summary>
    /// Add a sales target for a fiscal year.
    /// </summary>
    [Authorize(MyERPPermissions.SalesPersons.Edit)]
    public async Task AddTargetAsync(Guid id, CreateSalesTargetDto input)
    {
        var sp = await _repository.GetAsync(id);
        sp.AddTarget(input.FiscalYearId, input.TargetQty, input.TargetAmount);
        await _repository.UpdateAsync(sp);
    }

    /// <summary>
    /// Disable a sales person (cannot be assigned to new transactions).
    /// </summary>
    [Authorize(MyERPPermissions.SalesPersons.Edit)]
    public async Task DisableAsync(Guid id)
    {
        var sp = await _repository.GetAsync(id);
        sp.Disable();
        await _repository.UpdateAsync(sp);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "SalesPerson", sp.Id,
            "Disabled", Guid.Empty,
            sp.Name, "Active", "Disabled",
            CurrentUser.Id,
            $"Sales person '{sp.Name}' disabled", CurrentTenant.Id));
    }

    [Authorize(MyERPPermissions.SalesPersons.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}
