using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Manufacturing;

public interface IIssuedItemsAgainstWorkOrderAppService : IApplicationService
{
    Task<IssuedItemsAgainstWorkOrderReportDto> GetReportAsync(IssuedItemsAgainstWorkOrderRequestDto input);
}
