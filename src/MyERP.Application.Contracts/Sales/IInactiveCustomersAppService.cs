using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface IInactiveCustomersAppService : IApplicationService
{
    Task<InactiveCustomersReportDto> GetReportAsync(InactiveCustomersFilterDto input);
}
