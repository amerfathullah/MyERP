using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface IPaymentReconciliationAppService : IApplicationService
{
    Task<List<OutstandingInvoiceDto>> GetOutstandingInvoicesAsync(string partyType, Guid partyId, Guid? accountId = null);
    Task<List<UnreconciledPaymentDto>> GetUnreconciledPaymentsAsync(string partyType, Guid partyId);
    Task<List<ReconcileAllocationDto>> GetAutoAllocationAsync(string partyType, Guid partyId);
    Task ReconcileAsync(ReconcilePaymentDto input);
    Task UnreconcileAsync(UnreconcileDto input);
    Task<List<LinkedAllocationDto>> GetLinkedAllocationsAsync(string voucherType, Guid voucherId);
    Task UnreconcileAllocationsAsync(List<UnreconcileDto> input);
}
