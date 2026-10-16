using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface ICustomerWiseItemPriceAppService : IApplicationService
{
    Task<CustomerWiseItemPriceReportDto> GetReportAsync(GetCustomerWiseItemPriceInput input);
}
