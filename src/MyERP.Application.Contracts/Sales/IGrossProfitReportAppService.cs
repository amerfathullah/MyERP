using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface IGrossProfitReportAppService : IApplicationService
{
    Task<GrossProfitReportDto> GetReportAsync(GrossProfitRequestDto input);
}
