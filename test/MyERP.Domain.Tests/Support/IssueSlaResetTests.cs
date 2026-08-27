using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Support;
using MyERP.Support.Entities;
using NSubstitute;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.Support;

/// <summary>
/// Unit tests for Issue SLA Reset workflow, validation, and deadline recomputations.
/// Verifies rules from erpnext/support/doctype/issue/issue.js and service_level_agreement.py (#6007).
/// </summary>
public class IssueSlaResetTests
{
    private readonly IRepository<Issue, Guid> _issueRepository = Substitute.For<IRepository<Issue, Guid>>();
    private readonly IRepository<ServiceLevelAgreement, Guid> _slaRepository = Substitute.For<IRepository<ServiceLevelAgreement, Guid>>();
    private readonly IssueAppService _appService;

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();

    public IssueSlaResetTests()
    {
        _appService = new IssueAppService(_issueRepository, _slaRepository);
    }

    [Fact]
    public async Task ResetSlaAsync_WithoutReason_ThrowsValidationException()
    {
        var input = new ResetIssueSlaDto
        {
            ResetReason = "" // empty reason
        };

        var ex = await Assert.ThrowsAsync<BusinessException>(() => _appService.ResetSlaAsync(Guid.NewGuid(), input));
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
    }

    [Fact]
    public async Task ResetSlaAsync_ValidReason_ResetsSlaMetricsAndDeadlines()
    {
        var issueId = Guid.NewGuid();
        var slaId = Guid.NewGuid();
        var issue = new Issue(issueId, _companyId, "Cannot login to portal", null)
        {
            CustomerId = _customerId,
            Priority = "Medium"
        };
        issue.ApplySla(slaId, 4m, 24m);
        issue.Reply(); // Status becomes Replied, FirstRespondedOn is set
        issue.Hold();  // Status becomes OnHold

        _issueRepository.GetAsync(issueId).Returns(issue);

        var sla = new ServiceLevelAgreement(slaId, _companyId, "Standard SLA", 24, 4, null);
        _slaRepository.FindAsync(slaId).Returns(sla);

        var input = new ResetIssueSlaDto
        {
            ResetReason = "Customer clarified issue scope; priority recalibrated"
        };

        var result = await _appService.ResetSlaAsync(issueId, input);

        Assert.NotNull(result);
        Assert.Equal(IssueStatus.Open, issue.Status);
        Assert.Null(issue.FirstRespondedOn);
        Assert.Equal(0, issue.TotalHoldTime);
        Assert.Null(issue.HoldStartedOn);
        Assert.False(issue.IsSlaBreach);
        Assert.Equal(AgreementStatus.FirstResponseDue, issue.AgreementStatus);
        Assert.NotNull(issue.ResponseByDate);
        Assert.NotNull(issue.ResolutionByDate);

        await _issueRepository.Received(1).UpdateAsync(issue);
    }

    [Fact]
    public async Task ResetSlaAsync_WithNewPriority_RecomputesTargets()
    {
        var issueId = Guid.NewGuid();
        var slaId = Guid.NewGuid();
        var issue = new Issue(issueId, _companyId, "Server outage", null)
        {
            CustomerId = _customerId,
            Priority = "Low"
        };
        issue.ApplySla(slaId, 8m, 48m);

        _issueRepository.GetAsync(issueId).Returns(issue);

        var sla = new ServiceLevelAgreement(slaId, _companyId, "Enterprise SLA", 48, 8, null);
        sla.AddPriority(new ServiceLevelPriority(Guid.NewGuid(), slaId, "Urgent", 1m, 4m));
        _slaRepository.FindAsync(slaId).Returns(sla);

        var input = new ResetIssueSlaDto
        {
            ResetReason = "Escalated to P1 severity due to production impact",
            NewPriority = "Urgent"
        };

        var result = await _appService.ResetSlaAsync(issueId, input);

        Assert.NotNull(result);
        Assert.Equal("Urgent", issue.Priority);
        Assert.Equal(1m, issue.FirstResponseTime);
        Assert.Equal(4m, issue.ResolutionTime);
        Assert.Equal(AgreementStatus.FirstResponseDue, issue.AgreementStatus);
    }
}
