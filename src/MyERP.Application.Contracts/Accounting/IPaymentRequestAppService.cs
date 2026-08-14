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
}
