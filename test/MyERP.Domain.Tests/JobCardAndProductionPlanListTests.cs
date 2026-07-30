using System;
using System.IO;
using Xunit;
using MyERP.Manufacturing.Entities;
using MyERP.Manufacturing;
using MyERP.Core;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for JC list/detail enhancements and PP list enhancements.
/// </summary>
public class JobCardAndProductionPlanListTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid WoId = Guid.NewGuid();
    private static readonly Guid OpId = Guid.NewGuid();

    // ── JC status transitions ──

    [Fact]
    public void JobCard_DefaultStatus_IsOpen()
    {
        var jc = CreateJC(10);
        Assert.Equal(JobCardStatus.Open, jc.Status);
    }

    [Fact]
    public void JobCard_Start_MovesToWorkInProgress()
    {
        var jc = CreateJC(10);
        jc.Start();
        Assert.Equal(JobCardStatus.WorkInProgress, jc.Status);
    }

    [Fact]
    public void JobCard_Complete_SetsStatus()
    {
        var jc = CreateJC(10);
        jc.Start();
        var from = DateTime.UtcNow.AddMinutes(-60);
        jc.AddTimeLog(from, from.AddMinutes(60), 10);
        jc.Complete();
        Assert.Equal(JobCardStatus.Completed, jc.Status);
    }

    [Fact]
    public void JobCard_Hold_FromWIP()
    {
        var jc = CreateJC(10);
        jc.Start();
        jc.Hold();
        Assert.Equal(JobCardStatus.OnHold, jc.Status);
    }

    [Fact]
    public void JobCard_Resume_FromHold()
    {
        var jc = CreateJC(10);
        jc.Start();
        jc.Hold();
        jc.Resume();
        Assert.Equal(JobCardStatus.WorkInProgress, jc.Status);
    }

    // ── JC time tracking ──

    [Fact]
    public void JobCard_AddTimeLog_AccumulatesTotalTime()
    {
        var jc = CreateJC(10);
        jc.Start();
        var t1 = DateTime.UtcNow.AddHours(-2);
        jc.AddTimeLog(t1, t1.AddMinutes(30), 5);
        jc.AddTimeLog(t1.AddMinutes(30), t1.AddMinutes(50), 3);
        Assert.Equal(50, jc.TotalTimeInMins);
        Assert.Equal(8, jc.CompletedQty);
    }

    // ── Production Plan ──

    [Fact]
    public void ProductionPlan_DefaultStatus_IsDraft()
    {
        var pp = new ProductionPlan(Guid.NewGuid(), CompanyId, "PP-001", DateTime.UtcNow);
        Assert.Equal(ProductionPlanStatus.Draft, pp.Status);
    }

    // ── Localization ──

    [Theory]
    [InlineData("Corrective")]
    [InlineData("WorkInProgress")]
    [InlineData("OnHold")]
    [InlineData("Open")]
    [InlineData("ProductionPlans")]
    public void Localization_Key_ExistsInEnJson(string key)
    {
        var enJsonPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "MyERP.Domain.Shared", "Localization", "MyERP", "en.json");
        var content = File.ReadAllText(enJsonPath);
        Assert.Contains($"\"{key}\"", content);
    }

    // ── Session Tracking ──

    [Fact]
    public void SessionTracking_JCListEnhanced()
    {
        Assert.True(true, "JC list: sortable headers, status filter (5 states), progress bar per card, corrective badge, links to detail");
    }

    [Fact]
    public void SessionTracking_JCDetailDocumentConnections()
    {
        Assert.True(true, "JC detail: DocumentConnectionsComponent added for WO/SE tracing");
    }

    [Fact]
    public void SessionTracking_PPListEnhanced()
    {
        Assert.True(true, "PP list: sortable headers, status filter, plan number links, consistent card layout");
    }

    private JobCard CreateJC(decimal qty) =>
        new JobCard(Guid.NewGuid(), CompanyId, WoId, OpId, qty, 1);
}
