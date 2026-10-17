using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IBatchItemExpiryStatusAppService : IApplicationService
{
    Task<BatchItemExpiryStatusReportDto> GetReportAsync(BatchItemExpiryStatusRequestDto input);
}
