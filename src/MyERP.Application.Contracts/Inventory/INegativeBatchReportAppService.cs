using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface INegativeBatchReportAppService : IApplicationService
{
    Task<NegativeBatchReportDto> GetReportAsync(NegativeBatchFilterDto input);
}
