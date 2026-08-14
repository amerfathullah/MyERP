using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface IFinancialReportTemplateAppService : IApplicationService
{
    Task<PagedResultDto<FinancialReportTemplateDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<FinancialReportTemplateDto> GetAsync(Guid id);
    Task<FinancialReportTemplateDto> CreateAsync(CreateFinancialReportTemplateDto input);
    Task ToggleAsync(Guid id);
    Task DeleteAsync(Guid id);
    Task<FinancialReportResultDto> ExecuteAsync(ExecuteReportDto input);
    Task<IReadOnlyList<string>> ValidateAsync(Guid id);
}
