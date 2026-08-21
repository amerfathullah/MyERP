using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Permissions;
using MyERP.Support;
using MyERP.Support.Entities;
using NSubstitute;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.Support;

/// <summary>
/// Unit tests for Issue splitting and SLA re-assignment.
/// Verifies rules migrated from erpnext/support/doctype/issue/issue.py (Gotcha #822).
/// </summary>
public class IssueSplitAndSlaTests
{
    private readonly IRepository<Issue, Guid> _issueRepository = Substitute.For<IRepository<Issue, Guid>>();
    private readonly IRepository<ServiceLevelAgreement, Guid> _slaRepository = Substitute.For<IRepository<ServiceLevelAgreement, Guid>>();
    private readonly IssueAppService _issueAppService;

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _originalIssueId = Guid.NewGuid();

    public IssueSplitAndSlaTests()
    {
        _issueAppService = new IssueAppService(_issueRepository, _slaRepository);
    }

    [Fact]
    public async Task SplitAsync_CreatesNewIssueWithResetSla_AndPreservedMetadata()
    {
        // Arrange
        var originalIssue = new Issue(_originalIssueId, _companyId, "Original Ticket Subject")
        {
            Description = "Original detailed description",
            Priority = "High",
            IssueType = "Bug",
            CustomerId = _customerId,
            RaisedVia = "Email"
        };
        originalIssue.Reply(); // Sets FirstRespondedOn and Replied status

        _issueRepository.GetAsync(_originalIssueId).Returns(originalIssue);

        var sla = new ServiceLevelAgreement(Guid.NewGuid(), _companyId, "Standard Customer SLA", 24, 4)
        {
            IsActive = true,
            IsDefault = true
        };
        _slaRepository.WithDetailsAsync().Returns(Task.FromResult(new[] { sla }.AsQueryable()));

        Issue? createdIssue = null;
        await _issueRepository.InsertAsync(Arg.Do<Issue>(i => createdIssue = i));

        var splitInput = new SplitIssueDto
        {
            Subject = "Secondary problem isolated into sub-ticket"
        };

        // Act
        var result = await _issueAppService.SplitAsync(_originalIssueId, splitInput);

        // Assert
        Assert.NotNull(createdIssue);
        Assert.Equal("Secondary problem isolated into sub-ticket", createdIssue.Subject);
        Assert.Equal(originalIssue.Description, createdIssue.Description);
        Assert.Equal(originalIssue.Priority, createdIssue.Priority);
        Assert.Equal(originalIssue.IssueType, createdIssue.IssueType);
        Assert.Equal(originalIssue.CustomerId, createdIssue.CustomerId);
        Assert.Equal(_originalIssueId, createdIssue.SplitFromIssueId);

        // Status and SLA reset to fresh open ticket
        Assert.Equal(IssueStatus.Open, createdIssue.Status);
        Assert.Null(createdIssue.FirstRespondedOn);
        Assert.Null(createdIssue.ResolutionDate);
        Assert.Equal(AgreementStatus.FirstResponseDue, createdIssue.AgreementStatus);
        Assert.Equal(sla.Id, createdIssue.ServiceLevelAgreementId);
        Assert.Equal(4m, createdIssue.FirstResponseTime);
        Assert.Equal(24m, createdIssue.ResolutionTime);
    }
}
