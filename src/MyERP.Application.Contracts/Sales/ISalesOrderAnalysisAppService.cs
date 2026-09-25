using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface ISalesOrderAnalysisAppService : IApplicationService
{
    Task<SalesOrderAnalysisReportDto> GetAnalysisAsync(GetSalesOrderAnalysisDto input);
}
