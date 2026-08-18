using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface IPaymentLedgerRepostAppService : IApplicationService
{
    Task<PaymentLedgerRepostResultDto> RepostAsync(RepostPaymentLedgerDto input);
    Task<PaymentLedgerRepostResultDto> RepostForCompanyAsync(RepostPaymentLedgerForCompanyDto input);
}
