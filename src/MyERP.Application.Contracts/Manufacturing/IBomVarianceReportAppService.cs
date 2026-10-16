using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Manufacturing;

public interface IBomVarianceReportAppService : IApplicationService
{
    Task<BomVarianceReportDto> GetReportAsync(BomVarianceReportRequestDto input);
}
