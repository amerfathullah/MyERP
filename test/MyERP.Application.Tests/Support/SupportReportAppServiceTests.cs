using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.HumanResources.Entities;
using MyERP.Sales.Entities;
using MyERP.Support.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Support;

public abstract class SupportReportAppServiceTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly ISupportReportAppService _supportReportAppService;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IRepository<Employee, Guid> _employeeRepository;
    private readonly IRepository<Issue, Guid> _issueRepository;

    protected SupportReportAppServiceTests()
    {
        _supportReportAppService = GetRequiredService<ISupportReportAppService>();
        _companyRepository = GetRequiredService<IRepository<Company, Guid>>();
        _customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
        _employeeRepository = GetRequiredService<IRepository<Employee, Guid>>();
        _issueRepository = GetRequiredService<IRepository<Issue, Guid>>();
    }

    [Fact]
    public async Task Should_Compute_IssueSummary_And_Exclude_Sla_Status_Without_Sla()
    {
        var company = await _companyRepository.InsertAsync(
            new Company(Guid.NewGuid(), "Support Report Co"), autoSave: true);
        var customer = await _customerRepository.InsertAsync(
            new Customer(Guid.NewGuid(), company.Id, "Support Customer A"), autoSave: true);
        var agent = await _employeeRepository.InsertAsync(
            new Employee(Guid.NewGuid(), company.Id, "EMP-SUP-1", "Alice") { LastName = "Tan" }, autoSave: true);

        var now = DateTime.UtcNow;
        var opened = now.AddHours(-6);
        var slaId = Guid.NewGuid();
        var issue1 = new Issue(Guid.NewGuid(), company.Id, "Cannot login")
        {
            CustomerId = customer.Id,
            AssignedToId = agent.Id,
            Priority = "High",
            IssueType = "Bug",
            OpeningDate = opened,
            FirstRespondedOn = opened.AddHours(2),
        };
        issue1.ApplySla(slaId, 4m, 24m);
        issue1.Resolve("Reset password");
        issue1.ResolutionDate = opened.AddHours(6);
        await _issueRepository.InsertAsync(issue1, autoSave: true);

        // Issue without SLA should NOT count toward SLA Fulfilled/Failed/Due buckets
        var issue2 = new Issue(Guid.NewGuid(), company.Id, "Feature idea")
        {
            CustomerId = customer.Id,
            Priority = "Low",
            IssueType = "Feature",
            OpeningDate = opened.AddHours(1),
        };
        await _issueRepository.InsertAsync(issue2, autoSave: true);

        var summaryByCustomer = await _supportReportAppService.GetIssueSummaryAsync(new SupportReportFilterDto
        {
            CompanyId = company.Id,
            BasedOn = "Customer",
            FromDate = opened.Date.AddDays(-1),
            ToDate = now.Date.AddDays(1),
        });

        summaryByCustomer.TotalIssues.ShouldBe(2);
        summaryByCustomer.TotalOpen.ShouldBe(1);
        summaryByCustomer.TotalClosed.ShouldBe(1);
        summaryByCustomer.TotalSlaFulfilled.ShouldBe(1);
        var row = summaryByCustomer.Rows.Single();
        row.DimensionLabel.ShouldBe("Support Customer A");
        row.AvgFirstResponseTimeHours.ShouldBe(2m);
        row.AvgResolutionTimeHours.ShouldBe(6m);
        row.FirstResponseDueCount.ShouldBe(0);

        var summaryByAgent = await _supportReportAppService.GetIssueSummaryAsync(new SupportReportFilterDto
        {
            CompanyId = company.Id,
            BasedOn = "Assigned To",
            FromDate = opened.Date.AddDays(-1),
            ToDate = now.Date.AddDays(1),
        });
        summaryByAgent.Rows.ShouldContain(r => r.DimensionLabel == "Alice Tan" && r.TotalIssues == 1);
        summaryByAgent.Rows.ShouldContain(r => r.DimensionLabel == "Not Specified" && r.TotalIssues == 1);
    }

    [Fact]
    public async Task Should_Compute_IssueAnalytics_FirstResponseTime_And_HourDistribution()
    {
        var company = await _companyRepository.InsertAsync(
            new Company(Guid.NewGuid(), "Support Analytics Co"), autoSave: true);

        var i1 = new Issue(Guid.NewGuid(), company.Id, "Morning ticket")
        {
            Priority = "Urgent",
            OpeningDate = new DateTime(2026, 10, 15, 7, 30, 0, DateTimeKind.Utc), // Oct, slot 6AM-9AM (index 2)
            FirstRespondedOn = new DateTime(2026, 10, 15, 9, 0, 0, DateTimeKind.Utc), // 1.5h
        };
        var i2 = new Issue(Guid.NewGuid(), company.Id, "Afternoon ticket")
        {
            Priority = "Urgent",
            OpeningDate = new DateTime(2026, 11, 5, 13, 15, 0, DateTimeKind.Utc), // Nov, slot 12PM-3PM (index 4)
            FirstRespondedOn = new DateTime(2026, 11, 5, 16, 45, 0, DateTimeKind.Utc), // 3.5h
        };
        await _issueRepository.InsertAsync(i1, autoSave: true);
        await _issueRepository.InsertAsync(i2, autoSave: true);

        var analytics = await _supportReportAppService.GetIssueAnalyticsAsync(new SupportReportFilterDto
        {
            CompanyId = company.Id,
            BasedOn = "Issue Priority",
            Range = "Monthly",
            FromDate = new DateTime(2026, 10, 1),
            ToDate = new DateTime(2026, 11, 30),
        });

        analytics.PeriodLabels.ShouldBe(new[] { "Oct", "Nov" });
        analytics.GrandTotal.ShouldBe(2);
        var urgentRow = analytics.Rows.Single(r => r.DimensionLabel == "Urgent");
        urgentRow.PeriodCounts.ShouldBe(new[] { 1, 1 });

        var frt = await _supportReportAppService.GetFirstResponseTimeAsync(new SupportReportFilterDto
        {
            CompanyId = company.Id,
            FromDate = new DateTime(2026, 10, 1),
            ToDate = new DateTime(2026, 11, 30),
        });

        frt.TotalIssues.ShouldBe(2);
        frt.OverallAverageHours.ShouldBe(2.5m);
        frt.Rows.Count.ShouldBe(2);
        frt.Rows[0].Date.Date.ShouldBe(new DateTime(2026, 11, 5));
        frt.Rows[0].AvgFirstResponseTimeHours.ShouldBe(3.5m);

        var dist = await _supportReportAppService.GetSupportHourDistributionAsync(new SupportReportFilterDto
        {
            CompanyId = company.Id,
            FromDate = new DateTime(2026, 10, 15),
            ToDate = new DateTime(2026, 10, 15),
        });
        dist.TimeSlotLabels.Length.ShouldBe(8);
        dist.GrandTotal.ShouldBe(1);
        dist.SlotTotals[2].ShouldBe(1); // 6AM - 9AM

        await Should.ThrowAsync<BusinessException>(() =>
            _supportReportAppService.GetIssueSummaryAsync(new SupportReportFilterDto
            {
                CompanyId = company.Id,
                FromDate = new DateTime(2026, 11, 20),
                ToDate = new DateTime(2026, 11, 1),
            }));
    }
}
