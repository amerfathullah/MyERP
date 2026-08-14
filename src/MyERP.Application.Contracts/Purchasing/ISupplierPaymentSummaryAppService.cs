using System.Threading.Tasks;
using MyERP.Sales;
using Volo.Abp.Application.Services;

namespace MyERP.Purchasing;

public interface ISupplierPaymentSummaryAppService : IApplicationService
{
    Task<SupplierPaymentSummaryReportDto> GetReportAsync(RegisterFilterDto input);
}
