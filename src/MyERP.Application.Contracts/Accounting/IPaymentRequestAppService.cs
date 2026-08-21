using System;
using System.Threading.Tasks;
using MyERP.Shared;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface IPaymentRequestAppService : IApplicationService
{
    Task<PagedResultDto<PaymentRequestDto>> GetListAsync(CompanyFilteredPagedRequestDto input);
    Task<PaymentRequestDto> GetAsync(Guid id);
    Task<PaymentRequestDto> CreateAsync(CreatePaymentRequestDto input);
    Task<PaymentRequestDto> SubmitAsync(Guid id);
    Task<PaymentRequestDto> CancelAsync(Guid id);

    /// <summary>Creates, submits, and posts a real Payment Entry for this request's full
    /// outstanding amount against Company default accounts, then marks the request Paid.</summary>
    Task<PaymentRequestDto> PayAsync(Guid id);
}
