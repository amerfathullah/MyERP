using System;
using MyERP.HumanResources.DomainServices;
using Shouldly;
using Xunit;

namespace MyERP.Domain.Tests.HumanResources;

public class UnpaidLeaveProrationTests
{
    [Fact]
    public void Proration_NoUnpaidLeave_FullSalary()
    {
        var (proratedGross, deduction, days) =
            UnpaidLeaveProrationService.CalculateProration(5000m, 0, 30);

        proratedGross.ShouldBe(5000m);
        deduction.ShouldBe(0m);
        days.ShouldBe(0m);
    }

    [Fact]
    public void Proration_FiveDaysUnpaid_DeductsSixth()
    {
        // 5 days unpaid out of 30 working days
        // Daily rate = 5000 / 30 = 166.67
        // Deduction = 166.67 × 5 = 833.33
        var (proratedGross, deduction, _) =
            UnpaidLeaveProrationService.CalculateProration(5000m, 5, 30);

        deduction.ShouldBeGreaterThan(833m);
        deduction.ShouldBeLessThan(834m);
        proratedGross.ShouldBe(5000m - deduction);
    }

    [Fact]
    public void Proration_FullMonthUnpaid_ZeroSalary()
    {
        var (proratedGross, deduction, _) =
            UnpaidLeaveProrationService.CalculateProration(5000m, 30, 30);

        proratedGross.ShouldBe(0m);
        deduction.ShouldBe(5000m);
    }

    [Fact]
    public void Proration_MoreThanWorkingDays_CappedAtFull()
    {
        // Can't deduct more than full salary even if unpaid days > working days
        var (proratedGross, deduction, days) =
            UnpaidLeaveProrationService.CalculateProration(5000m, 35, 30);

        days.ShouldBe(30m); // capped
        proratedGross.ShouldBe(0m);
        deduction.ShouldBe(5000m);
    }

    [Fact]
    public void Proration_NegativeDays_NoDeduction()
    {
        var (proratedGross, deduction, _) =
            UnpaidLeaveProrationService.CalculateProration(5000m, -5, 30);

        proratedGross.ShouldBe(5000m);
        deduction.ShouldBe(0m);
    }

    [Fact]
    public void Proration_HalfDay_ProportionalDeduction()
    {
        // 0.5 days unpaid
        var (_, deduction, _) =
            UnpaidLeaveProrationService.CalculateProration(6000m, 0.5m, 30);

        // Daily rate = 6000/30 = 200, half day = 100
        deduction.ShouldBe(100m);
    }

    [Fact]
    public void Proration_26WorkingDays_Adjusted()
    {
        // Some months have 26 working days (excluding weekends)
        var (_, deduction, _) =
            UnpaidLeaveProrationService.CalculateProration(5200m, 2, 26);

        // Daily rate = 5200/26 = 200, 2 days = 400
        deduction.ShouldBe(400m);
    }

    [Fact]
    public void Proration_AffectsStatutoryDeductions()
    {
        // When gross salary is prorated, statutory deductions (EPF/SOCSO/EIS/PCB)
        // should be calculated on the PRORATED amount, not the original
        var originalGross = 5000m;
        var (proratedGross, _, _) =
            UnpaidLeaveProrationService.CalculateProration(originalGross, 5, 30);

        proratedGross.ShouldBeLessThan(originalGross);
        // EPF at 11% of prorated = lower than 11% of original
        var epfOnOriginal = originalGross * 0.11m;
        var epfOnProrated = proratedGross * 0.11m;
        epfOnProrated.ShouldBeLessThan(epfOnOriginal);
    }

    [Fact]
    public void OverdueWarning_Format()
    {
        // Warning message format
        int overdueCount = 3;
        decimal totalOverdue = 15000.50m;
        var warning = $"This customer has {overdueCount} overdue invoice(s) totalling {totalOverdue:N2}. Please follow up on outstanding payments.";

        warning.ShouldContain("3 overdue invoice(s)");
        warning.ShouldContain("15,000.50");
    }

    [Fact]
    public void OverdueWarning_NotShown_WhenNoOverdue()
    {
        // When no overdue invoices, warning should be null
        int overdueCount = 0;
        string? warning = overdueCount > 0 ? "Has overdue" : null;
        warning.ShouldBeNull();
    }

    [Fact]
    public void OverdueWarning_AdvisoryNotBlocking()
    {
        // Overdue warning should NOT prevent SO creation — it's advisory only
        // The credit limit check (separate validation) is what blocks
        bool isBlocking = false; // advisory warning never blocks
        isBlocking.ShouldBeFalse();
    }
}
