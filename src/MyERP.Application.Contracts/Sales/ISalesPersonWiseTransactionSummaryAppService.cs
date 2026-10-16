using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface ISalesPersonWiseTransactionSummaryAppService : IApplicationService
{
    Task<SalesPersonWiseTransactionSummaryReportDto> GetReportAsync(GetSalesPersonWiseTransactionSummaryInput input);
}
