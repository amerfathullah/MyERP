using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Assets.Entities;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing;
using MyERP.Manufacturing.DomainServices;
using MyERP.Manufacturing.Entities;
using MyERP.Manufacturing.Services;
using MyERP.Projects.Entities;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
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
    public void WorkOrderProductionService_CalculateRawMaterialConsumption_AccountsForProcessLossQty_67724()
    {
        var woRepo = Substitute.For<IRepository<WorkOrder, Guid>>();
        var service = new WorkOrderProductionService(woRepo);

        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-67724", Guid.NewGuid(), Guid.NewGuid(), 10m)
        {
            MaterialTransferred = 10m,
            ProducedQuantity = 2m,
            ProcessLossQty = 2m
        };
        var rmItemId = Guid.NewGuid();
        wo.RequiredItems.Add(new WorkOrderItem(Guid.NewGuid(), wo.Id, rmItemId, "Steel Rod", 20m)
        {
            TransferredQuantity = 20m,
            ConsumedQuantity = 8m // Remaining 12 must spread over (10 - 2 - 2 = 6) units => 2/unit, not 12/8 = 1.5/unit
        });

        var result = service.CalculateRawMaterialConsumption(wo, fgCompletedQty: 6m, backflushMethod: "BOM");
        result.Count.ShouldBe(1);
        result[0].ItemId.ShouldBe(rmItemId);
        result[0].Quantity.ShouldBe(12m);
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

    [Fact]
    public void StockEntryManager_ValidateWorkOrderStatus_RefusesMaterialConsumptionAgainstClosedWorkOrder_55074816c9()
    {
        var companyId = Guid.NewGuid();
        var wo = new WorkOrder(Guid.NewGuid(), companyId, "WO-CLOSED-MC", Guid.NewGuid(), Guid.NewGuid(), 10m);
        wo.Submit();
        wo.Close();

        var se = new StockEntry(Guid.NewGuid(), companyId, StockEntryType.MaterialConsumptionForManufacture, DateTime.UtcNow.Date)
        {
            WorkOrderId = wo.Id
        };

        var manager = new StockEntryManager(null!, null!, null!);
        var ex = Should.Throw<BusinessException>(() => manager.ValidateWorkOrderStatus(se, wo));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.StockEntryAgainstClosedWorkOrder);
    }

    [Fact]
    public void WorkOrder_RecordMaterialConsumption_SetsSkipTransferInProcess_AndUnstopReversesCleanly_bdb3a4e16c()
    {
        var companyId = Guid.NewGuid();
        var wo = new WorkOrder(Guid.NewGuid(), companyId, "WO-SKIP-MC", Guid.NewGuid(), Guid.NewGuid(), 10m)
        {
            SkipTransfer = true
        };
        var rm = new WorkOrderItem(Guid.NewGuid(), wo.Id, Guid.NewGuid(), "RM-1", 10m);
        wo.RequiredItems.Add(rm);
        wo.Submit();

        // Consuming material on skip-transfer WO moves it to InProcess
        rm.ConsumedQuantity = 4m;
        wo.RecordMaterialConsumption();
        wo.Status.ShouldBe(WorkOrderStatus.InProcess);

        // Stop & Unstop preserves InProcess because material has been consumed
        wo.Stop();
        wo.Status.ShouldBe(WorkOrderStatus.Stopped);
        wo.Unstop();
        wo.Status.ShouldBe(WorkOrderStatus.InProcess);

        // Reversing consumption reverts back to NotStarted when nothing else consumed/transferred/produced
        rm.ConsumedQuantity = 0m;
        wo.ReverseMaterialConsumptionStatus();
        wo.Status.ShouldBe(WorkOrderStatus.NotStarted);
    }

    [Fact]
    public void ItemLeadTime_ResolvePurchaseLeadTimeDays_UsesOrderingSupplierOrDefaultOrItemDays_e03a0ac44b_1cae8b6982()
    {
        var supplierA = Guid.NewGuid();
        var supplierB = Guid.NewGuid();
        var supplierOther = Guid.NewGuid();

        var ltWithDefault = new ItemLeadTime(Guid.NewGuid(), Guid.NewGuid(), purchaseTimeDays: 3, bufferTimeDays: 1);
        ltWithDefault.AddSupplier(supplierA, purchaseTimeDays: 20, bufferTimeDays: 1, isDefault: true);
        ltWithDefault.AddSupplier(supplierB, purchaseTimeDays: 10, bufferTimeDays: 2, isDefault: false);

        // No chosen supplier -> uses default supplier row (20 + 1 = 21)
        ltWithDefault.ResolvePurchaseLeadTimeDays().ShouldBe(21);
        // Explicit supplier B -> uses supplier B row (10 + 2 = 12)
        ltWithDefault.ResolvePurchaseLeadTimeDays(supplierB).ShouldBe(12);
        // Explicit supplier not in table -> falls back to item-level (3 + 1 = 4)
        ltWithDefault.ResolvePurchaseLeadTimeDays(supplierOther).ShouldBe(4);

        // Non-default supplier row only: no chosen supplier uses item-level (3), chosen supplier uses supplier row (11)
        var ltNoDefault = new ItemLeadTime(Guid.NewGuid(), Guid.NewGuid(), purchaseTimeDays: 3, bufferTimeDays: 0);
        ltNoDefault.AddSupplier(supplierA, purchaseTimeDays: 10, bufferTimeDays: 1, isDefault: false);

        ltNoDefault.ResolvePurchaseLeadTimeDays().ShouldBe(3);
        ltNoDefault.ResolvePurchaseLeadTimeDays(supplierA).ShouldBe(11);
    }

    [Fact]
    public void SubcontractingInwardOrder_UpdateReceivedStatus_CapsPerItemUsableQtyAt100Pct_ecb392f40b()
    {
        var scio = new SubcontractingInwardOrder(Guid.NewGuid(), Guid.NewGuid(), "SCIO-001", DateTime.UtcNow.Date, Guid.NewGuid());
        var item1 = new SubcontractingInwardOrderItem(Guid.NewGuid(), scio.Id, Guid.NewGuid(), 10m, 50m);
        var item2 = new SubcontractingInwardOrderItem(Guid.NewGuid(), scio.Id, Guid.NewGuid(), 10m, 50m);
        scio.AddItem(item1);
        scio.AddItem(item2);
        scio.Submit();

        // Over-receive item1 (15 of 10), item2 at 0 -> PerReceived must remain 0% (PartiallyReceived, not inflated)
        item1.ReceivedQty = 15m;
        scio.UpdateReceivedStatus();
        scio.PerReceived.ShouldBe(0m);
        scio.Status.ShouldBe(SubcontractingInwardOrderStatus.PartiallyReceived);

        // Over-receive item2 (12 of 10) -> PerReceived capped cleanly at 100%, never 120%
        item2.ReceivedQty = 12m;
        scio.UpdateReceivedStatus();
        scio.PerReceived.ShouldBe(100m);
        scio.Status.ShouldBe(SubcontractingInwardOrderStatus.Completed);
    }

    [Fact]
    public void Invoice_BaseOutstandingAmount_DeductsWriteOffAndTotalAdvance()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-WO-BASE", DateTime.UtcNow.Date)
        {
            CurrencyCode = "USD",
            ExchangeRate = 4.5m,
            GrandTotal = 100m,
            BaseGrandTotal = 450m
        };
        si.SetTotalAdvance(20m);
        si.SetWriteOff(30m);
        si.AmountPaid = 50m;
        si.OutstandingAmount.ShouldBe(0m);
        si.BaseOutstandingAmount.ShouldBe(0m);

        var pi = new PurchaseInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PI-WO-BASE", DateTime.UtcNow.Date)
        {
            CurrencyCode = "USD",
            ExchangeRate = 4.5m,
            GrandTotal = 100m,
            BaseGrandTotal = 450m
        };
        pi.SetTotalAdvance(25m);
        pi.SetWriteOff(25m);
        pi.AmountPaid = 50m;
        pi.OutstandingAmount.ShouldBe(0m);
        pi.BaseOutstandingAmount.ShouldBe(0m);
    }

    [Fact]
    public void AssetCapitalization_ClearItems_ResetsDraftLinesAndTotal()
    {
        var cap = new AssetCapitalization(Guid.NewGuid(), Guid.NewGuid(), "ACAP-001", DateTime.UtcNow.Date, Guid.NewGuid());
        cap.AddStockItem(Guid.NewGuid(), "Steel", 2m, 50m, Guid.NewGuid());
        cap.AddServiceItem(Guid.NewGuid(), "Install", 200m, Guid.NewGuid());
        cap.TotalCapitalizedAmount.ShouldBe(300m);

        cap.ClearItems();
        cap.StockItems.ShouldBeEmpty();
        cap.ServiceItems.ShouldBeEmpty();
        cap.ConsumedAssets.ShouldBeEmpty();
        cap.TotalCapitalizedAmount.ShouldBe(0m);

        cap.AddStockItem(Guid.NewGuid(), "Aluminum", 3m, 40m, Guid.NewGuid());
        cap.TotalCapitalizedAmount.ShouldBe(120m);
    }

    [Fact]
    public void BillOfMaterials_RecalculateCost_AppliesBatchSizeWhenBasedOnBomQty_AndSyncsOperationTargetWarehouses_22d504f5b6_e7be233789_f406f6e2b8_aba50eb01d_46c37b5eef()
    {
        var defaultTargetWh = Guid.NewGuid();
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-OP-001", Guid.NewGuid())
        {
            Quantity = 600m,
            TargetWarehouseId = defaultTargetWh
        };

        var op1 = new BomOperation(Guid.NewGuid(), bom.Id, Guid.NewGuid(), 10, timeInMins: 1m)
        {
            BatchSize = 10,
            SetCostBasedOnBomQty = true
        };
        op1.CalculateCost(100m); // Unrounded = 100/60 = 1.666666...; CostPerUnit = 1.666666.../10; * 600 = 100m exact

        bom.AddOperation(op1);
        op1.TargetWarehouseId.ShouldBe(defaultTargetWh);

        bom.RecalculateCost();
        Math.Round(bom.OperatingCost, 2).ShouldBe(100m);

        // Clearing default TargetWarehouseId clears operation target warehouses (46c37b5eef)
        bom.SyncOperationTargetWarehouses(null);
        op1.TargetWarehouseId.ShouldBeNull();
    }

    [Fact]
    public void SubcontractingInwardOrder_UpdateReceivedStatus_AndProposeMaterials_cf5585b7f5_c31b8c9de6()
    {
        var scio = new SubcontractingInwardOrder(Guid.NewGuid(), Guid.NewGuid(), "SCIO-002", DateTime.UtcNow.Date, Guid.NewGuid());
        var requiredItem = new SubcontractingInwardOrderItem(Guid.NewGuid(), scio.Id, Guid.NewGuid(), 10m, 50m);
        var additionalItem = new SubcontractingInwardOrderItem(Guid.NewGuid(), scio.Id, Guid.NewGuid(), 0m, 0m);
        scio.AddItem(requiredItem);
        scio.AddItem(additionalItem);
        scio.Submit();

        // Receiving only additional material transitions Open -> PartiallyReceived (Ongoing) even with PerReceived = 0 (cf5585b7f5)
        additionalItem.ReceivedQty = 3m;
        scio.UpdateReceivedStatus();
        scio.PerReceived.ShouldBe(0m);
        scio.Status.ShouldBe(SubcontractingInwardOrderStatus.PartiallyReceived);

        // Receiving full required qty completes the order without being diluted by the 0-required additional item
        requiredItem.ReceivedQty = 10m;
        scio.UpdateReceivedStatus();
        scio.PerReceived.ShouldBe(100m);
        scio.Status.ShouldBe(SubcontractingInwardOrderStatus.Completed);

        // Proposing customer materials keeps the fully received required item at 0 qty instead of skipping it (c31b8c9de6)
        var proposals = scio.GetProposedMaterialFromCustomerItems();
        proposals.Count.ShouldBe(1);
        proposals[0].OrderItemId.ShouldBe(requiredItem.Id);
        proposals[0].ProposedQty.ShouldBe(0m);
    }

    [Fact]
    public void SubcontractingManager_ValidateOrderAgainstPurchaseOrder_RefusesTwoRowsForOnePoItem_9ec94b2ae2()
    {
        var companyId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var po = new PurchaseOrder(Guid.NewGuid(), companyId, supplierId, "PO-SCO-DUP", DateTime.UtcNow.Date);
        po.AddItem(itemId, "Subcontracted FG", 10m, 100m, 0m);
        po.Submit();

        var manager = new MyERP.Purchasing.DomainServices.SubcontractingManager(null!, null!);
        var ex = Should.Throw<BusinessException>(() =>
            manager.ValidateOrderAgainstPurchaseOrder(po, new[] { itemId, itemId }));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("same Purchase Order Item row");
    }

    [Fact]
    public void SubcontractingManager_ResolveSecondaryItemRate_HandlesZeroQtyReceiptAndReturnRows_7e66f8e926_a1169be24c_e076a8adac()
    {
        // 1. Percentage secondary row rounding to 0 qty returns 0m instead of DivideByZeroException (7e66f8e926)
        var pctZeroRate = MyERP.Purchasing.DomainServices.SubcontractingManager.ResolveSecondaryItemRate(
            isReturn: false,
            secondaryQty: 0m,
            valuationType: SecondaryItemValuationType.PercentageOfComponentCost,
            bomSecondaryBaseCost: 0m,
            bomSecondaryStockQty: 1m,
            resolveWarehouseValuationRate: null,
            fgReceivedQty: 10m,
            rmCostPerQty: 50m,
            serviceCostPerQty: 10m,
            costAllocationPercentage: 20m);
        pctZeroRate.ShouldBe(0m);

        // 2. Receipt's Manual secondary row rounding to 0 qty still keeps its Manual rate (a1169be24c)
        var receiptManualZeroRate = MyERP.Purchasing.DomainServices.SubcontractingManager.ResolveSecondaryItemRate(
            isReturn: false,
            secondaryQty: 0m,
            valuationType: SecondaryItemValuationType.Manual,
            bomSecondaryBaseCost: 150m,
            bomSecondaryStockQty: 3m,
            resolveWarehouseValuationRate: null);
        receiptManualZeroRate.ShouldBe(50m);

        // 3. Return's secondary row rounding to 0 qty skips valuation lookup and returns 0m (e076a8adac)
        var lookupCalled = false;
        var returnValZeroRate = MyERP.Purchasing.DomainServices.SubcontractingManager.ResolveSecondaryItemRate(
            isReturn: true,
            secondaryQty: 0m,
            valuationType: SecondaryItemValuationType.ValuationRate,
            bomSecondaryBaseCost: 150m,
            bomSecondaryStockQty: 3m,
            resolveWarehouseValuationRate: () => { lookupCalled = true; throw new InvalidOperationException("Should not be called"); });
        returnValZeroRate.ShouldBe(0m);
        lookupCalled.ShouldBeFalse();
    }

    [Fact]
    public void StockReservationManager_GetSubcontractingUnreservedQty_ExcludesTransferReservations_3bf9fc0a98()
    {
        var companyId = Guid.NewGuid();
        var sco = new SubcontractingOrder(Guid.NewGuid(), companyId, "SCO-UNRES-01", DateTime.UtcNow.Date, Guid.NewGuid());
        var rmItemId = Guid.NewGuid();
        var supplied = new SubcontractingOrderSuppliedItem(Guid.NewGuid(), sco.Id, rmItemId, "Steel Sheet", requiredQty: 10m)
        {
            TransferredQty = 4m
        };
        sco.AddSuppliedItem(supplied);

        // Direct SCO reservation holding 2 units (4 already transferred)
        var orderSre = new StockReservationEntry(Guid.NewGuid(), companyId, rmItemId, Guid.NewGuid(), "SubcontractingOrder", sco.Id, 6m, 10m)
        {
            VoucherDetailId = supplied.Id,
            TransferredQty = 4m
        };
        orderSre.Submit();

        // Transfer SRE created in supplier warehouse for the 4 transferred units (FromVoucherType = "StockEntry")
        var transferSre = new StockReservationEntry(Guid.NewGuid(), companyId, rmItemId, Guid.NewGuid(), "SubcontractingOrder", sco.Id, 4m, 4m)
        {
            VoucherDetailId = supplied.Id,
            FromVoucherType = "StockEntry",
            FromVoucherId = Guid.NewGuid()
        };
        transferSre.Submit();

        // Unreserved = Required(10) - Transferred(4) - OrderHeld(6 - 4 = 2) = 4 (transferSre's 4 is NOT subtracted again!)
        var unreserved = StockReservationManager.GetSubcontractingUnreservedQty(sco, new[] { orderSre, transferSre });
        unreserved.ShouldContainKey(supplied.Id);
        unreserved[supplied.Id].ShouldBe(4m);
    }

    [Fact]
    public void WorkstationSchedulingService_FilterCoveredScheduleRows_EvaluatesCoveragePerWorkOrderNetOfUpstreamLoss_1c53d45356_3771672e23_68c6d2fe71_808549515d_bfd5ed73b5()
    {
        var companyId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var planRowId = Guid.NewGuid();
        var workstationId = Guid.NewGuid();
        var op1Id = Guid.NewGuid();
        var op2Id = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var bomId = Guid.NewGuid();

        var wo1 = new WorkOrder(Guid.NewGuid(), companyId, "WO-COV-1", itemId, bomId, 10m)
        {
            ProductionPlanId = planId,
            ProductionPlanItemId = planRowId
        };
        var wo2 = new WorkOrder(Guid.NewGuid(), companyId, "WO-COV-2", itemId, bomId, 10m)
        {
            ProductionPlanId = planId,
            ProductionPlanItemId = planRowId
        };

        // WO1 has upstream loss of 10 on op1 -> op2 required qty is 0 (fully lost upstream, 68c6d2fe71)
        // Even if WO1 has a draft job card of 10 on op2 with scheduled time, it MUST NOT cover WO2's uncovered 10 on op2 (bfd5ed73b5)
        var ops = new List<WorkOrderOperationCoverageSnapshot>
        {
            new(wo1.Id, op1Id, SequenceId: 1, CompletedQty: 0m, ProcessLossQty: 10m),
            new(wo1.Id, op2Id, SequenceId: 2, CompletedQty: 0m, ProcessLossQty: 0m),
            new(wo2.Id, op1Id, SequenceId: 1, CompletedQty: 8m, ProcessLossQty: 2m),
            new(wo2.Id, op2Id, SequenceId: 2, CompletedQty: 0m, ProcessLossQty: 0m),
        };

        var jcWo1Op2 = new JobCard(Guid.NewGuid(), companyId, wo1.Id, op2Id, forQuantity: 10m, sequenceId: 2);
        var scheduledSet = new HashSet<Guid> { jcWo1Op2.Id };

        var scheduleBlockOp2 = new ProductionPlanScheduleRow(
            workstationId,
            new DateTime(2026, 6, 1, 8, 0, 0),
            new DateTime(2026, 6, 1, 12, 0, 0),
            planId,
            planRowId,
            op2Id);

        // WO2 needs 8 on op2 (10 - 2 upstream loss) and has no job card yet -> block must NOT be freed!
        var remainingBeforeWo2Card = WorkstationSchedulingService.FilterCoveredScheduleRows(
            new[] { scheduleBlockOp2 },
            new[] { wo1, wo2 },
            ops,
            new[] { jcWo1Op2 },
            scheduledSet);
        remainingBeforeWo2Card.Count.ShouldBe(1);

        // Now add a job card with booked time for WO2 covering its 8 net of upstream loss (1c53d45356)
        var jcWo2Op2 = new JobCard(Guid.NewGuid(), companyId, wo2.Id, op2Id, forQuantity: 8m, sequenceId: 2);
        jcWo2Op2.AddTimeLog(new DateTime(2026, 6, 1, 8, 0, 0), new DateTime(2026, 6, 1, 10, 0, 0), 8m);

        var remainingAfterWo2Card = WorkstationSchedulingService.FilterCoveredScheduleRows(
            new[] { scheduleBlockOp2 },
            new[] { wo1, wo2 },
            ops,
            new[] { jcWo1Op2, jcWo2Op2 },
            scheduledSet);
        remainingAfterWo2Card.ShouldBeEmpty();
    }

    [Fact]
    public void ProjectUpdate_GetUncollectedMemberReplies_ValidatesSentCopyRecipientAndProjectMembership_5d66d483c3()
    {
        var member1Id = Guid.NewGuid();
        var member2Id = Guid.NewGuid();
        var outsiderId = Guid.NewGuid();

        var userByEmail = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase)
        {
            ["alice@example.com"] = member1Id,
            ["bob@example.com"] = member2Id,
            ["mallory@example.com"] = outsiderId
        };
        var projectMembers = new HashSet<Guid> { member1Id, member2Id };

        var sentCopies = new Dictionary<string, string>
        {
            ["COMM-COPY-ALICE"] = "alice@example.com",
            ["COMM-COPY-BOB"] = "bob@example.com",
            ["COMM-COPY-MALLORY"] = "mallory@example.com" // former member who was removed after email sent
        };

        var replies = new List<ProjectUpdateReplyMessage>
        {
            // 1. Valid direct reply from Alice to her own copy
            new("Alice@example.com", "COMM-COPY-ALICE", "Completed sprint tasks"),
            // 2. Spoofed/forwarded reply where Bob replies to Alice's copy -> rejected
            new("bob@example.com", "COMM-COPY-ALICE", "Forwarded reply"),
            // 3. Reply to a timeline message not in sentCopies -> rejected
            new("bob@example.com", "COMM-TIMELINE-OTHER", "Timeline reply"),
            // 4. Reply from Mallory to her own copy, but she is no longer in projectMembers -> rejected
            new("mallory@example.com", "COMM-COPY-MALLORY", "Outsider status"),
            // 5. Valid direct reply from Bob to his own copy
            new("bob@example.com", "COMM-COPY-BOB", "Working on deployment")
        };

        var alreadyCollected = new List<ProjectUpdateCollectedReply>
        {
            new(member1Id, "alice@example.com", "Completed sprint tasks")
        };

        var uncollected = MyERP.Projects.Entities.ProjectUpdate.GetUncollectedMemberReplies(
            replies, sentCopies, userByEmail, projectMembers, alreadyCollected);

        uncollected.Count.ShouldBe(1);
        uncollected[0].UserId.ShouldBe(member2Id);
        uncollected[0].ProjectStatus.ShouldBe("Working on deployment");
    }

    [Fact]
    public void MaterialRequest_UpdateItemRatesFromPriceList_UpdatesPriceListRateRateAndAmount_e65c791473()
    {
        var companyId = Guid.NewGuid();
        var item1Id = Guid.NewGuid();
        var item2Id = Guid.NewGuid();
        var mr = new MaterialRequest(Guid.NewGuid(), companyId, "MR-RATE-01", MaterialRequestType.Purchase, DateTime.UtcNow.Date);
        mr.AddItem(item1Id, "Item 1", 4m, "Unit");
        mr.AddItem(item2Id, "Item 2", 3m, "Box");

        mr.UpdateItemRatesFromPriceList(item => item.ItemId == item1Id ? 25.5m : null);

        mr.Items[0].PriceListRate.ShouldBe(25.5m);
        mr.Items[0].Rate.ShouldBe(25.5m);
        mr.Items[0].Amount.ShouldBe(102.0m);

        mr.Items[1].PriceListRate.ShouldBe(0m);
        mr.Items[1].Rate.ShouldBe(0m);
        mr.Items[1].Amount.ShouldBe(0m);
    }

    [Fact]
    public void SerialBatchBundleValidationService_ResizeOrRemoveDraftInternalReceiptPackage_HandlesStockUomAndZeroQty_436188ea40_27ea54fc6e_78a6ceb4a3_f449a8fb26()
    {
        var companyId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var whId = Guid.NewGuid();
        var receiptId = Guid.NewGuid();
        var rowId = Guid.NewGuid();

        var package = new SerialAndBatchBundle(
            Guid.NewGuid(), companyId, itemId, whId,
            BundleTransactionType.Inward, "PurchaseReceipt", receiptId, DateTime.UtcNow.Date)
        {
            VoucherDetailId = rowId,
            HasBatchNo = true
        };
        package.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), package.Id, qty: 10m, incomingRate: 5m, batchId: Guid.NewGuid()));

        // 1. Only UOM conversion factor changes (qty stays 10, conversion factor changes 1 -> 12) -> resizes entry to 120 in stock UOM (436188ea40, 27ea54fc6e)
        var resized = SerialBatchBundleValidationService.ResizeOrRemoveDraftInternalReceiptPackage(
            rowId,
            newQty: 10m,
            newConversionFactor: 12m,
            oldQty: 10m,
            oldConversionFactor: 1m,
            package: package,
            isInternalReceipt: true,
            isReturn: false,
            isNewReceipt: false);
        resized.Resized.ShouldBeTrue();
        resized.Removed.ShouldBeFalse();
        package.TotalQty.ShouldBe(120m);

        // 2. Accepted or rejected qty drops to 0 -> removes draft package owned by row and clears bundle reference (78a6ceb4a3, f449a8fb26)
        var removed = SerialBatchBundleValidationService.ResizeOrRemoveDraftInternalReceiptPackage(
            rowId,
            newQty: 0m,
            newConversionFactor: 12m,
            oldQty: 10m,
            oldConversionFactor: 12m,
            package: package,
            isInternalReceipt: true,
            isReturn: false,
            isNewReceipt: false);
        removed.Removed.ShouldBeTrue();
        removed.BundleId.ShouldBeNull();
        package.IsCancelled.ShouldBeTrue();
    }

    [Fact]
    public void SerialBatchBundleValidationService_OverflowBatchSplitAndRateLockedAutoPickAndPickListPackedQty_36fb8a7028_eba5799d6a_51a94d8372()
    {
        var itemId = Guid.NewGuid();
        var b1 = Guid.NewGuid();
        var b2 = Guid.NewGuid();

        // 1. Overflow batch rows keep UseSerialBatchFields = true (36fb8a7028)
        var splitRows = SerialBatchBundleValidationService.SplitStockEntryRowAcrossReservedBatches(
            itemId,
            requestedQty: 15m,
            reservedBatches: new[]
            {
                (b1, "BATCH-01", 9m),
                (b2, "BATCH-02", 10m)
            });
        splitRows.Count.ShouldBe(2);
        splitRows[0].Quantity.ShouldBe(9m);
        splitRows[0].UseSerialBatchFields.ShouldBeTrue();
        splitRows[0].IsOverflowRow.ShouldBeFalse();
        splitRows[1].Quantity.ShouldBe(6m);
        splitRows[1].UseSerialBatchFields.ShouldBeTrue();
        splitRows[1].IsOverflowRow.ShouldBeTrue();

        // 2. Rate-locked row keeps source rate when batch is auto-picked (eba5799d6a)
        var lockedRates = SerialBatchBundleValidationService.ResolveBatchAutoPickRate(
            currentRate: 120m, currentPriceListRate: 125m, batchBasedItemPrice: 95m, hasLockedRate: true);
        lockedRates.Rate.ShouldBe(120m);
        lockedRates.PriceListRate.ShouldBe(125m);

        var unlockedRates = SerialBatchBundleValidationService.ResolveBatchAutoPickRate(
            currentRate: 120m, currentPriceListRate: 125m, batchBasedItemPrice: 95m, hasLockedRate: false);
        unlockedRates.Rate.ShouldBe(95m);
        unlockedRates.PriceListRate.ShouldBe(95m);

        // 3. Pick List packed rows of a bundle component get per-location pack_qty rather than full component qty (51a94d8372)
        var packedRow1 = Guid.NewGuid();
        var packedRow2 = Guid.NewGuid();
        var keptRows = SerialBatchBundleValidationService.GetKeptComponentPackedRowQuantities(
            stalePackedRowIds: new[] { packedRow1, packedRow2 },
            componentStockQtyPerUnit: 2m,
            bundleRowStockQty: 5m,
            pickListLocationPackQuantities: new[] { 4m, 6m });
        keptRows.Count.ShouldBe(2);
        keptRows[0].ShouldBe((packedRow1, 4m));
        keptRows[1].ShouldBe((packedRow2, 6m));
    }

    [Fact]
    public void SalesOrderAndSalesInvoice_FilterBillableByItemAndWriteOffUsingRoundedTotal_b099a6aa25_f2219cf5b9()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var billedItemId = Guid.NewGuid();
        var unbilledItemId = Guid.NewGuid();

        var so = new MyERP.Sales.Entities.SalesOrder(Guid.NewGuid(), companyId, customerId, "SO-BILL-ITEM", DateTime.UtcNow.Date);
        so.AddItem(billedItemId, "Fully Billed Item", 5m, 100m, 0m);
        so.AddItem(unbilledItemId, "Unbilled Item", 3m, 50m, 0m);
        so.Submit();
        so.Items[0].BilledQty = 5m;

        // Filtering by the fully billed item returns false; filtering by the unbilled item returns true (b099a6aa25)
        MyERP.Sales.DomainServices.SalesOrderManager.HasPotentiallyBillableItems(so, filterItemId: billedItemId).ShouldBeFalse();
        MyERP.Sales.DomainServices.SalesOrderManager.HasPotentiallyBillableItems(so, filterItemId: unbilledItemId).ShouldBeTrue();

        // SalesInvoice automatic write-off uses RoundedTotal when rounding is enabled (f2219cf5b9)
        var si = new MyERP.Sales.Entities.SalesInvoice(
            Guid.NewGuid(), companyId, customerId, "SI-ROUNDOFF-01", DateTime.UtcNow.Date);
        si.AddItem(unbilledItemId, "Item", 1m, 99.60m, 0m);
        si.DisableRoundedTotal = false;
        si.ApplyRounding(); // GrandTotal = 99.60, RoundedTotal = 100.00
        si.AmountPaid = 98.00m;

        var writeOff = si.ApplyAutomaticWriteOff(writeOffAutomatically: true);
        writeOff.ShouldBe(2.00m); // 100.00 (RoundedTotal) - 98.00 (AmountPaid) = 2.00, not 1.60
    }

    [Fact]
    public void WorkOrder_HasReturnableComponents_And_JobCard_ValidateTotalJobCardQty_And_BomSecondaryBaseCost_db34b763e9_12c81a9dd5_a636da6cdd()
    {
        var companyId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var rmItemId = Guid.NewGuid();

        // 1. WorkOrder.HasReturnableComponents aggregates by ItemId across duplicate rows (db34b763e9)
        var wo = new WorkOrder(Guid.NewGuid(), companyId, "WO-RET-01", itemId, Guid.NewGuid(), 10m);
        var req1 = new WorkOrderItem(Guid.NewGuid(), wo.Id, rmItemId, "Steel Bar", 5m)
        {
            TransferredQuantity = 10m,
            ConsumedQuantity = 5m
        };
        var req2 = new WorkOrderItem(Guid.NewGuid(), wo.Id, rmItemId, "Steel Bar", 5m)
        {
            TransferredQuantity = 0m,
            ConsumedQuantity = 5m
        };
        wo.RequiredItems.Add(req1);
        wo.RequiredItems.Add(req2);
        wo.Submit();
        wo.Close();
        // Total transferred = 10, total consumed = 10 -> net returnable = 0
        wo.HasReturnableComponents().ShouldBeFalse();

        req2.ConsumedQuantity = 3m;
        // Total transferred = 10, total consumed = 8 -> net returnable = 2 > 0
        wo.HasReturnableComponents().ShouldBeTrue();

        // 2. JobCardManager.ValidateTotalJobCardQty compares totalJobCardQty directly against allowedWorkOrderQty (12c81a9dd5)
        Should.NotThrow(() => JobCardManager.ValidateTotalJobCardQty(totalJobCardQty: 10m, allowedWorkOrderQty: 10m));
        var jcEx = Should.Throw<BusinessException>(() => JobCardManager.ValidateTotalJobCardQty(totalJobCardQty: 12m, allowedWorkOrderQty: 10m));
        jcEx.Data["detail"]!.ToString()!.ShouldContain("Total Job Card quantity (12) exceeds allowed Work Order quantity (10)");

        // 3. BillOfMaterials.CalculateSecondaryItemsCostSummary computes BaseCost on PercentageOfComponentCost rows too (a636da6cdd)
        var bom = new BillOfMaterials(Guid.NewGuid(), companyId, "BOM-SEC-01", itemId)
        {
            Quantity = 1m
        };
        bom.AddItem(new BomItem(Guid.NewGuid(), bom.Id, rmItemId, "Raw Material", 10m, 100m, "Kg")); // Material cost = 1000
        var manualSec = new BomSecondaryItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), SecondaryItemType.Scrap, 2m)
        {
            ItemName = "Scrap",
            ValuationType = SecondaryItemValuationType.Manual,
            StockUom = "Kg",
            Rate = 50m
        };
        var pctSec = new BomSecondaryItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), SecondaryItemType.CoProduct, 4m)
        {
            ItemName = "Co-Product",
            ValuationType = SecondaryItemValuationType.PercentageOfComponentCost,
            StockUom = "Kg",
            CostAllocationPercentage = 20m
        };
        bom.AddSecondaryItem(manualSec);
        bom.AddSecondaryItem(pctSec);
        bom.RecalculateCost();

        var (totalCost, totalBaseCost, rowCosts) = bom.CalculateSecondaryItemsCostSummary(conversionRate: 4.5m, costPrecision: 2);
        // Manual: Cost = 100, BaseCost = 450
        // Pct: 20% of (1000 - 100) = 180, BaseCost = 180 * 4.5 = 810, Rate = 180 / 4 = 45
        rowCosts.Count.ShouldBe(2);
        rowCosts[0].Cost.ShouldBe(100m);
        rowCosts[0].BaseCost.ShouldBe(450m);
        rowCosts[1].Cost.ShouldBe(180m);
        rowCosts[1].BaseCost.ShouldBe(810m);
        pctSec.Rate.ShouldBe(45m);
        totalCost.ShouldBe(280m);
        totalBaseCost.ShouldBe(1260m);
    }

    [Fact]
    public void ProductionPlanMaterialPlanning_LeadTimeAndShortageBin_1cae8b6982_3b8085319d()
    {
        var itemId = Guid.NewGuid();
        var supplierA = Guid.NewGuid();
        var supplierB = Guid.NewGuid();
        var leadTimes = new Dictionary<(Guid ItemId, Guid SupplierId), decimal>
        {
            [(itemId, supplierA)] = 14m,
            [(itemId, supplierB)] = 21m
        };

        // 1. Purchase row uses supplier-specific lead time first, falls back to item lead time, and does not scale by requiredQty (1cae8b6982)
        ProductionPlanMaterialPlanningService.ResolveMrpItemLeadTimeDays(
            itemId, "Purchase", requiredQty: 50m, defaultSupplierId: supplierA,
            supplierPurchaseLeadTimes: leadTimes, itemPurchaseLeadTimeDays: 7m)
            .ShouldBe(14);

        ProductionPlanMaterialPlanningService.ResolveMrpItemLeadTimeDays(
            itemId, "Purchase", requiredQty: 50m, defaultSupplierId: Guid.NewGuid(),
            supplierPurchaseLeadTimes: leadTimes, itemPurchaseLeadTimeDays: 7m)
            .ShouldBe(7);

        // Manufacture row returns 0 when requiredQty <= 0, otherwise scales per-unit manufacturing time by requiredQty
        ProductionPlanMaterialPlanningService.ResolveMrpItemLeadTimeDays(
            itemId, "Manufacture", requiredQty: 0m, itemManufacturingLeadTimeDaysPerUnit: 0.5m)
            .ShouldBe(0);

        ProductionPlanMaterialPlanningService.ResolveMrpItemLeadTimeDays(
            itemId, "Manufacture", requiredQty: 5m, itemManufacturingLeadTimeDaysPerUnit: 0.5m)
            .ShouldBe(3);

        // 2. Receiving warehouse stock is preserved even when receiving warehouse is also in transferFromWarehouses (3b8085319d)
        var receivingWh = Guid.NewGuid();
        var transferWh2 = Guid.NewGuid();
        var bins = new[]
        {
            (WarehouseId: receivingWh, ActualQty: 30m, ProjectedQty: 40m, ReservedQtyForProduction: 5m, OrderedQty: 15m, PlannedQty: 0m),
            (WarehouseId: transferWh2, ActualQty: 50m, ProjectedQty: 50m, ReservedQtyForProduction: 0m, OrderedQty: 0m, PlannedQty: 0m)
        };

        var withReceivingWh = ProductionPlanMaterialPlanningService.ResolveShortageBinDetails(
            itemWarehouseId: receivingWh,
            transferFromWarehouseIds: new[] { receivingWh, transferWh2 },
            companyBins: bins);
        withReceivingWh.ActualQty.ShouldBe(30m);
        withReceivingWh.OrderedQty.ShouldBe(15m);

        var withoutReceivingWh = ProductionPlanMaterialPlanningService.ResolveShortageBinDetails(
            itemWarehouseId: null,
            transferFromWarehouseIds: new[] { transferWh2 },
            companyBins: bins);
        withoutReceivingWh.ActualQty.ShouldBe(30m);
    }

    [Fact]
    public async Task BlanketOrder_ValidateRowItemsAndPartyAndClearOnItemChange_f6cca3f59e_51df5c2ef0_09fd286163_ad16ee4b4b()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var allowedItemId = Guid.NewGuid();
        var otherItemId = Guid.NewGuid();
        var today = DateTime.UtcNow.Date;

        var bo = new BlanketOrder(
            Guid.NewGuid(), companyId, "BO-VAL-01", "Selling", customerId,
            today.AddDays(-5), today.AddDays(30))
        {
            Currency = "MYR",
            ExchangeRate = 1m
        };
        bo.AddItem(allowedItemId, 100m, 42m, "Allowed Item");
        bo.Submit();

        // 1. ValidateOrderParty throws even when orderPartyId is null (e.g., Quotation to Lead without Customer) (09fd286163, ad16ee4b4b)
        var partyEx = Should.Throw<BusinessException>(() => bo.ValidateOrderParty(null, "LEAD-0001"));
        partyEx.Data["detail"]!.ToString()!.ShouldContain("Selected Blanket Order BO-VAL-01 does not belong to LEAD-0001");
        Should.NotThrow(() => bo.ValidateOrderParty(customerId, "CUST-0001"));

        // 2. ValidateOrderRowItems throws when a row referencing this Blanket Order has an item not in the Blanket Order (f6cca3f59e)
        var rowEx = Should.Throw<BusinessException>(() => bo.ValidateOrderRowItems(new[]
        {
            (RowNumber: 1, ItemId: allowedItemId, ItemCode: (string?)"ITEM-OK", BlanketOrderId: (Guid?)bo.Id),
            (RowNumber: 2, ItemId: otherItemId, ItemCode: (string?)"ITEM-BAD", BlanketOrderId: (Guid?)bo.Id)
        }));
        rowEx.Data["detail"]!.ToString()!.ShouldContain("Row #2: Item ITEM-BAD is not in Blanket Order BO-VAL-01");

        // 3. ResolveRowBlanketOrderOnItemChangeAsync clears BlanketOrderId and BlanketOrderRate when item is not in BO or party is null (51df5c2ef0)
        var boRepo = Substitute.For<IRepository<BlanketOrder, Guid>>();
        boRepo.GetQueryableAsync().Returns(new List<BlanketOrder> { bo }.AsQueryable());
        var rateService = new BlanketOrderRateService(boRepo);

        var matched = await rateService.ResolveRowBlanketOrderOnItemChangeAsync(
            companyId, customerId, allowedItemId, "Selling", today, againstBlanketOrder: true, existingBlanketOrderId: bo.Id);
        matched.BlanketOrderId.ShouldBe(bo.Id);
        matched.BlanketOrderRate.ShouldBe(42m);

        var clearedOnItemChange = await rateService.ResolveRowBlanketOrderOnItemChangeAsync(
            companyId, customerId, otherItemId, "Selling", today, againstBlanketOrder: true, existingBlanketOrderId: bo.Id);
        clearedOnItemChange.BlanketOrderId.ShouldBeNull();
        clearedOnItemChange.BlanketOrderRate.ShouldBe(0m);

        var clearedOnNullParty = await rateService.ResolveRowBlanketOrderOnItemChangeAsync(
            companyId, null, allowedItemId, "Selling", today, againstBlanketOrder: true, existingBlanketOrderId: bo.Id);
        clearedOnNullParty.BlanketOrderId.ShouldBeNull();
        clearedOnNullParty.BlanketOrderRate.ShouldBe(0m);
    }

    [Fact]
    public void SubcontractingReturnSecondaryRounding_PurchaseReceiptExchangeDiff_JournalEntryAccountRate_And_SalesInvoicePosRounding_4b9002b2a4_ce1484591d_ef07a6b62b_0548448122_7b255d1b94()
    {
        // 1. Subcontracting return secondary item whose share rounds to 0 on early returns gets remaining balance on last FG return (4b9002b2a4)
        var secRowId = Guid.NewGuid();
        var secItemId = Guid.NewGuid();
        var sourceRows = new[]
        {
            (SourceSecondaryRowId: secRowId, ItemId: secItemId,
             OriginalReceivedQty: 0.01m, OriginalProcessLossQty: 0m, OriginalQty: 0.01m,
             PriorReturnedReceivedQty: 0m, PriorReturnedProcessLossQty: 0m, PriorReturnedQty: 0m)
        };

        // Partial return (20% of 0.01 = 0.002 -> rounds to 0.00 at 2dp -> dropped on non-last return)
        var partialReturn = MyERP.Purchasing.DomainServices.SubcontractingManager.ResolveReturnSecondaryItemQuantities(
            returnFgRatio: 0.2m, isLastFgReturn: false, sourceSecondaryRows: sourceRows, precision: 2);
        partialReturn.ShouldBeEmpty();

        // Last FG return recovers the full remaining -0.01 secondary qty instead of dropping the row early
        var lastReturn = MyERP.Purchasing.DomainServices.SubcontractingManager.ResolveReturnSecondaryItemQuantities(
            returnFgRatio: 0.2m, isLastFgReturn: true, sourceSecondaryRows: sourceRows, precision: 2);
        lastReturn.Count.ShouldBe(1);
        lastReturn[0].Qty.ShouldBe(-0.01m);

        // 2. Purchase Receipt exchange rate difference rounds to baseNetAmountPrecision (ce1484591d)
        var subCentDiff = MyERP.Purchasing.DomainServices.PurchaseReceiptManager.CalculateExchangeRateDifference(
            qty: 1m, netRate: 0.1m, purchaseInvoiceExchangeRate: 4.5001m, purchaseReceiptExchangeRate: 4.5000m, baseNetAmountPrecision: 2);
        subCentDiff.ShouldBe(0m);

        var realDiff = MyERP.Purchasing.DomainServices.PurchaseReceiptManager.CalculateExchangeRateDifference(
            qty: 100m, netRate: 10m, purchaseInvoiceExchangeRate: 4.55m, purchaseReceiptExchangeRate: 4.50m, baseNetAmountPrecision: 2);
        realDiff.ShouldBe(50.00m);

        // 3. Journal Entry line exchange rate preserved only while account currency is unchanged (ef07a6b62b)
        MyERP.Accounting.Entities.JournalEntry.ResolveRowExchangeRateOnAccountChange(
            previousAccountCurrency: "USD", previousExchangeRate: 4.48m, newAccountCurrency: "USD", fetchedExchangeRate: 4.50m)
            .ShouldBe(4.48m);
        MyERP.Accounting.Entities.JournalEntry.ResolveRowExchangeRateOnAccountChange(
            previousAccountCurrency: "USD", previousExchangeRate: 4.48m, newAccountCurrency: "EUR", fetchedExchangeRate: 4.92m)
            .ShouldBe(4.92m);

        // 4. Sales Invoice POS change amount uses RoundedTotal (0548448122) & split return payments round sum (7b255d1b94)
        var posInvoice = new MyERP.Sales.Entities.SalesInvoice(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-POS-01", DateTime.UtcNow.Date)
        {
            IsPos = true,
            DisableRoundedTotal = false
        };
        posInvoice.AddItem(Guid.NewGuid(), "Item", 1m, 99.60m, 0m);
        posInvoice.ApplyRounding(); // GrandTotal = 99.60, RoundedTotal = 100.00
        posInvoice.AmountPaid = 105.00m;

        var change = posInvoice.CalculateChangeAmount(hasCashPayment: true, precision: 2);
        change.ShouldBe(5.00m); // 105.00 - 100.00 (RoundedTotal), not 5.40

        var posReturn = new MyERP.Sales.Entities.SalesInvoice(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-POS-RET-01", DateTime.UtcNow.Date)
        {
            IsPos = true,
            IsReturn = true,
            DisableRoundedTotal = true
        };
        posReturn.AddItem(Guid.NewGuid(), "Return Item", -1m, 68.59m, 0m);
        posReturn.IsPosReturnSplitPaymentBalanced(new[] { -18.59m, -50.00m }, precision: 2).ShouldBeTrue();
    }

    [Fact]
    public void BlanketOrderPlcRate_JobCardCancelCheck_ProductionPlanRequestedStatus_And_StockReservationDialogStockQty_6b75a73931_6f328657c8_e00b16f781_3bf7091474_6e83c51495()
    {
        var companyId = Guid.NewGuid();
        var today = DateTime.UtcNow.Date;

        // 1. Blanket Order price list in order currency uses Blanket Order ExchangeRate (6b75a73931)
        var bo = new BlanketOrder(
            Guid.NewGuid(), companyId, "BO-PLC-01", "Selling", Guid.NewGuid(),
            today, today.AddDays(30))
        {
            Currency = "USD",
            ExchangeRate = 4.45m
        };
        bo.ResolvePriceListConversionRate(companyCurrency: "MYR", priceListCurrency: "MYR").ShouldBe(1.0m);
        bo.ResolvePriceListConversionRate(companyCurrency: "MYR", priceListCurrency: "USD").ShouldBe(4.45m);
        bo.ResolvePriceListConversionRate(companyCurrency: "MYR", priceListCurrency: "EUR", priceListCurrencyChanged: true, fetchedExchangeRateToCompany: 4.90m).ShouldBe(4.90m);

        // 2. Job Card cancel check does not count handed-over pending qty as completed by remaining cards (6f328657c8)
        // Work Order of 10 has produced 10. Card 1 completed 6 and handed over 4 pending to Card 2 (completed 4).
        // Cancelling Card 2 leaves remaining completed = 6 (even though Card 1 had pending_qty = 4) -> must throw!
        var cancelEx = Should.Throw<BusinessException>(() =>
            JobCardManager.ValidateProducedQuantityOnCancel(
                remainingOperationCompletedQty: 6m,
                remainingOperationProcessLossQty: 0m,
                workOrderQty: 10m,
                operationRequiredQty: 10m,
                workOrderProducedQty: 10m));
        cancelEx.Data["detail"]!.ToString()!.ShouldContain("Cannot cancel Job Card because the Work Order has already produced 10");

        // When remaining cards completed 10 (covering produced 10), cancel check passes
        Should.NotThrow(() =>
            JobCardManager.ValidateProducedQuantityOnCancel(
                remainingOperationCompletedQty: 8m,
                remainingOperationProcessLossQty: 2m,
                workOrderQty: 10m,
                operationRequiredQty: 10m,
                workOrderProducedQty: 10m));

        // 3. Production Plan sets MaterialRequested only when at least one row has PlannedQty > 0 (e00b16f781)
        var planInStock = new ProductionPlan(Guid.NewGuid(), companyId, "PP-INSTOCK-01", today);
        planInStock.AddPlannedItem(new ProductionPlanItem(Guid.NewGuid(), planInStock.Id, Guid.NewGuid(), "FG-1", Guid.NewGuid(), 10m));
        planInStock.Submit();
        var zeroMrRow = new ProductionPlanMrItem(Guid.NewGuid(), planInStock.Id, Guid.NewGuid(), "RM-1", 10m)
        {
            AvailableQty = 10m,
            PlannedQty = 0m,
            RequestedQty = 0m
        };
        planInStock.AddMaterialRequirement(zeroMrRow);
        planInStock.UpdateRequestedStatus();
        planInStock.Status.ShouldBe(ProductionPlanStatus.Submitted);

        var positiveMrRow = new ProductionPlanMrItem(Guid.NewGuid(), planInStock.Id, Guid.NewGuid(), "RM-2", 10m)
        {
            AvailableQty = 2m,
            PlannedQty = 8m,
            RequestedQty = 8m
        };
        planInStock.AddMaterialRequirement(positiveMrRow);
        planInStock.UpdateRequestedStatus();
        planInStock.Status.ShouldBe(ProductionPlanStatus.MaterialRequested);

        // 4. Stock Reservation dialog unreserved qty stays in stock UOM without dividing by conversionFactor (3bf7091474, 6e83c51495)
        StockReservationManager.CalculateDialogUnreservedStockQty(
            stockQty: 120m, stockReservedQty: 24m, dispatchedQtyInOrderUom: 0m, conversionFactor: 12m)
            .ShouldBe(96m);
        StockReservationManager.CalculateDialogUnreservedStockQty(
            stockQty: 120m, stockReservedQty: 0m, dispatchedQtyInOrderUom: 3m, conversionFactor: 12m)
            .ShouldBe(84m);
    }

    [Fact]
    public void SalesOrderPoScheduleDateAndIndicator_StockReconciliationPreview_PurchaseReceiptUomReset_And_AssetCapQtyRateRefresh_3a7d1a09b6_f1e1cd00b5_3fc1fd394f_9b3dd19043_f1eb56f686_4b8fadae2f_c17e33a06b_82c0595f3a_5f0ca9b071()
    {
        var today = new DateTime(2026, 11, 19);

        // 1. Sales Order overdue delivery date falls back to today when creating Purchase Order (3a7d1a09b6)
        var soItem = new SalesOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Widget", 10m, 50m, 0m, "Box")
        {
            DeliveryDate = today.AddDays(-5),
            ConversionFactor = 3m,
            DeliveredQty = 6.6667m
        };
        soItem.ResolvePurchaseOrderScheduleDate(today).ShouldBe(today);
        soItem.ResolvePurchaseOrderScheduleDate(today, bundleDeliveryDate: today.AddDays(7)).ShouldBe(today.AddDays(7));

        // 2. Sales Order item indicator rounds pending stock qty before comparing with actualBinStockQty (f1e1cd00b5, 3fc1fd394f)
        // StockQty = 30, DeliveredQty * ConversionFactor = 20.0001 -> pendingStockQty = 9.9999 <= 10 -> "green"
        soItem.GetStockAvailabilityIndicatorColor(actualBinStockQty: 9.9999m, stockQtyPrecision: 4).ShouldBe("green");
        soItem.GetStockAvailabilityIndicatorColor(actualBinStockQty: 9.9998m, stockQtyPrecision: 4).ShouldBe("orange");

        // 3. Stock Reconciliation preview derives CurrentAmount, respects blank vs 0, and rounds diffs (9b3dd19043, f1eb56f686, 4b8fadae2f, c17e33a06b)
        var srItem = new StockReconciliationItem(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            newQuantity: 10m, newValuationRate: 12.345m, currentQuantity: 10m, currentValuationRate: 12.345m);

        var blankPreview = srItem.CalculatePreviewDifference(inputQty: null, inputValuationRate: null);
        blankPreview.CurrentAmount.ShouldBe(123.45m);
        blankPreview.Amount.ShouldBe(123.45m);
        blankPreview.QuantityDifference.ShouldBe(0m);
        blankPreview.AmountDifference.ShouldBe(0m);

        var zeroQtyPreview = srItem.CalculatePreviewDifference(inputQty: 0m, inputValuationRate: null);
        zeroQtyPreview.EffectiveQty.ShouldBe(0m);
        zeroQtyPreview.Amount.ShouldBe(0m);
        zeroQtyPreview.QuantityDifference.ShouldBe(-10m);
        zeroQtyPreview.AmountDifference.ShouldBe(-123.45m);

        // 4. PurchaseReceiptItem resets ConversionFactor to 1 before computing StockUomRate when Uom == StockUom (82c0595f3a)
        var prItem = new PurchaseReceiptItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Bolt", 10m, 24m, 0m, "Unit")
        {
            StockUom = "Unit",
            ConversionFactor = 12m
        };
        prItem.ResetConversionFactorAndGetStockUomRate().ShouldBe(24m);
        prItem.ConversionFactor.ShouldBe(1m);

        // 5. AssetCapitalization refreshes stock item valuation rate on qty change (5f0ca9b071)
        var whId = Guid.NewGuid();
        var stockItemId = Guid.NewGuid();
        var cap = new AssetCapitalization(Guid.NewGuid(), Guid.NewGuid(), "ACAP-QTY-01", today, Guid.NewGuid());
        cap.AddStockItem(stockItemId, "Steel Plate", 2m, 50m, whId);
        cap.TotalCapitalizedAmount.ShouldBe(100m);

        cap.UpdateStockItemQuantity(stockItemId, newQty: 5m, resolveOutgoingValuationRate: (_, _, qty) => qty >= 5m ? 44m : 50m);
        cap.StockItems[0].Qty.ShouldBe(5m);
        cap.StockItems[0].Rate.ShouldBe(44m);
        cap.StockItems[0].Amount.ShouldBe(220m);
        cap.TotalCapitalizedAmount.ShouldBe(220m);
    }

    [Fact]
    public void PurchaseReceiptExchangeDiffGlSides_InternalTransitQtyCap_And_PackedItemSplitBundleReturnParity_7eab0353ba_99e9697f80_5b81b52467_926b3cd52d_1d46e4a453_3eea52f18c_428918d86a_ae4fab557c()
    {
        // 1. Purchase Receipt exchange difference GL row sets CreditInAccountCurrency on credit rows, never negative DebitInAccountCurrency (7eab0353ba)
        var creditSides = MyERP.Purchasing.DomainServices.PurchaseReceiptManager.BuildExchangeRateDifferenceGlSides(15.25m);
        creditSides.Debit.ShouldBe(0m);
        creditSides.Credit.ShouldBe(15.25m);
        creditSides.DebitInAccountCurrency.ShouldBe(0m);
        creditSides.CreditInAccountCurrency.ShouldBe(15.25m);

        var debitSides = MyERP.Purchasing.DomainServices.PurchaseReceiptManager.BuildExchangeRateDifferenceGlSides(-15.25m);
        debitSides.Debit.ShouldBe(15.25m);
        debitSides.Credit.ShouldBe(0m);
        debitSides.DebitInAccountCurrency.ShouldBe(15.25m);
        debitSides.CreditInAccountCurrency.ShouldBe(0m);

        // 2. Internal transfer Purchase Receipt caps rounding against remaining transit qty (counting returns), but leaves over-receipts uncapped (99e9697f80, 5b81b52467)
        var dnItemId = Guid.NewGuid();
        var cappedSecondHalf = MyERP.Purchasing.DomainServices.PurchaseReceiptManager.ResolveInternalReceiptSourceWarehouseStockQty(
            receivedStockQty: 5.0001m,
            isInternalReceipt: true,
            isReturn: false,
            deliveryNoteItemId: dnItemId,
            hasSerialOrBatchPackage: false,
            deliveredStockQtyInTransit: 10.0000m,
            currentRowUnroundedReceivedStockQty: 5.0000m,
            otherReceiptsAndReturnsUnroundedReceivedStockQty: 5.0000m,
            otherReceiptsAndReturnsSleActualQtyInTransit: -5.0001m);
        cappedSecondHalf.ShouldBe(4.9999m); // Caps 5.0001 to the 4.9999 left in transit so 5.0001 + 4.9999 = 10.0000

        var uncappedOverReceipt = MyERP.Purchasing.DomainServices.PurchaseReceiptManager.ResolveInternalReceiptSourceWarehouseStockQty(
            receivedStockQty: 6.0000m,
            isInternalReceipt: true,
            isReturn: false,
            deliveryNoteItemId: dnItemId,
            hasSerialOrBatchPackage: false,
            deliveredStockQtyInTransit: 10.0000m,
            currentRowUnroundedReceivedStockQty: 6.0000m,
            otherReceiptsAndReturnsUnroundedReceivedStockQty: 6.0000m,
            otherReceiptsAndReturnsSleActualQtyInTransit: -6.0000m);
        uncappedOverReceipt.ShouldBe(6.0000m); // Over-receipt (12 > 10) is not capped

        // 3. Repeated bundle component delivered from two warehouses pairs each occurrence with its own delivered rows on return (926b3cd52d)
        var bundleItemId = Guid.NewGuid();
        var compItemId = Guid.NewGuid();
        var soPackedOcc1 = Guid.NewGuid();
        var soPackedOcc2 = Guid.NewGuid();
        var delRow1WhA = Guid.NewGuid();
        var delRow1WhB = Guid.NewGuid();
        var delRow2WhA = Guid.NewGuid();

        var pairs = SerialBatchBundleValidationService.PairReturnComponentOrderRows(
            parentBundleItemId: bundleItemId,
            bundleComponents: new[] { (0, compItemId), (1, compItemId) },
            deliveredPackedRows: new[]
            {
                (delRow1WhA, bundleItemId, compItemId, (Guid?)soPackedOcc1),
                (delRow1WhB, bundleItemId, compItemId, (Guid?)soPackedOcc1),
                (delRow2WhA, bundleItemId, compItemId, (Guid?)soPackedOcc2)
            },
            isReturn: true);
        pairs.Count.ShouldBe(3);
        pairs[0].ShouldBe((0, compItemId, (Guid?)delRow1WhA));
        pairs[1].ShouldBe((0, compItemId, (Guid?)delRow1WhB));
        pairs[2].ShouldBe((1, compItemId, (Guid?)delRow2WhA));

        // 4. Returned packed row batch valuation accepts any bundle on the parent row and prioritizes the delivered row's own bundle rate (3eea52f18c, 1d46e4a453)
        var batchId = Guid.NewGuid();
        var bundleWhA = Guid.NewGuid();
        var bundleWhB = Guid.NewGuid();
        var entries = new[]
        {
            (BundleId: bundleWhA, BatchId: batchId, IncomingRate: 10m),
            (BundleId: bundleWhB, BatchId: batchId, IncomingRate: 15m)
        };
        SerialBatchBundleValidationService.ResolveReturnPackedItemBatchRate(batchId, bundleWhA, entries).ShouldBe(10m);
        SerialBatchBundleValidationService.ResolveReturnPackedItemBatchRate(batchId, bundleWhB, entries).ShouldBe(15m);

        // 5. Rate repost matches packed item by bundle before warehouse (c0fe71cee9, 428918d86a) & allocate picked qty rounds remaining to packed qty precision (ae4fab557c)
        var parentDetailId = Guid.NewGuid();
        var whA = Guid.NewGuid();
        var whB = Guid.NewGuid();
        SerialBatchBundleValidationService.MatchesPackedItemForRateRepost(
            parentDetailId, compItemId, whA, bundleWhA,
            parentDetailId, compItemId, whB, bundleWhA).ShouldBeTrue();
        SerialBatchBundleValidationService.MatchesPackedItemForRateRepost(
            parentDetailId, compItemId, whA, null,
            parentDetailId, compItemId, whB, null).ShouldBeFalse();
        SerialBatchBundleValidationService.MatchesPackedItemForRateRepost(
            parentDetailId, compItemId, whA, bundleWhA,
            parentDetailId, compItemId, whA, bundleWhB).ShouldBeFalse();

        var pl1 = Guid.NewGuid();
        var pl2 = Guid.NewGuid();
        var allocated = SerialBatchBundleValidationService.AllocatePickedQtyForPacking(
            new[] { (pl1, 3.3333m, 0m), (pl2, 5m, 0m) },
            requiredPackQty: 5m,
            deliveredQtyPrecision: 4,
            packedQtyPrecision: 4);
        allocated.Count.ShouldBe(2);
        allocated[0].ShouldBe((pl1, 3.3333m));
        allocated[1].ShouldBe((pl2, 1.6667m));
    }

    [Fact]
    public void InterCompanyPendingOrderUom_DuplicateReferenceClear_ChildItemDraftPoGuard_And_PaymentReconciliationLimits_fe71cadf90_3941f742be_0fda60d8c7_14a3847119_85048a06c4_7b630d1df1()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var today = DateTime.UtcNow.Date;

        // 1. Inter-company pending order qty compared in order UOM (fe71cadf90)
        var so = new SalesOrder(Guid.NewGuid(), companyId, customerId, "SO-IC-UOM", today);
        so.AddItem(Guid.NewGuid(), "Box Item", 1m, 100m, 0m, "Box");
        so.Items[0].ConversionFactor = 1000m;
        // Ordered 999.99 stock units -> remaining stock = 0.01, which in Box UOM (0.01 / 1000 = 0.00001) rounds to 0.0000 at 4dp
        var subUnitRemainder = new Dictionary<Guid, decimal> { [so.Items[0].Id] = 999.99m };
        MyERP.Core.DomainServices.InterCompanyTransactionService.HasPendingInterCompanyOrderQty(so, subUnitRemainder, qtyPrecision: 4)
            .ShouldBeFalse();

        var realRemainder = new Dictionary<Guid, decimal> { [so.Items[0].Id] = 500m };
        MyERP.Core.DomainServices.InterCompanyTransactionService.HasPendingInterCompanyOrderQty(so, realRemainder, qtyPrecision: 4)
            .ShouldBeTrue();

        // 2. Duplicating SI/PI/PO clears inter-company references (3941f742be)
        var si = new SalesInvoice(Guid.NewGuid(), companyId, customerId, "SI-DUP-01", today)
        {
            InterCompanyPurchaseInvoiceId = Guid.NewGuid()
        };
        var pi = new PurchaseInvoice(Guid.NewGuid(), companyId, supplierId, "PI-DUP-01", today)
        {
            InterCompanyInvoiceId = Guid.NewGuid()
        };
        var po = new PurchaseOrder(Guid.NewGuid(), companyId, supplierId, "PO-DUP-01", today)
        {
            InterCompanySalesOrderId = Guid.NewGuid()
        };
        MyERP.Core.DomainServices.InterCompanyTransactionService.ClearInterCompanyReferencesOnDuplicate(si, pi, po);
        si.InterCompanyPurchaseInvoiceId.ShouldBeNull();
        pi.InterCompanyInvoiceId.ShouldBeNull();
        po.InterCompanySalesOrderId.ShouldBeNull();

        // 3. Draft or submitted Purchase Order linking a Sales Order row or its packed row blocks deletion (0fda60d8c7, 14a3847119)
        var soItemId = so.Items[0].Id;
        var packedRowId = Guid.NewGuid();
        var poRows = new[]
        {
            (PurchaseOrderId: Guid.NewGuid(), PurchaseOrderNumber: "PO-DRAFT-01", Status: MyERP.Core.DocumentStatus.Draft,
             SalesOrderId: (Guid?)so.Id, SalesOrderItemId: (Guid?)null, SalesOrderPackedItemId: (Guid?)packedRowId)
        };
        var linkedPoNo = MyERP.Core.DomainServices.ChildItemUpdateService.ResolveLinkedPurchaseOrderNumberForSalesOrderRow(
            so.Id, soItemId, new[] { packedRowId }, poRows);
        linkedPoNo.ShouldBe("PO-DRAFT-01");

        var childService = new MyERP.Core.DomainServices.ChildItemUpdateService();
        var delEx = Should.Throw<BusinessException>(() =>
            childService.ValidateSalesOrderItemDeletion(so.Items[0], linkedPurchaseOrderNumber: linkedPoNo));
        delEx.Data["detail"]!.ToString()!.ShouldContain("already ordered on Purchase Order PO-DRAFT-01");

        // 4. Payment Reconciliation validates both payment_limit and invoice_limit >= 0 (85048a06c4)
        Should.NotThrow(() => MyERP.Accounting.DomainServices.PaymentReconciliationEngine.ValidateReconciliationLimits(null, 0));
        var payLimEx = Should.Throw<BusinessException>(() =>
            MyERP.Accounting.DomainServices.PaymentReconciliationEngine.ValidateReconciliationLimits(-1, 50));
        payLimEx.Data["detail"]!.ToString()!.ShouldContain("Payment Limit must be a non-negative integer");
        var invLimEx = Should.Throw<BusinessException>(() =>
            MyERP.Accounting.DomainServices.PaymentReconciliationEngine.ValidateReconciliationLimits(50, -5));
        invLimEx.Data["detail"]!.ToString()!.ShouldContain("Invoice Limit must be a non-negative integer");

        // 5. Return against a tax-free document does not auto-apply default taxes (7b630d1df1)
        SalesInvoiceManager.ShouldApplyDefaultTaxes(isReturn: true, returnAgainstId: Guid.NewGuid(), sourceDocumentHasTaxes: false).ShouldBeFalse();
        SalesInvoiceManager.ShouldApplyDefaultTaxes(isReturn: true, returnAgainstId: Guid.NewGuid(), sourceDocumentHasTaxes: true).ShouldBeTrue();
        SalesInvoiceManager.ShouldApplyDefaultTaxes(isReturn: false, returnAgainstId: null, sourceDocumentHasTaxes: false).ShouldBeTrue();
    }

    [Fact]
    public void SerialBatchBundle_DraftOwnership_UnroundedBatchPick_SplitBundleReturnAllocation_And_ZeroDeliveredRate_2fda0bb04c_dcaf1a81c0_ff83c45efe_bb77453ba8_bc6c911436_8bcb291efd_bd09247690()
    {
        var companyId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var whId = Guid.NewGuid();
        var draftDn1Id = Guid.NewGuid();
        var draftDn2Id = Guid.NewGuid();

        // 1. Draft voucher cannot take over another draft voucher's Serial and Batch Bundle (2fda0bb04c)
        var bundle = new SerialAndBatchBundle(
            Guid.NewGuid(), companyId, itemId, whId,
            BundleTransactionType.Outward, "DeliveryNote", draftDn1Id, DateTime.UtcNow.Date);

        var draftEx = Should.Throw<BusinessException>(() =>
            SerialBatchBundleValidationService.ValidateBundleNotLinkedToAnotherDraft(
                bundle, "DeliveryNote", draftDn2Id, rowNumber: 1, isVoucherDraft: (_, _) => true));
        draftEx.Data["detail"]!.ToString()!.ShouldContain("belongs to DeliveryNote");

        // Same draft voucher or already-submitted/cancelled voucher passes
        Should.NotThrow(() =>
            SerialBatchBundleValidationService.ValidateBundleNotLinkedToAnotherDraft(
                bundle, "DeliveryNote", draftDn1Id, rowNumber: 1, isVoucherDraft: (_, _) => true));

        // 2. Picking fractional batches keeps unrounded remaining qty after each batch (dcaf1a81c0)
        var b1 = Guid.NewGuid();
        var b2 = Guid.NewGuid();
        var pickedBatches = SerialBatchBundleValidationService.PickQtyBasedAvailableBatches(
            new[]
            {
                (b1, "B-1", 0.16666m),
                (b2, "B-2", 0.50000m)
            },
            requiredQty: 0.3333m,
            precision: 4);
        pickedBatches.Count.ShouldBe(2);
        pickedBatches.Sum(x => x.Qty).ShouldBe(0.3333m);

        // 3. Split bundle return fills delivered rows row-by-row up to what is left on each and updates tracker across multiple return rows (ff83c45efe, bb77453ba8, bc6c911436)
        var delRowA = Guid.NewGuid();
        var delRowB = Guid.NewGuid();
        var componentDelivered = new[]
        {
            (ComponentIndex: 0, DeliveredPackedRowId: delRowA, DeliveredQty: 2m, QtyPerBundleUnit: 0.5m),
            (ComponentIndex: 0, DeliveredPackedRowId: delRowB, DeliveredQty: 2m, QtyPerBundleUnit: 0.5m)
        };
        var returnedSoFar = new Dictionary<Guid, decimal>();

        // First return row of -1 bundle (needs 1 unit total -> fills -1 from delRowA and 0 from delRowB, not -0.5 and -0.5!)
        var firstAlloc = SerialBatchBundleValidationService.AllocateSplitBundleReturnPackedQuantities(
            returnBundleRowStockQty: -1m,
            componentDeliveredRows: componentDelivered,
            returnedPackedQtyByDeliveredRowId: returnedSoFar);
        firstAlloc[delRowA].ShouldBe(-1m);
        firstAlloc[delRowB].ShouldBe(0m);

        // Second return row of -2 bundles in the same return doc (needs 2 units -> takes remaining -1 from delRowA and -1 from delRowB!)
        var secondAlloc = SerialBatchBundleValidationService.AllocateSplitBundleReturnPackedQuantities(
            returnBundleRowStockQty: -2m,
            componentDeliveredRows: componentDelivered,
            returnedPackedQtyByDeliveredRowId: returnedSoFar);
        secondAlloc[delRowA].ShouldBe(-1m);
        secondAlloc[delRowB].ShouldBe(-1m);

        // 4. Returned packed item valuation rate takes qty-weighted delivered rate and preserves explicit 0 delivered rate (8bcb291efd, bd09247690)
        var zeroRateMap = new Dictionary<Guid, decimal> { [delRowA] = 0m };
        var zeroRate = SerialBatchBundleValidationService.ResolveReturnedPackedItemRate(
            new[] { (delRowA, -1m) }, zeroRateMap);
        zeroRate.ShouldNotBeNull();
        zeroRate.Value.ShouldBe(0m);

        var weightedRateMap = new Dictionary<Guid, decimal> { [delRowA] = 100m, [delRowB] = 200m };
        var weightedRate = SerialBatchBundleValidationService.ResolveReturnedPackedItemRate(
            new[] { (delRowA, -1m), (delRowB, -3m) }, weightedRateMap);
        weightedRate.ShouldBe(175m);
    }

    [Fact]
    public void SubcontractingReturnOverConsumedGuard_LegacySecondaryQtyRank_TransferCancelRebuild_And_InternalReceiptReturnTransitCap_b354d3f1c3_2da4575f47_4f03188e04_2b23d4dff8_3914480936_bebce66db8_42c7a9d78b_33410f737d()
    {
        var fgReceiptRowId = Guid.NewGuid();
        var rmItemId = Guid.NewGuid();

        // 1. ValidateReturnedRawMaterialsDoNotExceedConsumed throws when cumulative returned RM exceeds what the receipt consumed (b354d3f1c3)
        var returnableMap = new Dictionary<(Guid, Guid), (decimal, decimal, string?)>
        {
            [(fgReceiptRowId, rmItemId)] = (10m, 7m, "RM-STEEL")
        };
        var overReturnEx = Should.Throw<BusinessException>(() =>
            MyERP.Purchasing.DomainServices.SubcontractingManager.ValidateReturnedRawMaterialsDoNotExceedConsumed(
                "SCR-0001",
                returnableMap,
                new[] { ((Guid?)fgReceiptRowId, rmItemId, -4m) }));
        overReturnEx.Data["detail"]!.ToString()!.ShouldContain("Returns of SCR-0001 would take back more of raw material RM-STEEL than it consumed");

        // 2. ShouldSkipBomRequiredQtyValidation always returns true on Subcontracting Return (2da4575f47)
        MyERP.Purchasing.DomainServices.SubcontractingManager.ShouldSkipBomRequiredQtyValidation(
            isReturn: true, backflushBasedOn: "BOM", validateConsumedQty: true).ShouldBeTrue();

        // 3. PairReturnSecondaryRowsWithOriginalReceiptRows pairs legacy pre-v16 rows by ascending qty rank (4f03188e04, 0263dc3b72, 2f92fcf85f)
        var secItemId = Guid.NewGuid();
        var origSecSmall = Guid.NewGuid();
        var origSecLarge = Guid.NewGuid();
        var bomSecSmall = Guid.NewGuid();
        var bomSecLarge = Guid.NewGuid();

        var pairedSecondary = MyERP.Purchasing.DomainServices.SubcontractingManager.PairReturnSecondaryRowsWithOriginalReceiptRows(
            originalSecondaryRows: new[]
            {
                (OriginalSecondaryRowId: origSecLarge, FgReceiptItemId: fgReceiptRowId, ItemId: secItemId, BomSecondaryItemId: (Guid?)null, Qty: 6m),
                (OriginalSecondaryRowId: origSecSmall, FgReceiptItemId: fgReceiptRowId, ItemId: secItemId, BomSecondaryItemId: (Guid?)null, Qty: 2m)
            },
            bomSecondaryRowsByFgReceiptItemId: new Dictionary<Guid, IReadOnlyList<(Guid, Guid, decimal)>>
            {
                [fgReceiptRowId] = new[]
                {
                    (bomSecLarge, secItemId, 3m),
                    (bomSecSmall, secItemId, 1m)
                }
            });
        pairedSecondary[(fgReceiptRowId, bomSecSmall)].ShouldBe(origSecSmall);
        pairedSecondary[(fgReceiptRowId, bomSecLarge)].ShouldBe(origSecLarge);

        // 4. RebuildSubcontractingTransferReservationsOnCancel cancels supplier-warehouse SREs and replays only remaining active transfers (2b23d4dff8, 3914480936)
        var cancelledSeId = Guid.NewGuid();
        var suppliedRowId = Guid.NewGuid();
        var batch1 = Guid.NewGuid();
        var batch2 = Guid.NewGuid();
        var orderSreBatch1 = Guid.NewGuid();
        var orderSreBatch2 = Guid.NewGuid();
        var supplierWhTransferSre = Guid.NewGuid();

        var rebuildResult = MyERP.Purchasing.DomainServices.SubcontractingManager.RebuildSubcontractingTransferReservationsOnCancel(
            cancelledStockEntryId: cancelledSeId,
            allOrderSres: new[]
            {
                (orderSreBatch1, suppliedRowId, 5m, true, (string?)null, (Guid?)batch1, (string?)null, (Guid?)null),
                (orderSreBatch2, suppliedRowId, 5m, true, (string?)null, (Guid?)batch2, (string?)null, (Guid?)null),
                (supplierWhTransferSre, suppliedRowId, 5m, true, (string?)null, (Guid?)batch2, (string?)"StockEntry", (Guid?)cancelledSeId)
            },
            remainingActiveTransfers: new[]
            {
                (suppliedRowId, 5m, (IReadOnlyCollection<string>?)null, (IReadOnlyDictionary<Guid, decimal>?)new Dictionary<Guid, decimal> { [batch1] = 5m })
            });
        rebuildResult.TransferReservationsToCancel.ShouldBe(new[] { supplierWhTransferSre });
        rebuildResult.RebuiltTransferredQtyBySreId[orderSreBatch1].ShouldBe(5m);
        rebuildResult.RebuiltTransferredQtyBySreId[orderSreBatch2].ShouldBe(0m); // Only batch2's reservation reopens!

        // 5. Internal receipt return caps transit qty to outward SLE minus other returns' inward SLEs, and skips rejected package when rejected stock qty rounds to 0 (bebce66db8, 42c7a9d78b, 33410f737d)
        var cappedReturnTransit = MyERP.Purchasing.DomainServices.PurchaseReceiptManager.ResolveInternalReceiptReturnSourceWarehouseStockQty(
            returnStockQty: -5.0001m,
            isInternalReceipt: true,
            isReturn: true,
            originalPurchaseReceiptItemId: Guid.NewGuid(),
            hasSerialOrBatchPackage: false,
            originalReceiptOutwardSleActualQtyInTransit: -10.0000m,
            otherReturnsInwardSleActualQtyInTransit: 5.0001m);
        cappedReturnTransit.ShouldBe(-4.9999m);

        var zeroRejectedPkg = MyERP.Purchasing.DomainServices.PurchaseReceiptManager.EvaluateInternalReceiptPackageStockQty(
            qtyInTransactionUom: 0.00001m, conversionFactor: 1m, stockQtyPrecision: 4);
        zeroRejectedPkg.ShouldCreatePackage.ShouldBeFalse();
        zeroRejectedPkg.RoundedStockQty.ShouldBe(0m);
    }

    [Fact]
    public void InternalTransferTransitValuationAndGl_ReturnedActualValuationCharges_And_PurchaseStatusAggregation_86229973c0_cceafa6af4_5c38112008_1d5eb2a43d_d0942983e5_e661476a74_42fb302bca_d8a07e8d66_ae1ba15068_235499af25_ff4f660d34_0e77c8d15e_393217f004_127f4070e0()
    {
        // 1. CalculateInternalTransferTransitAmount values internal transfer at rounded stock qty (86229973c0, cceafa6af4)
        var transitAmt = MyERP.Purchasing.DomainServices.PurchaseReceiptManager.CalculateInternalTransferTransitAmount(
            qtyInTransactionUom: 0.3333m, conversionFactor: 3m, salesIncomingRate: 300m, stockQtyPrecision: 4);
        // roundedStockQty = Round(0.9999, 4) = 0.9999 -> 0.9999 * 300 / 3 = 99.99
        transitAmt.ShouldBe(99.99m);

        // 2. ResolveInternalTransferInvoiceTransitAndWarehouseGlAmounts includes LCV, preserves 0 transit SLE, and falls back when rejected stays in transit (5c38112008, 1d5eb2a43d, 0c17bbd80c, 42fb302bca, e661476a74, d0942983e5)
        var transitWh = Guid.NewGuid();
        var rejectedWh = Guid.NewGuid();
        var glWithLcv = MyERP.Purchasing.DomainServices.PurchaseReceiptManager.ResolveInternalTransferInvoiceTransitAndWarehouseGlAmounts(
            isInternalTransfer: true,
            fromWarehouseId: transitWh,
            rejectedWarehouseId: rejectedWh,
            transitWarehouseSleStockValue: -100.00m,
            rejectedWarehouseSleStockValue: 20.00m,
            landedCostVoucherAmount: 15.00m,
            fallbackWarehouseDebitAmount: 95.00m);
        glWithLcv.TransitStockValue.ShouldBe(100.00m);
        glWithLcv.WarehouseDebitAmount.ShouldBe(95.00m); // 100 - 20 + 15 = 95

        // Explicit 0 transit SLE is preserved (42fb302bca)
        var glZeroTransit = MyERP.Purchasing.DomainServices.PurchaseReceiptManager.ResolveInternalTransferInvoiceTransitAndWarehouseGlAmounts(
            isInternalTransfer: true,
            fromWarehouseId: transitWh,
            rejectedWarehouseId: rejectedWh,
            transitWarehouseSleStockValue: 0m,
            rejectedWarehouseSleStockValue: 0m,
            landedCostVoucherAmount: 0m,
            fallbackWarehouseDebitAmount: 50.00m);
        glZeroTransit.TransitStockValue.ShouldBe(0m);
        glZeroTransit.WarehouseDebitAmount.ShouldBe(0m);

        // Rejected material stays in transit (rejectedWarehouse == fromWarehouse) -> returns null TransitStockValue and uses fallback (e661476a74)
        var glRejectedInTransit = MyERP.Purchasing.DomainServices.PurchaseReceiptManager.ResolveInternalTransferInvoiceTransitAndWarehouseGlAmounts(
            isInternalTransfer: true,
            fromWarehouseId: transitWh,
            rejectedWarehouseId: transitWh,
            transitWarehouseSleStockValue: -80.00m,
            rejectedWarehouseSleStockValue: 20.00m,
            landedCostVoucherAmount: 0m,
            fallbackWarehouseDebitAmount: 80.00m);
        glRejectedInTransit.TransitStockValue.ShouldBeNull();
        glRejectedInTransit.WarehouseDebitAmount.ShouldBe(80.00m);

        // 3. Returned Actual valuation charges match by (AccountId, Description), spread BaseTaxAmount, and convert back by return fx rate (d8a07e8d66, ae1ba15068, 235499af25, ff4f660d34)
        var chargeAccId = Guid.NewGuid();
        var origRowId = Guid.NewGuid();
        var retCharge1Id = Guid.NewGuid();
        var retCharge2Id = Guid.NewGuid();

        var returnedCharges = MyERP.Purchasing.DomainServices.PurchaseReceiptManager.CalculateReturnedActualValuationCharges(
            returnCharges: new[]
            {
                (retCharge1Id, chargeAccId, (string?)"Handling", -100m),
                (retCharge2Id, chargeAccId, (string?)"Freight", -200m)
            },
            originalCharges: new[]
            {
                (chargeAccId, (string?)"Freight", 900m, true),
                (chargeAccId, (string?)"Handling", 450m, true)
            },
            originalItems: new[]
            {
                (origRowId, true, 10m, 0m, 1m)
            },
            returnItems: new[]
            {
                ((Guid?)origRowId, -4m, 0m, false)
            },
            billsRejectedQuantity: false,
            returnConversionRate: 4.5m,
            taxAmountPrecision: 2);
        // 4 of 10 returned (40%):
        // Handling: 40% of 450 base = 180 base / 4.5 fx = -40.00
        // Freight: 40% of 900 base = 360 base / 4.5 fx = -80.00
        returnedCharges.Count.ShouldBe(2);
        returnedCharges[0].ShouldBe((retCharge1Id, -40.00m));
        returnedCharges[1].ShouldBe((retCharge2Id, -80.00m));

        // Unlinked return row keeps charges untouched (ff4f660d34)
        var unlinkedCharges = MyERP.Purchasing.DomainServices.PurchaseReceiptManager.CalculateReturnedActualValuationCharges(
            returnCharges: new[] { (retCharge1Id, chargeAccId, (string?)"Handling", -25m) },
            originalCharges: new[] { (chargeAccId, (string?)"Handling", 450m, true) },
            originalItems: new[] { (origRowId, true, 10m, 0m, 1m) },
            returnItems: new[] { ((Guid?)null, -4m, 0m, false) });
        unlinkedCharges[0].ShouldBe((retCharge1Id, -25m));

        // 4. Remaining qty to order in row UOM and unrounded status updater aggregation (393217f004, c4c278ceb9, 0e77c8d15e, 127f4070e0, 04b2ca0ed1)
        var remainingInBoxUom = MyERP.Purchasing.DomainServices.PurchaseReceiptManager.CalculateRemainingQtyToOrderInRowUom(
            sourceRowQty: 1m,
            sourceRowConversionFactor: 3m,
            submittedPurchaseOrderRows: new[] { (OrderedQty: 1m, OrderedConversionFactor: 1m), (OrderedQty: 1m, OrderedConversionFactor: 1m) },
            qtyPrecision: 4);
        remainingInBoxUom.ShouldBe(0.3333m);

        var aggStockQty = MyERP.Purchasing.DomainServices.PurchaseReceiptManager.AggregateSourceOrderedOrReceivedStockQty(
            new[]
            {
                (Qty: 0.3333m, ConversionFactor: 3m, RoundedStockQty: 1.0000m),
                (Qty: 0.3333m, ConversionFactor: 3m, RoundedStockQty: 1.0000m),
                (Qty: 0.3334m, ConversionFactor: 3m, RoundedStockQty: 1.0000m)
            },
            sumUnroundedProduct: true,
            roundTotalToTargetPrecision: true,
            targetPrecision: 4);
        aggStockQty.ShouldBe(3.0000m);
    }

    [Fact]
    public void SubcontractingReturnSecondaryReceiptRate_TransferSreSharedBatchAndWarehouseFilter_ReservationBundleLastEntryRounding_And_AutoPickBatchFilter_49411efce2_2e0c35f11b_ef3b45ff13_cd897009f7_189b35ae0a_b99774a83d_6d0dad665d_f681eaca6a_61879f601b_b54c2ca3eb_a9b6c77284()
    {
        // 1. Subcontracting Return secondary items (ValuationRate, Manual, and PercentageOfComponentCost) use original receipt rate (49411efce2, 2e0c35f11b, ef3b45ff13)
        MyERP.Purchasing.DomainServices.SubcontractingManager.ResolveSecondaryItemRate(
            isReturn: true,
            secondaryQty: -2m,
            valuationType: SecondaryItemValuationType.PercentageOfComponentCost,
            bomSecondaryBaseCost: 100m,
            bomSecondaryStockQty: 2m,
            resolveWarehouseValuationRate: () => 999m,
            originalReceiptSecondaryRate: 37.5m).ShouldBe(37.5m);

        // 2. Subcontracting transfer excludes supplier warehouse SREs and shares batch qty across row SREs (cd897009f7, 189b35ae0a, b99774a83d)
        var supplierWhId = Guid.NewGuid();
        var sourceWh1 = Guid.NewGuid();
        var sourceWh2 = Guid.NewGuid();
        var sharedBatchId = Guid.NewGuid();
        var sreSupplier = Guid.NewGuid();
        var sreOrder1 = Guid.NewGuid();
        var sreOrder2 = Guid.NewGuid();

        var updatedSres = MyERP.Purchasing.DomainServices.SubcontractingManager.ApplySubcontractingTransferToOrderReservations(
            targetSupplierWarehouseId: supplierWhId,
            transferStockQty: 8m,
            transferredSerialNos: null,
            transferredBatchQuantities: new Dictionary<Guid, decimal> { [sharedBatchId] = 8m },
            candidateSres: new[]
            {
                (sreSupplier, supplierWhId, 5m, 0m, true, (string?)null, (Guid?)sharedBatchId),
                (sreOrder1, sourceWh1, 5m, 0m, true, (string?)null, (Guid?)sharedBatchId),
                (sreOrder2, sourceWh2, 5m, 0m, true, (string?)null, (Guid?)sharedBatchId)
            });
        updatedSres.ShouldNotContainKey(sreSupplier); // Supplier warehouse SRE excluded!
        updatedSres[sreOrder1].ShouldBe(5m);
        updatedSres[sreOrder2].ShouldBe(3m); // Shared batch has 8 - 5 = 3 left for second SRE!

        // 3. RoundReservationBundleEntriesToLedgerTotal rounds only on the last entry (6d0dad665d) & CalculateQtyToUndeliverOnCancel clears rounding residue (f681eaca6a)
        var e1 = Guid.NewGuid();
        var e2 = Guid.NewGuid();
        var roundedEntries = StockReservationManager.RoundReservationBundleEntriesToLedgerTotal(
            new[] { (e1, 0.16666m), (e2, 0.16666m) }, precision: 4);
        roundedEntries[0].Qty.ShouldBe(0.16666m);
        roundedEntries[1].Qty.ShouldBe(0.3333m - 0.16666m);
        (roundedEntries[0].Qty + roundedEntries[1].Qty).ShouldBe(0.3333m);

        var undelivered = StockReservationManager.CalculateQtyToUndeliverOnCancel(
            currentDeliveredQty: 0.333333m, cancelledQty: 0.3333m, precision: 4);
        undelivered.ShouldBe(0.333333m); // Leaves 0 delivered residue after cancel!

        // 4. FilterAvailableBatchesTakenByDocumentRows subtracts direct rows and SerialAndBatchBundle rows at rounded stock qty (61879f601b, b54c2ca3eb)
        var targetWh = Guid.NewGuid();
        var batchA = Guid.NewGuid();
        var batchB = Guid.NewGuid();
        var availableBatches = new Dictionary<Guid, decimal>
        {
            [batchA] = 10m,
            [batchB] = 6m
        };
        var remainingBatches = SerialBatchBundleValidationService.FilterAvailableBatchesTakenByDocumentRows(
            availableBatches,
            targetWh,
            new[]
            {
                (WarehouseId: (Guid?)targetWh, BatchId: (Guid?)batchA, StockQty: 4.12344m, BundleBatchEntries: (IReadOnlyList<(Guid, decimal)>?)null),
                (WarehouseId: (Guid?)null, BatchId: (Guid?)null, StockQty: 6m, BundleBatchEntries: (IReadOnlyList<(Guid, decimal)>?)new[] { (batchB, -6m) })
            },
            precision: 4);
        remainingBatches[batchA].ShouldBe(5.8766m);
        remainingBatches.ShouldNotContainKey(batchB); // Used up through SerialAndBatchBundle!

        // 5. ResizeTransferBundleEntriesToTotalStockQty puts difference on last entry of non-serialized transfer package (a9b6c77284, 764214128a)
        var resizedEntries = SerialBatchBundleValidationService.ResizeTransferBundleEntriesToTotalStockQty(
            new[] { (e1, -6m), (e2, -4m) },
            totalPostedStockQty: -9.5m,
            hasSerialNo: false);
        resizedEntries[0].Qty.ShouldBe(6m);
        resizedEntries[1].Qty.ShouldBe(3.5m);
    }

    [Fact]
    public void PaymentReallocationDeductions_EarlyDiscountBankCurrency_BankRuleBoundedRegex_InvoiceReceiptMappingNetOfDebitNotes_ProjectCostingAndSalesSplit_And_TrustedPosConsolidation_b4d2d8651d_bb60f4696b_5c839ca732_d82d54cf42_e460e60a30_3e5f527cfd_4dd309aec7_ac3371a279_e1ed14c303_fdf421605d_31689c27ba_b81f5e5630_22fdf879af_79ddc67a45_bd5cac782d()
    {
        // 1. Payment Entry reallocation deductions in company currency converted to party currency (b4d2d8651d)
        var reallocReceive = MyERP.Accounting.DomainServices.PaymentEntryManager.CalculateReallocationPaidAmountWithDeductions(
            paidAmountInPartyCurrency: 90m,
            paymentType: PaymentType.Receive,
            partyExchangeRate: 4.5m,
            deductions: new[] { (Amount: 45m, IsExchangeGainLoss: false), (Amount: 10m, IsExchangeGainLoss: true) },
            precision: 2);
        // 90 + (45 / 4.5) * 1 = 100.00 (exchange gain/loss ignored)
        reallocReceive.ShouldBe(100.00m);

        var reallocPay = MyERP.Accounting.DomainServices.PaymentEntryManager.CalculateReallocationPaidAmountWithDeductions(
            paidAmountInPartyCurrency: 110m,
            paymentType: PaymentType.Pay,
            partyExchangeRate: 2m,
            deductions: new[] { (Amount: 20m, IsExchangeGainLoss: false) },
            precision: 2);
        // 110 + (20 / 2) * (-1) = 100.00
        reallocPay.ShouldBe(100.00m);

        // 2. Early payment discount in bank currency (bb60f4696b)
        // USD bank + USD party (same currency despite multi-currency doc) -> conversion_rate = 1 on bank side
        var sameBankCurrDiscount = MyERP.Accounting.DomainServices.PaymentEntryManager.ApplyEarlyPaymentDiscount(
            paidAmount: 1000m,
            receivedAmount: 1000m,
            paymentType: PaymentType.Receive,
            partyAccountCurrency: "USD",
            bankAccountCurrency: "USD",
            isMultiCurrency: true,
            documentConversionRate: 4.45m,
            eligibleTermDiscounts: new[] { ("Percentage", 10m, 1000m) });
        sameBankCurrDiscount.PaidAmount.ShouldBe(900m);
        sameBankCurrDiscount.ReceivedAmount.ShouldBe(900m);
        sameBankCurrDiscount.TotalDiscountAmount.ShouldBe(100m);

        // MYR bank + USD party -> bank side discount multiplied by conversion_rate (4.45)
        var crossBankCurrDiscount = MyERP.Accounting.DomainServices.PaymentEntryManager.ApplyEarlyPaymentDiscount(
            paidAmount: 1000m,
            receivedAmount: 4450m,
            paymentType: PaymentType.Receive,
            partyAccountCurrency: "USD",
            bankAccountCurrency: "MYR",
            isMultiCurrency: true,
            documentConversionRate: 4.45m,
            eligibleTermDiscounts: new[] { ("Percentage", 10m, 1000m) });
        crossBankCurrDiscount.PaidAmount.ShouldBe(900m);
        crossBankCurrDiscount.ReceivedAmount.ShouldBe(4005m);

        // 3. BankTransactionRule bounded regex matching with timeout and case-insensitivity (5c839ca732)
        var bankRule = new MyERP.Accounting.Entities.BankTransactionRule(Guid.NewGuid(), Guid.NewGuid(), "Regex Rule", 1);
        bankRule.AddCondition(MyERP.Accounting.BankRuleMatchType.Regex, @"inv-\d+");
        bankRule.Conditions.First().Matches("Payment for INV-2026").ShouldBeTrue();
        bankRule.Conditions.First().Matches("No match here").ShouldBeFalse();

        // 4. Purchase Invoice -> Purchase Receipt mapping net of debit notes in another UOM & Billed Items To Be Received report (d82d54cf42, e460e60a30, 3e5f527cfd, 4dd309aec7, ac3371a279, f1504e8c71)
        // Invoice row: 2 Boxes (conversion_factor = 3 -> 6 stock units), rate = 30, fx = 4.0
        // Already received: 0.5 Box. Two debit notes of -1 Unit each (conversion_factor = 1) -> 2 stock units = 2/3 Box returned.
        // Remaining = Round(2 - 0.5 - 2/3, 4) = Round(0.833333..., 4) = 0.8333 Box
        var mappedPrRow = MyERP.Purchasing.DomainServices.PurchaseInvoiceManager.CalculateMappedPurchaseReceiptItemFromInvoice(
            invoiceRowQty: 2m,
            invoiceRowConversionFactor: 3m,
            invoiceRowRate: 30m,
            invoiceConversionRate: 4m,
            receivedAndMappedQtyInInvoiceUom: 0.5m,
            submittedDebitNoteRows: new[] { (-1m, 1m), (-1m, 1m) },
            qtyPrecision: 4);
        mappedPrRow.Qty.ShouldBe(0.8333m);
        mappedPrRow.ReceivedQty.ShouldBe(0.8333m);
        mappedPrRow.StockQty.ShouldBe(0.8333m * 3m);
        mappedPrRow.Amount.ShouldBe(0.8333m * 30m);
        mappedPrRow.BaseAmount.ShouldBe(0.8333m * 30m * 4m);

        var reportReturned = MyERP.Purchasing.DomainServices.PurchaseInvoiceManager.CalculateBilledItemsToBeReceivedDebitNoteQuantities(
            originalInvoiceRowConversionFactor: 12m,
            submittedDebitNoteRows: new[]
            {
                (UpdateStock: true, Qty: -6m, ReceivedQty: -6m, ConversionFactor: 1m),
                (UpdateStock: false, Qty: -3m, ReceivedQty: 0m, ConversionFactor: 1m)
            });
        reportReturned.ReturnedQtyInInvoiceUom.ShouldBe(0.75m); // 9 / 12
        reportReturned.StockReturnedQtyInInvoiceUom.ShouldBe(0.5m); // 6 / 12
        reportReturned.BillingOnlyQtyInInvoiceUom.ShouldBe(0.25m); // 3 / 12

        MyERP.Purchasing.DomainServices.PurchaseInvoiceManager.ShouldEnableApplyTdsFromSupplierDefault(
            forValidate: false,
            supplierTaxWithholdingCategoryId: Guid.NewGuid(),
            supplierTaxWithholdingGroupId: null).ShouldBeTrue();

        // 5. Project Costing & Sales Order row project split (e1ed14c303, 4b368ed5d4, fdf421605d, e9483ccbc0, 31689c27ba, b2bf715e2d)
        var headerProj = Guid.NewGuid();
        var rowProj = Guid.NewGuid();
        var prevProj = Guid.NewGuid();
        MyERP.Projects.Entities.Project.ResolveEffectiveRowProjectId(null, headerProj).ShouldBe(headerProj);
        MyERP.Projects.Entities.Project.ResolveEffectiveRowProjectId(Guid.Empty, headerProj).ShouldBe(headerProj);
        MyERP.Projects.Entities.Project.ResolveEffectiveRowProjectId(rowProj, headerProj).ShouldBe(rowProj);

        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-PROJ-01", DateTime.UtcNow)
        {
            ProjectId = headerProj,
            ExchangeRate = 2m
        };
        so.AddItem(Guid.NewGuid(), "Header Project Item", 2m, 100m, 0m);
        so.AddItem(Guid.NewGuid(), "Row Project Item", 3m, 50m, 0m);
        so.Items[1].ProjectId = rowProj;

        so.GetReferencedProjectIds(new[] { prevProj }).ShouldBe(new HashSet<Guid> { headerProj, rowProj, prevProj });
        so.CalculateProjectSalesAmount(headerProj).ShouldBe(400m); // 2 * 100 * 2
        so.CalculateProjectSalesAmount(rowProj).ShouldBe(300m); // 3 * 50 * 2

        // Landed cost claim comes out of valuation rows first (31689c27ba, b2bf715e2d)
        // Invoice base_net_total = 1000: 400 in valuation account, 600 in non-stock expense row.
        // LCV claimed 550 -> 400 absorbed by valuation row, remaining 150 reduces 600 expense row to 450 (75% of 600).
        var expensedAfterClaim = MyERP.Projects.Entities.Project.CalculateNonStockPurchaseRowExpensedAmount(
            rowBaseNetAmount: 600m,
            expenseAccountType: "Expense Account",
            isStockItem: false,
            deliveredBySupplier: false,
            isSubcontracted: false,
            invoiceBaseNetTotal: 1000m,
            claimedLandedCostAmount: 550m,
            valuationRowsBaseNetTotal: 400m);
        expensedAfterClaim.ShouldBe(450m);

        // 6. Trusted POS consolidation flag, commission rate bounds, tax row description, and newly added row margin (b81f5e5630, 2a8c73991c, bd5cac782d, 22fdf879af, 79ddc67a45)
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-POS-01", DateTime.UtcNow)
        {
            IsConsolidated = true
        };
        si.EnforceTrustedPosConsolidationFlag(isTrustedPosMergeConsolidation: false);
        si.IsConsolidated.ShouldBeFalse();

        var negCommEx = Should.Throw<BusinessException>(() => si.SetAndValidateCommissionRate(-5m));
        negCommEx.Data["detail"]!.ToString()!.ShouldContain("Commission Rate must be between 0 and 100");
        si.CommissionRate.ShouldBe(0m);

        var taxAccId = Guid.NewGuid();
        var missingTaxRow = MyERP.Tax.DomainServices.TaxesAndTotalsService.CreateMissingTaxRowFromItemTaxTemplate(
            taxAccId, "SST 6%", 1);
        missingTaxRow.Description.ShouldBe("SST 6%");

        var newlyAddedRate = MyERP.Tax.DomainServices.TaxesAndTotalsService.CalculateItemRateWithMargin(
            priceListRate: 125m,
            marginType: "Percentage",
            marginRateOrAmount: null,
            discountPercentage: 10m);
        newlyAddedRate.RateWithMargin.ShouldBe(125m);
        newlyAddedRate.EffectiveRate.ShouldBe(112.5m);
    }

    [Fact]
    public void PurchaseReceiptRejectedReturnGlAndInvoiceFxReversal_LifoReturnRateMatching_MovingAverageOutflowFloor_RepostEarlyStop_And_ReservationPickListGuards_39a75b170e_097411fb47_3837f7b46a_1d8fad1d6b_81f685b0f8_93f8c80982_433707f5f4_492e2e0e4a_33a523c99d_806c01be1c_cd04caf3d6_e7ee6255f0_9533f3f19d_ca335b6613_0bf6a5cdca_bbc5b1285f()
    {
        // 1. Rejected warehouse return debits Stock Received But Not Billed at the receipt's net rate when the receipt had accepted qty (39a75b170e, 097411fb47, 3837f7b46a)
        var origPrItemId = Guid.NewGuid();
        var rejReturnWithAccepted = MyERP.Purchasing.DomainServices.PurchaseReceiptManager.ResolveRejectedWarehouseReturnCreditAmount(
            isReturn: true,
            returnQtyFromRejectedWarehouse: true,
            isInternalTransfer: false,
            originalPurchaseReceiptItemId: origPrItemId,
            originalReceiptAcceptedQty: 10m,
            returnStockQty: -2m,
            returnBaseNetRate: 50m,
            returnConversionFactor: 1m,
            sleStockValueDifference: -130m); // Current warehouse value or valuation charges excluded!
        rejReturnWithAccepted.ShouldBe(-100m);

        // When original receipt row had 0 accepted qty (rejected-only receipt), valuation charges sat on rejected stock -> use SLE value
        var rejReturnRejectedOnly = MyERP.Purchasing.DomainServices.PurchaseReceiptManager.ResolveRejectedWarehouseReturnCreditAmount(
            isReturn: true,
            returnQtyFromRejectedWarehouse: true,
            isInternalTransfer: false,
            originalPurchaseReceiptItemId: origPrItemId,
            originalReceiptAcceptedQty: 0m,
            returnStockQty: -2m,
            returnBaseNetRate: 50m,
            returnConversionFactor: 1m,
            sleStockValueDifference: -130m);
        rejReturnRejectedOnly.ShouldBe(-130m);

        // 2. Return of a receipt made from an invoice reverses exchange difference even when return is in another UOM (1d8fad1d6b, 81f685b0f8)
        var fxReturnDiff = MyERP.Purchasing.DomainServices.PurchaseReceiptManager.CalculatePurchaseReceiptInvoiceExchangeDiscrepancy(
            isReturn: true,
            receiptRowQty: -1m, // 1 Box of 12
            receiptRowNetRate: 120m, // 120 per Box
            receiptConversionRate: 4.5m,
            originalReceiptRowNetRate: 10m, // Original receipt was 10 per Unit (matched invoice)
            invoiceRowNetRate: 10m,
            invoiceConversionRate: 4.2m);
        fxReturnDiff.IsAtInvoiceRate.ShouldBeTrue();
        fxReturnDiff.ExchangeRateDiscrepancy.ShouldBe(-36.00m); // (-1 * 120) * (4.5 - 4.2) = -36.00

        // 3. Internal transfer return incoming rate & Last purchase details across PO/PR/PI (93f8c80982, c08a053ae3, 9d7f0c4807, 63717f769f)
        MyERP.Purchasing.DomainServices.PurchaseReceiptManager.ResolveInternalTransferIncomingRate(
            outwardDeliverySleStockValueDifference: -450m,
            outwardDeliverySleActualQty: -10m,
            landedCostVoucherAmount: 25m,
            currentSleActualQty: -5m).ShouldBe(50m); // 45 + 25/5 = 50

        var lastPurchase = MyERP.Purchasing.DomainServices.PurchaseReceiptManager.ResolveLastPurchaseDetails(
            new[]
            {
                (VoucherType: "Purchase Order", PurchaseDate: new DateTime(2026, 11, 10), BaseNetRate: 100m, ConversionFactor: 1m, IsFreeItem: false, IsInternalSupplier: false),
                (VoucherType: "Purchase Invoice", PurchaseDate: new DateTime(2026, 11, 15), BaseNetRate: 240m, ConversionFactor: 2m, IsFreeItem: false, IsInternalSupplier: false),
                (VoucherType: "Purchase Receipt", PurchaseDate: new DateTime(2026, 11, 20), BaseNetRate: 0m, ConversionFactor: 1m, IsFreeItem: true, IsInternalSupplier: false),
                (VoucherType: "Purchase Order", PurchaseDate: new DateTime(2026, 11, 20), BaseNetRate: 80m, ConversionFactor: 1m, IsFreeItem: false, IsInternalSupplier: true)
            });
        lastPurchase.ShouldNotBeNull();
        lastPurchase!.Value.VoucherType.ShouldBe("Purchase Invoice");
        lastPurchase.Value.BaseNetRatePerStockUom.ShouldBe(120m);

        var s1 = Guid.NewGuid();
        var s2 = Guid.NewGuid();
        MyERP.Purchasing.DomainServices.PurchaseReceiptManager.ResolveFirstDefaultSupplierFromItems(
            new[] { ((Guid?)null, (Guid?)s1), ((Guid?)s2, (Guid?)null) }).ShouldBe(s1);

        // 4. LIFO purchase return matches bin rate at 9-decimal database precision & Moving Average outflow floor (433707f5f4, 492e2e0e4a)
        var lifo = new FifoValuation(isLifo: true);
        lifo.AddStock(5m, 33.3333333334m);
        lifo.AddStock(5m, 50m);
        var lifoConsumed = lifo.RemoveStock(2m, 33.3333333331m); // Rounds to 33.333333333 at 9 decimals -> matches first bin!
        FifoValuation.GetOutgoingRate(lifoConsumed).ShouldBe(33.3333333334m);

        ExpectedValuationReplayEngine.CalculateMovingAverageRateAfterSetRateOutflow(
            previousStockValue: 100m,
            previousQty: 10m,
            actualQtyChange: -4m,
            outgoingRate: 30m).ShouldBe(0m); // 100 - 120 clamped to 0 / 6 = 0 (never negative)

        // 5. Cancel before same-timestamp Adjustment Entry, StockReconciliation bundle zero-rate guard, and Serial/Batch repost zero rate (33a523c99d, 806c01be1c, 6c0d8e9a97, 89ad66af48, 995b851ba1)
        var ts = new DateTime(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc);
        ExpectedValuationReplayEngine.IsAdjustmentEntryLaterThanSle(
            adjustmentPostingDateTime: ts,
            adjustmentCreationTime: ts.AddSeconds(10),
            slePostingDateTime: ts,
            isCancellingSle: true,
            originalCancelledSleCreationTime: ts.AddSeconds(2)).ShouldBeTrue();

        StockReconciliationItem.ResolveBundleRowValuationRate(
            valuationRate: 0m,
            currentValuationRate: 45m,
            allowZeroValuationRate: false).ShouldBe(45m);
        StockReconciliationItem.ResolveBundleRowValuationRate(
            valuationRate: 0m,
            currentValuationRate: 45m,
            allowZeroValuationRate: true).ShouldBe(0m);

        SerialBatchBundleValidationService.ResolveRepostReturnedSerialOrBatchValuationRate(
            matchedValuationRate: 0m,
            throughRepostItemValuation: true,
            fallbackPoolRates: new[] { 50m, 70m }).ShouldBe(0m); // Explicit 0 preserved!

        SerialBatchBundleValidationService.IsBundleEntryPostedBeforeSle(
            bundleEntryPostingDateTime: ts,
            hasActiveBundleSle: true,
            bundleSleCreationTime: ts.AddSeconds(5),
            currentSlePostingDateTime: ts,
            currentSleCreationTime: ts.AddSeconds(2)).ShouldBeFalse(); // Created after current SLE at same timestamp!

        // 6. Repost early stop (skip_unchanged_entries) & coverage check (cd04caf3d6)
        RepostItemValuation.IsRepostSettledAtEntry(
            canSkipUnchangedEntries: true,
            entryPostingDateTime: ts.AddHours(2),
            walkStartDateTime: ts,
            voucherType: "Delivery Note",
            isAdjustmentEntry: false,
            hasBatchNo: false,
            hasSerialNo: false,
            skipSerialBatchValuation: false,
            hasInventoryDimensions: false,
            runningBalance: (10m, 500m, 50m, "[[10,50]]"),
            savedBalance: (10m, 500m, 50m, "[[10,50]]"),
            isLedgerShifted: false).ShouldBeTrue();

        RepostItemValuation.IsQueuedRepostCoveredTill(
            queuedSkipUnchangedEntries: false,
            queuedRepostDateTime: ts.AddHours(1),
            repostedTill: ts.AddHours(2)).ShouldBeFalse(); // Full repost not covered by early-stopped repost!

        // 7. Stock Reservation undelivered Pick List check, float residue guard, Pick List item validation, RFQ source MR validation, and Item UOM removal guard (e7ee6255f0, b053136fb2, fff90c71e6, 9533f3f19d, ca335b6613, 628480dcb1, e5ff908919, 0bf6a5cdca, bbc5b1285f)
        var soItem1 = Guid.NewGuid();
        var undeliveredPick = StockReservationManager.CalculateUndeliveredPickedQtyBySalesOrderItem(
            new[]
            {
                (SalesOrderItemId: soItem1, IsSubmitted: true, PickedQty: 5m, DeliveredQty: 6m), // Over-delivered row ignored!
                (SalesOrderItemId: soItem1, IsSubmitted: true, PickedQty: 4m, DeliveredQty: 4m)
            });
        undeliveredPick.GetValueOrDefault(soItem1, 0m).ShouldBe(0m);

        var unresEval = StockReservationManager.EvaluateSalesOrderRowUnreservedAndPartialReservation(
            rowStockQty: 10m,
            rowDeliveredQty: 3.333333m,
            rowConversionFactor: 3m,
            existingReservedQty: 0m,
            qtyToBeReserved: 0m,
            precision: 4);
        unresEval.UnreservedQty.ShouldBe(0m); // Float residue 0.000001 rounds to 0 at precision 4

        var sreStatus = StockReservationManager.EvaluateStockReservationEntryStatus(
            docStatus: MyERP.Core.DocumentStatus.Submitted,
            voucherQty: 10m,
            reservedQty: 10m,
            deliveredQty: 0.000001m, // Float residue!
            transferredQty: 0m,
            consumedQty: 0m,
            reservedQtyPrecision: 4);
        sreStatus.Status.ShouldBe("Reserved");
        sreStatus.StillHoldsStock.ShouldBeTrue();

        var pickRowId = Guid.NewGuid();
        var pickedItemId = Guid.NewGuid();
        var wrongItemId = Guid.NewGuid();
        var pickWhId = Guid.NewGuid();
        var pickMap = new Dictionary<Guid, (Guid, string, Guid, string, decimal, decimal)>
        {
            [pickRowId] = (pickedItemId, "ITEM-A", pickWhId, "Stores", 10m, 0m)
        };
        var pickItemEx = Should.Throw<BusinessException>(() =>
            PickListManager.ValidateRowsAgainstPickListItems(
                pickListNumber: "PL-0001",
                isStockEntry: false,
                workOrderId: null,
                stockEntryPurpose: null,
                pickedRowsById: pickMap,
                documentRows: new[] { (1, (Guid?)pickRowId, wrongItemId, "ITEM-B", (Guid?)pickedItemId, (Guid?)pickWhId, (string?)"Stores", 5m) }));
        pickItemEx.Data["detail"]!.ToString()!.ShouldContain("Item ITEM-B is not the item ITEM-A picked in PL-0001");

        // Work Order Manufacture Stock Entry honours original_item == picked_item (628480dcb1, 9f33bb012e)
        Should.NotThrow(() =>
            PickListManager.ValidateRowsAgainstPickListItems(
                pickListNumber: "PL-0001",
                isStockEntry: true,
                workOrderId: Guid.NewGuid(),
                stockEntryPurpose: "Manufacture",
                pickedRowsById: pickMap,
                documentRows: new[] { (1, (Guid?)pickRowId, wrongItemId, "ITEM-B", (Guid?)pickedItemId, (Guid?)pickWhId, (string?)"Stores", 10.00004m) }));

        // RFQ validates items against source Material Request (0bf6a5cdca)
        var rfqCompanyId = Guid.NewGuid();
        var mrItemId = Guid.NewGuid();
        var rfq = new MyERP.Purchasing.Entities.RequestForQuotation(Guid.NewGuid(), rfqCompanyId, "RFQ-001", DateTime.UtcNow);
        rfq.AddItem(wrongItemId, "Wrong Item", 5m, "Unit", materialRequestItemId: mrItemId);
        Should.Throw<BusinessException>(() =>
            rfq.ValidateWithSourceMaterialRequests(
                new Dictionary<Guid, (Guid, Guid, string)>
                {
                    [mrItemId] = (rfqCompanyId, pickedItemId, "Unit")
                }));

        // Block removing an Item UOM that an Item Price uses (bbc5b1285f)
        var uomEx = Should.Throw<BusinessException>(() =>
            MyERP.Inventory.Entities.Item.ValidateRemovedUomsNotInItemPrice(
                previousUoms: new[] { "Unit", "Box" },
                currentUoms: new[] { "Unit" },
                existingItemPrices: new[] { ("IP-0001", "Box") }));
        uomEx.Data["detail"]!.ToString()!.ShouldContain("UOM Box cannot be removed from the Item as Item Price IP-0001 uses it");
    }

    [Fact]
    public void SubcontractingAdditionalCostsRunningRounding_ServiceItemPairingAndCompanyCurrencyRmCost_PerRmTransferStatus_ProcessLossReturnAndOrphanSecondaryGuard_LcvFgFilter_And_InwardConversionBatchReturnSecondaryAggregation_4884d5e433_9d9cfcae0c_c3b83d3d20_cd45f385e5_adbe37cbad_922ad20647_e9d264a6a4_17c2629110_22222b39a8_31d2158574_42258d5660_76c448206a_a36fc2fd7a_9b5a83412c_3770398f2f_19b846a497_a7755dcb8d()
    {
        // 1. Subcontracting additional costs running-total rounding & zero-weight guard (4884d5e433, 9d9cfcae0c, c3b83d3d20)
        var fg1 = Guid.NewGuid();
        var fg2 = Guid.NewGuid();
        var fg3 = Guid.NewGuid();
        var sec = Guid.NewGuid();
        // 100.00 split across 3 equal FG rows: 33.33, 33.34, 33.33 -> sum = 100.00!
        var shares = MyERP.Purchasing.DomainServices.SubcontractingManager.CalculateAdditionalCostPerQtyForFinishedGoods(
            totalAdditionalCosts: 100m,
            distributeBasedOn: "Qty",
            rows: new[]
            {
                (fg1, false, 100m, 1m),
                (fg2, false, 100m, 1m),
                (fg3, false, 100m, 1m),
                (sec, true, 50m, 5m) // Secondary item ignored!
            },
            precision: 2);
        shares.Count.ShouldBe(3);
        shares.Sum(s => s.ShareAmount).ShouldBe(100.00m);
        shares[0].ShareAmount.ShouldBe(33.33m);
        shares[1].ShareAmount.ShouldBe(33.34m);
        shares[2].ShareAmount.ShouldBe(33.33m);

        // Zero total weight throws (c3b83d3d20)
        var zeroWeightEx = Should.Throw<BusinessException>(() =>
            MyERP.Purchasing.DomainServices.SubcontractingManager.CalculateAdditionalCostPerQtyForFinishedGoods(
                totalAdditionalCosts: 50m,
                distributeBasedOn: "Amount",
                rows: new[] { (fg1, false, 0m, 2m) }));
        zeroWeightEx.Data["detail"]!.ToString()!.ShouldContain("Cannot distribute Additional Costs based on Amount, as the finished goods have zero Amount");

        // 2. Cost Subcontracting Order raw materials from BOM unrounded base_rate & service item pairing (adbe37cbad, 922ad20647, eb6aecb851, cd45f385e5, 90944b84dd)
        var rmCosting = MyERP.Purchasing.DomainServices.SubcontractingManager.CalculateSubcontractingRawMaterialCostInCompanyCurrency(
            bomItemBaseRate: 100m,
            bomItemConversionFactor: 3m, // Unrounded rate = 33.333333...
            suppliedRequiredQty: 3m,
            bomQuantity: 2m);
        rmCosting.SuppliedItemRate.ShouldBe(33.33m);
        rmCosting.SuppliedItemAmount.ShouldBe(100.00m); // 3 * (100/3) = 100.00 from unrounded base_rate!
        rmCosting.OrderItemRmCostPerQty.ShouldBe(150.00m); // (100 * 3) / 2 = 150.00

        var poItem1 = Guid.NewGuid();
        var missingServiceEx = Should.Throw<BusinessException>(() =>
            MyERP.Purchasing.DomainServices.SubcontractingManager.ValidateSubcontractingOrderServiceItemPairing(
                new[] { (1, "FG-001", (Guid?)poItem1), (2, "FG-002", (Guid?)Guid.NewGuid()) },
                new Guid?[] { poItem1 }));
        missingServiceEx.Data["detail"]!.ToString()!.ShouldContain("Row 2: Finished Good FG-002 has no Service Item row");

        // 3. Subcontracting Order per-RM transfer status (e9d264a6a4)
        var sco = new MyERP.Purchasing.Entities.SubcontractingOrder(Guid.NewGuid(), Guid.NewGuid(), "SCO-001", DateTime.UtcNow, Guid.NewGuid());
        var rmA = Guid.NewGuid();
        var rmB = Guid.NewGuid();
        sco.AddSuppliedItem(new MyERP.Purchasing.Entities.SubcontractingOrderSuppliedItem(Guid.NewGuid(), sco.Id, rmA, "RM-A", 10m) { TransferredQty = 15m });
        sco.AddSuppliedItem(new MyERP.Purchasing.Entities.SubcontractingOrderSuppliedItem(Guid.NewGuid(), sco.Id, rmB, "RM-B", 10m) { TransferredQty = 5m });
        // Total supplied (20) >= total required (20), but RM-B is still short (5 < 10) -> Partial Material Transferred!
        sco.GetMaterialTransferStatus().ShouldBe("Partial Material Transferred");
        sco.SuppliedItems[1].TransferredQty = 10m;
        sco.GetMaterialTransferStatus().ShouldBe("Material Transferred");

        // 4. Subcontracting Receipt process-loss returnable qty, orphan secondary item guard, and LCV FG filter (17c2629110, 22222b39a8, 31d2158574, 42258d5660)
        var scr = new MyERP.Purchasing.Entities.SubcontractingReceipt(Guid.NewGuid(), Guid.NewGuid(), "SCR-001", DateTime.UtcNow, Guid.NewGuid(), sco.Id);
        var fgRow = new MyERP.Purchasing.Entities.SubcontractingReceiptItem(Guid.NewGuid(), scr.Id, Guid.NewGuid(), "FG-001", 8m, 100m)
        {
            ProcessLossQty = 2m,
            ReturnedQty = 3m
        };
        // ReceivedQty (Qty) = 8, ProcessLossQty = 2 -> ReturnableQty = 6. Returning -3 more brings total returned to 6 >= 6!
        fgRow.ReturnableQty.ShouldBe(6m);
        fgRow.IsFinalFinishedGoodReturn(-3m).ShouldBeTrue();

        scr.AddItem(fgRow);
        var orphanSecRow = new MyERP.Purchasing.Entities.SubcontractingReceiptItem(Guid.NewGuid(), scr.Id, Guid.NewGuid(), "SCRAP-01", 1m, 10m)
        {
            SecondaryItemType = "Scrap",
            ReferenceFinishedGoodRowId = Guid.NewGuid() // Not on this receipt!
        };
        scr.AddItem(orphanSecRow);
        var orphanEx = Should.Throw<BusinessException>(() => scr.ValidateSecondaryItemsHaveFinishedGoodRow());
        orphanEx.Data["detail"]!.ToString()!.ShouldContain("The Finished Good row of Secondary Item SCRAP-01 is not on this receipt");

        var lcvEligible = LandedCostVoucher.FilterEligibleSubcontractingReceiptItems(scr.Items);
        lcvEligible.Count.ShouldBe(1);
        lcvEligible[0].Id.ShouldBe(fgRow.Id);

        // 5. Subcontracting Inward Order conversion factor from SO row, batch return cap, and secondary item aggregation (76c448206a, a36fc2fd7a, 9b5a83412c, 3770398f2f, 19b846a497, a7755dcb8d)
        var scio = new MyERP.Purchasing.Entities.SubcontractingInwardOrder(Guid.NewGuid(), Guid.NewGuid(), "SCIO-001", DateTime.UtcNow, Guid.NewGuid());
        var soRow1 = Guid.NewGuid();
        var soRowZero = Guid.NewGuid();
        var scioItem1 = new MyERP.Purchasing.Entities.SubcontractingInwardOrderItem(Guid.NewGuid(), scio.Id, Guid.NewGuid(), 10m, 50m)
        {
            SalesOrderItemId = soRow1,
            SubcontractingConversionFactor = 1m
        };
        var scioItem2 = new MyERP.Purchasing.Entities.SubcontractingInwardOrderItem(Guid.NewGuid(), scio.Id, Guid.NewGuid(), 10m, 50m)
        {
            SalesOrderItemId = soRowZero,
            SubcontractingConversionFactor = 2.5m
        };
        scio.AddItem(scioItem1);
        scio.AddItem(scioItem2);
        scio.SyncSubcontractingConversionFactorsFromSalesOrder(
            new Dictionary<Guid, (decimal StockQty, decimal FgItemQty)>
            {
                [soRow1] = (12m, 4m),
                [soRowZero] = (0m, 4m) // Zero stock_qty -> keep existing 2.5m ratio!
            });
        scioItem1.SubcontractingConversionFactor.ShouldBe(3m);
        scioItem2.SubcontractingConversionFactor.ShouldBe(2.5m);

        var batchOverflowEx = Should.Throw<BusinessException>(() =>
            MyERP.Purchasing.DomainServices.SubcontractingManager.ValidateInwardReturnBatchQuantities(
                scioNumber: "SCIO-001",
                unreturnedDeliveredBatchQtyByDetail: new Dictionary<(Guid, string), decimal>
                {
                    [(scioItem1.Id, "BATCH-01")] = 5m
                },
                returnRows: new[]
                {
                    (1, scioItem1.Id, (IReadOnlyDictionary<string, decimal>)new Dictionary<string, decimal> { ["BATCH-01"] = 3m }),
                    (2, scioItem1.Id, (IReadOnlyDictionary<string, decimal>)new Dictionary<string, decimal> { ["BATCH-01"] = 3m })
                }));
        batchOverflowEx.Data["detail"]!.ToString()!.ShouldContain("Row #2: Only 2 of Batch BATCH-01 delivered against Subcontracting Inward Order SCIO-001 is left to return");

        var secItemId = Guid.NewGuid();
        var secWhId = Guid.NewGuid();
        var summedInwardSec = MyERP.Purchasing.DomainServices.SubcontractingManager.AggregateAndValidateInwardSecondaryItems(
            isCancel: false,
            entrySecondaryRows: new[]
            {
                (secItemId, "BYPROD-1", secWhId, 1.11114m),
                (secItemId, "BYPROD-1", secWhId, 2.22224m)
            },
            producedQtyPrecision: 4);
        summedInwardSec.Count.ShouldBe(1);
        summedInwardSec[(secItemId, secWhId)].ShouldBe(3.3334m);

        Should.Throw<BusinessException>(() =>
            MyERP.Purchasing.DomainServices.SubcontractingManager.AggregateAndValidateInwardSecondaryItems(
                isCancel: true,
                entrySecondaryRows: new[]
                {
                    (secItemId, "BYPROD-1", secWhId, 2m),
                    (secItemId, "BYPROD-1", secWhId, 2m)
                },
                existingScioSecondaryItems: new Dictionary<(Guid, Guid), (decimal, decimal)>
                {
                    [(secItemId, secWhId)] = (5m, 2m) // 5 - 4 = 1 < 2 delivered!
                }));
    }
}
