using System;
using Xunit;
using MyERP.HumanResources.Entities;
using MyERP.HumanResources;
using MyERP.CRM.Entities;
using MyERP.CRM;
using MyERP.Support.Entities;
using MyERP.Support;
using MyERP.Accounting.Entities;

namespace MyERP.DomainTests;

/// <summary>
/// Tests for detail views, workflow actions, and orphaned service cleanup.
/// Covers: Employee, Opportunity, Issue, AccountingPeriod, Contract, Prospect.
/// </summary>
public class DetailViewAndWorkflowTests
{
    [Fact]
    public void Employee_FullName_Contains_FirstName()
    {
        var emp = new Employee(Guid.NewGuid(), Guid.NewGuid(), "EMP-001", "John");
        Assert.Contains("John", emp.FullName);
    }

    [Fact]
    public void Employee_DateOfJoining_DefaultsNull()
    {
        var emp = new Employee(Guid.NewGuid(), Guid.NewGuid(), "EMP-002", "Alice");
        Assert.Null(emp.DateOfJoining);
    }

    [Fact]
    public void Employee_Status_DefaultsActive()
    {
        var emp = new Employee(Guid.NewGuid(), Guid.NewGuid(), "EMP-003", "Carol");
        Assert.Equal(EmploymentStatus.Active, emp.Status);
    }

    [Fact]
    public void Opportunity_DeclareLost_FromOpen()
    {
        var opp = new Opportunity(Guid.NewGuid(), Guid.NewGuid(), "OPP-001", "Deal A");
        opp.DeclareLost("Competitor won");
        Assert.Equal(OpportunityStatus.Lost, opp.Status);
    }

    [Fact]
    public void Opportunity_Close_FromOpen()
    {
        var opp = new Opportunity(Guid.NewGuid(), Guid.NewGuid(), "OPP-002", "Deal B");
        opp.Close();
        Assert.Equal(OpportunityStatus.Closed, opp.Status);
    }

    [Fact]
    public void Opportunity_Reopen_FromClosed()
    {
        var opp = new Opportunity(Guid.NewGuid(), Guid.NewGuid(), "OPP-003", "Deal C");
        opp.Close();
        opp.Reopen();
        Assert.Equal(OpportunityStatus.Open, opp.Status);
    }

    [Fact]
    public void Opportunity_Reopen_FromLost()
    {
        var opp = new Opportunity(Guid.NewGuid(), Guid.NewGuid(), "OPP-004", "Deal D");
        opp.DeclareLost("Price too high");
        opp.Reopen();
        Assert.Equal(OpportunityStatus.Open, opp.Status);
    }

    [Fact]
    public void Issue_Hold_FromOpen()
    {
        var issue = new Issue(Guid.NewGuid(), Guid.NewGuid(), "Bug report");
        issue.Hold();
        Assert.Equal(IssueStatus.OnHold, issue.Status);
    }

    [Fact]
    public void Issue_Reopen_FromClosed()
    {
        var issue = new Issue(Guid.NewGuid(), Guid.NewGuid(), "Feature request");
        issue.Resolve();
        issue.Reopen();
        Assert.Equal(IssueStatus.Open, issue.Status);
    }

    [Fact]
    public void Issue_Hold_FromReplied()
    {
        var issue = new Issue(Guid.NewGuid(), Guid.NewGuid(), "Support ticket");
        issue.Reply();
        issue.Hold();
        Assert.Equal(IssueStatus.OnHold, issue.Status);
    }

    [Fact]
    public void AccountingPeriod_DefaultOpen()
    {
        var period = new AccountingPeriod(Guid.NewGuid(), Guid.NewGuid(), "Q1-2026",
            new DateTime(2026, 1, 1), new DateTime(2026, 3, 31));
        Assert.False(period.IsClosed);
    }

    [Fact]
    public void AccountingPeriod_Close()
    {
        var period = new AccountingPeriod(Guid.NewGuid(), Guid.NewGuid(), "Q2-2026",
            new DateTime(2026, 4, 1), new DateTime(2026, 6, 30));
        period.Close();
        Assert.True(period.IsClosed);
    }

    [Fact]
    public void AccountingPeriod_CloseDocumentType_Specific()
    {
        var period = new AccountingPeriod(Guid.NewGuid(), Guid.NewGuid(), "Q3-2026",
            new DateTime(2026, 7, 1), new DateTime(2026, 9, 30));
        period.CloseDocumentType("SalesInvoice");
        Assert.True(period.IsClosedForDocumentType("SalesInvoice"));
        Assert.False(period.IsClosedForDocumentType("PurchaseInvoice"));
    }

    [Fact]
    public void Contract_DefaultUnsigned()
    {
        var contract = new Contract(Guid.NewGuid(), Guid.NewGuid(), "CTR-001",
            "Customer", Guid.NewGuid(), DateTime.Today);
        Assert.Equal(ContractStatus.Unsigned, contract.Status);
    }

    [Fact]
    public void Contract_Sign_TransitionsToActive()
    {
        var contract = new Contract(Guid.NewGuid(), Guid.NewGuid(), "CTR-002",
            "Customer", Guid.NewGuid(), DateTime.Today);
        contract.Sign(DateTime.Today);
        Assert.Equal(ContractStatus.Active, contract.Status);
    }

    [Fact]
    public void Contract_Cancel_FromActive()
    {
        var contract = new Contract(Guid.NewGuid(), Guid.NewGuid(), "CTR-003",
            "Customer", Guid.NewGuid(), DateTime.Today);
        contract.Sign(DateTime.Today);
        contract.Cancel();
        Assert.Equal(ContractStatus.Cancelled, contract.Status);
    }

    [Fact]
    public void Prospect_DefaultState()
    {
        var prospect = new Prospect(Guid.NewGuid(), Guid.NewGuid(), "Acme Corp");
        Assert.Equal("Acme Corp", prospect.ProspectName);
    }

    [Fact]
    public void TaxEngine_Deleted_Superseded_By_TaxesAndTotalsService()
    {
        var type = Type.GetType("MyERP.Tax.DomainServices.TaxEngine, MyERP.Domain");
        Assert.Null(type);
    }

    [Fact]
    public void DocumentStatusGuardService_Deleted()
    {
        var type = Type.GetType("MyERP.Core.DomainServices.DocumentStatusGuardService, MyERP.Domain");
        Assert.Null(type);
    }
}
