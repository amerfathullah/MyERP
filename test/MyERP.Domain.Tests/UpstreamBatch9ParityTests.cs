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
}
