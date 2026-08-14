using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface IPaymentReconciliationAppService : IApplicationService
{
    Task<List<OutstandingInvoiceDto>> GetOutstandingInvoicesAsync(string partyType, Guid partyId);
    Task ReconcileAsync(ReconcilePaymentDto input);
    Task UnreconcileAsync(UnreconcileDto input);
}
