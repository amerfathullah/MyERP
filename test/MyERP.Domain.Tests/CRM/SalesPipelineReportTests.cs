using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.CRM;
using MyERP.CRM.Entities;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;
using OpportunityTypeEnum = MyERP.CRM.OpportunityType;

namespace MyERP.Domain.Tests.CRM;

public class SalesPipelineReportTests
{
    private readonly Guid _companyA = Guid.NewGuid();
    private readonly Guid _companyB = Guid.NewGuid();

    private SalesPipelineAppService CreateAppService(
        List<Opportunity> opportunities,
        List<SalesStage>? salesStages = null)
    {
        var leadRepo = Substitute.For<IRepository<Lead, Guid>>();
        var quotationRepo = Substitute.For<IRepository<Quotation, Guid>>();
        var salesOrderRepo = Substitute.For<IRepository<SalesOrder, Guid>>();

        var oppRepo = Substitute.For<IRepository<Opportunity, Guid>>();
        oppRepo.GetQueryableAsync().Returns(Task.FromResult(opportunities.AsQueryable()));

        var stageRepo = Substitute.For<IRepository<SalesStage, Guid>>();
        stageRepo.GetListAsync().Returns(Task.FromResult(salesStages ?? new List<SalesStage>()));

        return new SalesPipelineAppService(
            leadRepo,
            oppRepo,
            quotationRepo,
            salesOrderRepo,
            stageRepo);
    }

    // =========================================================================
    // Opportunity MarkReplied & FirstResponseTime tracking (PR #59922)
    // =========================================================================

    [Fact]
    public void Opportunity_MarkReplied_Computes_FirstResponseTime()
    {
        var opp = new Opportunity(Guid.NewGuid(), _companyA, "OPP-001", "Website Inquiry")
        {
            CreationTime = DateTime.UtcNow.AddMinutes(-30)
        };

        opp.FirstResponseTime.ShouldBeNull();
        opp.FirstRespondedOn.ShouldBeNull();

        opp.MarkReplied();

        opp.Status.ShouldBe(OpportunityStatus.Replied);
        opp.FirstRespondedOn.ShouldNotBeNull();
        opp.FirstResponseTime.ShouldNotBeNull();
        opp.FirstResponseTime.Value.ShouldBeGreaterThanOrEqualTo(1700); // ~1800s (30m)
    }

    [Fact]
    public void Opportunity_SetFirstResponseTime_ExplicitDuration()
    {
        var baseTime = new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc);
        var opp = new Opportunity(Guid.NewGuid(), _companyA, "OPP-002", "Big Deal")
        {
            CreationTime = baseTime
        };

        opp.SetFirstResponseTime(baseTime.AddHours(2));

        opp.FirstResponseTime.ShouldBe(7200); // 2h = 7200s
    }

    // =========================================================================
    // ERPNext PR #59908 / commit f4feb24f70: Keep empty stage apart from "Not Set"
    // =========================================================================

    [Fact]
    public async Task OpportunitySummary_Keeps_EmptyStage_Apart_From_Stage_Named_NotSet()
    {
        var stages = new List<SalesStage>
        {
            new SalesStage(Guid.NewGuid(), "Not Set", sortOrder: 1),
            new SalesStage(Guid.NewGuid(), "Prospecting", sortOrder: 2)
        };

        var opp1 = new Opportunity(Guid.NewGuid(), _companyA, "OPP-1", "Opp With Stage Not Set")
        {
            OpportunityType = OpportunityTypeEnum.Sales,
            SalesStage = "Not Set",
            OpportunityAmount = 1000m
        };

        var opp2 = new Opportunity(Guid.NewGuid(), _companyA, "OPP-2", "Opp Without Stage")
        {
            OpportunityType = OpportunityTypeEnum.Sales,
            SalesStage = null, // Empty stage
            OpportunityAmount = 500m
        };

        var opp3 = new Opportunity(Guid.NewGuid(), _companyA, "OPP-3", "Prospecting Opp")
        {
            OpportunityType = OpportunityTypeEnum.Sales,
            SalesStage = "Prospecting",
            OpportunityAmount = 2000m
        };

        var appService = CreateAppService(new List<Opportunity> { opp1, opp2, opp3 }, stages);

        var result = await appService.GetOpportunitySummaryBySalesStageAsync(new GetOpportunitySummaryBySalesStageRequestDto
        {
            BasedOn = "Opportunity Type",
            DataBasedOn = "Amount",
            CompanyId = _companyA
        });

        // Columns must contain BOTH "Not Set" and "__no_sales_stage"
        var fieldnames = result.Columns.Select(c => c.Fieldname).ToList();
        fieldnames.ShouldContain("Not Set");
        fieldnames.ShouldContain(OpportunitySummaryConsts.NoSalesStageKey);
        fieldnames.ShouldContain("Prospecting");

        // Labels for both must display "Not Set"
        var notSetCol = result.Columns.First(c => c.Fieldname == "Not Set");
        notSetCol.Label.ShouldBe("Not Set");

        var emptyCol = result.Columns.First(c => c.Fieldname == OpportunitySummaryConsts.NoSalesStageKey);
        emptyCol.Label.ShouldBe("Not Set");

        // The two columns must have unique fieldnames (no collision)
        fieldnames.Distinct().Count().ShouldBe(fieldnames.Count);

        // Chart labels must have a point for each distinct stage column
        result.Chart.Labels.Count.ShouldBe(result.Columns.Count - 1);

        // Row values: "Not Set" stage got 1000, empty stage got 500
        var salesRow = result.Data.First(r => r.Key == OpportunityTypeEnum.Sales.ToString());
        salesRow.StageValues["Not Set"].ShouldBe(1000m);
        salesRow.StageValues[OpportunitySummaryConsts.NoSalesStageKey].ShouldBe(500m);
        salesRow.StageValues["Prospecting"].ShouldBe(2000m);
    }

    [Fact]
    public async Task OpportunitySummary_Throws_When_Amount_Without_Company()
    {
        var appService = CreateAppService(new List<Opportunity>());

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            appService.GetOpportunitySummaryBySalesStageAsync(new GetOpportunitySummaryBySalesStageRequestDto
            {
                DataBasedOn = "Amount",
                CompanyId = null
            }));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.CompanyMandatoryWhenDataBasedOnAmount);
    }

    [Fact]
    public async Task OpportunitySummary_Applies_ConversionRate()
    {
        var opp = new Opportunity(Guid.NewGuid(), _companyA, "OPP-USD", "US Client")
        {
            OpportunityType = OpportunityTypeEnum.Sales,
            SalesStage = "Proposal",
            OpportunityAmount = 100m,
            CurrencyCode = "USD",
            ConversionRate = 4.5m // 100 USD * 4.5 = 450 MYR
        };

        var appService = CreateAppService(new List<Opportunity> { opp });

        var result = await appService.GetOpportunitySummaryBySalesStageAsync(new GetOpportunitySummaryBySalesStageRequestDto
        {
            BasedOn = "Opportunity Type",
            DataBasedOn = "Amount",
            CompanyId = _companyA
        });

        var row = result.Data.First(r => r.Key == OpportunityTypeEnum.Sales.ToString());
        row.StageValues["Proposal"].ShouldBe(450m);
    }

    // =========================================================================
    // ERPNext PR #59922 / commit ab8a279282: First Response Time report
    // =========================================================================

    [Fact]
    public async Task FirstResponseTimeReport_Limits_To_Company_And_Averages()
    {
        var date1 = new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc);
        var date2 = new DateTime(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc);

        var opp1 = new Opportunity(Guid.NewGuid(), _companyA, "OPP-A1", "Opp 1")
        {
            CreationTime = date1.AddHours(1),
            FirstResponseTime = 3600 // 1 hour
        };
        var opp2 = new Opportunity(Guid.NewGuid(), _companyA, "OPP-A2", "Opp 2")
        {
            CreationTime = date1.AddHours(2),
            FirstResponseTime = 7200 // 2 hours -> avg = 5400
        };
        // Unanswered opportunity (response time 0 or null) - excluded from average
        var opp3 = new Opportunity(Guid.NewGuid(), _companyA, "OPP-A3", "Opp 3 Unanswered")
        {
            CreationTime = date1.AddHours(3),
            FirstResponseTime = 0
        };
        // Other company - excluded
        var oppOther = new Opportunity(Guid.NewGuid(), _companyB, "OPP-B1", "Other Co")
        {
            CreationTime = date1.AddHours(4),
            FirstResponseTime = 10000
        };

        var oppDate2 = new Opportunity(Guid.NewGuid(), _companyA, "OPP-A4", "Opp Date 2")
        {
            CreationTime = date2.AddHours(1),
            FirstResponseTime = 1800 // 30m
        };

        var appService = CreateAppService(new List<Opportunity> { opp1, opp2, opp3, oppOther, oppDate2 });

        var report = await appService.GetFirstResponseTimeReportAsync(new GetFirstResponseTimeReportRequestDto
        {
            CompanyId = _companyA
        });

        // 2 distinct creation dates
        report.Data.Count.ShouldBe(2);

        // Date 2 (descending order)
        var rowDate2 = report.Data.First(r => r.CreationDate.Date == date2.Date);
        rowDate2.AvgResponseTimeSeconds.ShouldBe(1800);
        rowDate2.FormattedResponseTime.ShouldBe("30m 0s");

        // Date 1: average of 3600 and 7200 = 5400
        var rowDate1 = report.Data.First(r => r.CreationDate.Date == date1.Date);
        rowDate1.AvgResponseTimeSeconds.ShouldBe(5400);
        rowDate1.FormattedResponseTime.ShouldBe("1h 30m");
    }

    // =========================================================================
    // ERPNext PR #59918 / commit c541ac3f98: Lost Opportunity keeps all reasons
    // =========================================================================

    [Fact]
    public async Task LostOpportunityReport_Keeps_Every_Lost_Reason_When_Filtering_By_One()
    {
        var opp1 = new Opportunity(Guid.NewGuid(), _companyA, "OPP-L1", "Lost Big Deal")
        {
            LostReason = "_Test Lost Reason A, _Test Lost Reason B",
            OpportunityAmount = 5000m,
            CreationTime = DateTime.UtcNow
        };
        opp1.DeclareLost("_Test Lost Reason A, _Test Lost Reason B");

        var opp2 = new Opportunity(Guid.NewGuid(), _companyA, "OPP-L2", "Lost Small Deal")
        {
            LostReason = "_Test Lost Reason B, _Test Lost Reason C",
            OpportunityAmount = 1000m,
            CreationTime = DateTime.UtcNow
        };
        opp2.DeclareLost("_Test Lost Reason B, _Test Lost Reason C");

        var opp3 = new Opportunity(Guid.NewGuid(), _companyA, "OPP-L3", "Lost Irrelevant")
        {
            LostReason = "_Test Lost Reason D",
            OpportunityAmount = 200m,
            CreationTime = DateTime.UtcNow
        };
        opp3.DeclareLost("_Test Lost Reason D");

        var appService = CreateAppService(new List<Opportunity> { opp1, opp2, opp3 });

        // Filter by reason A
        var report = await appService.GetLostOpportunityReportAsync(new GetLostOpportunityReportRequestDto
        {
            CompanyId = _companyA,
            LostReason = "_Test Lost Reason A"
        });

        // Must return only opp1
        report.Data.Count.ShouldBe(1);
        var row = report.Data[0];
        row.OpportunityNumber.ShouldBe("OPP-L1");

        // Crucial PR #59918 invariant: the LostReasons field MUST contain both Reason A AND Reason B!
        row.LostReasons.ShouldContain("_Test Lost Reason A");
        row.LostReasons.ShouldContain("_Test Lost Reason B");
    }
}
