using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface IProfitLossByCostCenterAppService : IApplicationService
{
    Task<ProfitLossByCostCenterDto> GetReportAsync(Guid companyId, DateTime fromDate, DateTime toDate);
}
