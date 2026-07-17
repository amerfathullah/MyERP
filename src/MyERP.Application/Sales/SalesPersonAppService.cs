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
public class SalesPersonAppService : ApplicationService
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
        var sp = new SalesPerson(
            GuidGenerator.Create(),
            input.Name,
            input.ParentSalesPersonId,
            CurrentTenant.Id);

        sp.IsGroup = input.IsGroup;
        sp.EmployeeId = input.EmployeeId;
        sp.SetCommissionRate(input.CommissionRate);

        await _repository.InsertAsync(sp);
        return ObjectMapper.Map<SalesPerson, SalesPersonDto>(sp);
    }

    [Authorize(MyERPPermissions.SalesPersons.Edit)]
    public async Task<SalesPersonDto> UpdateAsync(Guid id, UpdateSalesPersonDto input)
    {
        var sp = await _repository.GetAsync(id);
        sp.SetCommissionRate(input.CommissionRate);
        sp.IsGroup = input.IsGroup;
        sp.EmployeeId = input.EmployeeId;
        sp.ParentSalesPersonId = input.ParentSalesPersonId;
        await _repository.UpdateAsync(sp);
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
    }

    [Authorize(MyERPPermissions.SalesPersons.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}

#region DTOs

public class SalesPersonDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public Guid? ParentSalesPersonId { get; set; }
    public bool IsGroup { get; set; }
    public Guid? EmployeeId { get; set; }
    public decimal CommissionRate { get; set; }
    public bool IsEnabled { get; set; }
    public List<SalesTargetDto> Targets { get; set; } = new();
}

public class SalesTargetDto
{
    public Guid? FiscalYearId { get; set; }
    public decimal TargetQty { get; set; }
    public decimal TargetAmount { get; set; }
}

public class CreateSalesPersonDto
{
    public string Name { get; set; } = null!;
    public Guid? ParentSalesPersonId { get; set; }
    public bool IsGroup { get; set; }
    public Guid? EmployeeId { get; set; }
    public decimal CommissionRate { get; set; }
}

public class UpdateSalesPersonDto
{
    public Guid? ParentSalesPersonId { get; set; }
    public bool IsGroup { get; set; }
    public Guid? EmployeeId { get; set; }
    public decimal CommissionRate { get; set; }
}

public class CreateSalesTargetDto
{
    public Guid? FiscalYearId { get; set; }
    public decimal TargetQty { get; set; }
    public decimal TargetAmount { get; set; }
}

#endregion
