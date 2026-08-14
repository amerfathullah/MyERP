using System.Threading.Tasks;
using MyERP.Sales;
using Volo.Abp.Application.Services;

namespace MyERP.Purchasing;

public interface ISupplierDeliveryPerformanceAppService : IApplicationService
{
    Task<DeliveryPerformanceReportDto> GetReportAsync(RegisterFilterDto input);
}
