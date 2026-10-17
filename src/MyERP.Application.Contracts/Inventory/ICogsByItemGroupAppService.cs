using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface ICogsByItemGroupAppService : IApplicationService
{
    Task<CogsByItemGroupReportDto> GetReportAsync(CogsByItemGroupFilterDto input);
}
