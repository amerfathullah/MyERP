using System;
using MyERP.Manufacturing.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Manufacturing;

public class ManufacturingSettingsTests
{
    [Fact]
    public void Create_SetsDefaults()
    {
        var settings = new ManufacturingSettings(Guid.NewGuid(), Guid.NewGuid());

        settings.OverproductionPercentage.ShouldBe(5m);
        settings.BackflushRawMaterialsBasedOn.ShouldBe("BOM");
        settings.MinsBetweenOperations.ShouldBe(10);
        settings.CapacityPlanningForDays.ShouldBe(30);
        settings.ValidateComponentsQuantitiesPerBom.ShouldBeTrue();
        settings.MaterialConsumption.ShouldBeFalse();
        settings.AllowOvertime.ShouldBeFalse();
        settings.AllowProductionOnHolidays.ShouldBeFalse();
    }

    [Fact]
    public void EnforceMutualExclusions_BomMode_KeepsValidation()
    {
        var settings = new ManufacturingSettings(Guid.NewGuid(), Guid.NewGuid());
        settings.BackflushRawMaterialsBasedOn = "BOM";
        settings.ValidateComponentsQuantitiesPerBom = true;

        settings.EnforceMutualExclusions();

        settings.ValidateComponentsQuantitiesPerBom.ShouldBeTrue();
    }

    [Fact]
    public void EnforceMutualExclusions_MaterialTransferredMode_ForcesValidationOff()
    {
        var settings = new ManufacturingSettings(Guid.NewGuid(), Guid.NewGuid());
        settings.BackflushRawMaterialsBasedOn = "Material Transferred";
        settings.ValidateComponentsQuantitiesPerBom = true;

        settings.EnforceMutualExclusions();

        // Per ERPNext: backflush != BOM forces validation off
        settings.ValidateComponentsQuantitiesPerBom.ShouldBeFalse();
    }

    [Fact]
    public void OverproductionPercentage_CanBeCustomized()
    {
        var settings = new ManufacturingSettings(Guid.NewGuid(), Guid.NewGuid());
        settings.OverproductionPercentage = 10m;

        settings.OverproductionPercentage.ShouldBe(10m);
    }

    [Fact]
    public void TransferExtraMaterials_DefaultZero()
    {
        var settings = new ManufacturingSettings(Guid.NewGuid(), Guid.NewGuid());
        settings.TransferExtraMaterialsPercentage.ShouldBe(0m);
    }
}
