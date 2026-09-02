using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Purchasing;

public interface ISupplierQuotationComparisonAppService : IApplicationService
{
    Task<SupplierQuotationComparisonDto> GetComparisonByRfqAsync(Guid rfqId, string? status = null, string? orderStatus = null);
    Task<SupplierQuotationComparisonDto> GetComparisonByIdsAsync(List<Guid> quotationIds);
}
