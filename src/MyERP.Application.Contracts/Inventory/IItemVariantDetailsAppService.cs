using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IItemVariantDetailsAppService : IApplicationService
{
    Task<ItemVariantDetailsReportDto> GetReportAsync(ItemVariantDetailsFilterDto input);
}
