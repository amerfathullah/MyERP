using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface ISerialAndBatchWiseStockBalanceAppService : IApplicationService
{
    Task<SerialAndBatchWiseStockBalanceReportDto> GetReportAsync(SerialAndBatchWiseStockBalanceFilterDto input);
}
