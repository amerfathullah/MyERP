using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Purchasing;

public interface ISubcontractOrderSummaryAppService : IApplicationService
{
    Task<SubcontractOrderSummaryReportDto> GetReportAsync(SubcontractOrderSummaryFilterDto input);
}
