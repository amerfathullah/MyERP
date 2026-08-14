using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface IAgingReportAppService : IApplicationService
{
    Task<AgingReportDto> GetReceivablesAgingAsync(AgingReportRequestDto input);
    Task<AgingReportDto> GetPayablesAgingAsync(AgingReportRequestDto input);
    Task<bool> SendPaymentReminderAsync(SendPaymentReminderInput input);
}
