using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IStockDiagnosticsAppService : IApplicationService
{
    Task<IncorrectBalanceQtyReportDto> GetIncorrectBalanceQtyAsync(IncorrectBalanceQtyRequestDto input);
    Task<IncorrectStockValueReportDto> GetIncorrectStockValueAsync(IncorrectStockValueRequestDto input);
}
