using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Manufacturing.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Manufacturing;

/// <summary>
/// Manages Manufacturing Settings per company.
/// Controls overproduction, backflush mode, capacity planning, and other manufacturing behaviors.
/// Per ERPNext: per-company configuration (not global singleton).
/// </summary>
[Authorize(MyERPPermissions.Manufacturing.Default)]
public class ManufacturingSettingsAppService : ApplicationService, IManufacturingSettingsAppService
{
    private readonly IRepository<ManufacturingSettings, Guid> _repository;

    public ManufacturingSettingsAppService(IRepository<ManufacturingSettings, Guid> repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Get manufacturing settings for a company. Returns null if not yet configured.
    /// </summary>
    public async Task<ManufacturingSettingsDto?> GetForCompanyAsync(Guid companyId)
    {
        var query = await _repository.GetQueryableAsync();
        var settings = query.FirstOrDefault(s => s.CompanyId == companyId);
        return settings != null ? ObjectMapper.Map<ManufacturingSettings, ManufacturingSettingsDto>(settings) : null;
    }

    /// <summary>
    /// Create or update manufacturing settings for a company.
    /// Enforces mutual exclusion rules automatically.
    /// </summary>
    [Authorize(MyERPPermissions.Manufacturing.Edit)]
    public async Task<ManufacturingSettingsDto> SaveAsync(SaveManufacturingSettingsDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var existing = query.FirstOrDefault(s => s.CompanyId == input.CompanyId);

        if (existing == null)
        {
            existing = new ManufacturingSettings(GuidGenerator.Create(), input.CompanyId, CurrentTenant.Id);
            await _repository.InsertAsync(existing);
        }

        existing.OverproductionPercentage = input.OverproductionPercentage;
        existing.BackflushRawMaterialsBasedOn = input.BackflushRawMaterialsBasedOn;
        existing.MaterialConsumption = input.MaterialConsumption;
        existing.TransferExtraMaterialsPercentage = input.TransferExtraMaterialsPercentage;
        existing.MinsBetweenOperations = input.MinsBetweenOperations;
        existing.CapacityPlanningForDays = input.CapacityPlanningForDays;
        existing.MakeSerialNoBatchFromWorkOrder = input.MakeSerialNoBatchFromWorkOrder;
        existing.UpdateBomCostsAutomatically = input.UpdateBomCostsAutomatically;
        existing.AllowOvertime = input.AllowOvertime;
        existing.AllowProductionOnHolidays = input.AllowProductionOnHolidays;
        existing.DisableCapacityPlanning = input.DisableCapacityPlanning;
        existing.JobCardExcessTransfer = input.JobCardExcessTransfer;
        existing.EnforceTimeLogs = input.EnforceTimeLogs;
        existing.AddCorrectiveOpCostInFGValuation = input.AddCorrectiveOpCostInFGValuation;
        existing.ValidateComponentsQuantitiesPerBom = input.ValidateComponentsQuantitiesPerBom;

        // Auto-enforce: if backflush != "BOM", validate_components forced off
        existing.EnforceMutualExclusions();

        await _repository.UpdateAsync(existing);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "ManufacturingSettings", existing.Id,
            "Saved", existing.CompanyId,
            "ManufacturingSettings", "", "Saved", CurrentUser.Id,
            $"Manufacturing settings updated for company {existing.CompanyId}", CurrentTenant.Id));

        return ObjectMapper.Map<ManufacturingSettings, ManufacturingSettingsDto>(existing);
    }
}
