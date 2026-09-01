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

    /// <summary>Resends payment link email for an initiated Payment Request (Gotcha #6012).</summary>
    Task<ResendPaymentEmailResultDto> ResendPaymentEmailAsync(Guid id);

    /// <summary>Gets comprehensive summary metrics and capability flags for Payment Request.</summary>
    Task<PaymentRequestSummaryDto> GetSummaryAsync(Guid id);

    /// <summary>Resolves subscription plans linked to reference document per PR #58438.</summary>
    Task<System.Collections.Generic.List<PaymentRequestSubscriptionPlanDto>> GetSubscriptionDetailsAsync(string referenceDoctype, Guid referenceId);
}
