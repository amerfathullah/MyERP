using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface IUpcomingPaymentsDueAppService : IApplicationService
{
    Task<UpcomingPaymentsDueReportDto> GetReportAsync(GetUpcomingPaymentsDueInput input);
}
