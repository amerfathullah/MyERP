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
        ValidatePercentages(input.Percentages);

        var distribution = new MonthlyDistribution(GuidGenerator.Create(), input.DistributionName, input.FiscalYearId, CurrentTenant.Id);
        distribution.SetPercentages(input.Percentages!.Select(p => (p.Month, p.PercentageAllocation)));
        await _repository.InsertAsync(distribution);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "MonthlyDistribution", distribution.Id,
            "Created", Guid.Empty,
            distribution.DistributionName, "Draft", "Active",
            CurrentUser.Id,
            $"Monthly distribution '{distribution.DistributionName}' created", CurrentTenant.Id));

        return ObjectMapper.Map<MonthlyDistribution, MonthlyDistributionDto>(distribution);
    }

    [Authorize(MyERPPermissions.Budgets.Edit)]
    public async Task<MonthlyDistributionDto> UpdateAsync(Guid id, CreateUpdateMonthlyDistributionDto input)
    {
        ValidatePercentages(input.Percentages);

        var distribution = await _repository.GetAsync(id);
        distribution.DistributionName = input.DistributionName;
        distribution.FiscalYearId = input.FiscalYearId;
        distribution.SetPercentages(input.Percentages!.Select(p => (p.Month, p.PercentageAllocation)));
        await _repository.UpdateAsync(distribution);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "MonthlyDistribution", distribution.Id,
            "Updated", Guid.Empty,
            distribution.DistributionName, "Active", "Active",
            CurrentUser.Id,
            $"Monthly distribution '{distribution.DistributionName}' updated", CurrentTenant.Id));

        return ObjectMapper.Map<MonthlyDistribution, MonthlyDistributionDto>(distribution);
    }

    [Authorize(MyERPPermissions.Budgets.Delete)]
    public async Task DeleteAsync(Guid id) => await _repository.DeleteAsync(id);

    private static void ValidatePercentages(System.Collections.Generic.List<MonthlyDistributionPercentageDto>? percentages)
    {
        if (percentages == null || percentages.Count == 0)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.DocumentMustHaveItems);
        }

        if (percentages.Any(p => p.Month < 1 || p.Month > 12) || percentages.Select(p => p.Month).Distinct().Count() != percentages.Count)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Monthly distribution months must be valid (1-12) without duplicate months.");
        }
    }
}
