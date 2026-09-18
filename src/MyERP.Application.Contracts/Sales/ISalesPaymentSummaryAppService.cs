using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface ISalesPaymentSummaryAppService : IApplicationService
{
    Task<SalesPaymentSummaryReportDto> GetReportAsync(SalesPaymentSummaryFilterDto input);
}
