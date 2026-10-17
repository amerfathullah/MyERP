using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IWarehouseWiseItemBalanceAgeAndValueAppService : IApplicationService
{
    Task<WarehouseWiseItemBalanceAgeAndValueReportDto> GetReportAsync(WarehouseWiseItemBalanceAgeAndValueFilterDto input);
}
