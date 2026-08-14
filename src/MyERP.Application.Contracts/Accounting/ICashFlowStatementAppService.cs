using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface ICashFlowStatementAppService : IApplicationService
{
    Task<CashFlowStatementDto> GetCashFlowStatementAsync(CashFlowRequestDto input);
}
