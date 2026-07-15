using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using MyERP.Projects.Entities;
using MyERP.Shared;
using Xunit;

namespace MyERP;

/// <summary>
/// Tests verifying backend filter DTOs and recently-added entity interactions.
/// </summary>
public class BackendFilterAndEntityTests
{
    // --- CompanyFilteredPagedRequestDto tests ---

    [Fact]
    public void CompanyFilteredPagedRequestDto_DefaultValues()
    {
        var dto = new CompanyFilteredPagedRequestDto();
        Assert.Null(dto.CompanyId);
        Assert.Null(dto.Filter);
        Assert.Null(dto.Status);
        Assert.Equal(0, dto.SkipCount);
        Assert.Equal(10, dto.MaxResultCount);
    }

    [Fact]
    public void CompanyFilteredPagedRequestDto_AllFieldsSettable()
    {
        var companyId = Guid.NewGuid();
        var dto = new CompanyFilteredPagedRequestDto
        {
            CompanyId = companyId,
            Filter = "test search",
            Status = "Posted",
            SkipCount = 20,
            MaxResultCount = 50,
        };
        Assert.Equal(companyId, dto.CompanyId);
        Assert.Equal("test search", dto.Filter);
        Assert.Equal("Posted", dto.Status);
        Assert.Equal(20, dto.SkipCount);
        Assert.Equal(50, dto.MaxResultCount);
    }

    // --- BOM filter concept tests ---

    [Fact]
    public void BOM_BomNumber_IsSearchable()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-2026-00001", Guid.NewGuid());
        Assert.Contains("BOM-2026", bom.BomNumber);
    }

    [Fact]
    public void BOM_BomNumber_CaseInsensitiveSearch()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-2026-00001", Guid.NewGuid());
        Assert.Contains("bom-2026", bom.BomNumber.ToLower());
    }

    [Fact]
    public void BOM_Quantity_DefaultsToOne()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-001", Guid.NewGuid());
        Assert.Equal(1m, bom.Quantity);
    }

    // --- Timesheet filter concept tests ---

    [Fact]
    public void Timesheet_EmployeeName_IsFilterable()
    {
        var ts = new Timesheet(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.Today, DateTime.Today.AddDays(7))
        {
            EmployeeName = "Ahmad Bin Abdullah"
        };
        Assert.Contains("ahmad", ts.EmployeeName.ToLower());
    }

    [Fact]
    public void Timesheet_Status_Default_Is_Draft()
    {
        var ts = new Timesheet(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.Today, DateTime.Today.AddDays(7));
        Assert.Equal(TimesheetStatus.Draft, ts.Status);
    }

    // --- Workstation DTO tests ---

    [Fact]
    public void WorkstationDto_Costs_EmptyByDefault()
    {
        var dto = new WorkstationDto();
        Assert.Empty(dto.Costs);
        Assert.Empty(dto.WorkingHours);
    }

    [Fact]
    public void WorkstationDto_MultipleCosts_SumToHourRate()
    {
        var ws = new Workstation(Guid.NewGuid(), Guid.NewGuid(), "CNC-01");
        ws.AddCost("Labor", 30m);
        ws.AddCost("Power", 12m);
        ws.AddCost("Rent", 8m);
        Assert.Equal(50m, ws.HourRate);
        Assert.Equal(3, ws.Costs.Count);
    }

    // --- Workstation entity edge cases ---

    [Fact]
    public void Workstation_EmptyName_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Workstation(Guid.NewGuid(), Guid.NewGuid(), ""));
    }

    [Fact]
    public void Workstation_DefaultCapacity_Is_One()
    {
        var ws = new Workstation(Guid.NewGuid(), Guid.NewGuid(), "WS");
        Assert.Equal(1, ws.ProductionCapacity);
    }

    [Fact]
    public void Workstation_HourRate_Zero_WithNoCosts()
    {
        var ws = new Workstation(Guid.NewGuid(), Guid.NewGuid(), "WS");
        Assert.Equal(0m, ws.HourRate);
    }

    [Fact]
    public void Workstation_IsActive_DefaultTrue()
    {
        var ws = new Workstation(Guid.NewGuid(), Guid.NewGuid(), "Active WS");
        Assert.True(ws.IsActive);
    }

    [Fact]
    public void Workstation_WorkingHour_Added()
    {
        var ws = new Workstation(Guid.NewGuid(), Guid.NewGuid(), "WS");
        ws.AddWorkingHour("Monday", new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0));
        Assert.Single(ws.WorkingHours);
        Assert.Equal("Monday", ws.WorkingHours[0].Day);
    }

    // --- WorkstationDto mapping ---

    [Fact]
    public void WorkstationCostDto_Properties()
    {
        var dto = new WorkstationCostDto { Name = "Gas", Amount = 5.25m };
        Assert.Equal("Gas", dto.Name);
        Assert.Equal(5.25m, dto.Amount);
    }

    [Fact]
    public void WorkstationWorkingHourDto_Properties()
    {
        var dto = new WorkstationWorkingHourDto { DayOfWeek = "Friday", StartTime = "09:00", EndTime = "18:00" };
        Assert.Equal("Friday", dto.DayOfWeek);
        Assert.Equal("09:00", dto.StartTime);
        Assert.Equal("18:00", dto.EndTime);
    }

    [Fact]
    public void CreateWorkstationDto_HasRequiredFields()
    {
        var dto = new CreateWorkstationDto
        {
            CompanyId = Guid.NewGuid(),
            Name = "Press 01",
            WorkstationType = "Assembly",
            ProductionCapacity = 3,
        };
        Assert.NotEqual(Guid.Empty, dto.CompanyId);
        Assert.Equal("Press 01", dto.Name);
        Assert.Equal(3, dto.ProductionCapacity);
    }

    // --- Filter query concept: case-insensitive Contains ---

    [Fact]
    public void Filter_CaseInsensitive_MatchesUppercase()
    {
        var filter = "CNC";
        var name = "CNC Machine Alpha";
        Assert.Contains(filter.ToLower(), name.ToLower());
    }

    [Fact]
    public void Filter_CaseInsensitive_MatchesLowercase()
    {
        var filter = "cnc";
        var name = "CNC Machine Alpha";
        Assert.Contains(filter.ToLower(), name.ToLower());
    }

    [Fact]
    public void Filter_NoMatch_ReturnsFalse()
    {
        var filter = "xyz";
        var name = "CNC Machine Alpha";
        Assert.DoesNotContain(filter.ToLower(), name.ToLower());
    }
}
