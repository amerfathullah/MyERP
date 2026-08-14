using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Tax;

public interface ITaxSummaryReportAppService : IApplicationService
{
    Task<TaxSummaryDto> GetTaxSummaryAsync(Guid companyId, DateTime fromDate, DateTime toDate);
    Task<Sst02FilingDataDto> GetSst02FilingDataAsync(Guid companyId, DateTime fromDate, DateTime toDate);
}
