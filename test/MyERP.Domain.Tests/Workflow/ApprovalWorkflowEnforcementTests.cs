using System;
using MyERP.Workflow;
using MyERP.Workflow.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Tests.Workflow;

public class ApprovalWorkflowEnforcementTests
{
    [Fact]
    public void ApprovalRule_Create_SetsDefaults()
    {
        var rule = new ApprovalRule(Guid.NewGuid(), "SalesOrder", "Manager Approval", 1);
        rule.DocumentType.ShouldBe("SalesOrder");
        rule.Name.ShouldBe("Manager Approval");
        rule.Level.ShouldBe(1);
        rule.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void ApprovalRule_WithAmountThreshold()
    {
        var rule = new ApprovalRule(Guid.NewGuid(), "PurchaseOrder", "Director Approval", 2);
        rule.MinimumAmount = 50000m;
        rule.MinimumAmount.ShouldBe(50000m);
    }

    [Fact]
    public void ApprovalRule_WithRoleName()
    {
        var rule = new ApprovalRule(Guid.NewGuid(), "SalesOrder", "Sales Mgr", 1);
        rule.ApproverRoleName = "SalesManager";
        rule.ApproverRoleName.ShouldBe("SalesManager");
    }

    [Fact]
    public void ApprovalRule_WithUserId()
    {
        var userId = Guid.NewGuid();
        var rule = new ApprovalRule(Guid.NewGuid(), "SalesInvoice", "CFO Review", 2);
        rule.ApproverUserId = userId;
        rule.ApproverUserId.ShouldBe(userId);
    }

    [Fact]
    public void ApprovalRule_WithCompanyScope()
    {
        var companyId = Guid.NewGuid();
        var rule = new ApprovalRule(Guid.NewGuid(), "PurchaseOrder", "Branch Approval", 1);
        rule.CompanyId = companyId;
        rule.CompanyId.ShouldBe(companyId);
    }

    [Fact]
    public void ApprovalRequest_Create_Pending()
    {
        var request = new ApprovalRequest(
            Guid.NewGuid(), Guid.NewGuid(), "SalesOrder",
            Guid.NewGuid(), 1, Guid.NewGuid());
        request.Status.ShouldBe(ApprovalStatus.Pending);
    }

    [Fact]
    public void ApprovalRequest_Approve_SetsApproved()
    {
        var request = new ApprovalRequest(
            Guid.NewGuid(), Guid.NewGuid(), "PurchaseOrder",
            Guid.NewGuid(), 1, Guid.NewGuid());
        var reviewerId = Guid.NewGuid();
        request.Approve(reviewerId, "Looks good");
        request.Status.ShouldBe(ApprovalStatus.Approved);
    }

    [Fact]
    public void ApprovalRequest_Reject_SetsRejected()
    {
        var request = new ApprovalRequest(
            Guid.NewGuid(), Guid.NewGuid(), "SalesOrder",
            Guid.NewGuid(), 1, Guid.NewGuid());
        request.Reject(Guid.NewGuid(), "Exceeds budget");
        request.Status.ShouldBe(ApprovalStatus.Rejected);
    }

    [Fact]
    public void ApprovalRequest_CannotApproveAlreadyApproved()
    {
        var request = new ApprovalRequest(
            Guid.NewGuid(), Guid.NewGuid(), "SalesOrder",
            Guid.NewGuid(), 1, Guid.NewGuid());
        request.Approve(Guid.NewGuid());
        Should.Throw<BusinessException>(() => request.Approve(Guid.NewGuid()));
    }

    [Fact]
    public void ApprovalRule_Inactive_NotApplicable()
    {
        var rule = new ApprovalRule(Guid.NewGuid(), "SalesOrder", "Disabled Rule", 1);
        rule.IsActive = false;
        rule.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void ApprovalRule_MultiLevel_Chain()
    {
        var rule1 = new ApprovalRule(Guid.NewGuid(), "PurchaseOrder", "Manager", 1);
        var rule2 = new ApprovalRule(Guid.NewGuid(), "PurchaseOrder", "Director", 2);
        var rule3 = new ApprovalRule(Guid.NewGuid(), "PurchaseOrder", "CFO", 3);

        rule1.Level.ShouldBeLessThan(rule2.Level);
        rule2.Level.ShouldBeLessThan(rule3.Level);
    }
}
