using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.HumanResources.DomainServices;
using MyERP.HumanResources.Entities;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Unit tests for upstream ERPNext parity batch 7:
/// - dd07d12215 &amp; c9257731eb: Skip batches no longer in the warehouse on a purchase/subcontracting return when remaining batches cover the return qty; throw only when none available.
/// - 6caabeb477 &amp; 73c5f3a6b4: Skip reservations of batches that are not available (e.g. expired).
/// - 1853ef83ea, e8a8b0b260, 629e32b70a &amp; 0957add0db: Convert and round Standard Selling Rate Item Price to the target price list currency's precision.
/// - e17d3aac93 &amp; 44d935b8f2: Pass the target date to the holiday list lookup in IsHoliday.
/// - 461d586d8b &amp; 80ef213995: Use same start date for all rows in accumulated cash flow and track opening/closing balances by movement start date.
/// </summary>
public class UpstreamBatch7ParityTests
{
    [Fact]
    public void FilterReturnBatches_SkipsBatchNoLongerInWarehouseWhenRemainingCoversQty_ThrowsWhenNoneAvailable_dd07d12215_c9257731eb()
    {
        var warehouseId = Guid.NewGuid();
        var movedBatchId = Guid.NewGuid();
        var remainingBatchId = Guid.NewGuid();

        // Source receipt row received 2 of movedBatch and 3 of remainingBatch (total 5)
        var sourceBatches = new Dictionary<Guid, decimal>
        {
            [movedBatchId] = 2m,
            [remainingBatchId] = 3m,
        };

        // movedBatch was transferred out (0 left in warehouse); remainingBatch still has 3 in warehouse
        var availableInWh = new Dictionary<Guid, decimal>
        {
            [movedBatchId] = 0m,
            [remainingBatchId] = 3m,
        };

        // 1) Return -3 qty: skips movedBatchId and allocates all 3 from remainingBatchId without throwing
        var filtered = SerialBatchBundleValidationService.FilterReturnBatches(
            sourceBatches,
            requiredReturnQty: -3m,
            warehouseId: warehouseId,
            availableBatchQuantitiesInWarehouse: availableInWh,
            isPurchaseReturn: true);

        filtered.Count.ShouldBe(1);
        filtered.ContainsKey(movedBatchId).ShouldBeFalse();
        filtered[remainingBatchId].ShouldBe(3m);

        // 2) If NEITHER batch is available in warehouse -> throws InsufficientStock
        var noneAvailable = new Dictionary<Guid, decimal>
        {
            [movedBatchId] = 0m,
            [remainingBatchId] = 0m,
        };

        var ex = Should.Throw<BusinessException>(() =>
            SerialBatchBundleValidationService.FilterReturnBatches(
                sourceBatches,
                requiredReturnQty: -1m,
                warehouseId: warehouseId,
                availableBatchQuantitiesInWarehouse: noneAvailable,
                isPurchaseReturn: true));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.InsufficientStock);
    }

    [Fact]
    public async Task GetAvailableBatches_SkipsExpiredBatchAndDoesNotApplyExpiredBatchReservation_6caabeb477_73c5f3a6b4()
    {
        var companyId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();

        var batchRepo = Substitute.For<IRepository<Batch, Guid>>();
        var sleRepo = Substitute.For<IRepository<StockLedgerEntry, Guid>>();
        var whRepo = Substitute.For<IRepository<Warehouse, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var sreRepo = Substitute.For<IRepository<StockReservationEntry, Guid>>();
        var stockEntryAppService = Substitute.For<IStockEntryAppService>();
        var lazyProvider = Substitute.For<IAbpLazyServiceProvider>();

        lazyProvider.LazyGetRequiredService<IRepository<Item, Guid>>().Returns(itemRepo);
        lazyProvider.LazyGetService<IRepository<StockReservationEntry, Guid>>().Returns(sreRepo);
        lazyProvider.LazyGetService<IRepository<SerialAndBatchBundle, Guid>>().Returns((IRepository<SerialAndBatchBundle, Guid>?)null);

        var expiredBatch = new Batch(Guid.NewGuid(), itemId, "BATCH-EXPIRED")
        {
            ExpiryDate = DateTime.UtcNow.Date.AddDays(-1)
        };

        var wh = new Warehouse(warehouseId, companyId, "POS Warehouse");
        var item = new Item(itemId, companyId, "POS-BATCH-ITEM", "POS Batch Item", ItemType.Goods) { HasBatchNo = true };

        whRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Warehouse> { wh }.AsQueryable()));
        batchRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Batch> { expiredBatch }.AsQueryable()));
        itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { item }.AsQueryable()));

        var sles = new List<StockLedgerEntry>
        {
            new(Guid.NewGuid(), companyId, itemId, warehouseId, DateTime.UtcNow.Date.AddDays(-5), 5m, 100m, 5m, 500m)
            {
                BatchId = expiredBatch.Id
            }
        };
        sleRepo.GetQueryableAsync().Returns(Task.FromResult(sles.AsQueryable()));

        var sre = new StockReservationEntry(
            Guid.NewGuid(), companyId, itemId, warehouseId,
            "SalesOrder", Guid.NewGuid(), 2m, 2m)
        {
            BatchId = expiredBatch.Id
        };
        sre.Submit();
        sreRepo.GetQueryableAsync().Returns(Task.FromResult(new List<StockReservationEntry> { sre }.AsQueryable()));

        var service = new BatchAppService(batchRepo, sleRepo, whRepo, stockEntryAppService)
        {
            LazyServiceProvider = lazyProvider
        };

        var available = await service.GetAvailableBatchesAsync(new GetAvailableBatchesDto
        {
            CompanyId = companyId,
            ItemId = itemId,
            WarehouseId = warehouseId,
        });

        // Expired batch and its reservation are completely skipped (73c5f3a6b4 & 6caabeb477)
        available.ShouldBeEmpty();
    }

    [Fact]
    public void CalculateStandardRateItemPrice_ConvertsAndRoundsToPriceListCurrencyPrecision_1853ef83ea_e8a8b0b260_629e32b70a_0957add0db()
    {
        // 1) Standard 2-decimal currency (TPR -> INR at 1.1 exchange rate): 100 / 1.1 = 90.90909... -> 90.91 (0957add0db)
        var rate2Dec = ItemPriceAutoInsertService.CalculateStandardRateItemPrice(
            standardSellingRate: 100m,
            defaultCurrencyCode: "INR",
            priceListCurrencyCode: "TPR",
            exchangeRate: 1.1m);
        rate2Dec.ShouldBe(90.91m);

        // 2) 3-decimal currency (TPT / KWD -> INR at 1.1 exchange rate): 100 / 1.1 = 90.90909... -> 90.909 (1853ef83ea & 629e32b70a)
        var rate3Dec = ItemPriceAutoInsertService.CalculateStandardRateItemPrice(
            standardSellingRate: 100m,
            defaultCurrencyCode: "INR",
            priceListCurrencyCode: "TPT",
            exchangeRate: 1.1m);
        rate3Dec.ShouldBe(90.909m);

        // 3) 0-decimal currency (JPY -> MYR at 0.033 exchange rate): 100 / 0.033 = 3030.303... -> 3030
        var rate0Dec = ItemPriceAutoInsertService.CalculateStandardRateItemPrice(
            standardSellingRate: 100m,
            defaultCurrencyCode: "MYR",
            priceListCurrencyCode: "JPY",
            exchangeRate: 0.033m);
        rate0Dec.ShouldBe(3030m);

        // 4) Foreign currency with missing exchange rate -> returns null (does not create unrounded or 1:1 Item Price)
        var missingFx = ItemPriceAutoInsertService.CalculateStandardRateItemPrice(
            standardSellingRate: 100m,
            defaultCurrencyCode: "MYR",
            priceListCurrencyCode: "USD",
            exchangeRate: null);
        missingFx.ShouldBeNull();
    }

    [Fact]
    public async Task EmployeeLifecycleManager_IsHoliday_PassesTargetDateToHolidayListLookup_e17d3aac93_44d935b8f2()
    {
        var employeeRepo = Substitute.For<IRepository<Employee, Guid>>();
        var leaveRepo = Substitute.For<IRepository<LeaveApplication, Guid>>();
        var payrollLineRepo = Substitute.For<IRepository<PayrollEntryLine, Guid>>();
        var attendanceRepo = Substitute.For<IRepository<Attendance, Guid>>();

        var manager = new EmployeeLifecycleManager(employeeRepo, leaveRepo, payrollLineRepo, attendanceRepo);
        var employeeId = Guid.NewGuid();
        var checkDate = new DateTime(2026, 1, 1);

        Guid? capturedEmployeeId = null;
        bool? capturedRaiseException = null;
        DateTime? capturedDate = null;

        var isHoliday = await manager.IsHolidayAsync(
            employeeId,
            date: checkDate,
            raiseException: true,
            holidayListOverride: (empId, raiseEx, asOn) =>
            {
                capturedEmployeeId = empId;
                capturedRaiseException = raiseEx;
                capturedDate = asOn;
                return Task.FromResult<Guid?>(null);
            });

        isHoliday.ShouldBeFalse();
        capturedEmployeeId.ShouldBe(employeeId);
        capturedRaiseException.ShouldBe(true);
        capturedDate.ShouldBe(checkDate);
    }

    [Fact]
    public void CashFlow_AccumulatedStartDateAndOpeningClosingBalances_FollowFiscalYearAndRangeStart_461d586d8b_80ef213995()
    {
        var fy1Start = new DateTime(2025, 1, 1);
        var fy2Start = new DateTime(2026, 1, 1);
        var midYearRangeStart = new DateTime(2026, 4, 1);

        // 1) 461d586d8b: When a date range starts mid-fiscal-year (Apr 1, 2026 > Jan 1, 2026),
        //    accumulated start date is max(periodYearStartDate, fiscalYearStartDate) = Apr 1, 2026.
        var effectiveStartMidYear = CashFlowStatementAppService.GetEffectiveStartDate(
            periodFromDate: new DateTime(2026, 7, 1),
            periodYearStartDate: midYearRangeStart,
            fiscalYearStartDate: fy2Start,
            accumulatedValues: true);
        effectiveStartMidYear.ShouldBe(midYearRangeStart);

        // 2) 80ef213995: Across 2 fiscal years of 4 quarters each in accumulated mode:
        //    In FY1, Q1..Q4 all share EffectiveStartDate = 2025-01-01 and accumulated NetCashChange = -200.
        //    In FY2, Q1..Q4 all share EffectiveStartDate = 2026-01-01 and accumulated NetCashChange = -200.
        //    Opening for FY1 Q1..Q4 is 1000, Closing is 800 (change -200).
        //    Opening for FY2 Q1..Q4 advances to 800, Closing is 600 (total change -400 from initial 1000).
        var periods = new List<CashFlowPeriodColumnDto>();
        for (int q = 1; q <= 4; q++)
        {
            periods.Add(new CashFlowPeriodColumnDto
            {
                Key = $"fy1_q{q}",
                EffectiveStartDate = fy1Start,
                NetCashChange = -200m
            });
        }
        for (int q = 1; q <= 4; q++)
        {
            periods.Add(new CashFlowPeriodColumnDto
            {
                Key = $"fy2_q{q}",
                EffectiveStartDate = fy2Start,
                NetCashChange = -200m
            });
        }

        CashFlowStatementAppService.CalculateOpeningAndClosingBalances(periods, initialOpeningBalance: 1000m);

        // FY1 Q1..Q4 opening = 1000, closing = 800
        periods.Take(4).Select(p => p.OpeningCashBalance).ShouldAllBe(x => x == 1000m);
        periods.Take(4).Select(p => p.ClosingCashBalance).ShouldAllBe(x => x == 800m);

        // FY2 Q1..Q4 opening = 800, closing = 600
        periods.Skip(4).Select(p => p.OpeningCashBalance).ShouldAllBe(x => x == 800m);
        periods.Skip(4).Select(p => p.ClosingCashBalance).ShouldAllBe(x => x == 600m);
    }
}
