using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface IBatchPaymentAppService : IApplicationService
{
    Task<PayableInvoicePartitionDto> GetPayableInvoicesAsync(ValidatePayableInvoicesDto input);
    Task<List<BatchPaymentInvoiceDto>> GetOutstandingInvoicesAsync(GetOutstandingForBatchDto input);
    Task<BatchPaymentResultDto> CreateBatchPaymentAsync(CreateBatchPaymentDto input);
}
