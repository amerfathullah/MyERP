using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IItemWiseConsumptionAppService : IApplicationService
{
    Task<ItemWiseConsumptionReportDto> GetReportAsync(ItemWiseConsumptionRequestDto input);
}
