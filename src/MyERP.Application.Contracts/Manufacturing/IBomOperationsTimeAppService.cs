using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Manufacturing;

public interface IBomOperationsTimeAppService : IApplicationService
{
    Task<BomOperationsTimeReportDto> GetReportAsync(BomOperationsTimeRequestDto input);
}
