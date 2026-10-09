using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface IChartOfAccountsImportAppService : IApplicationService
{
    Task<CoaImportResultDto> ImportAsync(ImportCoaDto input);
    Task<List<CoaTemplateRowDto>> GetMalaysianTemplateAsync();
    Task<List<CoaTemplateRowDto>> GetUsTemplateAsync();
    Task<List<CoaTemplateRowDto>> GetTemplateByCountryAsync(string countryCode);
}

