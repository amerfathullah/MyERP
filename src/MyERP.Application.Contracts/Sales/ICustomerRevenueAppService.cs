using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface ICustomerRevenueAppService : IApplicationService
{
    Task<CustomerRevenueReportDto> GetReportAsync(RegisterFilterDto input);
}
