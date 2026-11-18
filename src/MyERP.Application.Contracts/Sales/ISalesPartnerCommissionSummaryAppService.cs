using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface ISalesPartnerCommissionSummaryAppService : IApplicationService
{
    Task<SalesPartnerCommissionSummaryReportDto> GetReportAsync(GetSalesPartnerCommissionSummaryInput input);
    Task<SalesPartnerTransactionSummaryReportDto> GetTransactionSummaryAsync(GetSalesPartnerCommissionSummaryInput input);
}
