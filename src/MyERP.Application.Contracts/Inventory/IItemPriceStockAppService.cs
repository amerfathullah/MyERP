using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IItemPriceStockAppService : IApplicationService
{
    Task<ItemPriceStockReportDto> GetReportAsync(ItemPriceStockFilterDto input);
}
