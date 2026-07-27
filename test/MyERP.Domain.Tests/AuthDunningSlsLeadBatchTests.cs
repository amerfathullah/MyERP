using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Sales.Entities;
using MyERP.Sales.DomainServices;
using MyERP.Support.Entities;
using MyERP.Support;
using MyERP.Core;
using MyERP.CRM;
using MyERP.CRM.Entities;
using MyERP.CRM.DomainServices;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using MyERP.Inventory;
using Shouldly;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for Authorization Control wiring, Dunning level sequencing + interest calculation,
/// Issue SLA hold-time tracking, Lead auto-status + contact creation, and Item batch/serial resolution.
/// </summary>
public class AuthDunningSlsLeadBatchTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _supplierId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();

    // --- Authorization Control ---

    [Fact]
    public void AuthorizationRule_IsExceeded_AboveThreshold_ReturnsTrue()
    {
        var rule = new AuthorizationRule(Guid.NewGuid(), "SalesOrder", AuthorizationBasedOn.GrandTotal, 10000m);
        rule.IsExceeded(15000m).ShouldBeTrue();
    }

    [Fact]
    public void AuthorizationRule_IsExceeded_BelowThreshold_ReturnsFalse()
    {
        var rule = new AuthorizationRule(Guid.NewGuid(), "SalesOrder", AuthorizationBasedOn.GrandTotal, 10000m);
        rule.IsExceeded(5000m).ShouldBeFalse();
    }

    [Fact]
    public void AuthorizationRule_IsExceeded_ExactThreshold_ReturnsFalse()
    {
        var rule = new AuthorizationRule(Guid.NewGuid(), "SalesOrder", AuthorizationBasedOn.GrandTotal, 10000m);
        rule.IsExceeded(10000m).ShouldBeFalse();
    }

    [Fact]
    public void AuthorizationRule_TransactionTypes_Include_AllDocuments()
    {
        // Verify we can create rules for all 6 transaction types
        var types = new[] { "SalesInvoice", "SalesOrder", "PurchaseOrder", "PurchaseInvoice", "DeliveryNote", "PurchaseReceipt" };
        foreach (var type in types)
        {
            var rule = new AuthorizationRule(Guid.NewGuid(), type, AuthorizationBasedOn.GrandTotal, 5000m);
            rule.TransactionType.ShouldBe(type);
        }
    }

    [Fact]
    public void AuthorizationRule_IsAuthorizedApprover_ByRole()
    {
        var rule = new AuthorizationRule(Guid.NewGuid(), "SalesOrder", AuthorizationBasedOn.GrandTotal, 10000m)
        { ApprovingRole = "Sales Manager" };
        rule.IsAuthorizedApprover(Guid.NewGuid(), new[] { "Sales Manager" }).ShouldBeTrue();
    }

    [Fact]
    public void AuthorizationRule_IsAuthorizedApprover_WrongRole()
    {
        var rule = new AuthorizationRule(Guid.NewGuid(), "SalesOrder", AuthorizationBasedOn.GrandTotal, 10000m)
        { ApprovingRole = "Sales Manager" };
        rule.IsAuthorizedApprover(Guid.NewGuid(), new[] { "Sales User" }).ShouldBeFalse();
    }

    // --- Dunning Level Sequencing ---

    [Fact]
    public void DunningManager_CalculateInterest_SingleInvoice()
    {
        // rate=12%/year, 30 days overdue, RM 10,000 outstanding
        // Daily rate = 12/100/365 = 0.000328767...
        // Interest = 0.000328767 × 30 × 10000 = 98.63
        var invoices = new List<(decimal outstanding, int overdueDays)> { (10000m, 30) };
        var interest = DunningManager.CalculateInterest(12m, invoices);
        interest.ShouldBeGreaterThan(98m);
        interest.ShouldBeLessThan(99m);
    }

    [Fact]
    public void DunningManager_CalculateInterest_MultipleInvoices()
    {
        var invoices = new List<(decimal outstanding, int overdueDays)>
        {
            (5000m, 60),
            (3000m, 30),
        };
        var interest = DunningManager.CalculateInterest(10m, invoices);
        interest.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void DunningManager_CalculateInterest_ZeroRate_ReturnsZero()
    {
        var invoices = new List<(decimal outstanding, int overdueDays)> { (10000m, 30) };
        DunningManager.CalculateInterest(0m, invoices).ShouldBe(0);
    }

    [Fact]
    public void DunningManager_CalculateInterest_EmptyInvoices_ReturnsZero()
    {
        DunningManager.CalculateInterest(12m, new List<(decimal, int)>()).ShouldBe(0);
    }

    [Fact]
    public void DunningManager_ShouldAutoResolve_AllPaid_ReturnsTrue()
    {
        var dunning = new Dunning(Guid.NewGuid(), _companyId, _customerId, DateTime.UtcNow, 1);
        dunning.AddOverduePayment(Guid.NewGuid(), 0m, DateTime.UtcNow.AddDays(-30), 30); // zero outstanding = paid
        dunning.Submit();
        DunningManager.ShouldAutoResolve(dunning).ShouldBeTrue();
    }

    [Fact]
    public void DunningManager_ShouldAutoResolve_NotSubmitted_ReturnsFalse()
    {
        var dunning = new Dunning(Guid.NewGuid(), _companyId, _customerId, DateTime.UtcNow, 1);
        dunning.AddOverduePayment(Guid.NewGuid(), 0m, DateTime.UtcNow.AddDays(-30), 30);
        DunningManager.ShouldAutoResolve(dunning).ShouldBeFalse(); // Draft
    }

    [Fact]
    public void Dunning_GrandTotal_Includes_Fee_And_Interest()
    {
        var dunning = new Dunning(Guid.NewGuid(), _companyId, _customerId, DateTime.UtcNow, 1)
        { DunningFee = 50m, InterestAmount = 98.63m };
        dunning.AddOverduePayment(Guid.NewGuid(), 10000m, DateTime.UtcNow.AddDays(-30), 30);
        dunning.GrandTotal.ShouldBe(10000m + 50m + 98.63m);
    }

    // --- Issue SLA Hold-Time Tracking ---

    [Fact]
    public void Issue_Hold_Sets_HoldStartedOn()
    {
        var issue = new Issue(Guid.NewGuid(), _companyId, "Test issue");
        issue.Hold();
        issue.Status.ShouldBe(IssueStatus.OnHold);
        issue.HoldStartedOn.ShouldNotBeNull();
    }

    [Fact]
    public void Issue_Reopen_From_Hold_Accumulates_HoldTime()
    {
        var issue = new Issue(Guid.NewGuid(), _companyId, "Test issue");
        issue.Hold();
        // Simulate time passing (set HoldStartedOn to 2 hours ago)
        issue.HoldStartedOn = DateTime.UtcNow.AddHours(-2);
        issue.Reopen();
        issue.TotalHoldTime.ShouldBeGreaterThan(1.9m); // ~2 hours
        issue.HoldStartedOn.ShouldBeNull(); // Cleared after accumulation
    }

    [Fact]
    public void Issue_Multiple_Holds_Accumulate()
    {
        var issue = new Issue(Guid.NewGuid(), _companyId, "Test issue");

        // First hold period: 1 hour
        issue.Hold();
        issue.HoldStartedOn = DateTime.UtcNow.AddHours(-1);
        issue.Reopen();
        var firstHold = issue.TotalHoldTime;
        firstHold.ShouldBeGreaterThan(0.9m);

        // Second hold period: 3 hours
        issue.Hold();
        issue.HoldStartedOn = DateTime.UtcNow.AddHours(-3);
        issue.Reopen();
        issue.TotalHoldTime.ShouldBeGreaterThan(firstHold + 2.9m); // ~1 + ~3 = ~4 hours
    }

    [Fact]
    public void Issue_Resolve_While_OnHold_Accumulates_HoldTime()
    {
        var issue = new Issue(Guid.NewGuid(), _companyId, "Test issue");
        issue.Hold();
        issue.HoldStartedOn = DateTime.UtcNow.AddHours(-1);
        issue.Resolve("Resolved while on hold");
        issue.TotalHoldTime.ShouldBeGreaterThan(0.9m);
        issue.HoldStartedOn.ShouldBeNull();
        issue.Status.ShouldBe(IssueStatus.Closed);
    }

    [Fact]
    public void Issue_ActualResolutionTimeHours_ExcludesHoldTime()
    {
        var issue = new Issue(Guid.NewGuid(), _companyId, "Test issue");
        // Simulate: opened 5 hours ago, held for 2 hours, then resolved
        var openingTime = DateTime.UtcNow.AddHours(-5);
        typeof(Issue).GetProperty("OpeningDate")!.SetValue(issue, openingTime);
        issue.TotalHoldTime = 2m;
        issue.Resolve();

        // Actual resolution time = ~5 hours - 2 hours hold = ~3 hours
        issue.ActualResolutionTimeHours.ShouldBeGreaterThan(2.5m);
        issue.ActualResolutionTimeHours.ShouldBeLessThan(3.5m);
    }

    [Fact]
    public void Issue_SLA_Breach_Detected_When_Exceeded()
    {
        var issue = new Issue(Guid.NewGuid(), _companyId, "Test issue");
        issue.ResolutionTime = 2m; // 2 hour SLA target
        var openingTime = DateTime.UtcNow.AddHours(-5);
        typeof(Issue).GetProperty("OpeningDate")!.SetValue(issue, openingTime);

        issue.Resolve();
        issue.IsSlaBreach.ShouldBeTrue(); // ~5 hours > 2 hour target
    }

    [Fact]
    public void Issue_SLA_No_Breach_When_Within_Target()
    {
        var issue = new Issue(Guid.NewGuid(), _companyId, "Test issue");
        issue.ResolutionTime = 24m; // 24 hour SLA target
        var openingTime = DateTime.UtcNow.AddHours(-1);
        typeof(Issue).GetProperty("OpeningDate")!.SetValue(issue, openingTime);

        issue.Resolve();
        issue.IsSlaBreach.ShouldBeFalse(); // ~1 hour < 24 hour target
    }

    [Fact]
    public void Issue_ActualFirstResponseTimeHours()
    {
        var issue = new Issue(Guid.NewGuid(), _companyId, "Test issue");
        var openingTime = DateTime.UtcNow.AddHours(-3);
        typeof(Issue).GetProperty("OpeningDate")!.SetValue(issue, openingTime);
        issue.Reply();
        issue.ActualFirstResponseTimeHours.ShouldBeGreaterThan(2.5m);
    }

    [Fact]
    public void Issue_ServiceLevelAgreementId_DefaultsNull()
    {
        var issue = new Issue(Guid.NewGuid(), _companyId, "Test issue");
        issue.ServiceLevelAgreementId.ShouldBeNull();
    }

    // --- Lead Auto-Status + Contact Creation ---

    [Fact]
    public void LeadManager_AutoAdvanceOnInteraction_FromNew_ToOpen()
    {
        var lead = new Lead(Guid.NewGuid(), _companyId, "L-001", "John");
        lead.Status.ShouldBe(LeadStatus.New);
        LeadManager.AutoAdvanceOnInteraction(lead);
        lead.Status.ShouldBe(LeadStatus.Open);
    }

    [Fact]
    public void LeadManager_AutoAdvanceOnInteraction_FromOpen_NoChange()
    {
        var lead = new Lead(Guid.NewGuid(), _companyId, "L-001", "John");
        lead.MarkOpen();
        LeadManager.AutoAdvanceOnInteraction(lead);
        lead.Status.ShouldBe(LeadStatus.Open); // Already open, no double-advance
    }

    [Fact]
    public void LeadManager_InferCustomerType_CompanyName_ReturnsCompany()
    {
        var lead = new Lead(Guid.NewGuid(), _companyId, "L-001", "John")
        { CompanyName = "ABC Corp" };
        LeadManager.InferCustomerType(lead).ShouldBe("Company");
    }

    [Fact]
    public void LeadManager_InferCustomerType_NoCompanyName_ReturnsIndividual()
    {
        var lead = new Lead(Guid.NewGuid(), _companyId, "L-001", "John");
        LeadManager.InferCustomerType(lead).ShouldBe("Individual");
    }

    [Fact]
    public void LeadManager_BuildContactFromLead_WithEmail()
    {
        var lead = new Lead(Guid.NewGuid(), _companyId, "L-001", "John")
        { LastName = "Doe", Email = "john@example.com", Phone = "+60123456789" };
        var contact = LeadManager.BuildContactFromLead(lead);
        contact.ShouldNotBeNull();
        contact!.FirstName.ShouldBe("John");
        contact.LastName.ShouldBe("Doe");
        contact.Email.ShouldBe("john@example.com");
        contact.Phone.ShouldBe("+60123456789");
    }

    [Fact]
    public void LeadManager_BuildContactFromLead_NoContactInfo_ReturnsNull()
    {
        var lead = new Lead(Guid.NewGuid(), _companyId, "L-001", "John");
        LeadManager.BuildContactFromLead(lead).ShouldBeNull();
    }

    [Fact]
    public void Lead_Qualify_FromOpen()
    {
        var lead = new Lead(Guid.NewGuid(), _companyId, "L-001", "John");
        lead.MarkOpen();
        lead.Qualify();
        lead.Status.ShouldBe(LeadStatus.Qualified);
    }

    [Fact]
    public void Lead_ConvertToCustomer_SetsConverted()
    {
        var lead = new Lead(Guid.NewGuid(), _companyId, "L-001", "John");
        var customerId = Guid.NewGuid();
        lead.ConvertToCustomer(customerId);
        lead.Status.ShouldBe(LeadStatus.Converted);
        lead.ConvertedCustomerId.ShouldBe(customerId);
    }

    // --- Error Code Verification ---

    [Fact]
    public void ErrorCode_DuplicateLeadEmail_Exists()
    {
        MyERPDomainErrorCodes.DuplicateLeadEmail.ShouldBe("MyERP:03022");
    }

    // --- Item Batch/Serial Resolution ---

    [Fact]
    public void Item_HasBatchNo_DefaultsFalse()
    {
        var item = new MyERP.Inventory.Entities.Item(Guid.NewGuid(), _companyId, "ITEM-001", "Test Item", ItemType.Goods);
        item.HasBatchNo.ShouldBeFalse();
    }

    [Fact]
    public void Item_HasSerialNo_DefaultsFalse()
    {
        var item = new MyERP.Inventory.Entities.Item(Guid.NewGuid(), _companyId, "ITEM-001", "Test Item", ItemType.Goods);
        item.HasSerialNo.ShouldBeFalse();
    }

    [Fact]
    public void Item_HasBatchNo_CanBeEnabled()
    {
        var item = new MyERP.Inventory.Entities.Item(Guid.NewGuid(), _companyId, "ITEM-001", "Test Item", ItemType.Goods)
        { HasBatchNo = true };
        item.HasBatchNo.ShouldBeTrue();
    }

    [Fact]
    public void Item_HasSerialNo_CanBeEnabled()
    {
        var item = new MyERP.Inventory.Entities.Item(Guid.NewGuid(), _companyId, "ITEM-001", "Test Item", ItemType.Goods)
        { HasSerialNo = true };
        item.HasSerialNo.ShouldBeTrue();
    }
}
