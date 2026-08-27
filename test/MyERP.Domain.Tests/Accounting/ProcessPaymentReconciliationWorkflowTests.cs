using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.BackgroundJobs;
using MyERP.Accounting.Entities;
using MyERP.Core;
using NSubstitute;
using Volo.Abp;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.Accounting;

/// <summary>
/// Unit tests for Process Payment Reconciliation pause, resume, cancellation, and progress metrics.
/// Verifies rules from erpnext/accounts/doctype/process_payment_reconciliation (#6008).
/// </summary>
public class ProcessPaymentReconciliationWorkflowTests
{
    private readonly IRepository<ProcessPaymentReconciliation, Guid> _repo = Substitute.For<IRepository<ProcessPaymentReconciliation, Guid>>();
    private readonly IBackgroundJobManager _jobManager = Substitute.For<IBackgroundJobManager>();
    private readonly ProcessPaymentReconciliationAppService _appService;

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _partyId = Guid.NewGuid();
    private readonly Guid _accountRecPayId = Guid.NewGuid();

    public ProcessPaymentReconciliationWorkflowTests()
    {
        _appService = new ProcessPaymentReconciliationAppService(_repo, _jobManager);
    }

    [Fact]
    public async Task PauseAsync_RunningRequest_TransitionsToPaused()
    {
        var requestId = Guid.NewGuid();
        var request = new ProcessPaymentReconciliation(
            requestId, _companyId, "Customer", _partyId, _accountRecPayId);
        request.Submit();
        request.StartProcessing();

        Assert.Equal(ProcessPaymentReconciliationStatus.Running, request.Status);

        _repo.GetAsync(requestId).Returns(Task.FromResult(request));

        var result = await _appService.PauseAsync(requestId);

        Assert.Equal(ProcessPaymentReconciliationStatus.Paused, request.Status);
        Assert.Equal("Paused", result.StatusName);
    }

    [Fact]
    public async Task ResumeAsync_PausedRequest_TransitionsToQueuedAndEnqueuesJob()
    {
        var requestId = Guid.NewGuid();
        var request = new ProcessPaymentReconciliation(
            requestId, _companyId, "Customer", _partyId, _accountRecPayId);
        request.Submit();
        request.StartProcessing();
        request.Pause();

        Assert.Equal(ProcessPaymentReconciliationStatus.Paused, request.Status);

        _repo.GetAsync(requestId).Returns(Task.FromResult(request));

        var result = await _appService.ResumeAsync(requestId);

        Assert.Equal(ProcessPaymentReconciliationStatus.Queued, request.Status);
        await _jobManager.Received(1).EnqueueAsync(Arg.Is<ProcessPaymentReconciliationJobArgs>(args => args.RequestId == requestId));
    }

    [Fact]
    public async Task PauseAsync_CompletedRequest_ThrowsValidationException()
    {
        var requestId = Guid.NewGuid();
        var request = new ProcessPaymentReconciliation(
            requestId, _companyId, "Customer", _partyId, _accountRecPayId);
        request.Submit();
        request.StartProcessing();
        request.Complete();

        _repo.GetAsync(requestId).Returns(Task.FromResult(request));

        var ex = await Assert.ThrowsAsync<BusinessException>(() => _appService.PauseAsync(requestId));
        Assert.Equal(MyERPDomainErrorCodes.InvalidStatusTransition, ex.Code);
    }

    [Fact]
    public async Task GetProgressAsync_ReturnsAccurateProgressAndActionFlags()
    {
        var requestId = Guid.NewGuid();
        var request = new ProcessPaymentReconciliation(
            requestId, _companyId, "Customer", _partyId, _accountRecPayId);
        request.Submit();
        request.StartProcessing();
        request.RecordProgress(7);

        _repo.GetAsync(requestId).Returns(Task.FromResult(request));

        var progress = await _appService.GetProgressAsync(requestId);

        Assert.NotNull(progress);
        Assert.Equal(requestId, progress.Id);
        Assert.Equal(7, progress.ReconciledCount);
        Assert.Equal((int)ProcessPaymentReconciliationStatus.Running, progress.Status);
        Assert.True(progress.CanPause);
        Assert.False(progress.CanResume);
        Assert.True(progress.CanCancel);
    }
}
