using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface ISalesCommissionReportAppService : IApplicationService
{
    Task<SalesCommissionReportDto> GetReportAsync(Guid companyId, DateTime fromDate, DateTime toDate);
}
