using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface ISalesPartnerTargetVarianceAppService : IApplicationService
{
    Task<SalesPartnerTargetVarianceReportDto> GetReportAsync(GetSalesPartnerTargetVarianceInput input);
}
