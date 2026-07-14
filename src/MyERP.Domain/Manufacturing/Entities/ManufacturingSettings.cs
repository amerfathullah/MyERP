using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Manufacturing.Entities;

/// <summary>
/// Manufacturing Settings — singleton per-company configuration.
/// Maps to ERPNext manufacturing/doctype/manufacturing_settings.
/// Per settings-configuration.instructions.md: 14 critical flags.
/// </summary>
public class ManufacturingSettings : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    /// <summary>Maximum overproduction allowed beyond Work Order qty (default 5%).</summary>
    public decimal OverproductionPercentage { get; set; } = 5m;

    /// <summary>"BOM" = consume per BOM qty, "Material Transferred" = consume what was actually transferred.</summary>
    public string BackflushRawMaterialsBasedOn { get; set; } = "BOM";

    /// <summary>Track actual vs planned material consumption.</summary>
    public bool MaterialConsumption { get; set; }

    /// <summary>Extra material percentage allowed per Work Order beyond BOM qty.</summary>
    public decimal TransferExtraMaterialsPercentage { get; set; }

    /// <summary>Minimum gap between sequential operations in minutes.</summary>
    public int MinsBetweenOperations { get; set; } = 10;

    /// <summary>Planning horizon for capacity planning (days).</summary>
    public int CapacityPlanningForDays { get; set; } = 30;

    /// <summary>Auto-generate serial/batch numbers from Work Order.</summary>
    public bool MakeSerialNoBatchFromWorkOrder { get; set; }

    /// <summary>Automatically update BOM costs when RM prices change.</summary>
    public bool UpdateBomCostsAutomatically { get; set; }

    /// <summary>Allow production outside working hours.</summary>
    public bool AllowOvertime { get; set; }

    /// <summary>Allow production on holidays.</summary>
    public bool AllowProductionOnHolidays { get; set; }

    /// <summary>Skip capacity checks in scheduling.</summary>
    public bool DisableCapacityPlanning { get; set; }

    /// <summary>Allow extra material transfer per Job Card.</summary>
    public bool JobCardExcessTransfer { get; set; }

    /// <summary>Require time logs on Job Cards.</summary>
    public bool EnforceTimeLogs { get; set; }

    /// <summary>Include corrective operation cost in FG valuation.</summary>
    public bool AddCorrectiveOpCostInFGValuation { get; set; }

    /// <summary>Validate component quantities against BOM (auto-disabled when backflush != BOM).</summary>
    public bool ValidateComponentsQuantitiesPerBom { get; set; } = true;

    protected ManufacturingSettings() { }

    public ManufacturingSettings(Guid id, Guid companyId, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        TenantId = tenantId;
    }

    /// <summary>
    /// Per ERPNext settings-configuration: mutual exclusion rule.
    /// If backflush != "BOM", ValidateComponentsQuantitiesPerBom is forced off.
    /// </summary>
    public void EnforceMutualExclusions()
    {
        if (BackflushRawMaterialsBasedOn != "BOM")
        {
            ValidateComponentsQuantitiesPerBom = false;
        }
    }
}
