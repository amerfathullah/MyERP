using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Tax;

public interface ITaxWithholdingReportAppService : IApplicationService
{
    Task<TaxWithholdingDetailsReportDto> GetDetailsAsync(TaxWithholdingReportFilterDto input);
    Task<TdsComputationSummaryReportDto> GetTdsComputationSummaryAsync(TaxWithholdingReportFilterDto input);
}
