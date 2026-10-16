using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface ISalesPersonTargetVarianceAppService : IApplicationService
{
    Task<SalesPersonTargetVarianceReportDto> GetReportAsync(GetSalesPersonTargetVarianceInput input);
}
