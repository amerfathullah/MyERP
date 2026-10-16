using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface ISalesOrderTrendsAppService : IApplicationService
{
    Task<SalesOrderTrendsReportDto> GetReportAsync(SalesOrderTrendsRequestDto input);
}
