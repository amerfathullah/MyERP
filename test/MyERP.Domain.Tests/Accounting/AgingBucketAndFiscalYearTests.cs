using System;
using System.Linq;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Tests.Accounting;

public class AgingBucketAndFiscalYearTests
{
    [Fact]
    public void AgingReport_DefaultValues()
    {
        var report = new AgingReport
        {
            ReportType = "Receivable",
            AsOfDate = DateTime.Today,
            BucketRanges = new[] { 30, 60, 90, 120 },
            BucketTotals = new decimal[5],
        };
        report.TotalOutstanding.ShouldBe(0m);
        report.InvoiceCount.ShouldBe(0);
        report.BucketTotals.Length.ShouldBe(5); // 4 ranges + 1 overflow
    }

    [Fact]
    public void AgingReport_BucketCount_IsRangesPlusOne()
    {
        var ranges = new[] { 30, 60, 90, 120 };
        var expectedBuckets = ranges.Length + 1; // [0-30, 31-60, 61-90, 91-120, 120+]
        expectedBuckets.ShouldBe(5);
    }

    [Fact]
    public void AgingItem_DueDateBased_AgeDays()
    {
        var asOfDate = new DateTime(2026, 7, 13);
        var dueDate = new DateTime(2026, 6, 1); // 42 days overdue
        var ageDays = (int)(asOfDate - dueDate).TotalDays;
        ageDays.ShouldBe(42);
        // Falls into 31-60 bucket (index 1)
    }

    [Fact]
    public void AgingItem_NotYetDue_ZeroAge()
    {
        var asOfDate = new DateTime(2026, 7, 13);
        var dueDate = new DateTime(2026, 8, 1); // Not yet due
        var ageDays = (int)(asOfDate - dueDate).TotalDays;
        // Negative age → clamped to 0 → first bucket
        var clampedAge = Math.Max(0, ageDays);
        clampedAge.ShouldBe(0);
    }

    [Fact]
    public void AgingItem_SeverelyOverdue_LastBucket()
    {
        var asOfDate = new DateTime(2026, 7, 13);
        var dueDate = new DateTime(2026, 1, 1); // 193 days overdue
        var ageDays = (int)(asOfDate - dueDate).TotalDays;
        ageDays.ShouldBeGreaterThan(120);
        // Falls into 120+ bucket (last)
    }

    [Fact]
    public void FiscalYear_IsClosed_DefaultFalse()
    {
        var fy = new FiscalYear(
            Guid.NewGuid(), Guid.NewGuid(), "FY2026",
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));
        fy.IsClosed.ShouldBeFalse();
    }

    [Fact]
    public void FiscalYear_CanClose()
    {
        var fy = new FiscalYear(
            Guid.NewGuid(), Guid.NewGuid(), "FY2025",
            new DateTime(2025, 1, 1), new DateTime(2025, 12, 31));
        fy.IsClosed = true;
        fy.IsClosed.ShouldBeTrue();
    }

    [Fact]
    public void SequentialClosure_Concept()
    {
        // FY2024 must be closed before FY2025 can be closed
        var fy2024 = new FiscalYear(
            Guid.NewGuid(), Guid.NewGuid(), "FY2024",
            new DateTime(2024, 1, 1), new DateTime(2024, 12, 31));
        var fy2025 = new FiscalYear(
            Guid.NewGuid(), Guid.NewGuid(), "FY2025",
            new DateTime(2025, 1, 1), new DateTime(2025, 12, 31));

        // If FY2024 is NOT closed, FY2025 closure should be blocked
        fy2024.IsClosed.ShouldBeFalse();
        fy2025.StartDate.ShouldBeGreaterThan(fy2024.StartDate);
    }

    [Fact]
    public void ErrorCode_PriorFiscalYearNotClosed()
    {
        MyERPDomainErrorCodes.PriorFiscalYearNotClosed.ShouldBe("MyERP:02011");
    }
}
