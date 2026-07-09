using System;
using MyERP.CRM.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.CRM;

public class OpportunityTests
{
    [Fact]
    public void Create_ShouldSetStatusToOpen()
    {
        var opp = CreateOpportunity();

        opp.Status.ShouldBe(OpportunityStatus.Open);
        opp.SalesStage.ShouldBe("Prospecting");
    }

    [Fact]
    public void MarkQuotation_FromOpen_ShouldSucceed()
    {
        var opp = CreateOpportunity();

        opp.MarkQuotation();

        opp.Status.ShouldBe(OpportunityStatus.Quotation);
    }

    [Fact]
    public void Convert_FromQuotation_ShouldSucceed()
    {
        var opp = CreateOpportunity();
        opp.MarkQuotation();

        opp.Convert();

        opp.Status.ShouldBe(OpportunityStatus.Converted);
    }

    [Fact]
    public void Convert_FromClosed_ShouldThrow()
    {
        var opp = CreateOpportunity();
        opp.Close();

        Assert.Throws<BusinessException>(() => opp.Convert());
    }

    [Fact]
    public void DeclareLost_ShouldSetReasonAndStatus()
    {
        var opp = CreateOpportunity();

        opp.DeclareLost("Budget constraints");

        opp.Status.ShouldBe(OpportunityStatus.Lost);
        opp.LostReason.ShouldBe("Budget constraints");
    }

    [Fact]
    public void DeclareLost_FromConverted_ShouldThrow()
    {
        var opp = CreateOpportunity();
        opp.Convert();

        Assert.Throws<BusinessException>(() => opp.DeclareLost("reason"));
    }

    [Fact]
    public void Reopen_FromLost_ShouldClearReason()
    {
        var opp = CreateOpportunity();
        opp.DeclareLost("Some reason");

        opp.Reopen();

        opp.Status.ShouldBe(OpportunityStatus.Open);
        opp.LostReason.ShouldBeNull();
    }

    [Fact]
    public void Reopen_FromOpen_ShouldThrow()
    {
        var opp = CreateOpportunity();

        Assert.Throws<BusinessException>(() => opp.Reopen());
    }

    [Fact]
    public void RecalculateAmount_ShouldSumItems()
    {
        var opp = CreateOpportunity();
        var oppId = opp.Id;
        opp.Items.Add(new OpportunityItem(Guid.NewGuid(), oppId, "Widget A", 10, 100));
        opp.Items.Add(new OpportunityItem(Guid.NewGuid(), oppId, "Widget B", 5, 200));

        opp.RecalculateAmount();

        opp.OpportunityAmount.ShouldBe(2000m); // (10*100) + (5*200)
    }

    [Fact]
    public void Close_FromOpen_ShouldSucceed()
    {
        var opp = CreateOpportunity();

        opp.Close();

        opp.Status.ShouldBe(OpportunityStatus.Closed);
    }

    private static Opportunity CreateOpportunity()
    {
        return new Opportunity(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "OPP-20260709-DEF456",
            "Enterprise ERP Deal",
            Guid.NewGuid());
    }
}
