using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Purchasing;

public interface IItemWisePurchaseHistoryAppService : IApplicationService
{
    Task<ItemWisePurchaseHistoryReportDto> GetHistoryAsync(GetItemWisePurchaseHistoryDto input);
}
