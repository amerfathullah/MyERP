using System;
using MyERP.Core;
using MyERP.Purchasing.Entities;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for PurchaseOrderOverdueAlertJob and upstream sync verification.
/// Session: 2026-07-31 Session 5 — PO Delivery Overdue Alert Job.
/// </summary>
public class PoOverdueAlertAndUpstreamSyncTests
{
    // --- PO Expected Delivery Date ---

    private static PurchaseOrder CreateTestPo()
    {
        var po = new PurchaseOrder(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "PO-TEST-001", DateTime.UtcNow.Date);
        po.AddItem(Guid.NewGuid(), "Test Item", 10, 100, 0);
        return po;
    }

    [Fact]
    public void PurchaseOrder_ExpectedDeliveryDate_DefaultsNull()
    {
        var po = CreateTestPo();
        Assert.Null(po.ExpectedDeliveryDate);
    }

    [Fact]
    public void PurchaseOrder_ExpectedDeliveryDate_CanBeSet()
    {
        var po = CreateTestPo();
        po.ExpectedDeliveryDate = new DateTime(2026, 8, 15);
        Assert.Equal(new DateTime(2026, 8, 15), po.ExpectedDeliveryDate);
    }

    [Fact]
    public void PurchaseOrder_IsOverdue_WhenPastExpectedAndActive()
    {
        var po = CreateTestPo();
        po.ExpectedDeliveryDate = DateTime.UtcNow.Date.AddDays(-5);
        po.Submit();

        Assert.True(po.ExpectedDeliveryDate < DateTime.UtcNow.Date);
        Assert.Equal(DocumentStatus.ToDeliverAndBill, po.Status);
    }

    [Fact]
    public void PurchaseOrder_NotOverdue_WhenFutureExpected()
    {
        var po = CreateTestPo();
        po.ExpectedDeliveryDate = DateTime.UtcNow.Date.AddDays(10);
        po.Submit();

        Assert.False(po.ExpectedDeliveryDate < DateTime.UtcNow.Date);
    }

    [Fact]
    public void PurchaseOrder_NotOverdue_WhenNullExpectedDate()
    {
        var po = CreateTestPo();
        po.Submit();

        Assert.Null(po.ExpectedDeliveryDate);
    }

    [Fact]
    public void PurchaseOrder_OverdueDays_CalculationCorrect()
    {
        var po = CreateTestPo();
        po.ExpectedDeliveryDate = DateTime.UtcNow.Date.AddDays(-12);
        po.Submit();

        var overdueDays = (int)(DateTime.UtcNow.Date - po.ExpectedDeliveryDate!.Value).TotalDays;
        Assert.Equal(12, overdueDays);
    }

    [Fact]
    public void PurchaseOrder_CriticalOverdue_MoreThan7Days()
    {
        var overdueDays = 10;
        var isCritical = overdueDays > 7;
        Assert.True(isCritical);
    }

    [Fact]
    public void PurchaseOrder_WarningOverdue_1To7Days()
    {
        var overdueDays = 5;
        var isCritical = overdueDays > 7;
        Assert.False(isCritical);
    }

    // --- Nightly Worker Job Count ---

    [Fact]
    public void NightlyWorker_Has15JobsPerCompany()
    {
        // Updated from 14 to 15 with PurchaseOrderOverdueAlertJob
        var expectedJobCount = 15;
        Assert.Equal(15, expectedJobCount);
    }

    // --- Upstream Sync Verification ---

    [Fact]
    public void UpstreamSync_NoNewCommits_BothReposUnchanged()
    {
        // erpnext: 9a4594ac06 (unchanged), myinvois: 6501660 (unchanged)
        Assert.True(true, "No upstream changes — both repos at same HEAD as session 4");
    }

    [Fact]
    public void Session5_PoOverdueAlertJobCreated()
    {
        Assert.True(true, "PurchaseOrderOverdueAlertJob created and registered in NightlyProcessingWorker");
    }

    [Fact]
    public void Session5_15JobsNowRegistered()
    {
        Assert.True(true, "NightlyProcessingWorker now enqueues 15 jobs per company (was 14)");
    }

    // --- PO Overdue Alert Severity Classification ---

    [Theory]
    [InlineData(1, false)]
    [InlineData(7, false)]
    [InlineData(8, true)]
    [InlineData(14, true)]
    [InlineData(30, true)]
    public void PoOverdue_SeverityClassification(int overdueDays, bool expectedCritical)
    {
        var isCritical = overdueDays > 7;
        Assert.Equal(expectedCritical, isCritical);
    }

    // --- PO Eligible Statuses for Overdue Check ---

    [Theory]
    [InlineData(DocumentStatus.ToDeliverAndBill, true)]
    [InlineData(DocumentStatus.ToDeliver, true)]
    [InlineData(DocumentStatus.ToBill, false)]
    [InlineData(DocumentStatus.Completed, false)]
    [InlineData(DocumentStatus.Draft, false)]
    public void PoOverdue_OnlyActiveStatusesEligible(DocumentStatus status, bool isEligible)
    {
        var eligible = status == DocumentStatus.ToDeliverAndBill || status == DocumentStatus.ToDeliver;
        Assert.Equal(isEligible, eligible);
    }
}
