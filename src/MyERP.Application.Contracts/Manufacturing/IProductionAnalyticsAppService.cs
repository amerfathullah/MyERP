using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Manufacturing;

public interface IProductionAnalyticsAppService : IApplicationService
{
    Task<ProductionAnalyticsDto> GetAnalyticsAsync(Guid companyId, DateTime fromDate, DateTime toDate);
}
