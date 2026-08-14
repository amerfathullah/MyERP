using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface IBudgetVarianceReportAppService : IApplicationService
{
    Task<BudgetVarianceReportDto> GetReportAsync(BudgetVarianceRequestDto input);
}
