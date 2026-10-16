using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface ISalesPersonCommissionSummaryAppService : IApplicationService
{
    Task<SalesPersonCommissionSummaryReportDto> GetReportAsync(GetSalesPersonCommissionSummaryInput input);
}
