using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface IPaymentTermsStatusForSalesOrderAppService : IApplicationService
{
    Task<PaymentTermsStatusForSalesOrderReportDto> GetReportAsync(PaymentTermsStatusForSalesOrderRequestDto input);
}
