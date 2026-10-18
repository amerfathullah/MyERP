using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IReservedStockReportAppService : IApplicationService
{
    Task<ReservedStockReportDto> GetReportAsync(ReservedStockFilterDto input);
}
