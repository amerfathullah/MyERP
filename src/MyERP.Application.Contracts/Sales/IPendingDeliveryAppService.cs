using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface IPendingDeliveryAppService : IApplicationService
{
    Task<PendingDeliveryReportDto> GetReportAsync(PendingDeliveryRequestDto input);
    Task<CreateDeliveryNoteResultDto> CreateDeliveryNoteAsync(CreateDnFromPendingDto input);
}
