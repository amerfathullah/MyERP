using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Purchasing;

public interface ISubcontractedRawMaterialsToBeTransferredAppService : IApplicationService
{
    Task<SubcontractedRawMaterialsToBeTransferredReportDto> GetReportAsync(SubcontractedRawMaterialsToBeTransferredFilterDto input);
}
