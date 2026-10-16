using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface ICustomersWithoutAnySalesTransactionsAppService : IApplicationService
{
    Task<CustomersWithoutAnySalesTransactionsReportDto> GetReportAsync(CustomersWithoutAnySalesTransactionsFilterDto input);
}
