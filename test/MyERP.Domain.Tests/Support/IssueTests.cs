using System;
using MyERP.Support.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Support;

public class IssueTests
{
    [Fact]
    public void Create_SetsOpenStatus()
    {
        var issue = CreateIssue("Printer not working");
        issue.Status.ShouldBe(IssueStatus.Open);
        issue.Subject.ShouldBe("Printer not working");
    }

    [Fact]
    public void Reply_SetsFirstRespondedOn()
    {
        var issue = CreateIssue("Login error");

        issue.Reply();

        issue.Status.ShouldBe(IssueStatus.Replied);
        issue.FirstRespondedOn.ShouldNotBeNull();
    }

    [Fact]
    public void Reply_DoesNotOverwriteFirstResponse()
    {
        var issue = CreateIssue("Bug");
        issue.Reply();
        var firstResponse = issue.FirstRespondedOn;

        issue.Reopen();
        issue.Reply();

        // FirstRespondedOn should NOT change on second reply
        issue.FirstRespondedOn.ShouldBe(firstResponse);
    }

    [Fact]
    public void Hold_FromOpen_Succeeds()
    {
        var issue = CreateIssue("Slow system");
        issue.Hold();
        issue.Status.ShouldBe(IssueStatus.OnHold);
    }

    [Fact]
    public void Hold_FromClosed_Throws()
    {
        var issue = CreateIssue("Done");
        issue.Resolve("Fixed");

        Should.Throw<BusinessException>(() => issue.Hold());
    }

    [Fact]
    public void Resolve_SetsClosedAndResolutionDate()
    {
        var issue = CreateIssue("Broken widget");
        issue.Resolve("Replaced widget");

        issue.Status.ShouldBe(IssueStatus.Closed);
        issue.ResolutionDate.ShouldNotBeNull();
        issue.Resolution.ShouldBe("Replaced widget");
    }

    [Fact]
    public void Reopen_FromClosed_Succeeds()
    {
        var issue = CreateIssue("Recurring issue");
        issue.Resolve("Fixed");
        issue.Reopen();

        issue.Status.ShouldBe(IssueStatus.Open);
        issue.ResolutionDate.ShouldBeNull();
    }

    [Fact]
    public void Reopen_FromOpen_Throws()
    {
        var issue = CreateIssue("Already open");
        Should.Throw<BusinessException>(() => issue.Reopen());
    }

    [Fact]
    public void Cancel_Succeeds()
    {
        var issue = CreateIssue("Duplicate");
        issue.Cancel();
        issue.Status.ShouldBe(IssueStatus.Cancelled);
    }

    [Fact]
    public void Cancel_AlreadyCancelled_Throws()
    {
        var issue = CreateIssue("Dup");
        issue.Cancel();
        Should.Throw<BusinessException>(() => issue.Cancel());
    }

    private static Issue CreateIssue(string subject) =>
        new(Guid.NewGuid(), Guid.NewGuid(), subject);
}
