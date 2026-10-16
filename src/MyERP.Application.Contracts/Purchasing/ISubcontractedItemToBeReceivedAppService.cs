using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Purchasing;

public interface ISubcontractedItemToBeReceivedAppService : IApplicationService
{
    Task<SubcontractedItemToBeReceivedReportDto> GetReportAsync(SubcontractedItemToBeReceivedFilterDto input);
}
