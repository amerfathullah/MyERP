using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Core;

public interface IPartyPerformanceAppService : IApplicationService
{
    Task<CustomerPerformanceDto> GetCustomerPerformanceAsync(Guid customerId, Guid? companyId = null);
    Task<SupplierPerformanceDto> GetSupplierPerformanceAsync(Guid supplierId, Guid? companyId = null);
    Task<PoFulfillmentReportDto> GetPoFulfillmentReportAsync(Guid companyId, Guid? supplierId = null);
}
