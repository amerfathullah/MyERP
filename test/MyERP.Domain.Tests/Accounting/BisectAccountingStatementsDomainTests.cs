using System;
using MyERP.Accounting.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Accounting;

public class BisectAccountingStatementsDomainTests
{
    [Fact]
    public void Should_Create_Valid_BisectAccountingStatements()
    {
        var id = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var fromDate = new DateTime(2026, 1, 1);
        var toDate = new DateTime(2026, 1, 31);

        var doc = new BisectAccountingStatements(id, companyId, fromDate, toDate, BisectAlgorithm.BFS);

        doc.Id.ShouldBe(id);
        doc.CompanyId.ShouldBe(companyId);
        doc.FromDate.ShouldBe(fromDate);
        doc.ToDate.ShouldBe(toDate);
        doc.Algorithm.ShouldBe(BisectAlgorithm.BFS);

        var nodeId = Guid.NewGuid();
        doc.SetCurrentNode(nodeId, fromDate, toDate, 1500m, 1200m);
        doc.CurrentNodeId.ShouldBe(nodeId);
        doc.PlSummary.ShouldBe(1500m);
        doc.BsSummary.ShouldBe(1200m);
        doc.Difference.ShouldBe(300m);
    }

    [Fact]
    public void Should_Throw_When_FromDate_Greater_Than_ToDate()
    {
        Should.Throw<BusinessException>(() =>
        {
            new BisectAccountingStatements(
                Guid.NewGuid(),
                Guid.NewGuid(),
                new DateTime(2026, 2, 1),
                new DateTime(2026, 1, 1));
        });
    }

    [Fact]
    public void Should_Calculate_Node_Difference_Correctly()
    {
        var node = new BisectNode(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateTime(2026, 1, 1),
            new DateTime(2026, 1, 15));

        node.SetSummary(2000m, 1850m);
        node.PlSummary.ShouldBe(2000m);
        node.BsSummary.ShouldBe(1850m);
        node.Difference.ShouldBe(150m);
        node.IsGenerated.ShouldBeTrue();
    }
}
