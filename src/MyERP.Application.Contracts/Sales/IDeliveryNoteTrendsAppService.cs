using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface IDeliveryNoteTrendsAppService : IApplicationService
{
    Task<DeliveryNoteTrendsReportDto> GetReportAsync(DeliveryNoteTrendsRequestDto input);
}
