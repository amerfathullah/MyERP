using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Shared;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface IPaymentEntryAppService : IApplicationService
{
    Task<PaymentEntryDto> GetAsync(Guid id);
    Task<PagedResultDto<PaymentEntryDto>> GetListAsync(CompanyFilteredPagedRequestDto input);
    Task<PaymentEntryDto> CreateAsync(CreatePaymentEntryDto input);
    Task<PaymentEntryDto> SubmitAsync(Guid id);
    Task<PaymentEntryDto> PostAsync(Guid id);
    Task<PaymentEntryDto> CancelAsync(Guid id);
    Task<List<OutstandingInvoiceForPaymentDto>> GetOutstandingForPartyAsync(string partyType, Guid partyId, Guid companyId);
    Task<PartyOutstandingDto> GetPartyOutstandingAsync(string partyType, Guid partyId, Guid companyId);
    Task<AutoAllocationResultDto> AutoAllocateAsync(AutoAllocateRequestDto input);
    Task<PaymentEntryDto> UpdateAsync(Guid id, CreatePaymentEntryDto input);
    Task DeleteAsync(Guid id);
    Task<BulkOperationResultDto> BulkSubmitAsync(List<Guid> ids);
}
