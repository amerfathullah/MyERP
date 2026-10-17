using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Purchasing;

/// <summary>
/// Report service for Material Requests for which Supplier Quotations are not created.
/// Ported from ERPNext stock/report/material_requests_for_which_supplier_quotations_are_not_created.
/// </summary>
public interface IMaterialRequestsWithoutSupplierQuotationsAppService : IApplicationService
{
    Task<MaterialRequestsWithoutSupplierQuotationsReportDto> GetReportAsync(MaterialRequestsWithoutSupplierQuotationsFilterDto input);
}
