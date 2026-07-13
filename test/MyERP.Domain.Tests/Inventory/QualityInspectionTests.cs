using System;
using MyERP.Inventory.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Inventory;

public class QualityInspectionTests
{
    private static QualityInspection CreateQI()
        => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), InspectionType.Incoming, DateTime.UtcNow);

    [Fact]
    public void Create_SetsDefaults()
    {
        var qi = CreateQI();
        qi.Status.ShouldBe(InspectionStatus.Draft);
        qi.DocStatus.ShouldBe(Core.DocumentStatus.Draft);
        qi.Readings.ShouldBeEmpty();
    }

    [Fact]
    public void AddReading_ValueBased_Accepted()
    {
        var qi = CreateQI();
        qi.AddReading("Color", "Red", null, null, "Red");
        qi.Evaluate();
        qi.Status.ShouldBe(InspectionStatus.Accepted);
        qi.Readings[0].Status.ShouldBe(InspectionStatus.Accepted);
    }

    [Fact]
    public void AddReading_ValueBased_Rejected()
    {
        var qi = CreateQI();
        qi.AddReading("Color", "Red", null, null, "Blue");
        qi.Evaluate();
        qi.Status.ShouldBe(InspectionStatus.Rejected);
    }

    [Fact]
    public void AddReading_NumericInRange_Accepted()
    {
        var qi = CreateQI();
        qi.AddReading("Weight", null, 10m, 20m, "15.5", isNumeric: true);
        qi.Evaluate();
        qi.Readings[0].Status.ShouldBe(InspectionStatus.Accepted);
    }

    [Fact]
    public void AddReading_NumericOutOfRange_Rejected()
    {
        var qi = CreateQI();
        qi.AddReading("Weight", null, 10m, 20m, "25", isNumeric: true);
        qi.Evaluate();
        qi.Readings[0].Status.ShouldBe(InspectionStatus.Rejected);
    }

    [Fact]
    public void ManualInspection_OverridesRejection()
    {
        var qi = CreateQI();
        qi.ManualInspection = true;
        qi.AddReading("Appearance", "Good", null, null, "Bad");
        qi.Evaluate();
        qi.Status.ShouldBe(InspectionStatus.Accepted); // manual override
    }

    [Fact]
    public void Submit_WithReadings_Succeeds()
    {
        var qi = CreateQI();
        qi.AddReading("Test", "OK", null, null, "OK");
        qi.Submit();
        qi.DocStatus.ShouldBe(Core.DocumentStatus.Submitted);
    }

    [Fact]
    public void Submit_WithoutReadings_Throws()
    {
        var qi = CreateQI();
        Should.Throw<BusinessException>(() => qi.Submit());
    }

    [Fact]
    public void Cancel_Submitted_Succeeds()
    {
        var qi = CreateQI();
        qi.AddReading("Test", "OK", null, null, "OK");
        qi.Submit();
        qi.Cancel();
        qi.DocStatus.ShouldBe(Core.DocumentStatus.Cancelled);
    }

    [Fact]
    public void MultipleReadings_OneRejected_OverallRejected()
    {
        var qi = CreateQI();
        qi.AddReading("Color", "Red", null, null, "Red");
        qi.AddReading("Weight", null, 10m, 20m, "25", isNumeric: true);
        qi.Evaluate();
        qi.Status.ShouldBe(InspectionStatus.Rejected);
    }
}
