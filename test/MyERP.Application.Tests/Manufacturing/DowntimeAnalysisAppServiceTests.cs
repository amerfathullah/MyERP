using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.HumanResources.Entities;
using MyERP.Manufacturing.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Manufacturing;

public class DowntimeAnalysisAppServiceTests
{
    private readonly IRepository<DowntimeEntry, Guid> _downtimeRepo = Substitute.For<IRepository<DowntimeEntry, Guid>>();
    private readonly IRepository<Workstation, Guid> _workstationRepo = Substitute.For<IRepository<Workstation, Guid>>();
    private readonly IRepository<Employee, Guid> _employeeRepo = Substitute.For<IRepository<Employee, Guid>>();

    private readonly DowntimeAnalysisAppService _appService;
    private readonly Guid _companyId = Guid.NewGuid();

    public DowntimeAnalysisAppServiceTests()
    {
        _appService = new DowntimeAnalysisAppService(
            _downtimeRepo,
            _workstationRepo,
            _employeeRepo);
    }

    [Fact]
    public async Task GetReportAsync_ReturnsRows_WithHoursAndWorkstationOperatorResolved()
    {
        // Arrange
        var wsId = Guid.NewGuid();
        var workstation = new Workstation(wsId, _companyId, "CNC Machine 1");

        var empId = Guid.NewGuid();
        var employee = new Employee(empId, _companyId, "EMP-001", "Ahmad") { LastName = "Zakaria" };

        var fromTime = new DateTime(2026, 10, 15, 8, 0, 0);
        var toTime = new DateTime(2026, 10, 15, 9, 30, 0); // 90 minutes = 1.5 hours
        var entry = new DowntimeEntry(Guid.NewGuid(), _companyId, wsId, empId, fromTime, toTime, "Tool Breakage")
        {
            Remarks = "Drill bit snapped during heavy feed"
        };

        _downtimeRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<DowntimeEntry> { entry }.AsQueryable()));

        _workstationRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Workstation> { workstation }.AsQueryable()));

        _employeeRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Employee> { employee }.AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new DowntimeAnalysisRequestDto
        {
            CompanyId = _companyId
        });

        // Assert
        result.ShouldNotBeNull();
        result.TotalEntries.ShouldBe(1);
        result.TotalDowntimeMinutes.ShouldBe(90m);
        result.TotalDowntimeHours.ShouldBe(1.5m);
        result.Rows.Count.ShouldBe(1);

        var row = result.Rows[0];
        row.WorkstationName.ShouldBe("CNC Machine 1");
        row.OperatorName.ShouldBe("Ahmad Zakaria");
        row.DowntimeMinutes.ShouldBe(90m);
        row.DowntimeHours.ShouldBe(1.5m);
        row.StopReason.ShouldBe("Tool Breakage");
        row.Remarks.ShouldBe("Drill bit snapped during heavy feed");
    }

    [Fact]
    public async Task GetReportAsync_FiltersByDateRange()
    {
        // Arrange
        var wsId = Guid.NewGuid();
        var empId = Guid.NewGuid();

        var entryInside = new DowntimeEntry(Guid.NewGuid(), _companyId, wsId, empId,
            new DateTime(2026, 10, 15, 10, 0, 0), new DateTime(2026, 10, 15, 11, 0, 0), "Power Outage");

        var entryOutside = new DowntimeEntry(Guid.NewGuid(), _companyId, wsId, empId,
            new DateTime(2026, 10, 10, 10, 0, 0), new DateTime(2026, 10, 10, 11, 0, 0), "Maintenance");

        _downtimeRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<DowntimeEntry> { entryInside, entryOutside }.AsQueryable()));

        _workstationRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Workstation>().AsQueryable()));

        _employeeRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Employee>().AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new DowntimeAnalysisRequestDto
        {
            CompanyId = _companyId,
            FromDate = new DateTime(2026, 10, 14),
            ToDate = new DateTime(2026, 10, 16)
        });

        // Assert
        result.Rows.Count.ShouldBe(1);
        result.Rows[0].StopReason.ShouldBe("Power Outage");
    }

    [Fact]
    public async Task GetReportAsync_FiltersByWorkstation()
    {
        // Arrange
        var ws1 = Guid.NewGuid();
        var ws2 = Guid.NewGuid();
        var empId = Guid.NewGuid();

        var entry1 = new DowntimeEntry(Guid.NewGuid(), _companyId, ws1, empId,
            new DateTime(2026, 10, 15, 8, 0, 0), new DateTime(2026, 10, 15, 9, 0, 0), "Reason A");

        var entry2 = new DowntimeEntry(Guid.NewGuid(), _companyId, ws2, empId,
            new DateTime(2026, 10, 15, 8, 0, 0), new DateTime(2026, 10, 15, 9, 0, 0), "Reason B");

        _downtimeRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<DowntimeEntry> { entry1, entry2 }.AsQueryable()));

        _workstationRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Workstation>().AsQueryable()));

        _employeeRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Employee>().AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new DowntimeAnalysisRequestDto
        {
            CompanyId = _companyId,
            WorkstationId = ws1
        });

        // Assert
        result.Rows.Count.ShouldBe(1);
        result.Rows[0].WorkstationId.ShouldBe(ws1);
    }

    [Fact]
    public async Task GetReportAsync_ComputesWorkstationAndReasonSummaries()
    {
        // Arrange
        var ws1Id = Guid.NewGuid();
        var ws2Id = Guid.NewGuid();
        var ws1 = new Workstation(ws1Id, _companyId, "Lathe 1");
        var ws2 = new Workstation(ws2Id, _companyId, "Milling 1");

        var empId = Guid.NewGuid();

        var entry1 = new DowntimeEntry(Guid.NewGuid(), _companyId, ws1Id, empId,
            new DateTime(2026, 10, 15, 8, 0, 0), new DateTime(2026, 10, 15, 10, 0, 0), "Tool Wear"); // 120 mins = 2h

        var entry2 = new DowntimeEntry(Guid.NewGuid(), _companyId, ws1Id, empId,
            new DateTime(2026, 10, 15, 11, 0, 0), new DateTime(2026, 10, 15, 12, 0, 0), "No Material"); // 60 mins = 1h

        var entry3 = new DowntimeEntry(Guid.NewGuid(), _companyId, ws2Id, empId,
            new DateTime(2026, 10, 15, 13, 0, 0), new DateTime(2026, 10, 15, 14, 0, 0), "Tool Wear"); // 60 mins = 1h

        _downtimeRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<DowntimeEntry> { entry1, entry2, entry3 }.AsQueryable()));

        _workstationRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Workstation> { ws1, ws2 }.AsQueryable()));

        _employeeRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Employee>().AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new DowntimeAnalysisRequestDto
        {
            CompanyId = _companyId
        });

        // Assert
        result.TotalEntries.ShouldBe(3);
        result.TotalDowntimeHours.ShouldBe(4m);

        // Workstation summaries
        result.WorkstationSummaries.Count.ShouldBe(2);
        var latheSummary = result.WorkstationSummaries.First(s => s.Label == "Lathe 1");
        latheSummary.Hours.ShouldBe(3m);
        latheSummary.Count.ShouldBe(2);

        var millingSummary = result.WorkstationSummaries.First(s => s.Label == "Milling 1");
        millingSummary.Hours.ShouldBe(1m);
        millingSummary.Count.ShouldBe(1);

        // Reason summaries
        result.ReasonSummaries.Count.ShouldBe(2);
        var toolWearSummary = result.ReasonSummaries.First(s => s.Label == "Tool Wear");
        toolWearSummary.Hours.ShouldBe(3m);
        toolWearSummary.Count.ShouldBe(2);

        var noMaterialSummary = result.ReasonSummaries.First(s => s.Label == "No Material");
        noMaterialSummary.Hours.ShouldBe(1m);
        noMaterialSummary.Count.ShouldBe(1);
    }
}
