using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Purchasing;

public interface ISupplierLedgerSummaryAppService : IApplicationService
{
    Task<SupplierLedgerSummaryReportDto> GetReportAsync(SupplierLedgerSummaryRequestDto input);
}
