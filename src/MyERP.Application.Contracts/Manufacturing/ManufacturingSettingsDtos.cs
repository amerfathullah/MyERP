using System;
using Volo.Abp.Application.Dtos;

namespace MyERP.Manufacturing;

public class ManufacturingSettingsDto : EntityDto<Guid>
{
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
