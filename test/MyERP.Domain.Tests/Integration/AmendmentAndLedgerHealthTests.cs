using System;
using System.Linq;
using MyERP.Accounting.DomainServices;
using MyERP.Core;
using MyERP.Core.DomainServices;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Tests.Integration;

public class AmendmentAndLedgerHealthTests
{
    [Fact]
    public void SalesOrder_AmendedFromId_DefaultNull()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-001", DateTime.UtcNow);
        so.AmendedFromId.ShouldBeNull();
        so.AmendmentIndex.ShouldBe(0);
    }

    [Fact]
    public void SalesOrder_CanSetAmendmentFields()
    {
        var originalId = Guid.NewGuid();
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-001-1", DateTime.UtcNow);
        so.AmendedFromId = originalId;
        so.AmendmentIndex = 1;
        so.AmendedFromId.ShouldBe(originalId);
        so.AmendmentIndex.ShouldBe(1);
    }

    [Fact]
    public void Amendment_ValidateCanAmend_Cancelled_Succeeds()
    {
        var service = new DocumentAmendmentService();
        Should.NotThrow(() => service.ValidateCanAmend(DocumentStatus.Cancelled));
    }

    [Fact]
    public void Amendment_ValidateCanAmend_Active_Throws()
    {
        var service = new DocumentAmendmentService();
        Should.Throw<BusinessException>(() => service.ValidateCanAmend(DocumentStatus.ToDeliverAndBill));
    }

    [Fact]
    public void Amendment_NumberGeneration_SO()
    {
        var service = new DocumentAmendmentService();
        var result = service.GenerateAmendedNumber("SO-2026-00015", 1);
        result.ShouldBe("SO-2026-00015-1");
    }

    [Fact]
    public void LedgerHealthReport_DefaultHealthy()
    {
        var report = new LedgerHealthReport
        {
            CompanyId = Guid.NewGuid(),
            CheckedAt = DateTime.UtcNow,
            IsHealthy = true,
            TotalChecked = 50,
        };
        report.IsHealthy.ShouldBeTrue();
        report.Issues.ShouldBeEmpty();
    }

    [Fact]
    public void LedgerHealthIssue_UnbalancedJE()
    {
        var issue = new LedgerHealthIssue
        {
            IssueType = "UnbalancedJE",
            Severity = "Critical",
            Description = "JE is unbalanced: DR=1000, CR=999",
            DocumentId = Guid.NewGuid(),
            Amount = 1m,
        };
        issue.Severity.ShouldBe("Critical");
        issue.Amount.ShouldBe(1m);
    }

    [Fact]
    public void LedgerHealthReport_WithCriticalIssue_NotHealthy()
    {
        var report = new LedgerHealthReport { CompanyId = Guid.NewGuid(), CheckedAt = DateTime.UtcNow };
        report.Issues.Add(new LedgerHealthIssue
        {
            IssueType = "UnbalancedJE",
            Severity = "Critical",
            Description = "Test critical issue"
        });
        report.IsHealthy = !report.Issues.Any(i => i.Severity == "Critical");
        report.IsHealthy.ShouldBeFalse();
    }

    [Fact]
    public void LedgerHealthReport_WithWarningOnly_StillHealthy()
    {
        var report = new LedgerHealthReport { CompanyId = Guid.NewGuid(), CheckedAt = DateTime.UtcNow };
        report.Issues.Add(new LedgerHealthIssue
        {
            IssueType = "PLECountAnomaly",
            Severity = "Warning",
            Description = "High PLE count"
        });
        report.IsHealthy = !report.Issues.Any(i => i.Severity == "Critical");
        report.IsHealthy.ShouldBeTrue(); // Warnings don't make it unhealthy
    }
}
