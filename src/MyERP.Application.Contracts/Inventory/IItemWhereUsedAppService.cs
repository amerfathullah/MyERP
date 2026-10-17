using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IItemWhereUsedAppService : IApplicationService
{
    Task<ItemWhereUsedReportDto> GetReportAsync(ItemWhereUsedRequestDto input);
}
