using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface IUnreconcilePaymentAppService : IApplicationService
{
    Task<UnreconcilePaymentDto> GetAsync(Guid id);
    Task<PagedResultDto<UnreconcilePaymentDto>> GetListAsync(GetUnreconcilePaymentListDto input);

    /// <summary>
    /// Creates a draft Unreconcile Payment pre-populated with every Payment Ledger Entry allocation
    /// currently linked to the given voucher. Mirrors ERPNext's get_allocations_from_payment.
    /// </summary>
    Task<UnreconcilePaymentDto> CreateAsync(CreateUnreconcilePaymentDto input);

    Task<UnreconcilePaymentDto> SubmitAsync(Guid id);
    Task<UnreconcilePaymentDto> CancelAsync(Guid id);
}
