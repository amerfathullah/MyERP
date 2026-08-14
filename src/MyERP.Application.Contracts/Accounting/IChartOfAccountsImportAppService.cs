using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface IChartOfAccountsImportAppService : IApplicationService
{
    Task<CoaImportResultDto> ImportAsync(ImportCoaDto input);
    Task<List<CoaTemplateRowDto>> GetMalaysianTemplateAsync();
}
