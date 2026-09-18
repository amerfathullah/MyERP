using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Manufacturing;

/// <summary>
/// Application service for Material Requirements Planning (MRP) report.
/// Generates time-bucketed material requirements across sales demand, BOM explosions, and scheduled receipts.
/// Maps to ERPNext manufacturing/report/material_requirements_planning_report.
/// </summary>
public interface IMaterialRequirementsPlanningAppService : IApplicationService
{
    Task<MaterialRequirementsPlanningReportDto> GetReportAsync(MaterialRequirementsPlanningFilterDto input);
}
