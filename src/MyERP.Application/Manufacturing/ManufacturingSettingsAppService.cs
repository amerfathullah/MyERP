using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Manufacturing.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Manufacturing;

/// <summary>
/// Manages Manufacturing Settings per company.
/// Controls overproduction, backflush mode, capacity planning, and other manufacturing behaviors.
/// Per ERPNext: per-company configuration (not global singleton).
/// </summary>
[Authorize(MyERPPermissions.Manufacturing.Default)]
public class ManufacturingSettingsAppService : ApplicationService
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
        return settings != null ? MapToDto(settings) : null;
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
        return MapToDto(existing);
    }

    private static ManufacturingSettingsDto MapToDto(ManufacturingSettings s) => new()
    {
        Id = s.Id,
        CompanyId = s.CompanyId,
        OverproductionPercentage = s.OverproductionPercentage,
        BackflushRawMaterialsBasedOn = s.BackflushRawMaterialsBasedOn,
        MaterialConsumption = s.MaterialConsumption,
        TransferExtraMaterialsPercentage = s.TransferExtraMaterialsPercentage,
        MinsBetweenOperations = s.MinsBetweenOperations,
        CapacityPlanningForDays = s.CapacityPlanningForDays,
        MakeSerialNoBatchFromWorkOrder = s.MakeSerialNoBatchFromWorkOrder,
        UpdateBomCostsAutomatically = s.UpdateBomCostsAutomatically,
        AllowOvertime = s.AllowOvertime,
        AllowProductionOnHolidays = s.AllowProductionOnHolidays,
        DisableCapacityPlanning = s.DisableCapacityPlanning,
        JobCardExcessTransfer = s.JobCardExcessTransfer,
        EnforceTimeLogs = s.EnforceTimeLogs,
        AddCorrectiveOpCostInFGValuation = s.AddCorrectiveOpCostInFGValuation,
        ValidateComponentsQuantitiesPerBom = s.ValidateComponentsQuantitiesPerBom
    };
}

public class ManufacturingSettingsDto
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public decimal OverproductionPercentage { get; set; }
    public string BackflushRawMaterialsBasedOn { get; set; } = "BOM";
    public bool MaterialConsumption { get; set; }
    public decimal TransferExtraMaterialsPercentage { get; set; }
    public int MinsBetweenOperations { get; set; }
    public int CapacityPlanningForDays { get; set; }
    public bool MakeSerialNoBatchFromWorkOrder { get; set; }
    public bool UpdateBomCostsAutomatically { get; set; }
    public bool AllowOvertime { get; set; }
    public bool AllowProductionOnHolidays { get; set; }
    public bool DisableCapacityPlanning { get; set; }
    public bool JobCardExcessTransfer { get; set; }
    public bool EnforceTimeLogs { get; set; }
    public bool AddCorrectiveOpCostInFGValuation { get; set; }
    public bool ValidateComponentsQuantitiesPerBom { get; set; }
}

public class SaveManufacturingSettingsDto
{
    public Guid CompanyId { get; set; }
    public decimal OverproductionPercentage { get; set; } = 5m;
    public string BackflushRawMaterialsBasedOn { get; set; } = "BOM";
    public bool MaterialConsumption { get; set; }
    public decimal TransferExtraMaterialsPercentage { get; set; }
    public int MinsBetweenOperations { get; set; } = 10;
    public int CapacityPlanningForDays { get; set; } = 30;
    public bool MakeSerialNoBatchFromWorkOrder { get; set; }
    public bool UpdateBomCostsAutomatically { get; set; }
    public bool AllowOvertime { get; set; }
    public bool AllowProductionOnHolidays { get; set; }
    public bool DisableCapacityPlanning { get; set; }
    public bool JobCardExcessTransfer { get; set; }
    public bool EnforceTimeLogs { get; set; }
    public bool AddCorrectiveOpCostInFGValuation { get; set; }
    public bool ValidateComponentsQuantitiesPerBom { get; set; } = true;
}
