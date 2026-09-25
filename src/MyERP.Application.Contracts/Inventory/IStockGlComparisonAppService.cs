using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IStockGlComparisonAppService : IApplicationService
{
    Task<StockGlComparisonDto> GetComparisonAsync(StockGlComparisonRequestDto input);
    Task<CreateGlRepostingResultDto> CreateGlRepostingEntriesAsync(CreateGlRepostingInputDto input);
}
