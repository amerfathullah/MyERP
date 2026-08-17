using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public class MonthlyDistributionPercentageDto
{
    public int Month { get; set; }
    public decimal PercentageAllocation { get; set; }
}

public class MonthlyDistributionDto : EntityDto<Guid>
{
    public string DistributionName { get; set; } = null!;
    public Guid? FiscalYearId { get; set; }
    public List<MonthlyDistributionPercentageDto> Percentages { get; set; } = new();
}

public class CreateUpdateMonthlyDistributionDto
{
    public string DistributionName { get; set; } = null!;
    public Guid? FiscalYearId { get; set; }
    public List<MonthlyDistributionPercentageDto> Percentages { get; set; } = new();
}

public interface IMonthlyDistributionAppService : IApplicationService
{
    Task<ListResultDto<MonthlyDistributionDto>> GetListAsync();
    Task<MonthlyDistributionDto> GetAsync(Guid id);
    Task<MonthlyDistributionDto> CreateAsync(CreateUpdateMonthlyDistributionDto input);
    Task<MonthlyDistributionDto> UpdateAsync(Guid id, CreateUpdateMonthlyDistributionDto input);
    Task DeleteAsync(Guid id);
}
