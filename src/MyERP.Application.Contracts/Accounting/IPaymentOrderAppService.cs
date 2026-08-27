using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Shared;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface IPaymentOrderAppService : IApplicationService
{
    Task<PaymentOrderDto> GetAsync(Guid id);
    Task<PagedResultDto<PaymentOrderDto>> GetListAsync(CompanyFilteredPagedRequestDto input);
    Task<PaymentOrderDto> CreateAsync(CreatePaymentOrderDto input);
    Task<PaymentOrderDto> SubmitAsync(Guid id);
    Task<PaymentOrderDto> CancelAsync(Guid id);
    Task DeleteAsync(Guid id);

    /// <summary>Batches all reference rows for one supplier into a single Journal Entry (bank submission run).</summary>
    Task<Guid> MakePaymentRecordsAsync(Guid id, MakePaymentRecordsDto input);

    /// <summary>Gets candidate pending Payment Requests for import into Payment Order.</summary>
    Task<List<CandidatePaymentRequestDto>> GetCandidatePaymentRequestsAsync(Guid companyId);

    /// <summary>Gets candidate pending Payment Entries for import into Payment Order.</summary>
    Task<List<CandidatePaymentEntryDto>> GetCandidatePaymentEntriesAsync(Guid companyId);
}
