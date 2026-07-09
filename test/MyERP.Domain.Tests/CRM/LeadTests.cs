using System;
using MyERP.CRM.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.CRM;

public class LeadTests
{
    [Fact]
    public void Create_ShouldSetStatusToNew()
    {
        var lead = CreateLead();

        lead.Status.ShouldBe(LeadStatus.New);
        lead.GetFullName().ShouldBe("John Doe");
    }

    [Fact]
    public void Qualify_FromOpen_ShouldSucceed()
    {
        var lead = CreateLead();
        lead.MarkOpen();

        lead.Qualify();

        lead.Status.ShouldBe(LeadStatus.Qualified);
    }

    [Fact]
    public void Qualify_FromNew_ShouldThrow()
    {
        var lead = CreateLead();

        Assert.Throws<BusinessException>(() => lead.Qualify());
    }

    [Fact]
    public void MarkInterested_FromOpen_ShouldSucceed()
    {
        var lead = CreateLead();
        lead.MarkOpen();

        lead.MarkInterested();

        lead.Status.ShouldBe(LeadStatus.Interested);
    }

    [Fact]
    public void ConvertToOpportunity_FromQualified_ShouldSetConverted()
    {
        var lead = CreateLead();
        lead.MarkOpen();
        lead.Qualify();
        var oppId = Guid.NewGuid();

        lead.ConvertToOpportunity(oppId);

        lead.Status.ShouldBe(LeadStatus.Converted);
        lead.ConvertedOpportunityId.ShouldBe(oppId);
    }

    [Fact]
    public void ConvertToOpportunity_FromLost_ShouldThrow()
    {
        var lead = CreateLead();
        lead.MarkOpen();
        lead.MarkLost();

        Assert.Throws<BusinessException>(() => lead.ConvertToOpportunity(Guid.NewGuid()));
    }

    [Fact]
    public void MarkLost_FromConverted_ShouldThrow()
    {
        var lead = CreateLead();
        lead.MarkOpen();
        lead.Qualify();
        lead.ConvertToOpportunity(Guid.NewGuid());

        Assert.Throws<BusinessException>(() => lead.MarkLost());
    }

    [Fact]
    public void Reopen_FromLost_ShouldSucceed()
    {
        var lead = CreateLead();
        lead.MarkOpen();
        lead.MarkLost();

        lead.Reopen();

        lead.Status.ShouldBe(LeadStatus.Open);
    }

    [Fact]
    public void Reopen_FromOpen_ShouldThrow()
    {
        var lead = CreateLead();
        lead.MarkOpen();

        Assert.Throws<BusinessException>(() => lead.Reopen());
    }

    [Fact]
    public void MarkDoNotContact_ShouldAlwaysSucceed()
    {
        var lead = CreateLead();
        lead.MarkOpen();

        lead.MarkDoNotContact();

        lead.Status.ShouldBe(LeadStatus.DoNotContact);
    }

    private static Lead CreateLead()
    {
        return new Lead(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "LEAD-20260709-ABC123",
            "John",
            Guid.NewGuid())
        {
            LastName = "Doe",
            Email = "john@example.com",
            CompanyName = "Acme Corp",
        };
    }
}
