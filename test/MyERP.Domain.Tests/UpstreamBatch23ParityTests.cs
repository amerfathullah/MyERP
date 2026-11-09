using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.HumanResources.DomainServices;
using MyERP.HumanResources.Entities;
using MyERP.Inventory.DomainServices;
using NSubstitute;
using Shouldly;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Unit tests for upstream parity batch 23:
/// 1) Skip reservations of batches that are not available (PR #64860 / commits 6caabeb477 &amp; 73c5f3a6b4).
/// 2) Accumulated Cash Flow movement start date across date range (PR #64939 / commit 461d586d8b).
/// 3) Accumulated Cash Flow opening and closing balance preservation within fiscal year (PR #64924 / commit 80ef213995).
/// 4) Financial statement non-monthly date range period boundaries &amp; fiscal year splitting (PR #61302 / commits 2548ec4047 &amp; 8c517abc81).
/// 5) Employee holiday lookup date propagation (PR #64974 / commits 481c0ec955 &amp; 44d935b8f2).
/// </summary>
public class UpstreamBatch23ParityTests
{
    // === 1) Skip Reservations of Unavailable/Expired Batches (PR #64860) ===

    [Fact]
    public void SerialBatchBundle_UpdateAvailableBatches_SkipsMissingBatches_WhenAddMissingFalse()
    {
        // Scenario (PR #64860 / commits 6caabeb477 & 73c5f3a6b4):
        // Batch A has 5 available in warehouse.
        // Batch B is expired or not in warehouse (available = 0, not present in available pool).
        // POS reservation or pick list reserves 2 of Batch A and 2 of Batch B.
        // With addMissing = false, reservation for Batch B must be skipped and not append a negative phantom row.
        var batchA = Guid.NewGuid();
        var batchB = Guid.NewGuid(); // Expired batch

        var availableBatches = new Dictionary<Guid, decimal>
        {
            [batchA] = 5m
        };

        var reservations = new List<KeyValuePair<Guid, decimal>>
        {
            new(batchA, -2m),
            new(batchB, -2m)
        };

        SerialBatchBundleValidationService.UpdateAvailableBatches(
            availableBatches,
            reservations,
            addMissing: false,
            precision: 4);

        availableBatches.ContainsKey(batchA).ShouldBeTrue();
        availableBatches[batchA].ShouldBe(3m);

        // Batch B was not available, so reservation had nothing to reduce and was NOT added
        availableBatches.ContainsKey(batchB).ShouldBeFalse();
    }

    [Fact]
    public void SerialBatchBundle_UpdateAvailableBatches_AddsMissingBatches_WhenAddMissingTrue()
    {
        // When addMissing = true (default for physical stock movements / returns), missing batches are added
        var batchA = Guid.NewGuid();
        var batchB = Guid.NewGuid();

        var availableBatches = new Dictionary<Guid, decimal>
        {
            [batchA] = 5m
        };

        var adjustments = new List<KeyValuePair<Guid, decimal>>
        {
            new(batchA, -2m),
            new(batchB, -2m)
        };

        SerialBatchBundleValidationService.UpdateAvailableBatches(
            availableBatches,
            adjustments,
            addMissing: true,
            precision: 4);

        availableBatches[batchA].ShouldBe(3m);
        availableBatches.ContainsKey(batchB).ShouldBeTrue();
        availableBatches[batchB].ShouldBe(-2m);
    }

    [Fact]
    public void SerialBatchBundle_UpdateAvailableBatches_ByBatchNo_SkipsMissingWhenAddMissingFalse()
    {
        var available = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["BATCH-001"] = 10m
        };

        var adjustments = new List<KeyValuePair<string, decimal>>
        {
            new("BATCH-001", -3m),
            new("BATCH-EXPIRED-002", -4m)
        };

        SerialBatchBundleValidationService.UpdateAvailableBatches(
            available,
            adjustments,
            addMissing: false,
            precision: 4);

        available["BATCH-001"].ShouldBe(7m);
        available.ContainsKey("BATCH-EXPIRED-002").ShouldBeFalse();
    }

    // === 2) Cash Flow Accumulated Movement Start Date (PR #64939) ===

    [Fact]
    public void CashFlow_GetEffectiveStartDate_AccumulatedDateRange_CountsOnlyFromRangeStart()
    {
        // Scenario (PR #64939 / commit 461d586d8b):
        // Fiscal year starts 2026-01-01. User selects Date Range starting 2026-04-01 (mid-year).
        // In accumulated mode, effective start date must be max(rangeStart, fyStart) = 2026-04-01,
        // so cash movements between 2026-01-01 and 2026-03-31 are NOT counted in the net change.
        var fyStart = new DateTime(2026, 1, 1);
        var rangeStart = new DateTime(2026, 4, 1);

        var effectiveStart = CashFlowStatementAppService.GetEffectiveStartDate(
            periodFromDate: rangeStart,
            periodYearStartDate: rangeStart,
            fiscalYearStartDate: fyStart,
            accumulatedValues: true);

        effectiveStart.ShouldBe(new DateTime(2026, 4, 1));

        // When accumulatedValues is false, returns periodFromDate directly
        var nonAccumulatedStart = CashFlowStatementAppService.GetEffectiveStartDate(
            periodFromDate: new DateTime(2026, 5, 1),
            periodYearStartDate: rangeStart,
            fiscalYearStartDate: fyStart,
            accumulatedValues: false);

        nonAccumulatedStart.ShouldBe(new DateTime(2026, 5, 1));
    }

    // === 3) Cash Flow Opening & Closing Rows in Accumulated Cash Flow (PR #64924) ===

    [Fact]
    public void CashFlow_CalculateOpeningAndClosingBalances_AccumulatedValues_PreservesOpeningWithinFiscalYear()
    {
        // Scenario (PR #64924 / commit 80ef213995):
        // 4 quarters in 2026 sharing effective start date 2026-01-01.
        // Each quarter's net change is already accumulated from 2026-01-01 (e.g. Q1=+100, Q2=+250, Q3=+300, Q4=+400).
        // Initial opening cash = 1000.
        // Opening balance for ALL quarters in 2026 must remain 1000!
        // Closing balance for each quarter is Opening + NetChange (1100, 1250, 1300, 1400).
        // In 2027 (new effective start date 2027-01-01), Opening advances to 2026 closing (1400).
        var periods = new List<CashFlowPeriodColumnDto>
        {
            new() { Key = "q1_2026", EffectiveStartDate = new DateTime(2026, 1, 1), NetCashChange = 100m },
            new() { Key = "q2_2026", EffectiveStartDate = new DateTime(2026, 1, 1), NetCashChange = 250m },
            new() { Key = "q3_2026", EffectiveStartDate = new DateTime(2026, 1, 1), NetCashChange = 300m },
            new() { Key = "q4_2026", EffectiveStartDate = new DateTime(2026, 1, 1), NetCashChange = 400m },
            new() { Key = "q1_2027", EffectiveStartDate = new DateTime(2027, 1, 1), NetCashChange = 50m }
        };

        CashFlowStatementAppService.CalculateOpeningAndClosingBalances(periods, initialOpeningBalance: 1000m);

        // Q1-Q4 Opening is 1000 (opening at start of movement period)
        periods[0].OpeningCashBalance.ShouldBe(1000m);
        periods[0].ClosingCashBalance.ShouldBe(1100m);

        periods[1].OpeningCashBalance.ShouldBe(1000m);
        periods[1].ClosingCashBalance.ShouldBe(1250m);

        periods[2].OpeningCashBalance.ShouldBe(1000m);
        periods[2].ClosingCashBalance.ShouldBe(1300m);

        periods[3].OpeningCashBalance.ShouldBe(1000m);
        periods[3].ClosingCashBalance.ShouldBe(1400m);

        // Q1 2027 starts new fiscal year: opening becomes closing of previous year (1400m)
        periods[4].OpeningCashBalance.ShouldBe(1400m);
        periods[4].ClosingCashBalance.ShouldBe(1450m);
    }

    // === 4) Financial Statement Non-Monthly Periods & Fiscal Year Splitting (PR #61302) ===

    [Fact]
    public void CashFlow_BuildPeriods_NonMonthlyDateRange_StartsOnSelectedDate()
    {
        // Scenario (PR #61302 / commit 2548ec4047):
        // Non-monthly periods (Quarterly, Half-Yearly, Yearly) with Date Range must start on fromDate,
        // unlike Monthly which aligns to month-end boundaries.
        var fromDate = new DateTime(2026, 1, 17);
        var toDate = new DateTime(2026, 7, 16);
        var fy = new FiscalYear(Guid.NewGuid(), Guid.NewGuid(), "FY2026", new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));

        var quarterlyPeriods = CashFlowStatementAppService.BuildPeriods(
            fromDate,
            toDate,
            periodicity: "Quarterly",
            fiscalYears: new List<FiscalYear> { fy },
            accumulatedValues: false);

        quarterlyPeriods.Count.ShouldBe(2);
        // Period 1: Jan 17 to Apr 16 (3 months minus 1 day)
        quarterlyPeriods[0].FromDate.ShouldBe(new DateTime(2026, 1, 17));
        quarterlyPeriods[0].ToDate.ShouldBe(new DateTime(2026, 4, 16));

        // Period 2: Apr 17 to Jul 16
        quarterlyPeriods[1].FromDate.ShouldBe(new DateTime(2026, 4, 17));
        quarterlyPeriods[1].ToDate.ShouldBe(new DateTime(2026, 7, 16));
    }

    [Fact]
    public void CashFlow_BuildPeriods_AccumulatedValues_SplitsAtFiscalYearBoundary()
    {
        // Scenario (PR #61302 / commit 8c517abc81):
        // Date range spans two fiscal years: 2049-10-17 to 2050-01-16.
        // With accumulatedValues = true, period must split at FY boundary (2049-12-31)
        // so reports retaining final balances preserve both years.
        var fy2049 = new FiscalYear(Guid.NewGuid(), Guid.NewGuid(), "FY2049", new DateTime(2049, 1, 1), new DateTime(2049, 12, 31));
        var fy2050 = new FiscalYear(Guid.NewGuid(), Guid.NewGuid(), "FY2050", new DateTime(2050, 1, 1), new DateTime(2050, 12, 31));

        var periods = CashFlowStatementAppService.BuildPeriods(
            new DateTime(2049, 10, 17),
            new DateTime(2050, 1, 16),
            periodicity: "Quarterly",
            fiscalYears: new List<FiscalYear> { fy2049, fy2050 },
            accumulatedValues: true);

        periods.Count.ShouldBe(2);

        // First period capped at 2049-12-31 (FY boundary)
        periods[0].FromDate.ShouldBe(new DateTime(2049, 10, 17));
        periods[0].ToDate.ShouldBe(new DateTime(2049, 12, 31));
        periods[0].Key.ShouldBe("dec_2049");

        // Second period starts in new FY 2050
        periods[1].FromDate.ShouldBe(new DateTime(2050, 1, 1));
        periods[1].ToDate.ShouldBe(new DateTime(2050, 1, 16));
        periods[1].Key.ShouldBe("jan_2050");
    }

    // === 5) Employee Holiday Lookup Date Propagation (PR #64974) ===

    [Fact]
    public async Task EmployeeLifecycleManager_IsHolidayAsync_PassesDateToHolidayLookup()
    {
        // Scenario (PR #64974 / commit 44d935b8f2):
        // is_holiday passes check date to get_holiday_list_for_employee(as_on=date).
        var employeeRepo = Substitute.For<Volo.Abp.Domain.Repositories.IRepository<Employee, Guid>>();
        var leaveRepo = Substitute.For<Volo.Abp.Domain.Repositories.IRepository<LeaveApplication, Guid>>();
        var payrollLineRepo = Substitute.For<Volo.Abp.Domain.Repositories.IRepository<PayrollEntryLine, Guid>>();
        var attendanceRepo = Substitute.For<Volo.Abp.Domain.Repositories.IRepository<Attendance, Guid>>();

        var manager = new EmployeeLifecycleManager(employeeRepo, leaveRepo, payrollLineRepo, attendanceRepo);
        var empId = Guid.NewGuid();
        var targetDate = new DateTime(2026, 6, 15);

        DateTime? receivedDateInOverride = null;

        var isHoliday = await manager.IsHolidayAsync(
            empId,
            date: targetDate,
            raiseException: false,
            onlyNonWeekly: false,
            holidayListOverride: (id, raiseEx, asOn) =>
            {
                receivedDateInOverride = asOn;
                return Task.FromResult<Guid?>(null);
            });

        receivedDateInOverride.ShouldBe(targetDate);
        isHoliday.ShouldBeFalse();
    }
}
