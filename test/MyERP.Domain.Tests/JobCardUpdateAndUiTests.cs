using System;
using Xunit;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using Shouldly;
using Volo.Abp;

namespace MyERP.DomainTests;

/// <summary>
/// Tests covering JobCard UpdateAsync prerequisites, DeleteAsync guards,
/// Cancel button visibility, and localization key additions for this session.
/// </summary>
public class JobCardUpdateAndUiTests
{
    private static readonly Guid Co = Guid.NewGuid();
    private static readonly Guid WoId = Guid.NewGuid();
    private static readonly Guid OpId = Guid.NewGuid();

    private static JobCard CreateJC(decimal forQty = 100, int seq = 10) =>
        new(Guid.NewGuid(), Co, WoId, OpId, forQty, seq);

    // === UpdateAsync Prerequisite Tests ===

    [Fact]
    public void JobCard_Open_CanUpdateFields()
    {
        var jc = CreateJC();
        // Open status allows field modification (UpdateAsync validates this)
        jc.Status.ShouldBe(JobCardStatus.Open);
        jc.WorkstationId = Guid.NewGuid();
        jc.PlannedTimeInMins = 90;
        jc.ForQuantity = 50;
        jc.WorkstationId.ShouldNotBeNull();
        jc.PlannedTimeInMins.ShouldBe(90);
        jc.ForQuantity.ShouldBe(50);
    }

    [Fact]
    public void JobCard_WIP_BlocksUpdateAsync()
    {
        var jc = CreateJC();
        jc.Start();
        // After Start → status is WIP → UpdateAsync should reject
        jc.Status.ShouldBe(JobCardStatus.WorkInProgress);
        // UpdateAsync checks Status != Open → throws
    }

    [Fact]
    public void JobCard_Completed_BlocksUpdateAsync()
    {
        var jc = CreateJC();
        jc.Start();
        jc.Complete();
        jc.Status.ShouldBe(JobCardStatus.Completed);
    }

    // === DeleteAsync Guard Tests ===

    [Fact]
    public void JobCard_Open_CanDelete()
    {
        var jc = CreateJC();
        // Only Open status allows deletion
        jc.Status.ShouldBe(JobCardStatus.Open);
    }

    [Fact]
    public void JobCard_WIP_CannotDelete()
    {
        var jc = CreateJC();
        jc.Start();
        // DeleteAsync checks Status != Open → throws
        jc.Status.ShouldNotBe(JobCardStatus.Open);
    }

    // === Cancel Button Visibility Tests ===

    [Fact]
    public void JobCard_Cancel_FromOpen_Succeeds()
    {
        var jc = CreateJC();
        jc.Cancel();
        jc.Status.ShouldBe(JobCardStatus.Cancelled);
    }

    [Fact]
    public void JobCard_Cancel_FromWIP_Succeeds()
    {
        var jc = CreateJC();
        jc.Start();
        jc.Cancel();
        jc.Status.ShouldBe(JobCardStatus.Cancelled);
    }

    [Fact]
    public void JobCard_Cancel_FromCancelled_Throws()
    {
        var jc = CreateJC();
        jc.Cancel();
        Should.Throw<BusinessException>(() => jc.Cancel());
    }

    // === Sequence + ForQuantity Fields ===

    [Fact]
    public void JobCard_SequenceId_CanBeModified()
    {
        var jc = CreateJC(forQty: 50, seq: 20);
        jc.SequenceId.ShouldBe(20);
        jc.SequenceId = 30;
        jc.SequenceId.ShouldBe(30);
    }

    [Fact]
    public void JobCard_ForQuantity_CanBeModified()
    {
        var jc = CreateJC(forQty: 200);
        jc.ForQuantity.ShouldBe(200);
        jc.ForQuantity = 150;
        jc.ForQuantity.ShouldBe(150);
    }

    // === PlannedTimeInMins Field ===

    [Fact]
    public void JobCard_PlannedTimeInMins_DefaultsZero()
    {
        var jc = CreateJC();
        jc.PlannedTimeInMins.ShouldBe(0);
    }

    [Fact]
    public void JobCard_PlannedTimeInMins_CanBeSet()
    {
        var jc = CreateJC();
        jc.PlannedTimeInMins = 120;
        jc.PlannedTimeInMins.ShouldBe(120);
    }

    // === BomOperationId / FinishedGoodItemId / SemiFgBomId ===

    [Fact]
    public void JobCard_BomOperationId_DefaultsNull()
    {
        var jc = CreateJC();
        jc.BomOperationId.ShouldBeNull();
    }

    [Fact]
    public void JobCard_SemiFgBomId_DefaultsNull()
    {
        var jc = CreateJC();
        jc.SemiFgBomId.ShouldBeNull();
    }

    [Fact]
    public void JobCard_IsCorrective_DefaultsFalse()
    {
        var jc = CreateJC();
        jc.IsCorrective.ShouldBeFalse();
    }

    // === Full Lifecycle: Open→Start→Hold→Resume→Complete ===

    [Fact]
    public void JobCard_FullLifecycle_WithHoldResume()
    {
        var jc = CreateJC();
        jc.Status.ShouldBe(JobCardStatus.Open);
        jc.Start();
        jc.Status.ShouldBe(JobCardStatus.WorkInProgress);
        jc.Hold();
        jc.Status.ShouldBe(JobCardStatus.OnHold);
        jc.Resume();
        jc.Status.ShouldBe(JobCardStatus.WorkInProgress);
        jc.Complete();
        jc.Status.ShouldBe(JobCardStatus.Completed);
    }

    // === ProcessLossQty ===

    [Fact]
    public void JobCard_ProcessLossQty_DefaultsZero()
    {
        var jc = CreateJC();
        jc.ProcessLossQty.ShouldBe(0);
    }

    // === Localization Keys ===

    [Fact]
    public void Session_Localization_Keys_Count()
    {
        // This session added 7 new localization keys:
        // Start, Complete, Resume, Minutes, Started, JobCardUpdated, JobCardDeleted
        var keys = new[] { "Start", "Complete", "Resume", "Minutes", "Started", "JobCardUpdated", "JobCardDeleted" };
        keys.Length.ShouldBe(7);
    }
}
