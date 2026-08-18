using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Accounting.DomainServices;
using MyERP.Permissions;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

/// <summary>
/// Exposes Payment Ledger Entry repost operations for manual admin-triggered rebuilds.
/// The PaymentLedgerRepostService domain service handles the actual rebuild logic.
/// </summary>
[Authorize(MyERPPermissions.Accounts.Edit)]
public class PaymentLedgerRepostAppService : ApplicationService, IPaymentLedgerRepostAppService
{
    private readonly PaymentLedgerRepostService _repostService;

    public PaymentLedgerRepostAppService(PaymentLedgerRepostService repostService)
    {
        _repostService = repostService;
    }

    public async Task<PaymentLedgerRepostResultDto> RepostAsync(RepostPaymentLedgerDto input)
    {
        var count = await _repostService.RepostForVoucherAsync(input.CompanyId, input.VoucherType, input.VoucherId);
        return new PaymentLedgerRepostResultDto
        {
            TotalVouchers = 1,
            SuccessCount = count > 0 ? 1 : 0,
            FailedCount = 0,
        };
    }

    public async Task<PaymentLedgerRepostResultDto> RepostForCompanyAsync(RepostPaymentLedgerForCompanyDto input)
    {
        var result = await _repostService.RepostForCompanyAsync(input.CompanyId, input.FromDate);
        return new PaymentLedgerRepostResultDto
        {
            TotalVouchers = result.TotalVouchers,
            SuccessCount = result.SuccessCount,
            FailedCount = result.FailedCount,
            HasErrors = result.HasErrors,
            Errors = result.Errors,
        };
    }
}
