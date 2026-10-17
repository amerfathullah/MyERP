using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IItemShortageReportAppService : IApplicationService
{
    Task<ItemShortageReportDto> GetReportAsync(ItemShortageReportRequestDto input);
}
