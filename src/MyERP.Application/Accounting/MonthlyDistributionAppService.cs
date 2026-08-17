using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

[Authorize(MyERPPermissions.Budgets.Default)]
public class MonthlyDistributionAppService : ApplicationService, IMonthlyDistributionAppService
{
    private readonly IRepository<MonthlyDistribution, Guid> _repository;

    public MonthlyDistributionAppService(IRepository<MonthlyDistribution, Guid> repository) => _repository = repository;

    public async Task<ListResultDto<MonthlyDistributionDto>> GetListAsync()
    {
        var query = (await _repository.WithDetailsAsync()).OrderBy(d => d.DistributionName);
        var items = query.ToList();
        return new ListResultDto<MonthlyDistributionDto>(items.Select(ObjectMapper.Map<MonthlyDistribution, MonthlyDistributionDto>).ToList());
    }

    public async Task<MonthlyDistributionDto> GetAsync(Guid id)
    {
        var distribution = (await _repository.WithDetailsAsync()).First(d => d.Id == id);
        return ObjectMapper.Map<MonthlyDistribution, MonthlyDistributionDto>(distribution);
    }

    [Authorize(MyERPPermissions.Budgets.Create)]
    public async Task<MonthlyDistributionDto> CreateAsync(CreateUpdateMonthlyDistributionDto input)
    {
        var distribution = new MonthlyDistribution(GuidGenerator.Create(), input.DistributionName, input.FiscalYearId, CurrentTenant.Id);
        distribution.SetPercentages(input.Percentages.Select(p => (p.Month, p.PercentageAllocation)));
        await _repository.InsertAsync(distribution);
        return ObjectMapper.Map<MonthlyDistribution, MonthlyDistributionDto>(distribution);
    }

    [Authorize(MyERPPermissions.Budgets.Edit)]
    public async Task<MonthlyDistributionDto> UpdateAsync(Guid id, CreateUpdateMonthlyDistributionDto input)
    {
        var distribution = await _repository.GetAsync(id);
        distribution.DistributionName = input.DistributionName;
        distribution.FiscalYearId = input.FiscalYearId;
        distribution.SetPercentages(input.Percentages.Select(p => (p.Month, p.PercentageAllocation)));
        await _repository.UpdateAsync(distribution);
        return ObjectMapper.Map<MonthlyDistribution, MonthlyDistributionDto>(distribution);
    }

    [Authorize(MyERPPermissions.Budgets.Delete)]
    public async Task DeleteAsync(Guid id) => await _repository.DeleteAsync(id);
}
