using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface ICustomerLedgerSummaryAppService : IApplicationService
{
    Task<CustomerLedgerSummaryReportDto> GetReportAsync(CustomerLedgerSummaryRequestDto input);
}
