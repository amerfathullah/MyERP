using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.HumanResources.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.HumanResources;

[Authorize(MyERPPermissions.Payroll.Default)]
public class SalaryComponentAppService : ApplicationService, ISalaryComponentAppService
{
    private readonly IRepository<SalaryComponent, Guid> _repository;

    public SalaryComponentAppService(IRepository<SalaryComponent, Guid> repository)
        => _repository = repository;

    public async Task<PagedResultDto<SalaryComponentDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var totalCount = query.Count();
        var items = query.OrderBy(c => c.ComponentType).ThenBy(c => c.Name)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<SalaryComponentDto>(totalCount,
            items.Select(ObjectMapper.Map<SalaryComponent, SalaryComponentDto>).ToList());
    }

    public async Task<SalaryComponentDto> GetAsync(Guid id)
        => ObjectMapper.Map<SalaryComponent, SalaryComponentDto>(await _repository.GetAsync(id));

    [Authorize(MyERPPermissions.Payroll.Create)]
    public async Task<SalaryComponentDto> CreateAsync(CreateUpdateSalaryComponentDto input)
    {
        var component = new SalaryComponent(
            GuidGenerator.Create(), input.Name,
            (SalaryComponentType)input.ComponentType, CurrentTenant.Id)
        {
            Abbreviation = input.Abbreviation ?? "",
            IsStatutory = input.IsStatutory,
            IsTaxApplicable = input.IsTaxApplicable,
            DependsOnPaymentDays = input.DependsOnPaymentDays,
            DefaultAccountId = input.DefaultAccountId,
            Description = input.Description,
        };
        await _repository.InsertAsync(component);
        return ObjectMapper.Map<SalaryComponent, SalaryComponentDto>(component);
    }

    [Authorize(MyERPPermissions.Payroll.Create)]
    public async Task<SalaryComponentDto> UpdateAsync(Guid id, CreateUpdateSalaryComponentDto input)
    {
        var component = await _repository.GetAsync(id);
        component.Name = input.Name;
        component.Abbreviation = input.Abbreviation ?? component.Abbreviation;
        component.IsTaxApplicable = input.IsTaxApplicable;
        component.DependsOnPaymentDays = input.DependsOnPaymentDays;
        component.DefaultAccountId = input.DefaultAccountId;
        component.Description = input.Description;
        await _repository.UpdateAsync(component);
        return ObjectMapper.Map<SalaryComponent, SalaryComponentDto>(component);
    }

    [Authorize(MyERPPermissions.Payroll.Create)]
    public async Task DeleteAsync(Guid id) => await _repository.DeleteAsync(id);
}
