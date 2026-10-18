using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Manufacturing;

public interface IBomSearchAppService : IApplicationService
{
    Task<BomSearchReportDto> SearchAsync(BomSearchFilterDto input);
}
