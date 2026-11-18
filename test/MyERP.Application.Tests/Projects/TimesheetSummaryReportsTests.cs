using System;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.HumanResources.Entities;
using MyERP.Projects.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Projects;

public abstract class TimesheetSummaryReportsTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly ITimesheetAppService _timesheetAppService;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<Employee, Guid> _employeeRepository;
    private readonly IRepository<Project, Guid> _projectRepository;
    private readonly IRepository<ProjectTask, Guid> _taskRepository;
    private readonly IRepository<Timesheet, Guid> _timesheetRepository;

    protected TimesheetSummaryReportsTests()
    {
        _timesheetAppService = GetRequiredService<ITimesheetAppService>();
        _companyRepository = GetRequiredService<IRepository<Company, Guid>>();
        _employeeRepository = GetRequiredService<IRepository<Employee, Guid>>();
        _projectRepository = GetRequiredService<IRepository<Project, Guid>>();
        _taskRepository = GetRequiredService<IRepository<ProjectTask, Guid>>();
        _timesheetRepository = GetRequiredService<IRepository<Timesheet, Guid>>();
    }

    [Fact]
    public async Task Should_Return_Daily_Timesheet_Summary_For_Submitted_Logs_And_Exclude_Draft_And_Cancelled()
    {
        var companyId = await WithUnitOfWorkAsync(async () =>
        {
            var company = await _companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Acme Tech"), true);
            var emp1 = await _employeeRepository.InsertAsync(
                new Employee(Guid.NewGuid(), company.Id, "EMP-001", "Alice Wong"), true);
            var emp2 = await _employeeRepository.InsertAsync(
                new Employee(Guid.NewGuid(), company.Id, "EMP-002", "Bob Tan"), true);

            var project = await _projectRepository.InsertAsync(
                new Project(Guid.NewGuid(), company.Id, "PROJ-001", "ERP Rollout"), true);
            var task = await _taskRepository.InsertAsync(
                new ProjectTask(Guid.NewGuid(), project.Id, "TASK-001", "Configure Ledger"), true);

            var day = new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc);

            var submitted = new Timesheet(Guid.NewGuid(), company.Id, emp1.Id, day, day)
            {
                Title = "TS-SUB-01"
            };
            submitted.AddDetail(new TimesheetDetail(
                Guid.NewGuid(), submitted.Id, "Development",
                day.AddHours(9), day.AddHours(12), 3m)
            {
                ProjectId = project.Id,
                TaskId = task.Id,
                IsBillable = true,
                BillingRate = 100m
            });
            submitted.Submit();
            await _timesheetRepository.InsertAsync(submitted, true);

            var draft = new Timesheet(Guid.NewGuid(), company.Id, emp2.Id, day, day)
            {
                Title = "TS-DRF-01"
            };
            draft.AddDetail(new TimesheetDetail(
                Guid.NewGuid(), draft.Id, "Testing",
                day.AddHours(13), day.AddHours(15), 2m)
            {
                ProjectId = project.Id
            });
            await _timesheetRepository.InsertAsync(draft, true);

            var cancelled = new Timesheet(Guid.NewGuid(), company.Id, emp2.Id, day, day)
            {
                Title = "TS-CAN-01"
            };
            cancelled.AddDetail(new TimesheetDetail(
                Guid.NewGuid(), cancelled.Id, "Testing",
                day.AddHours(15), day.AddHours(17), 2m)
            {
                ProjectId = project.Id
            });
            cancelled.Submit();
            cancelled.Cancel();
            await _timesheetRepository.InsertAsync(cancelled, true);

            return company.Id;
        });

        var report = await _timesheetAppService.GetDailyTimesheetSummaryAsync(new DailyTimesheetSummaryRequestDto
        {
            CompanyId = companyId,
            FromDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            ToDate = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc)
        });

        report.EntryCount.ShouldBe(1);
        report.TotalHours.ShouldBe(3m);
        report.Rows.Count.ShouldBe(1);
        var row = report.Rows[0];
        row.TimesheetLabel.ShouldBe("TS-SUB-01");
        row.EmployeeName.ShouldBe("Alice Wong");
        row.ProjectNumber.ShouldBe("PROJ-001");
        row.ProjectName.ShouldBe("ERP Rollout");
        row.TaskNumber.ShouldBe("TASK-001");
        row.TaskSubject.ShouldBe("Configure Ledger");
        row.Hours.ShouldBe(3m);
        row.Status.ShouldBe(TimesheetStatus.Submitted);
    }

    [Fact]
    public async Task Should_Throw_When_Daily_Timesheet_Summary_Date_Range_Invalid()
    {
        var companyId = await WithUnitOfWorkAsync(async () =>
        {
            var company = await _companyRepository.InsertAsync(new Company(Guid.NewGuid(), "DateCheck Co"), true);
            return company.Id;
        });

        await Should.ThrowAsync<BusinessException>(async () =>
            await _timesheetAppService.GetDailyTimesheetSummaryAsync(new DailyTimesheetSummaryRequestDto
            {
                CompanyId = companyId,
                FromDate = new DateTime(2026, 6, 20, 0, 0, 0, DateTimeKind.Utc),
                ToDate = new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc)
            }));
    }

    [Fact]
    public async Task Should_Aggregate_Timesheet_Billing_Summary_And_Toggle_Drafts()
    {
        var setup = await WithUnitOfWorkAsync(async () =>
        {
            var company = await _companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Billing Tech"), true);
            var emp = await _employeeRepository.InsertAsync(
                new Employee(Guid.NewGuid(), company.Id, "EMP-010", "Carol Lim"), true);
            var project = await _projectRepository.InsertAsync(
                new Project(Guid.NewGuid(), company.Id, "PROJ-010", "Cloud Upgrade"), true);

            var day = new DateTime(2026, 7, 5, 0, 0, 0, DateTimeKind.Utc);

            var submitted = new Timesheet(Guid.NewGuid(), company.Id, emp.Id, day, day)
            {
                Title = "TS-BILL-01"
            };
            submitted.AddDetail(new TimesheetDetail(
                Guid.NewGuid(), submitted.Id, "Consulting",
                day.AddHours(9), day.AddHours(12), 3m)
            {
                ProjectId = project.Id,
                IsBillable = true,
                BillingRate = 200m
            });
            submitted.AddDetail(new TimesheetDetail(
                Guid.NewGuid(), submitted.Id, "Consulting",
                day.AddHours(13), day.AddHours(15), 2m)
            {
                ProjectId = project.Id,
                IsBillable = false,
                BillingRate = 0m
            });
            submitted.Submit();
            await _timesheetRepository.InsertAsync(submitted, true);

            var draft = new Timesheet(Guid.NewGuid(), company.Id, emp.Id, day, day)
            {
                Title = "TS-BILL-DRF"
            };
            draft.AddDetail(new TimesheetDetail(
                Guid.NewGuid(), draft.Id, "Support",
                day.AddHours(16), day.AddHours(18), 2m)
            {
                ProjectId = project.Id,
                IsBillable = true,
                BillingRate = 150m
            });
            await _timesheetRepository.InsertAsync(draft, true);

            return new { CompanyId = company.Id, ProjectId = project.Id };
        });

        var withoutDrafts = await _timesheetAppService.GetTimesheetBillingSummaryAsync(
            new TimesheetBillingSummaryRequestDto
            {
                CompanyId = setup.CompanyId,
                ProjectId = setup.ProjectId,
                FromDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                ToDate = new DateTime(2026, 7, 31, 0, 0, 0, DateTimeKind.Utc),
                IncludeDraftTimesheets = false
            });

        withoutDrafts.CurrencyCode.ShouldBe("MYR");
        withoutDrafts.Rows.Count.ShouldBe(1);
        withoutDrafts.TotalWorkingHours.ShouldBe(5m);
        withoutDrafts.TotalBillingHours.ShouldBe(3m);
        withoutDrafts.TotalBillingAmount.ShouldBe(600m);

        var withDrafts = await _timesheetAppService.GetTimesheetBillingSummaryAsync(
            new TimesheetBillingSummaryRequestDto
            {
                CompanyId = setup.CompanyId,
                ProjectId = setup.ProjectId,
                FromDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                ToDate = new DateTime(2026, 7, 31, 0, 0, 0, DateTimeKind.Utc),
                IncludeDraftTimesheets = true
            });

        withDrafts.Rows.Count.ShouldBe(2);
        withDrafts.TotalWorkingHours.ShouldBe(7m);
        withDrafts.TotalBillingHours.ShouldBe(5m);
        withDrafts.TotalBillingAmount.ShouldBe(900m);
    }
}
