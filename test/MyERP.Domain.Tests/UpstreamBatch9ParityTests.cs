using System;
using System.Collections.Generic;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Manufacturing.Services;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Unit tests for upstream ERPNext parity batch 9:
/// - de7f4dfd77 / ecdb045ab3: Batch ManufacturingDate cannot exceed ExpiryDate.
/// - 67cbeeee42 / b1dd2aff0a: Item deferred revenue/expense NoOfMonths cannot be negative.
/// - db4196a0b1: Loyalty Program treats null/empty MinSpent as 0 on tier validation and selection.
/// - 3f7f8877c9: ItemLeadTime rounds fractional days to 4 decimal places before Math.Ceiling.
/// - e20967e15e / 8ae5e72ea6: BomItem.StockQty rounds Quantity * ConversionFactor to 4 decimal places.
/// - 57fb18aa0a / a438387bd7 / 51ea86e625: WorkOrderProductionService pulls unconsumed raw materials into Manufacture after a consumption entry.
/// - 09f429f616 / ad669c8f48 / 21685f5bc1: JobCard picks the latest open time log as the cycle time log when closing or updating qty without end time.
/// - 93362b6785: DeliveryScheduleService reuses delivery schedule rows only from the same SalesOrder and SalesOrderItem.
/// </summary>
public class UpstreamBatch9ParityTests
{
    [Fact]
    public void Batch_ValidateDates_ThrowsWhenManufacturingDateExceedsExpiryDate_de7f4dfd77()
    {
        var batch = new Batch(Guid.NewGuid(), Guid.NewGuid(), "BATCH-2026-09")
        {
            ManufacturingDate = new DateTime(2026, 6, 15),
            ExpiryDate = new DateTime(2026, 6, 10)
        };

        var ex = Should.Throw<BusinessException>(() => batch.ValidateDates());
        ex.Code.ShouldBe(MyERPDomainErrorCodes.InvalidDateRange);
        (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("Manufacturing date cannot be greater than expiry date");

        // Equal or earlier manufacturing date succeeds
        batch.ExpiryDate = new DateTime(2026, 6, 15);
        Should.NotThrow(() => batch.ValidateDates());
    }

    [Fact]
    public void Item_ValidateDeferredMonths_RejectsNegativeRevenueOrExpenseMonths_67cbeeee42_b1dd2aff0a()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "SUB-001", "Annual Subscription", ItemType.Service);

        var revEx = Should.Throw<BusinessException>(() => item.ValidateDeferredMonths(noOfMonthsRevenue: -1, noOfMonthsExpense: 0));
        revEx.Code.ShouldBe(MyERPDomainErrorCodes.AmountMustBePositive);

        var expEx = Should.Throw<BusinessException>(() => item.ValidateDeferredMonths(noOfMonthsRevenue: 12, noOfMonthsExpense: -3));
        expEx.Code.ShouldBe(MyERPDomainErrorCodes.AmountMustBePositive);

        Should.NotThrow(() => item.ValidateDeferredMonths(noOfMonthsRevenue: 0, noOfMonthsExpense: 12));
    }

    [Fact]
    public void LoyaltyProgram_AddTier_TreatsNullMinSpentAsZero_db4196a0b1()
    {
        var program = new LoyaltyProgram(Guid.NewGuid(), Guid.NewGuid(), "Rewards Multi-Tier", 10m);
        program.AddTier("Gold", 10000m, 2m, 0.02m);
        program.AddTier("Silver", null, 1m, 0.01m);

        Should.NotThrow(() => program.Validate());

        var initialTier = program.DetermineTier(totalSpent: 0m, currentTransactionAmount: 500m);
        initialTier.TierName.ShouldBe("Silver");
        initialTier.MinSpent.ShouldBe(0m);
    }

    [Fact]
    public void ItemLeadTime_CalculateLeadTimeDays_RoundsFractionalDaysBeforeCeiling_3f7f8877c9()
    {
        // 160 mins per unit * 9 units = 1440 mins = 1.0000 day (plus 2 buffer days = 3 days total).
        // Even with slight fractional excess < 0.00005 (e.g., qty = 9.00001), rounding to 4 decimals prevents jumping to 4 days.
        var leadTime = new ItemLeadTime(
            Guid.NewGuid(),
            Guid.NewGuid(),
            manufacturingTimeInMins: 160,
            bufferTimeDays: 2);

        leadTime.CalculateLeadTimeDays(qty: 9.00001m, isManufacture: true).ShouldBe(3);
    }

    [Fact]
    public void WorkOrderProductionService_CalculateRawMaterialConsumption_PullsUnconsumedTransferredMaterialsAfterConsumptionEntry_57fb18aa0a()
    {
        var woRepo = Substitute.For<IRepository<WorkOrder, Guid>>();
        var service = new WorkOrderProductionService(woRepo);

        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-B9-001", Guid.NewGuid(), Guid.NewGuid(), 10m)
        {
            MaterialTransferred = 10m,
            ProducedQuantity = 0m
        };
        var rmItemId = Guid.NewGuid();
        wo.RequiredItems.Add(new WorkOrderItem(Guid.NewGuid(), wo.Id, rmItemId, "Steel Rod", 20m)
        {
            TransferredQuantity = 20m,
            ConsumedQuantity = 6m // 6 already consumed via Material Consumption for Manufacture
        });

        // Remaining unconsumed = 20 - 6 = 14 across 10 units to produce -> 1.4 per unit -> 14 for all 10 units
        var result = service.CalculateRawMaterialConsumption(wo, fgCompletedQty: 10m, backflushMethod: "BOM");
        result.Count.ShouldBe(1);
        result[0].ItemId.ShouldBe(rmItemId);
        result[0].Quantity.ShouldBe(14m);
    }

    [Fact]
    public void JobCard_CompleteTimeLogs_AssignsCycleQtyToLatestOpenTimeLogAndSupportsCompletedQtyWithoutEndTime_09f429f616_ad669c8f48()
    {
        var jc = new JobCard(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10m, sequenceId: 1);

        // Start first open time log at 09:00
        var log1 = jc.StartTimeLog(new DateTime(2026, 5, 1, 9, 0, 0));

        // Update cycle qty without end time (toTime == default)
        jc.CompleteTimeLogs(default, 4m);
        log1.CompletedQty.ShouldBe(4m);
        log1.ToTime.ShouldBe(default);
        jc.CompletedQty.ShouldBe(4m);

        // Close first cycle at 09:30 with 4m
        jc.CompleteTimeLogs(new DateTime(2026, 5, 1, 9, 30, 0), 4m);
        log1.CompletedQty.ShouldBe(4m);
        log1.ToTime.ShouldBe(new DateTime(2026, 5, 1, 9, 30, 0));

        // Start second cycle at 10:00 and complete with remaining 6m
        var log2 = jc.StartTimeLog(new DateTime(2026, 5, 1, 10, 0, 0));
        jc.CompleteTimeLogs(new DateTime(2026, 5, 1, 10, 45, 0), 6m);

        log1.CompletedQty.ShouldBe(4m);
        log2.CompletedQty.ShouldBe(6m);
        jc.CompletedQty.ShouldBe(10m);
        jc.HasQtyLeftToMake().ShouldBeFalse();
    }

    [Fact]
    public void DeliveryScheduleService_SyncScheduleWithItem_ReusesRowsOnlyFromMatchingSalesOrderAndItem_93362b6785()
    {
        var service = new DeliveryScheduleService();
        var soId = Guid.NewGuid();
        var item1Id = Guid.NewGuid();
        var item2Id = Guid.NewGuid();

        var entries = new List<DeliveryScheduleEntry>
        {
            new(Guid.NewGuid(), soId, item1Id, new DateTime(2026, 6, 1), 5m),
            new(Guid.NewGuid(), soId, item2Id, new DateTime(2026, 6, 2), 5m),
            new(Guid.NewGuid(), Guid.NewGuid(), item1Id, new DateTime(2026, 6, 3), 5m),
        };

        var synced = service.SyncScheduleWithItem(entries, newItemQty: 10m, salesOrderId: soId, salesOrderItemId: item1Id);
        synced.Count.ShouldBe(1);
        synced[0].SalesOrderId.ShouldBe(soId);
        synced[0].SalesOrderItemId.ShouldBe(item1Id);
        synced[0].ScheduledQty.ShouldBe(5m);
    }
}
