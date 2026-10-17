using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IItemwiseRecommendedReorderLevelAppService : IApplicationService
{
    Task<ItemwiseRecommendedReorderLevelReportDto> GetReportAsync(ItemwiseRecommendedReorderLevelFilterDto input);
}
