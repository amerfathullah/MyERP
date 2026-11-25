using System;
using System.Collections.Generic;
using MyERP.Accounting.DomainServices;
using MyERP.Core.DomainServices;
using MyERP.Inventory.DomainServices;
using MyERP.Manufacturing;
using MyERP.Manufacturing.DomainServices;
using MyERP.Manufacturing.Entities;
using MyERP.Projects.Entities;
using MyERP.Purchasing.DomainServices;
using MyERP.Sales.DomainServices;
using MyERP.Settings;
using MyERP.Tax.DomainServices;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Settings;
using Xunit;

namespace MyERP;

/// <summary>
/// Parity tests for upstream ERPNext PRs #63403, #63784, #64304, #64596, #64998, #65444, #65601, #65637, #65650, #65669, #65791, #65961, #66259, #66260, #66292, #66299, #66947, #66966, #66971, #67344, #67364, #67378, #67379, #67383, #67384, #67395, #67399, #67400, #67432, #67445, #67450, #67462, #67468, #67480, #67485, #67503, #67530,
/// #67699, #67758, #67789, #67892, #67977, #67978, #67984, #67986, #68065, #68172, #68232, #68311, #68348, #68371, #68473, #68529, #68578, #68732,
/// #69286, #69293, #69299, #69301, #69312, #69314, #69315, #69316, #69322, #69324, #69327, #69337, #69350, #69351.
/// </summary>
public class UpstreamBatch42ParityTests
{
    [Fact]
    public void Pr69351_JournalEntryTds_RemovesStaleTdsRowAndRestoresPartyAmount_WhenUntickedOrBelowThreshold()
    {
        var partyAcc = Guid.NewGuid();
        var expenseAcc = Guid.NewGuid();
        var tdsAcc = Guid.NewGuid();

        var savedWithTds = new[]
        {
            new JournalTdsRow(expenseAcc, Debit: 1000m, Credit: 0m, IsPartyRow: false, IsTaxWithholdingRow: false),
            new JournalTdsRow(partyAcc, Debit: 0m, Credit: 900m, IsPartyRow: true, IsTaxWithholdingRow: false),
            new JournalTdsRow(tdsAcc, Debit: 0m, Credit: 100m, IsPartyRow: false, IsTaxWithholdingRow: true),
        };

        // Case 1: apply_tds unticked -> restores 100 to party row and removes TDS row
        var unticked = TaxWithholdingService.ReconcileJournalTdsRows(
            applyTds: false,
            taxWithholdingCategory: "TDS-10",
            voucherType: "Debit Note",
            existingRows: savedWithTds,
            calculatedTdsByAccount: new Dictionary<Guid, decimal> { [tdsAcc] = 100m });

        unticked.Count.ShouldBe(2);
        unticked.ShouldNotContain(r => r.IsTaxWithholdingRow);
        unticked.ShouldContain(r => r.IsPartyRow && r.Credit == 1000m);

        // Case 2: apply_tds ticked, but new amount below threshold (0 TDS due) -> restores 100 and removes TDS row
        var belowThreshold = TaxWithholdingService.ReconcileJournalTdsRows(
            applyTds: true,
            taxWithholdingCategory: "TDS-10",
            voucherType: "Credit Note",
            existingRows: savedWithTds,
            calculatedTdsByAccount: new Dictionary<Guid, decimal> { [tdsAcc] = 0m });

        belowThreshold.Count.ShouldBe(2);
        belowThreshold.ShouldNotContain(r => r.IsTaxWithholdingRow);
        belowThreshold.ShouldContain(r => r.IsPartyRow && r.Credit == 1000m);

        // Case 3: updated non-zero TDS (150) replaces prior 100 cleanly
        var updated = TaxWithholdingService.ReconcileJournalTdsRows(
            applyTds: true,
            taxWithholdingCategory: "TDS-15",
            voucherType: "Debit Note",
            existingRows: savedWithTds,
            calculatedTdsByAccount: new Dictionary<Guid, decimal> { [tdsAcc] = 150m });

        updated.Count.ShouldBe(3);
        updated.ShouldContain(r => r.IsPartyRow && r.Credit == 850m);
        updated.ShouldContain(r => r.IsTaxWithholdingRow && r.AccountId == tdsAcc && r.Credit == 150m);
    }

    [Fact]
    public void Pr69350_TaxWithholdingCancel_RefusesCancelWhenReturnReversedWithheldTax()
    {
        var ex = Should.Throw<BusinessException>(() =>
            TaxWithholdingService.ValidateNoReturnReversedTaxOnCancel(
                isTaxableDifferent: true,
                withholdingAmount: 500m,
                withholdingDoctype: "Purchase Invoice",
                withholdingName: "PI-0002",
                taxableDoctype: "Purchase Invoice",
                taxableName: "PI-0001",
                taxWithholdingCategory: "TDS-10",
                returnTaxEntries: new (string, string?, decimal, bool)[]
                {
                    ("PI-RET-0001", "TDS-10", -500m, true),
                }));

        (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("PI-RET-0001");
        (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("Cancel the return first");

        // Cancelled or draft returns do not block cancellation
        Should.NotThrow(() =>
            TaxWithholdingService.ValidateNoReturnReversedTaxOnCancel(
                isTaxableDifferent: true,
                withholdingAmount: 500m,
                withholdingDoctype: "Purchase Invoice",
                withholdingName: "PI-0002",
                taxableDoctype: "Purchase Invoice",
                taxableName: "PI-0001",
                taxWithholdingCategory: "TDS-10",
                returnTaxEntries: new (string, string?, decimal, bool)[]
                {
                    ("PI-RET-0001", "TDS-10", -500m, false),
                    ("PI-RET-0002", "OTHER-CAT", -500m, true),
                }));
    }

    [Fact]
    public void Pr69322_ProductionPlan_CountsPlannedWorkOrderOutputAndBundleReservationsOnce()
    {
        var soItemId = Guid.NewGuid();
        var otherSoItemId = Guid.NewGuid();

        var reservations = new[]
        {
            // Direct WO for this SO item (reserved 4, delivered 1 -> open 3)
            new WorkOrderOutputReservationSnapshot(
                VoucherDetailId: soItemId,
                IsSubmitted: true,
                IsFromStockEntry: true,
                WorkOrderSalesOrderItemId: soItemId,
                ProductionPlanItemSalesOrderItemId: null,
                ReservedQty: 4m, DeliveredQty: 1m, TransferredQty: 0m, ConsumedQty: 0m),
            // Planned WO linked via ProductionPlanItem.SalesOrderItemId (reserved 5 -> open 5)
            new WorkOrderOutputReservationSnapshot(
                VoucherDetailId: soItemId,
                IsSubmitted: true,
                IsFromStockEntry: true,
                WorkOrderSalesOrderItemId: null,
                ProductionPlanItemSalesOrderItemId: soItemId,
                ReservedQty: 5m, DeliveredQty: 0m, TransferredQty: 0m, ConsumedQty: 0m),
            // Unrelated SO item reservation should be ignored
            new WorkOrderOutputReservationSnapshot(
                VoucherDetailId: otherSoItemId,
                IsSubmitted: true,
                IsFromStockEntry: true,
                WorkOrderSalesOrderItemId: otherSoItemId,
                ProductionPlanItemSalesOrderItemId: null,
                ReservedQty: 10m, DeliveredQty: 0m, TransferredQty: 0m, ConsumedQty: 0m),
        };

        var reservedWoOutput = ProductionPlanMaterialPlanningService.CalculateReservedWorkOrderOutputQty(soItemId, reservations);
        reservedWoOutput.ShouldBe(8m);

        // With 10 ordered, 8 reserved from planned WO output, 8 already in productionPlanQty -> pending is 2 (not 0)
        var pendingSo = ProductionPlanMaterialPlanningService.CalculateSalesOrderItemPendingQtyForProductionPlan(
            stockQty: 10m,
            stockReservedQty: 8m,
            reservedOwnWorkOrderOutputQty: reservedWoOutput,
            workOrderQty: 8m,
            deliveredQtyInOrderUom: 0m,
            conversionFactor: 1m,
            productionPlanQty: 8m);
        pendingSo.ShouldBe(2m);

        // Bundle component: SO qty 5, packed row qty 10 (2 per bundle), 3 reserved on this Packed Item row, WO qty 2 (4 component)
        // max(workOrderQty=4, deliveredOrReserved=3) = 4 -> pending = 10 - 4 = 6
        var pendingPackedOverlap = ProductionPlanMaterialPlanningService.CalculatePackedItemPendingQtyForProductionPlan(
            salesOrderItemQty: 5m,
            packedItemQty: 10m,
            salesOrderWorkOrderQty: 2m,
            salesOrderDeliveredQty: 0m,
            reservedPackedItemQty: 3m);
        pendingPackedOverlap.ShouldBe(6m);

        // Bundle component with reserved 7 > workOrderQty 4 -> pending = 10 - 7 = 3 (not double-counted)
        var pendingPackedReservedWins = ProductionPlanMaterialPlanningService.CalculatePackedItemPendingQtyForProductionPlan(
            salesOrderItemQty: 5m,
            packedItemQty: 10m,
            salesOrderWorkOrderQty: 2m,
            salesOrderDeliveredQty: 0m,
            reservedPackedItemQty: 7m);
        pendingPackedReservedWins.ShouldBe(3m);
    }

    [Fact]
    public void Pr69327_BomOperation_DerivesPerBatchCostingWithoutForcingRowFlag()
    {
        var op = new BomOperation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, timeInMins: 60m)
        {
            BatchSize = 2,
            SetCostBasedOnBomQty = false,
            FixedTime = 0m,
        };
        op.CalculateCost(workstationHourRate: 100m); // OperatingCost = 100, CostPerUnit = 50

        // Operation master has create_job_card_based_on_batch_size = true: costed per batch without mutating row flag
        op.GetEffectiveOperatingCost(bomQuantity: 10m, operationCreateJobCardBasedOnBatchSize: true).ShouldBe(500m);
        op.SetCostBasedOnBomQty.ShouldBeFalse();

        // Fixed time overrides both SetCostBasedOnBomQty and operation batch-size flag
        op.SetCostBasedOnBomQty = true;
        op.FixedTime = 30m;
        BomCostPropagationService.IsCostedPerBatch(fixedTime: true, setCostBasedOnBomQty: true, operationCreateJobCardBasedOnBatchSize: true).ShouldBeFalse();
        op.GetEffectiveOperatingCost(bomQuantity: 10m, operationCreateJobCardBasedOnBatchSize: true).ShouldBe(100m);
    }

    [Fact]
    public void Pr69324_SplitReturn_SameDocumentRowsCapInOrder_AndZeroQtyRowTakesNothing()
    {
        var origRowId = Guid.NewGuid();
        var whA = Guid.NewGuid();
        var whB = Guid.NewGuid();

        var priorRows = new[]
        {
            // Original delivery moved 10 stock qty (SLE = -10)
            new SubmittedOriginalOrPriorReturnRow(origRowId, Qty: 10m, ConversionFactor: 1m, WarehouseId: whA, UpdateStock: true),
            // A credit note without update_stock moved no stock and must be ignored
            new SubmittedOriginalOrPriorReturnRow(Guid.NewGuid(), Qty: -5m, ConversionFactor: 1m, WarehouseId: whA, UpdateStock: false),
        };
        var postedSle = new Dictionary<Guid, decimal> { [origRowId] = -10m };

        // Current return has 3 rows against origRowId:
        // row 1: -6 (posts -6, leaves 4)
        // row 2: 0 qty (posts nothing, takes 0)
        // row 3: -5 (exceeds remaining 4 -> capped at -4)
        var returnRows = new[]
        {
            new ReturnDocumentRowSnapshot(Guid.NewGuid(), Qty: -6m, ConversionFactor: 1m, WarehouseId: whA),
            new ReturnDocumentRowSnapshot(Guid.NewGuid(), Qty: 0m, ConversionFactor: 1m, WarehouseId: whA),
            new ReturnDocumentRowSnapshot(Guid.NewGuid(), Qty: -5m, ConversionFactor: 1m, WarehouseId: whB),
        };

        var taken = DeliveryNoteManager.CalculateSplitReturnStockQuantities(
            originalRowId: origRowId,
            currentReturnRows: returnRows,
            submittedOriginalAndPriorReturnRows: priorRows,
            postedSleActualQtyByRowId: postedSle,
            isStockReceipt: false,
            targetWarehouseId: whA);

        taken.ShouldBe(new[] { -6m, 0m, -4m });

        // Cancel returns exact posted SLE qty (0 for a row that posted nothing)
        var postedCurrent = new Dictionary<Guid, decimal>
        {
            [returnRows[0].RowId] = 6m,
            [returnRows[2].RowId] = 4m,
        };
        var onCancel = DeliveryNoteManager.CalculateSplitReturnStockQuantities(
            originalRowId: origRowId,
            currentReturnRows: returnRows,
            submittedOriginalAndPriorReturnRows: priorRows,
            postedSleActualQtyByRowId: postedSle,
            isStockReceipt: false,
            targetWarehouseId: whA,
            isCancelled: true,
            postedCurrentReturnSleQtyByRowId: postedCurrent);

        onCancel.ShouldBe(new[] { -6m, 0m, -4m });
    }

    [Fact]
    public void Pr69315_SubcontractingReturnSplit_SkipsZeroPrecisionRemainder_AndSplitsUnroundedQty()
    {
        var row1 = Guid.NewGuid();
        var row2 = Guid.NewGuid();

        // Sub-precision float remainder after full consumption rounds to 0 -> produces no return rows
        var remainderResult = SubcontractingRmTransferService.SplitReturnQtyAcrossOrderRows(
            returnQty: 0.00001m,
            orderRows: new[] { (row1, 5m), (row2, 5m) },
            precision: 4);
        remainderResult.ShouldBeEmpty();

        // Unrounded split respects per-row available qty without pre-rounding total upward
        var split = SubcontractingRmTransferService.SplitReturnQtyAcrossOrderRows(
            returnQty: 1.00004m,
            orderRows: new[] { (row1, 0.5m), (row2, 0.6m) },
            precision: 4);
        split.Count.ShouldBe(2);
        split[0].ShouldBe((row1, 0.5m));
        split[1].ShouldBe((row2, 0.5m));
    }

    [Fact]
    public void Pr68311_Timesheet_SetsExchangeRateFromCurrency()
    {
        var ts = new Timesheet(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new DateTime(2026, 11, 1), new DateTime(2026, 11, 2));

        // Company currency always gets rate 1 (even if stale non-1 rate was set)
        ts.Currency = "MYR";
        ts.ExchangeRate = 4.5m;
        ts.SetExchangeRate("MYR", (_, _) => 9.9m);
        ts.ExchangeRate.ShouldBe(1m);

        // Foreign currency with zero rate fetches current rate
        ts.Currency = "USD";
        ts.ExchangeRate = 0m;
        ts.SetExchangeRate("MYR", (from, to) => from == "USD" && to == "MYR" ? 4.42m : 1m);
        ts.ExchangeRate.ShouldBe(4.42m);

        // Foreign currency with explicit rate (including 1) is preserved
        ts.ExchangeRate = 1m;
        ts.SetExchangeRate("MYR", (_, _) => 4.42m);
        ts.ExchangeRate.ShouldBe(1m);

        // Foreign currency with failed lookup falls back to 1
        ts.ExchangeRate = 0m;
        ts.SetExchangeRate("MYR", (_, _) => 0m);
        ts.ExchangeRate.ShouldBe(1m);
    }

    [Fact]
    public void Pr69312_WorkOrder_TryRefreshStatus_SkipsUnchangedConcurrentStoppedOrClosed()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-69312", Guid.NewGuid(), Guid.NewGuid(), 10m);
        wo.Submit();

        // Unchanged status skips write
        wo.TryRefreshStatus(savedStatus: WorkOrderStatus.Submitted, computedStatus: WorkOrderStatus.Submitted).ShouldBeFalse();

        // Concurrent change (savedStatus != current Status) skips write
        wo.TryRefreshStatus(savedStatus: WorkOrderStatus.InProcess, computedStatus: WorkOrderStatus.StockReserved).ShouldBeFalse();
        wo.Status.ShouldBe(WorkOrderStatus.Submitted);

        // Matching savedStatus updates cleanly
        wo.TryRefreshStatus(savedStatus: WorkOrderStatus.Submitted, computedStatus: WorkOrderStatus.StockReserved).ShouldBeTrue();
        wo.Status.ShouldBe(WorkOrderStatus.StockReserved);

        // Stopped or Closed saved status is never overwritten
        wo.Stop();
        wo.TryRefreshStatus(savedStatus: WorkOrderStatus.Stopped, computedStatus: WorkOrderStatus.StockReserved).ShouldBeFalse();
        wo.Status.ShouldBe(WorkOrderStatus.Stopped);
    }

    [Fact]
    public void Pr69299_RateValidation_ChecksInternalSalesOrderAndGuardsSourceRateByPermission()
    {
        // Sales Order still validates Quotation rate even on internal transfers; other doctypes skip
        TransactionValidationService.ShouldValidateRateWithReferenceDoc("Sales Order", isInternalTransfer: true).ShouldBeTrue();
        TransactionValidationService.ShouldValidateRateWithReferenceDoc("SalesOrder", isInternalTransfer: true).ShouldBeTrue();
        TransactionValidationService.ShouldValidateRateWithReferenceDoc("Delivery Note", isInternalTransfer: true).ShouldBeFalse();
        TransactionValidationService.ShouldValidateRateWithReferenceDoc("Delivery Note", isInternalTransfer: false).ShouldBeTrue();

        // Rate mismatch message shows numbers only when user has source document permission
        var withPerm = TransactionValidationService.FormatRateMismatchMessage(1, "Quotation", "QTN-001", 120m, 100m, hasSourceDocPermission: true);
        withPerm.ShouldBe("Row #1: Rate must be same as Quotation: QTN-001 (120 / 100)");

        var withoutPerm = TransactionValidationService.FormatRateMismatchMessage(1, "Quotation", "QTN-001", 120m, 100m, hasSourceDocPermission: false);
        withoutPerm.ShouldBe("Row #1: Rate must be same as Quotation: QTN-001");
    }

    [Fact]
    public void Pr69301_StandaloneDebitNote_IgnoresStaleFactorWithoutUomAndResolvesSupplierCurrencyRate()
    {
        // Stale rowConversionFactor (99) sent without a row UOM is ignored in favor of resolved purchase UOM factor (12)
        PurchaseInvoiceManager.ResolveStandaloneDebitNoteConversionFactor(
            rowUom: null,
            itemPurchaseUom: "Box",
            itemStockUom: "Nos",
            rowConversionFactor: 99m,
            itemUomConversionFactor: 12m).ShouldBe(12m);

        // Explicit row UOM keeps its explicit conversion factor
        PurchaseInvoiceManager.ResolveStandaloneDebitNoteConversionFactor(
            rowUom: "Box",
            itemPurchaseUom: "Box",
            itemStockUom: "Nos",
            rowConversionFactor: 10m,
            itemUomConversionFactor: 12m).ShouldBe(10m);

        // Supplier default currency == company currency -> exchange rate 1 even if stale conversionRate passed
        PurchaseInvoiceManager.ResolveStandaloneDebitNoteExchangeRate(
            invoiceCurrency: null,
            supplierDefaultCurrency: "MYR",
            companyCurrency: "MYR",
            conversionRate: 4.5m).ShouldBe(1m);

        // Supplier default currency is foreign (USD) -> uses conversionRate
        PurchaseInvoiceManager.ResolveStandaloneDebitNoteExchangeRate(
            invoiceCurrency: null,
            supplierDefaultCurrency: "USD",
            companyCurrency: "MYR",
            conversionRate: 4.45m).ShouldBe(4.45m);
    }

    [Fact]
    public void Pr69314_SubcontractingReservationReplay_ExcludesReturnsAtOrBeforeCutoff()
    {
        var cutoff = new DateTime(2026, 11, 24, 10, 0, 0, DateTimeKind.Utc);
        var rows = new[]
        {
            (Id: "RET-BEFORE", SubmittedAt: cutoff.AddMinutes(-5)),
            (Id: "RET-EXACT", SubmittedAt: cutoff),
            (Id: "RET-AFTER", SubmittedAt: cutoff.AddMinutes(5)),
        };

        var filtered = StockReservationManager.FilterComponentsReturnRowsByCutoff(rows, r => r.SubmittedAt, cutoff);
        filtered.Count.ShouldBe(1);
        filtered[0].Id.ShouldBe("RET-AFTER");

        // Null cutoff keeps all rows
        StockReservationManager.FilterComponentsReturnRowsByCutoff(rows, r => r.SubmittedAt, null).Count.ShouldBe(3);
    }

    [Fact]
    public void Pr69337_CustomerAcquisitionReport_EnforcesPermittedCompaniesFilter()
    {
        var compA = Guid.NewGuid();
        var compB = Guid.NewGuid();
        var permitted = new[] { compA };

        // Selected permitted company -> [compA]
        TransactionValidationService.ResolvePermittedCompaniesForReport(compA, permitted).ShouldBe(new[] { compA });

        // No company selected -> scoped to permitted companies [compA]
        TransactionValidationService.ResolvePermittedCompaniesForReport(null, permitted).ShouldBe(new[] { compA });

        // Unrestricted user with no company selected -> null (all companies)
        TransactionValidationService.ResolvePermittedCompaniesForReport(null, null).ShouldBeNull();

        // Selected forbidden company -> throws
        var ex = Should.Throw<BusinessException>(() =>
            TransactionValidationService.ResolvePermittedCompaniesForReport(compB, permitted));
        (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("You do not have permission to access Company");
    }

    [Fact]
    public void Pr63403_StockLedgerAudit_FindsFirstIncorrectSle_AndSkipsPendingOrDuplicateReposts()
    {
        var itemId = Guid.NewGuid();
        var whId = Guid.NewGuid();
        var itemB = Guid.NewGuid();
        var t1 = new DateTime(2026, 11, 1, 9, 0, 0);
        var t2 = new DateTime(2026, 11, 2, 9, 0, 0);

        // Pending reposts: accounting-only transaction repost is NOT skipped; stock repost IS skipped
        var inPending = StockLedgerAuditService.GetItemWarehousesInPendingRepost(new[]
        {
            new PendingRepostSnapshot(itemId, whId, IsSubmitted: true, IsQueuedOrInProgress: true, IsTransactionBased: true, RepostOnlyAccountingLedgers: true),
            new PendingRepostSnapshot(itemB, whId, IsSubmitted: true, IsQueuedOrInProgress: true, IsTransactionBased: false, RepostOnlyAccountingLedgers: false),
        });
        inPending.ShouldNotContain((itemId, whId));
        inPending.ShouldContain((itemB, whId));

        // SLE 1 consistent with opening (qty 10 + 5 = 15, val 100 + 50 = 150, rate 10)
        // SLE 2 has corrupted stock_value (150 - 30 = 120 expected, stored 110)
        var sle1 = new StockLedgerAuditEntry(Guid.NewGuid(), itemId, whId, t1, ActualQty: 5m, QtyAfterTransaction: 15m, ValuationRate: 10m, StockValue: 150m, StockValueDifference: 50m);
        var sle2 = new StockLedgerAuditEntry(Guid.NewGuid(), itemId, whId, t2, ActualQty: -3m, QtyAfterTransaction: 12m, ValuationRate: 10m, StockValue: 110m, StockValueDifference: -30m);

        var firstBad = StockLedgerAuditService.FindFirstIncorrectSle(new[] { sle1, sle2 }, openingQty: 10m, openingStockValue: 100m);
        firstBad.ShouldNotBeNull();
        firstBad.EntryId.ShouldBe(sle2.EntryId);

        // Should queue repost unless already queued by scheduler at same (item, warehouse, posting_datetime)
        StockLedgerAuditService.ShouldCreateSchedulerRepost(firstBad, inPending, existingSchedulerReposts: null).ShouldBeTrue();
        var existing = new HashSet<(Guid, Guid, DateTime)> { (itemId, whId, t2) };
        StockLedgerAuditService.ShouldCreateSchedulerRepost(firstBad, inPending, existing).ShouldBeFalse();

        // Clean sequence returns null
        var sle2Clean = sle2 with { StockValue = 120m };
        StockLedgerAuditService.FindFirstIncorrectSle(new[] { sle1, sle2Clean }, openingQty: 10m, openingStockValue: 100m).ShouldBeNull();
    }

    [Fact]
    public void Pr69286_DeliveryNoteReturnBilling_SkipsAlreadyUpdatedReturns()
    {
        var ret1 = Guid.NewGuid();
        var ret2 = Guid.NewGuid();
        var ret3 = Guid.NewGuid();
        var alreadyUpdated = new HashSet<Guid> { ret1, ret3 };

        var pending = DeliveryNoteManager.GetPendingReturnDeliveryNotesToUpdateBilling(
            new[] { ret1, ret2, ret3, ret2 },
            alreadyUpdated);

        pending.ShouldBe(new[] { ret2 });
    }

    [Fact]
    public void Pr69316_SubcontractingSerialField_ValidatesRowFieldAndDeduplicatesSupplierPool()
    {
        // Dedup preserves insertion order on outward transfer and removes returned serials in linear time
        var atSupplier = SubcontractingManager.UpdateSerialNosAtSupplier(
            existingAtSupplier: new[] { "SN-001", "SN-002" },
            serialNos: new[] { "SN-002", "SN-003", "SN-001" },
            isReturn: false);
        atSupplier.ShouldBe(new[] { "SN-001", "SN-002", "SN-003" });

        var afterReturn = SubcontractingManager.UpdateSerialNosAtSupplier(
            existingAtSupplier: atSupplier,
            serialNos: new[] { "SN-002" },
            isReturn: true);
        afterReturn.ShouldBe(new[] { "SN-001", "SN-003" });

        // Consumed validation checks BOTH bundle serials and row serial_no field
        Should.NotThrow(() =>
            SubcontractingManager.ValidateConsumedSerialNosAgainstSupplierPool(
                "Subcontracting Order", "SCO-001", afterReturn,
                bundleSerialNos: new[] { "SN-001" },
                rowFieldSerialNos: new[] { "SN-003" }));

        var ex = Should.Throw<BusinessException>(() =>
            SubcontractingManager.ValidateConsumedSerialNosAgainstSupplierPool(
                "Subcontracting Order", "SCO-001", afterReturn,
                bundleSerialNos: new[] { "SN-001" },
                rowFieldSerialNos: new[] { "SN-002" }));
        (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("SN-002");
    }

    [Fact]
    public void Pr69293_BomSecondaryItemValuationCost_ConvertsCompanyRateToBomCurrency()
    {
        // Company currency valuation rate 45 MYR, BOM currency USD (conversionRate = 4.5), stockQty 10, processLoss 2 -> produced 8
        // cost = (45 / 4.5) * 8 = 80 USD, baseCost = 80 * 4.5 = 360 MYR
        var (cost, baseCost) = BomCostPropagationService.CalculateSecondaryItemCostFromValuationRate(
            companyCurrencyValuationRate: 45m,
            stockQty: 10m,
            processLossQty: 2m,
            bomConversionRate: 4.5m);

        cost.ShouldBe(80m);
        baseCost.ShouldBe(360m);

        // Process loss exceeding stockQty caps producedQty to 0
        var (zeroCost, zeroBase) = BomCostPropagationService.CalculateSecondaryItemCostFromValuationRate(
            companyCurrencyValuationRate: 45m,
            stockQty: 5m,
            processLossQty: 8m,
            bomConversionRate: 4.5m);
        zeroCost.ShouldBe(0m);
        zeroBase.ShouldBe(0m);
    }

    [Fact]
    public void Pr68732_FutureSleExists_UsesSameTimestampCreationTieBreakIncludingCancelledVoucherSle()
    {
        var itemId = Guid.NewGuid();
        var whId = Guid.NewGuid();
        var earlierVoucher = Guid.NewGuid();
        var targetVoucher = Guid.NewGuid();
        var laterVoucher = Guid.NewGuid();
        var samePostingTime = new DateTime(2026, 11, 24, 10, 0, 0);

        // Earlier voucher created at 10:00:01, target voucher's SLE (even if cancelled) at 10:00:02
        var onlyEarlier = new[]
        {
            new FutureSleCandidate(itemId, whId, "StockEntry", earlierVoucher, samePostingTime, samePostingTime.AddSeconds(1), IsCancelled: false),
            new FutureSleCandidate(itemId, whId, "StockEntry", targetVoucher, samePostingTime, samePostingTime.AddSeconds(2), IsCancelled: true),
        };
        StockLedgerAuditService.HasFutureStockLedgerEntry(itemId, whId, "StockEntry", targetVoucher, samePostingTime, onlyEarlier)
            .ShouldBeFalse();

        // Adding another active voucher at same posting_datetime created AFTER target (10:00:03) -> future SLE exists
        var withLaterSameTimestamp = new[]
        {
            onlyEarlier[0],
            onlyEarlier[1],
            new FutureSleCandidate(itemId, whId, "StockEntry", laterVoucher, samePostingTime, samePostingTime.AddSeconds(3), IsCancelled: false),
        };
        StockLedgerAuditService.HasFutureStockLedgerEntry(itemId, whId, "StockEntry", targetVoucher, samePostingTime, withLaterSameTimestamp)
            .ShouldBeTrue();
    }

    [Fact]
    public void Pr67503_SubcontractingInward_TakeReservedSerialBatchEntries_AndIsBundleResized()
    {
        StockReservationManager.IsBundleResized(hasBundle: false, rowTransferQty: 5m, bundleTotalQty: 3m).ShouldBeFalse();
        StockReservationManager.IsBundleResized(hasBundle: true, rowTransferQty: 5m, bundleTotalQty: -5m).ShouldBeFalse();
        StockReservationManager.IsBundleResized(hasBundle: true, rowTransferQty: 5m, bundleTotalQty: 3m).ShouldBeTrue();

        var pool = new List<ReservedSerialBatchPoolEntry>
        {
            new() { SerialNo = "SN-1", BatchNo = "B-1", Available = 1m },
            new() { SerialNo = "SN-2", BatchNo = "B-1", Available = 1m },
            new() { SerialNo = "SN-3", BatchNo = "B-2", Available = 1m },
            new() { SerialNo = null, BatchNo = "B-2", Available = 4m },
        };

        // First row takes 2 from B-1 (SN-1, SN-2)
        var (serials1, batches1) = StockReservationManager.TakeReservedSerialBatchEntries(pool, qty: 2m, filterBatchNo: "B-1");
        serials1.ShouldBe(new[] { "SN-1", "SN-2" });
        batches1["B-1"].ShouldBe(2m);
        pool[0].Available.ShouldBe(0m);
        pool[1].Available.ShouldBe(0m);

        // Second row takes 3 across remaining B-2 entries (SN-3 + 2 from batch pool)
        var (serials2, batches2) = StockReservationManager.TakeReservedSerialBatchEntries(pool, qty: 3m);
        serials2.ShouldBe(new[] { "SN-3" });
        batches2["B-2"].ShouldBe(3m);
        pool[2].Available.ShouldBe(0m);
        pool[3].Available.ShouldBe(2m);
    }

    [Fact]
    public void Pr67758_PortalDocumentAccess_RefusesMissingAndUnauthorizedIdentically()
    {
        var missingEx = Should.Throw<BusinessException>(() =>
            TransactionValidationService.ValidatePortalDocumentAccess("MR-999", documentExists: false, hasReadOrWebsitePermission: false));
        var forbiddenEx = Should.Throw<BusinessException>(() =>
            TransactionValidationService.ValidatePortalDocumentAccess("MR-001", documentExists: true, hasReadOrWebsitePermission: false));
        var blankEx = Should.Throw<BusinessException>(() =>
            TransactionValidationService.ValidatePortalDocumentAccess("", documentExists: true, hasReadOrWebsitePermission: true));

        (missingEx.Data["detail"]?.ToString() ?? string.Empty).ShouldBe("Not Permitted");
        (forbiddenEx.Data["detail"]?.ToString() ?? string.Empty).ShouldBe("Not Permitted");
        (blankEx.Data["detail"]?.ToString() ?? string.Empty).ShouldBe("Not Permitted");

        Should.NotThrow(() =>
            TransactionValidationService.ValidatePortalDocumentAccess("MR-001", documentExists: true, hasReadOrWebsitePermission: true));
    }

    [Fact]
    public void Pr63403_AutoRepostIncorrectValuationEntriesSetting_DefaultsToFalse()
    {
        var definitions = new Dictionary<string, SettingDefinition>();
        var context = new SettingDefinitionContext(definitions);
        new MyERPSettingDefinitionProvider().Define(context);

        definitions.ShouldContainKey(MyERPSettings.Stock.AutoRepostIncorrectValuationEntries);
        definitions[MyERPSettings.Stock.AutoRepostIncorrectValuationEntries].DefaultValue.ShouldBe("false");
    }

    [Fact]
    public void Pr67383_ManufactureSecondaryItemBasis_DeductsManuallyRatedFinishedGoodsDownToZero()
    {
        // Basis 1000, costed-out 100 -> 900; automatic FG exists, manual FG takes 300 -> remaining basis 600
        StockEntryManager.CalculateSecondaryItemAllocationBasis(
            secondaryItemsCostBasis: 1000m,
            costedOutItemsCost: 100m,
            automaticFinishedItemsQty: 5m,
            manuallyRatedFinishedItemsCost: 300m).ShouldBe(600m);

        // Manual FG exceeds remaining basis -> clamped at 0
        StockEntryManager.CalculateSecondaryItemAllocationBasis(
            secondaryItemsCostBasis: 500m,
            costedOutItemsCost: 100m,
            automaticFinishedItemsQty: 5m,
            manuallyRatedFinishedItemsCost: 600m).ShouldBe(0m);

        // No automatic finished goods -> manual FG cost is NOT deducted from secondary basis
        StockEntryManager.CalculateSecondaryItemAllocationBasis(
            secondaryItemsCostBasis: 1000m,
            costedOutItemsCost: 100m,
            automaticFinishedItemsQty: 0m,
            manuallyRatedFinishedItemsCost: 300m).ShouldBe(900m);
    }

    [Fact]
    public void Pr67530_PurchaseReturnMovedWarehouse_RefusesWhenWarehouseLacksStockAtReceiptRate()
    {
        var itemId = Guid.NewGuid();
        var whB = Guid.NewGuid();

        var rows = new[]
        {
            // Non-moved row in WH-B takes 3 at rate 100
            new PurchaseReturnMovedWarehouseRow(1, itemId, "ITEM-A", whB, "WH-B", "WH-B", IsMovedWarehouse: false, Qty: -3m, Uom: "Nos", StockUom: "Nos", ConversionFactor: 1m, ReceiptReturnRate: 100m),
            // Moved row returning from WH-B (originally stocked in WH-A) takes 4 at rate 100 (total 7 > held 5)
            new PurchaseReturnMovedWarehouseRow(2, itemId, "ITEM-A", whB, "WH-B", "WH-A", IsMovedWarehouse: true, Qty: -4m, Uom: "Nos", StockUom: "Nos", ConversionFactor: 1m, ReceiptReturnRate: 100m),
        };

        var ex = Should.Throw<BusinessException>(() =>
            PurchaseReceiptManager.ValidateReturnWarehouseStockAtReceiptRate(
                "Purchase Receipt", "PR-001", rows,
                (_, _, rate) => rate == 100m ? 5m : 0m));

        (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("WH-B");
        (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("Move the goods back to WH-A");

        // Enough stock held at exact receipt rate (7 >= 7) -> allowed
        Should.NotThrow(() =>
            PurchaseReceiptManager.ValidateReturnWarehouseStockAtReceiptRate(
                "Purchase Receipt", "PR-001", rows,
                (_, _, rate) => rate == 100m ? 7m : 0m));
    }

    [Fact]
    public void Pr63784_ReservedSerialsPerSalesOrderRow_PicksRowReservedThenUnreservedStock()
    {
        var soRow1 = Guid.NewGuid();
        var soRow2 = Guid.NewGuid();
        var packedRow1 = Guid.NewGuid();

        var reserved = new (string SerialNo, Guid? VoucherDetailId)[]
        {
            ("SN-R1", soRow1),
            ("SN-P1", packedRow1),
            ("SN-OTHER", soRow2),
        };
        var inStock = new[] { "SN-FREE-1", "SN-OTHER", "SN-R1", "SN-P1", "SN-FREE-2" };

        // Request 3 for soRow1 (+ its packedRow1): takes SN-R1 and SN-P1 first, then fills 1 from unreserved SN-FREE-1 (never touches SN-OTHER)
        var picked = StockReservationManager.PickReservedAndUnreservedSerialNos(
            requestedQty: 3,
            allowedVoucherDetailIds: new HashSet<Guid> { soRow1, packedRow1 },
            allReservedSerials: reserved,
            inStockSerialsInOrder: inStock);

        picked.ShouldBe(new[] { "SN-R1", "SN-P1", "SN-FREE-1" });
    }

    [Fact]
    public void Pr67379_InternalTransferValuationLookup_NegatesOutgoingDeliveryQtyAndUsesFromWarehouseOnPurchase()
    {
        var whDest = Guid.NewGuid();
        var whSrc = Guid.NewGuid();

        // Outgoing internal customer delivery -> negative stock qty at warehouseId
        var (dnWh, dnQty) = InterCompanyTransactionService.ResolveInternalTransferValuationLookup(
            isInternalCustomer: true, isReturn: false, isInternalPurchase: false,
            qty: 5m, conversionFactor: 2m, warehouseId: whDest, fromWarehouseId: whSrc);
        dnWh.ShouldBe(whDest);
        dnQty.ShouldBe(-10m);

        // Internal customer return -> positive stock qty
        var (_, retQty) = InterCompanyTransactionService.ResolveInternalTransferValuationLookup(
            isInternalCustomer: true, isReturn: true, isInternalPurchase: false,
            qty: -5m, conversionFactor: 2m, warehouseId: whDest, fromWarehouseId: whSrc);
        retQty.ShouldBe(-10m);

        // Internal purchase receipt -> uses sending fromWarehouseId with positive stock qty
        var (prWh, prQty) = InterCompanyTransactionService.ResolveInternalTransferValuationLookup(
            isInternalCustomer: false, isReturn: false, isInternalPurchase: true,
            qty: 5m, conversionFactor: 2m, warehouseId: whDest, fromWarehouseId: whSrc);
        prWh.ShouldBe(whSrc);
        prQty.ShouldBe(10m);
    }

    [Fact]
    public void Pr67344_WorkOrderMixedTransfers_ComputesFractionWhenZeroFgTransferPresent()
    {
        var reqItems = new[] { (RequiredQty: 10m, TransferredQty: 8m), (RequiredQty: 20m, TransferredQty: 16m) };

        // Direct transfers only -> preserves sumFgCompletedQty (including excess transfer 12 > 10)
        WorkOrderManager.CalculateMixedMaterialTransferredForManufacturing(
            workOrderQty: 10m, sumFgCompletedQty: 12m, hasTransferWithoutForQuantity: false, requiredItems: reqItems)
            .ShouldBe(12m);

        // Mixed transfers (some fg_completed_qty == 0): minFraction = 0.8 * 10 = 8 > sumFgCompletedQty (3) -> 8
        WorkOrderManager.CalculateMixedMaterialTransferredForManufacturing(
            workOrderQty: 10m, sumFgCompletedQty: 3m, hasTransferWithoutForQuantity: true, requiredItems: reqItems)
            .ShouldBe(8m);
    }

    [Fact]
    public void Pr67462_JobCardNonStockItemCost_MatchesOperationOrFallsBackToFinalFinishedGoodCard()
    {
        var opRow1 = Guid.NewGuid();
        var opRow2 = Guid.NewGuid();
        var op1 = Guid.NewGuid();
        var semiFg = Guid.NewGuid();
        var finalFg = Guid.NewGuid();

        // Row bound to opRow1 -> charged only to opRow1 card
        JobCardManager.IsNonStockItemChargedToJobCard(opRow1, op1, opRow1, op1, semiFg, finalFg).ShouldBeTrue();
        JobCardManager.IsNonStockItemChargedToJobCard(opRow1, op1, opRow2, op1, finalFg, finalFg).ShouldBeFalse();

        // Row with NO operation -> charged ONLY to final FG card, not intermediate semi-FG card
        JobCardManager.IsNonStockItemChargedToJobCard(null, null, opRow1, op1, semiFg, finalFg).ShouldBeFalse();
        JobCardManager.IsNonStockItemChargedToJobCard(null, null, opRow2, op1, finalFg, finalFg).ShouldBeTrue();
    }

    [Fact]
    public void Pr66947_PaymentEntryUxMessagesAndPermissionCheckedBalance_Parity()
    {
        // 1. Clear unallocated references + singular/plural alert
        var rows = new[]
        {
            (Name: "SINV-001", Allocated: 100m),
            (Name: "SINV-002", Allocated: 0m),
        };
        var (kept1, removed1, alert1) = PaymentEntryManager.ClearUnallocatedReferences(rows, r => r.Allocated);
        kept1.Count.ShouldBe(1);
        removed1.ShouldBe(1);
        alert1.ShouldBe("Removed 1 row with nothing allocated from Payment References.");

        var rowsPlural = new[]
        {
            (Name: "SINV-001", Allocated: 0m),
            (Name: "SINV-002", Allocated: 0m),
        };
        var (kept2, removed2, alert2) = PaymentEntryManager.ClearUnallocatedReferences(rowsPlural, r => r.Allocated);
        kept2.ShouldBeEmpty();
        removed2.ShouldBe(2);
        alert2.ShouldBe("Removed 2 rows with nothing allocated from Payment References.");

        // 2. Non-zero difference amount submit error includes actionable guidance + title
        PaymentEntryManager.ValidateZeroDifferenceAmountOnSubmit(0m, "MYR");
        var ex = Should.Throw<BusinessException>(() =>
            PaymentEntryManager.ValidateZeroDifferenceAmountOnSubmit(25.5m, "MYR"));
        ex.Data["title"]!.ToString()!.ShouldBe("Amounts Don't Match");
        ex.Data["detail"]!.ToString()!.ShouldContain("MYR 25.50");
        ex.Data["detail"]!.ToString()!.ShouldContain("Write Off Difference Amount");

        // 3. No-outstanding-invoices message mentions From Date when filter is active
        var withDate = PaymentEntryManager.FormatNoOutstandingReferencesMessage(
            "Customer", "Acme Sdn Bhd", "invoices", new DateTime(2026, 1, 1));
        withDate.ShouldContain("posted on or after 2026-01-01");
        withDate.ShouldContain("Clear the From Date under Posting Date");

        var generic = PaymentEntryManager.FormatNoOutstandingReferencesMessage(
            "Customer", "Acme Sdn Bhd", "orders", null);
        generic.ShouldContain("No outstanding orders found");

        // 4. Party balance returned only for Customer/Supplier when read-permitted
        PaymentEntryManager.ResolvePartyBalanceIfPermitted("Customer", true, 500m).ShouldBe(500m);
        PaymentEntryManager.ResolvePartyBalanceIfPermitted("Supplier", true, -200m).ShouldBe(-200m);
        PaymentEntryManager.ResolvePartyBalanceIfPermitted("Customer", false, 500m).ShouldBeNull();
        PaymentEntryManager.ResolvePartyBalanceIfPermitted("Employee", true, 500m).ShouldBeNull();
    }

    [Fact]
    public void Pr68172_PickListRepackPackedRows_CreatesRowsForExtraPickListLocations()
    {
        var wh1 = Guid.NewGuid();
        var wh2 = Guid.NewGuid();

        // Empty stale rows -> nothing kept
        PickListManager.BuildKeptPackedRowsForPickListRepack(
            "COMP-A", 5m, Array.Empty<RepackedPackedItemRow>(),
            new[] { new PickListRepackLocation(wh1, "B1", null, 5m) }).ShouldBeEmpty();

        // 1 stale row + 2 pick list locations -> reuses stale row for matching location + creates new row for second location
        var stale = new[]
        {
            new RepackedPackedItemRow("COMP-A", "Box", wh1, "B1", null, 5m),
        };
        var locations = new[]
        {
            new PickListRepackLocation(wh1, "B1", null, 3m),
            new PickListRepackLocation(wh2, "B2", null, 2m),
        };

        var repacked = PickListManager.BuildKeptPackedRowsForPickListRepack("COMP-A", 5m, stale, locations);
        repacked.Count.ShouldBe(2);
        repacked[0].WarehouseId.ShouldBe(wh1);
        repacked[0].BatchNo.ShouldBe("B1");
        repacked[0].Qty.ShouldBe(3m);
        repacked[0].IsNewlyCreated.ShouldBeFalse();
        repacked[1].WarehouseId.ShouldBe(wh2);
        repacked[1].BatchNo.ShouldBe("B2");
        repacked[1].Uom.ShouldBe("Box");
        repacked[1].Qty.ShouldBe(2m);
        repacked[1].IsNewlyCreated.ShouldBeTrue();
    }

    [Fact]
    public void Pr68529_MappedPurchaseInvoiceApplyTds_PreservesOnLoadWhenSupplierHasWithholding()
    {
        // Mapped PI (forValidate = false) with category -> sets ApplyTds and PreserveApplyTdsOnLoad
        var (apply1, onload1) = PurchaseInvoiceManager.ResolveMappedInvoiceApplyTds(
            currentApplyTds: false, taxWithholdingCategory: "TDS-10", taxWithholdingGroup: null, forValidate: false);
        apply1.ShouldBeTrue();
        onload1.ShouldBeTrue();

        // Mapped PI with withholding group -> sets both
        var (apply2, onload2) = PurchaseInvoiceManager.ResolveMappedInvoiceApplyTds(
            currentApplyTds: false, taxWithholdingCategory: null, taxWithholdingGroup: "WHT-MY", forValidate: false);
        apply2.ShouldBeTrue();
        onload2.ShouldBeTrue();

        // During validate (forValidate = true) -> preserves user toggle without onload override
        var (apply3, onload3) = PurchaseInvoiceManager.ResolveMappedInvoiceApplyTds(
            currentApplyTds: false, taxWithholdingCategory: "TDS-10", taxWithholdingGroup: null, forValidate: true);
        apply3.ShouldBeFalse();
        onload3.ShouldBeFalse();
    }

    [Fact]
    public void Pr67892_JobCardCompleteTimeLogs_ValidatesFromToOrderAndRejectsOverlaps()
    {
        var jc = new JobCard(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10m, 1);
        jc.StartTimeLog(new DateTime(2026, 5, 1, 10, 0, 0));

        // Closing earlier than FromTime must throw
        var exOrder = Should.Throw<BusinessException>(() =>
            jc.CompleteTimeLogs(new DateTime(2026, 5, 1, 9, 30, 0), 2m));
        exOrder.Data["detail"]!.ToString()!.ShouldContain("To Time cannot be before From Time");

        // Valid close succeeds
        jc.CompleteTimeLogs(new DateTime(2026, 5, 1, 11, 0, 0), 5m);
        jc.CompletedQty.ShouldBe(5m);

        // Starting a log that overlaps [10:00, 11:00] and trying to close it must throw
        jc.StartTimeLog(new DateTime(2026, 5, 1, 10, 30, 0));
        var exOverlap = Should.Throw<BusinessException>(() =>
            jc.CompleteTimeLogs(new DateTime(2026, 5, 1, 11, 30, 0), 3m));
        exOverlap.Data["detail"]!.ToString()!.ShouldContain("overlaps");
    }

    [Fact]
    public void Pr67977_Pr67978_Pr67984_Pr68065_ProductionPlanExcludesClosedSalesOrderRowsAndClearsEmptyFetch()
    {
        var companyId = Guid.NewGuid();
        var otherCompanyId = Guid.NewGuid();
        var so1 = Guid.NewGuid();
        var openRowId = Guid.NewGuid();
        var closedRowId = Guid.NewGuid();

        var candidates = new[]
        {
            new OpenSalesOrderPlanCandidate(so1, openRowId, companyId, true, "To Deliver and Bill", false, 10m, 4m),
            new OpenSalesOrderPlanCandidate(so1, closedRowId, companyId, true, "To Deliver and Bill", true, 10m, 0m),
            new OpenSalesOrderPlanCandidate(Guid.NewGuid(), Guid.NewGuid(), companyId, true, "Closed", false, 10m, 0m),
            new OpenSalesOrderPlanCandidate(Guid.NewGuid(), Guid.NewGuid(), companyId, true, "Stopped", false, 10m, 0m),
            new OpenSalesOrderPlanCandidate(Guid.NewGuid(), Guid.NewGuid(), otherCompanyId, true, "To Deliver and Bill", false, 10m, 0m),
            new OpenSalesOrderPlanCandidate(Guid.NewGuid(), Guid.NewGuid(), companyId, true, "To Deliver and Bill", false, 10m, 10m),
        };

        var openRows = ProductionPlanMaterialPlanningService.FilterOpenSalesOrderRowsForProductionPlan(companyId, candidates);
        openRows.Count.ShouldBe(1);
        openRows[0].SalesOrderItemId.ShouldBe(openRowId);

        // Packed items match strictly by ParentSalesOrderItemId and skip closed parent rows (PR #67977)
        var closureMap = new Dictionary<Guid, bool>
        {
            [openRowId] = false,
            [closedRowId] = true,
        };
        var packed = new[]
        {
            new OpenPackedItemPlanCandidate(Guid.NewGuid(), openRowId, Guid.NewGuid(), 5m, true),
            new OpenPackedItemPlanCandidate(Guid.NewGuid(), closedRowId, Guid.NewGuid(), 5m, true),
            new OpenPackedItemPlanCandidate(Guid.NewGuid(), openRowId, Guid.NewGuid(), 0m, true),
            new OpenPackedItemPlanCandidate(Guid.NewGuid(), openRowId, Guid.NewGuid(), 5m, false),
        };
        var openPacked = ProductionPlanMaterialPlanningService.FilterOpenPackedItemsForProductionPlan(closureMap, packed);
        openPacked.Count.ShouldBe(1);
        openPacked[0].ParentSalesOrderItemId.ShouldBe(openRowId);

        // Empty Get Sales Orders clears stale rows (PR #67978)
        var existing = new List<Guid> { so1 };
        ProductionPlanMaterialPlanningService.ReplaceProductionPlanSalesOrders(existing, Array.Empty<Guid>());
        existing.ShouldBeEmpty();
    }

    [Fact]
    public void Pr67699_StockReconciliationSelectedBundle_KeepsExplicitZeroRateOnlyWhenAllowed()
    {
        // Explicit 0 with allowZeroValuationRate = true -> keeps 0
        MyERP.Inventory.Entities.StockReconciliation.ResolveSelectedBundleValuationRate(
            valuationRate: 0m, currentValuationRate: 45m, allowZeroValuationRate: true).ShouldBe(0m);

        // 0 without allowZeroValuationRate -> falls back to currentValuationRate (45)
        MyERP.Inventory.Entities.StockReconciliation.ResolveSelectedBundleValuationRate(
            valuationRate: 0m, currentValuationRate: 45m, allowZeroValuationRate: false).ShouldBe(45m);

        // Null valuationRate even with allowZeroValuationRate -> falls back to currentValuationRate
        MyERP.Inventory.Entities.StockReconciliation.ResolveSelectedBundleValuationRate(
            valuationRate: null, currentValuationRate: 45m, allowZeroValuationRate: true).ShouldBe(45m);

        // Explicit positive rate -> preserved
        MyERP.Inventory.Entities.StockReconciliation.ResolveSelectedBundleValuationRate(
            valuationRate: 52.5m, currentValuationRate: 45m, allowZeroValuationRate: false).ShouldBe(52.5m);
    }

    [Fact]
    public void Pr68371_Pr68578_Pr68473_AccountLookupCompanyDefaultAndEventsPermissionGuards_Parity()
    {
        var allowedCo = Guid.NewGuid();
        var deniedCo = Guid.NewGuid();
        var allowedMop = Guid.NewGuid();
        var deniedMop = Guid.NewGuid();

        // PR #68371: bank/cash account lookup checks both Company and Mode of Payment
        TransactionValidationService.ValidateCompanyAndModeOfPaymentAccess(
            allowedCo, allowedMop, new[] { allowedCo }, new[] { allowedMop });
        Should.Throw<BusinessException>(() =>
            TransactionValidationService.ValidateCompanyAndModeOfPaymentAccess(
                deniedCo, allowedMop, new[] { allowedCo }, new[] { allowedMop }));
        Should.Throw<BusinessException>(() =>
            TransactionValidationService.ValidateCompanyAndModeOfPaymentAccess(
                allowedCo, deniedMop, new[] { allowedCo }, new[] { allowedMop }));

        // PR #68578: get_company_default enforces Company read permission + missing-default guard
        TransactionValidationService.ResolveCompanyDefaultWithPermissionCheck(
            allowedCo, "default_cash_account", "Cash - MY", new[] { allowedCo }).ShouldBe("Cash - MY");
        Should.Throw<BusinessException>(() =>
            TransactionValidationService.ResolveCompanyDefaultWithPermissionCheck(
                deniedCo, "default_cash_account", "Cash - MY", new[] { allowedCo }));
        Should.Throw<BusinessException>(() =>
            TransactionValidationService.ResolveCompanyDefaultWithPermissionCheck(
                allowedCo, "default_cash_account", null, new[] { allowedCo }, ignoreValidation: false));
        TransactionValidationService.ResolveCompanyDefaultWithPermissionCheck(
            allowedCo, "default_cash_account", null, new[] { allowedCo }, ignoreValidation: true).ShouldBeNull();

        // PR #68473: Sales Order calendar events require Sales Order read permission
        TransactionValidationService.ValidateSalesOrderEventsReadPermission(true);
        Should.Throw<BusinessException>(() =>
            TransactionValidationService.ValidateSalesOrderEventsReadPermission(false));
    }

    [Fact]
    public void Pr67789_MaterialRequestRates_PreservedOnFirstSaveRefreshedOnPriceListChange()
    {
        // First save (no previous doc) -> must NOT overwrite typed item rates
        MaterialRequestManager.ShouldRefreshMaterialRequestItemRatesOnSave(
            hasPreviousSavedDoc: false, previousBuyingPriceList: null, currentBuyingPriceList: "Standard Buying")
            .ShouldBeFalse();

        // Update with unchanged buying price list -> no refresh
        MaterialRequestManager.ShouldRefreshMaterialRequestItemRatesOnSave(
            hasPreviousSavedDoc: true, previousBuyingPriceList: "Standard Buying", currentBuyingPriceList: "Standard Buying")
            .ShouldBeFalse();

        // Update with changed buying price list -> refresh rates
        MaterialRequestManager.ShouldRefreshMaterialRequestItemRatesOnSave(
            hasPreviousSavedDoc: true, previousBuyingPriceList: "Standard Buying", currentBuyingPriceList: "Wholesale Buying")
            .ShouldBeTrue();
    }

    [Fact]
    public void Pr67432_ItemLastPurchaseVoucher_PicksLatestAcrossPoPrPiExcludingFreeAndInternal()
    {
        var candidates = new[]
        {
            new MyERP.Inventory.Entities.LastPurchaseVoucherCandidate("Purchase Order", "PO-001", new DateTime(2026, 4, 1), 10m, 1m),
            new MyERP.Inventory.Entities.LastPurchaseVoucherCandidate("Purchase Receipt", "PR-001", new DateTime(2026, 4, 5), 11m, 1m),
            new MyERP.Inventory.Entities.LastPurchaseVoucherCandidate("Purchase Invoice", "PI-001", new DateTime(2026, 4, 10), 12m, 1m),
            // Newer free item and internal supplier rows must be ignored
            new MyERP.Inventory.Entities.LastPurchaseVoucherCandidate("Purchase Invoice", "PI-FREE", new DateTime(2026, 4, 15), 0m, 1m, IsFreeItem: true),
            new MyERP.Inventory.Entities.LastPurchaseVoucherCandidate("Purchase Invoice", "PI-INT", new DateTime(2026, 4, 16), 99m, 1m, IsInternalSupplier: true),
        };

        var latest = MyERP.Inventory.Entities.Item.ResolveLatestPurchaseVoucher(candidates);
        latest.ShouldNotBeNull();
        latest!.VoucherNumber.ShouldBe("PI-001");

        // Same date tie-breaker: Purchase Order beats Purchase Receipt beats Purchase Invoice
        var sameDay = new[]
        {
            new MyERP.Inventory.Entities.LastPurchaseVoucherCandidate("Purchase Invoice", "PI-SAME", new DateTime(2026, 5, 1), 12m, 1m),
            new MyERP.Inventory.Entities.LastPurchaseVoucherCandidate("Purchase Receipt", "PR-SAME", new DateTime(2026, 5, 1), 11m, 1m),
            new MyERP.Inventory.Entities.LastPurchaseVoucherCandidate("Purchase Order", "PO-SAME", new DateTime(2026, 5, 1), 10m, 1m),
        };
        MyERP.Inventory.Entities.Item.ResolveLatestPurchaseVoucher(sameDay)!.VoucherNumber.ShouldBe("PO-SAME");
    }

    [Fact]
    public void Pr67400_Pr67399_SubcontractingReceiptSecondaryRowsAndLandedCostFilter_Parity()
    {
        var fg1 = Guid.NewGuid();
        var fg2 = Guid.NewGuid();
        var sec1 = Guid.NewGuid();
        var orphanSec = Guid.NewGuid();

        var validRows = new (Guid RowId, int RowIdx, string ItemCode, string? SecondaryItemType, SecondaryItemValuationType? ValuationType, Guid? ReferenceRowId)[]
        {
            (fg1, 1, "FG-001", null, null, null),
            (sec1, 2, "SCRAP-001", "Scrap", SecondaryItemValuationType.ValuationRate, fg1),
            (fg2, 3, "FG-002", null, null, null),
        };

        // PR #67400: valid secondary row passes
        SubcontractingManager.ValidateNoOrphanSecondaryItemsOnReceipt(validRows);

        // PR #67400: removing FG-001 also removes its secondary row SCRAP-001, leaving FG-002
        var afterRemove = SubcontractingManager.RemoveFinishedGoodAndItsSecondaryRows(
            validRows, fg1, r => r.RowId, r => r.ReferenceRowId);
        afterRemove.Count.ShouldBe(1);
        afterRemove[0].RowId.ShouldBe(fg2);

        // PR #67400: orphan secondary row whose FG row is missing throws
        var orphanRows = new (Guid RowId, int RowIdx, string ItemCode, string? SecondaryItemType, SecondaryItemValuationType? ValuationType, Guid? ReferenceRowId)[]
        {
            (fg2, 1, "FG-002", null, null, null),
            (orphanSec, 2, "SCRAP-ORPHAN", "Scrap", SecondaryItemValuationType.ValuationRate, fg1),
        };
        var ex = Should.Throw<BusinessException>(() =>
            SubcontractingManager.ValidateNoOrphanSecondaryItemsOnReceipt(orphanRows));
        ex.Data["detail"]!.ToString()!.ShouldContain("SCRAP-ORPHAN");

        // PR #67399: Landed Cost Voucher spreads over Finished Good rows only (excludes secondary rows)
        var lcvEligible = MyERP.Inventory.Entities.LandedCostVoucher.FilterSubcontractingReceiptItemsForLandedCost(
            validRows, r => r.SecondaryItemType, r => r.ValuationType);
        lcvEligible.Count.ShouldBe(2);
        lcvEligible.ShouldAllBe(r => r.RowId == fg1 || r.RowId == fg2);
    }

    [Fact]
    public void Pr67364_SubcontractingOrderTransferStatus_RequiresEachRmItemFullySupplied()
    {
        var rmA = Guid.NewGuid();
        var rmB = Guid.NewGuid();

        // Nothing supplied -> Open
        SubcontractingManager.ResolveSubcontractingTransferStatus(
            new[] { (rmA, 10m, 0m), (rmB, 10m, 0m) }).ShouldBe("Open");

        // RM A over-supplied (15/10), RM B short (5/10): total sum 20 >= 20, yet RM B is short -> Partial Material Transferred
        SubcontractingManager.ResolveSubcontractingTransferStatus(
            new[] { (rmA, 10m, 15m), (rmB, 10m, 5m) }).ShouldBe("Partial Material Transferred");

        // Split rows for RM A sum to 10/10 and RM B is 10/10 -> Material Transferred
        SubcontractingManager.ResolveSubcontractingTransferStatus(
            new[] { (rmA, 6m, 4m), (rmA, 4m, 6m), (rmB, 10m, 10m) }).ShouldBe("Material Transferred");
    }

    [Fact]
    public void Pr68348_RfqItemsAgainstMaterialRequest_ValidatesCompanyItemUomAllowsDuplicateMrItem()
    {
        var co1 = Guid.NewGuid();
        var co2 = Guid.NewGuid();
        var mr1 = Guid.NewGuid();
        var mrItem1 = Guid.NewGuid();
        var itemA = Guid.NewGuid();
        var itemB = Guid.NewGuid();

        var mrCompanies = new Dictionary<Guid, Guid> { [mr1] = co1 };
        var mrItems = new Dictionary<Guid, (Guid ItemId, string Uom)> { [mrItem1] = (itemA, "Nos") };

        // Duplicate RFQ rows referencing same MR Item with matching company/item/UOM pass
        MaterialRequestManager.ValidateRfqItemsAgainstMaterialRequest(
            co1,
            new (int, Guid?, Guid?, Guid, string)[]
            {
                (1, mr1, mrItem1, itemA, "Nos"),
                (2, mr1, mrItem1, itemA, "Nos"),
            },
            mrCompanies,
            mrItems);

        // Wrong company throws
        Should.Throw<BusinessException>(() =>
            MaterialRequestManager.ValidateRfqItemsAgainstMaterialRequest(
                co2, new (int, Guid?, Guid?, Guid, string)[] { (1, mr1, mrItem1, itemA, "Nos") }, mrCompanies, mrItems));

        // Wrong item throws
        Should.Throw<BusinessException>(() =>
            MaterialRequestManager.ValidateRfqItemsAgainstMaterialRequest(
                co1, new (int, Guid?, Guid?, Guid, string)[] { (1, mr1, mrItem1, itemB, "Nos") }, mrCompanies, mrItems));

        // Wrong UOM throws
        Should.Throw<BusinessException>(() =>
            MaterialRequestManager.ValidateRfqItemsAgainstMaterialRequest(
                co1, new (int, Guid?, Guid?, Guid, string)[] { (1, mr1, mrItem1, itemA, "Box") }, mrCompanies, mrItems));
    }

    [Fact]
    public void Pr64596_Pr68232_PartyGroupAdvanceOnlyAccountAndSoAttributedReturnSettlement_Parity()
    {
        var co1 = Guid.NewGuid();
        var advAcc = Guid.NewGuid();

        // PR #64596: Customer/Supplier Group row with only AdvanceAccountId is valid
        TransactionValidationService.ValidatePartyGroupAccountRows(
            new (Guid, Guid?, Guid?)[] { (co1, null, advAcc) });

        // Row with neither AccountId nor AdvanceAccountId throws
        Should.Throw<BusinessException>(() =>
            TransactionValidationService.ValidatePartyGroupAccountRows(
                new (Guid, Guid?, Guid?)[] { (co1, null, null) }));

        // Duplicate company throws
        Should.Throw<BusinessException>(() =>
            TransactionValidationService.ValidatePartyGroupAccountRows(
                new (Guid, Guid?, Guid?)[] { (co1, null, advAcc), (co1, Guid.NewGuid(), null) }));

        // PR #68232: merged invoiced qty map includes both direct DN-invoiced and SO-attributed rows
        var dnRow1 = Guid.NewGuid();
        var dnRow2 = Guid.NewGuid();
        var merged = DeliveryNoteManager.ResolveInvoicedQtyByDeliveryNoteRow(
            new Dictionary<Guid, decimal> { [dnRow1] = 4m },
            new Dictionary<Guid, decimal> { [dnRow2] = 6m });
        merged[dnRow1].ShouldBe(4m);
        merged[dnRow2].ShouldBe(6m);
    }

    [Fact]
    public void Pr67395_Pr67384_JobCardScrapPendingQtyAndRepackManualRates_Parity()
    {
        // PR #67395: Job Card linked -> fgCompletedQty; WO-level -> completed - produced + pendingProcessLoss
        StockEntryManager.CalculatePendingSecondaryQtyForJobCardScrap(
            hasJobCard: true, fgCompletedQty: 4m, completedJobCardQty: 8m, workOrderProducedQty: 2m, pendingProcessLossQty: 2m)
            .ShouldBe(4m);
        StockEntryManager.CalculatePendingSecondaryQtyForJobCardScrap(
            hasJobCard: false, fgCompletedQty: 4m, completedJobCardQty: 8m, workOrderProducedQty: 2m, pendingProcessLossQty: 2m)
            .ShouldBe(8m);

        // PR #67384: Repack with 2 distinct FG items requires EVERY FG row to have SetBasicRateManually == true
        var splitFgRows = new[]
        {
            ("FG-A", true, true),
            ("FG-A", true, false), // second row of FG-A missing manual flag -> must throw!
            ("FG-B", true, true),
        };
        Should.Throw<BusinessException>(() =>
            StockEntryManager.ValidateRepackFinishedGoodsManualRates(splitFgRows));

        var allManual = new[]
        {
            ("FG-A", true, true),
            ("FG-A", true, true),
            ("FG-B", true, true),
        };
        StockEntryManager.ValidateRepackFinishedGoodsManualRates(allManual);
    }

    [Fact]
    public void Pr67378_Pr66971_SameTimestampAdjustmentCancelBlockAndTrustedPosConsolidation_Parity()
    {
        var itemId = Guid.NewGuid();
        var whId = Guid.NewGuid();
        var targetVoucher = Guid.NewGuid();
        var adjVoucher = Guid.NewGuid();
        var postingDt = new DateTime(2026, 5, 10, 12, 0, 0);

        var adjEntries = new[]
        {
            new AdjustmentSleCandidate(itemId, whId, adjVoucher, postingDt, new DateTime(2026, 5, 10, 12, 5, 0), IsAdjustmentEntry: true, IsCancelled: false),
        };

        // PR #67378: cancelling an entry created BEFORE the same-timestamp Adjustment Entry is blocked
        StockLedgerAuditService.HasLaterAdjustmentEntry(
            itemId, whId, targetVoucher, postingDt, isCancellation: true,
            originalEntryEarliestCreationTime: new DateTime(2026, 5, 10, 12, 1, 0), adjEntries)
            .ShouldBeTrue();

        // Cancelling an entry created AFTER the same-timestamp Adjustment Entry is allowed
        StockLedgerAuditService.HasLaterAdjustmentEntry(
            itemId, whId, targetVoucher, postingDt, isCancellation: true,
            originalEntryEarliestCreationTime: new DateTime(2026, 5, 10, 12, 9, 0), adjEntries)
            .ShouldBeFalse();

        // Non-cancellation at same timestamp is not blocked by same-timestamp adjustment
        StockLedgerAuditService.HasLaterAdjustmentEntry(
            itemId, whId, targetVoucher, postingDt, isCancellation: false,
            originalEntryEarliestCreationTime: new DateTime(2026, 5, 10, 12, 1, 0), adjEntries)
            .ShouldBeFalse();

        // PR #66971: client-spoofed isConsolidated without POS merge log flag resets to false and bypasses nothing
        var spoofed = TransactionValidationService.ResolveTrustedPosConsolidationState(
            requestedIsConsolidated: true, isFromPosMergeLog: false);
        spoofed.EffectiveIsConsolidated.ShouldBeFalse();
        spoofed.BypassCustomerBlockCheck.ShouldBeFalse();
        spoofed.BypassModeOfPaymentEnabledCheck.ShouldBeFalse();

        var trusted = TransactionValidationService.ResolveTrustedPosConsolidationState(
            requestedIsConsolidated: true, isFromPosMergeLog: true);
        trusted.EffectiveIsConsolidated.ShouldBeTrue();
        trusted.BypassCustomerBlockCheck.ShouldBeTrue();
        trusted.BypassModeOfPaymentEnabledCheck.ShouldBeTrue();
    }

    [Fact]
    public void Pr67485_Pr67468_Pr66966_ScioOverproducedDeliveryJobCardCancelAndTransitRejected_Parity()
    {
        // PR #67485: SCIO at 100% delivered still allows Subcontracting Delivery when overproduced goods remain
        var items = new[] { (ProducedQty: 12m, DeliveredQty: 10m) };
        SubcontractingManager.CanCreateSubcontractingDeliveryForInwardOrder(
            perProduced: 100m, perDelivered: 100m, allowDeliveryOfOverproducedQty: true, items).ShouldBeTrue();
        SubcontractingManager.CanCreateSubcontractingDeliveryForInwardOrder(
            perProduced: 100m, perDelivered: 100m, allowDeliveryOfOverproducedQty: false, items).ShouldBeFalse();
        SubcontractingManager.CanCreateSubcontractingDeliveryForInwardOrder(
            perProduced: 0m, perDelivered: 0m, allowDeliveryOfOverproducedQty: true, items).ShouldBeFalse();

        // PR #67468: Job Card cancel ignores handed-over pendingQty; remainingCompleted (4) < WO produced (6) throws
        Should.Throw<BusinessException>(() =>
            JobCardManager.ValidateProducedQuantityOnJobCardCancel(
                remainingCompletedQty: 4m, remainingProcessLossQty: 0m, workOrderQty: 10m, operationQty: 10m, workOrderProducedQty: 6m));
        JobCardManager.ValidateProducedQuantityOnJobCardCancel(
            remainingCompletedQty: 6m, remainingProcessLossQty: 0m, workOrderQty: 10m, operationQty: 10m, workOrderProducedQty: 6m);

        // PR #66966: Rejected material staying in the in-transit warehouse does NOT get separate rejected booking
        var transitWh = Guid.NewGuid();
        var rejWh = Guid.NewGuid();
        PurchaseReceiptManager.HasBookedRejectedMaterial(5m, true, transitWh, transitWh).ShouldBeFalse();
        PurchaseReceiptManager.HasBookedRejectedMaterial(5m, true, transitWh, rejWh).ShouldBeTrue();

        // PR #66966: SumStockValueDifferenceForRowInWarehouse sums all active SLEs for the row in the warehouse
        var prId = Guid.NewGuid();
        var rowId = Guid.NewGuid();
        var sles = new[]
        {
            (prId, rowId, transitWh, false, -800m),
            (prId, rowId, transitWh, false, -200m),
            (prId, rowId, transitWh, true, -999m), // cancelled ignored
        };
        PurchaseReceiptManager.SumStockValueDifferenceForRowInWarehouse(prId, rowId, transitWh, sles).ShouldBe(-1000m);
    }

    [Fact]
    public void Pr67480_Pr67450_Pr67445_Pr65791_ManufactureMrStockQty_WoOverConsumedReserve_SoReplacementReserve_DunningPostingDate_Parity()
    {
        var companyId = Guid.NewGuid();
        var mrId = Guid.NewGuid();
        var mrItemOpen = Guid.NewGuid();
        var mrItemDone = Guid.NewGuid();

        // PR #67480: Manufacture MR has qty=2 Box (cf=10 -> stockQty=20), orderedQty=5 (stock UOM, > qty=2!)
        // Must still appear as open with pendingQty = 20 - 5 = 15 (not filtered out by qty > orderedQty).
        var mrCandidates = new[]
        {
            new OpenManufactureMaterialRequestCandidate(
                mrId, mrItemOpen, companyId, IsSubmitted: true, RequestStatus: "Pending",
                IsManufactureType: true, HasBom: true, Qty: 2m, StockQty: 20m, OrderedQty: 5m, PlannedQty: 0m),
            new OpenManufactureMaterialRequestCandidate(
                mrId, mrItemDone, companyId, IsSubmitted: true, RequestStatus: "Ordered",
                IsManufactureType: true, HasBom: true, Qty: 2m, StockQty: 20m, OrderedQty: 20m, PlannedQty: 0m),
        };
        var openMrs = ProductionPlanMaterialPlanningService.FilterOpenManufactureMaterialRequestRows(mrCandidates, companyId);
        openMrs.Count.ShouldBe(1);
        openMrs[0].MaterialRequestItemId.ShouldBe(mrItemOpen);
        openMrs[0].PendingQty.ShouldBe(15m);

        // PR #67450: Work Order with skipTransfer=true consumed past requiredQty reserves 0 (not negative)
        WorkOrderManager.CalculateProductionReservedQtyForItem(
            skipTransfer: true, requiredQty: 10m, transferredQty: 0m, consumedQty: 14m).ShouldBe(0m);
        WorkOrderManager.CalculateProductionReservedQtyForItem(
            skipTransfer: true, requiredQty: 10m, transferredQty: 0m, consumedQty: 4m).ShouldBe(6m);
        WorkOrderManager.CalculateProductionReservedQtyForItem(
            skipTransfer: true, requiredQty: 10m, transferredQty: 0m, consumedQty: 4m, subcontractedQty: 7m).ShouldBe(3m);
        WorkOrderManager.CalculateProductionReservedQtyForItem(
            skipTransfer: false, requiredQty: 10m, transferredQty: 12m, consumedQty: 0m).ShouldBe(0m);

        // PR #67445: SO replacement delivery after return (qty=10, delivered=10, returned=10 -> qty - del - ret = -10)
        // clamps at 0 when dontReserveOnReturn=true so it cannot subtract from another open SO's reserved qty.
        var soRows = new[]
        {
            new SalesOrderReservedQtyRow(
                IsSubmitted: true, OrderStatus: "To Deliver and Bill", DeliveredBySupplier: false, IsRowClosed: false,
                Qty: 10m, StockQty: 10m, DeliveredQty: 10m, ReturnedQty: 10m),
            new SalesOrderReservedQtyRow(
                IsSubmitted: true, OrderStatus: "To Deliver and Bill", DeliveredBySupplier: false, IsRowClosed: false,
                Qty: 5m, StockQty: 10m, DeliveredQty: 0m, ReturnedQty: 0m),
        };
        StockReservationManager.SumSalesOrderReservedStockQty(soRows, dontReserveOnReturn: true).ShouldBe(10m);
        // Without replacement delivery (qty=10, delivered=0, returned=4 -> net 6)
        StockReservationManager.CalculateSalesOrderRowReservedStockQty(
            isSubmitted: true, orderStatus: "To Deliver and Bill", deliveredBySupplier: false, isRowClosed: false,
            qty: 10m, stockQty: 20m, deliveredQty: 0m, returnedQty: 4m, dontReserveOnReturn: true).ShouldBe(12m);

        // PR #65791: Changing Dunning PostingDate updates OverdueDays on every row and recalculates InterestAmount
        var dueDate = new DateTime(2026, 4, 1);
        var dunning = new MyERP.Sales.Entities.Dunning(Guid.NewGuid(), companyId, Guid.NewGuid(), new DateTime(2026, 4, 11), dunningLevel: 1);
        dunning.AddOverduePayment(Guid.NewGuid(), outstandingAmount: 3650m, dueDate: dueDate, overdueDays: 10);
        dunning.CalculateInterest(rateOfInterest: 10m);
        dunning.InterestAmount.ShouldBe(10m); // 3650 * 0.10 / 365 * 10 = 10

        dunning.UpdatePostingDateAndRecalculate(new DateTime(2026, 5, 1), rateOfInterest: 10m);
        dunning.PostingDate.ShouldBe(new DateTime(2026, 5, 1));
        dunning.OverduePayments[0].OverdueDays.ShouldBe(30);
        dunning.InterestAmount.ShouldBe(30m); // 3650 * 0.10 / 365 * 30 = 30
    }

    [Fact]
    public void Pr66299_Pr65961_Pr64998_Pr66292_BatchPickerPrecision_WoLeftoverTransfer_CashFlowYtdLabel_YearlyTimeline_Parity()
    {
        // PR #66299: get_batches_used_in_rows rounds direct row qty to Serial and Batch Entry precision
        var batchId = Guid.NewGuid();
        var whId = Guid.NewGuid();
        var bundleId = Guid.NewGuid();
        var rows = new[]
        {
            new DocumentBatchRowUsage(IsPackedRow: false, SerialAndBatchBundleId: null, BatchId: batchId, WarehouseId: whId, Qty: 1m, StockQty: 3.33336m),
            new DocumentBatchRowUsage(IsPackedRow: true, SerialAndBatchBundleId: null, BatchId: batchId, WarehouseId: whId, Qty: 1.66666m, StockQty: 99m),
            new DocumentBatchRowUsage(IsPackedRow: false, SerialAndBatchBundleId: bundleId, BatchId: null, WarehouseId: whId, Qty: 2m, StockQty: 2m),
        };
        var bundleEntries = new[] { (bundleId, batchId, whId, -2m) };
        var used = SerialBatchBundleValidationService.CalculateBatchesUsedInRows(rows, bundleEntries, entryQtyPrecision: 4);
        // -3.3334 + (-1.6667) + (-2.0000) = -7.0001
        used[(batchId, whId)].ShouldBe(-7.0001m);

        // PR #65961: Material Transfer for Manufacture allows qty=0 when maxTransferableQty <= 0 (leftover items pending)
        var leftover = WorkOrderManager.ResolveMaterialTransferStockEntryCreation(
            isMaterialTransferForManufacture: true, requestedQty: 0m, maxTransferableQty: 0m);
        leftover.CanCreate.ShouldBeTrue();
        leftover.EffectiveQty.ShouldBe(0m);

        var normalZero = WorkOrderManager.ResolveMaterialTransferStockEntryCreation(
            isMaterialTransferForManufacture: true, requestedQty: 0m, maxTransferableQty: 5m);
        normalZero.CanCreate.ShouldBeFalse();

        var manufactureZero = WorkOrderManager.ResolveMaterialTransferStockEntryCreation(
            isMaterialTransferForManufacture: false, requestedQty: 0m, maxTransferableQty: 0m);
        manufactureZero.CanCreate.ShouldBeFalse();

        // PR #64998: Cash Flow YTD label clamps to max(fyStart, rangeStart) when range starts mid-FY
        var fyStart = new DateTime(2026, 1, 1);
        var midYearRangeStart = new DateTime(2026, 4, 1);
        AccountBalanceService.ResolveAccumulatedPeriodLabelStartDate(
            periodFromDate: new DateTime(2026, 6, 1),
            rangeStartDate: midYearRangeStart,
            firstPeriodFromDate: midYearRangeStart,
            fromDateFiscalYearStartDate: fyStart,
            accumulatedValues: true,
            resetPeriodOnFiscalYearChange: true).ShouldBe(midYearRangeStart);

        // When a subsequent FY starts after rangeStart, label resets to that FY's start date
        var nextFyStart = new DateTime(2027, 1, 1);
        AccountBalanceService.ResolveAccumulatedPeriodLabelStartDate(
            periodFromDate: new DateTime(2027, 2, 1),
            rangeStartDate: midYearRangeStart,
            firstPeriodFromDate: midYearRangeStart,
            fromDateFiscalYearStartDate: nextFyStart,
            accumulatedValues: true,
            resetPeriodOnFiscalYearChange: true).ShouldBe(nextFyStart);

        // PR #66292: Account Balance Timeline handles Yearly timegrain without infinite loop
        var yearlyDates = AccountBalanceService.GetTimelineDatesForTimegrain(
            new DateTime(2024, 3, 15), new DateTime(2026, 6, 1), "Yearly");
        yearlyDates.Count.ShouldBe(3);
        yearlyDates[0].ShouldBe(new DateTime(2024, 12, 31));
        yearlyDates[1].ShouldBe(new DateTime(2025, 12, 31));
        yearlyDates[2].ShouldBe(new DateTime(2026, 12, 31));
    }

    [Fact]
    public void Pr64304_Pr65669_Pr66259_Pr66260_ScrReturnRmRates_SelectorMismatch_WoOverproductionPrompt_RejectedReturnUom_Parity()
    {
        // PR #64304: Returned SCR Supplied Item resolves incoming rates from original consumed bundle
        var rmItemId = Guid.NewGuid();
        var origFgRowId = Guid.NewGuid();
        var retFgRowId = Guid.NewGuid();
        var retSupRowId = Guid.NewGuid();
        var origSupRowId = Guid.NewGuid();
        var origBundleId = Guid.NewGuid();
        var batchId = Guid.NewGuid();

        var (serialRates, batchRates) = SubcontractingManager.ResolveReturnedSuppliedItemBundleRates(
            returnedSuppliedItemId: retSupRowId,
            returnedSuppliedItems: new[] { (retSupRowId, retFgRowId, rmItemId, (Guid?)null) },
            returnReceiptItems: new[] { (retFgRowId, (Guid?)origFgRowId) },
            originalSuppliedItems: new[] { (origSupRowId, origFgRowId, rmItemId, (Guid?)origBundleId) },
            originalBundleEntries: new[] { (origBundleId, (string?)"SN-RM-01", (Guid?)batchId, 42.5m) });
        serialRates["SN-RM-01"].ShouldBe(42.5m);
        batchRates[batchId].ShouldBe(42.5m);

        // PR #65669: Selector quantity mismatch compares totalSelectedQty against itemTargetQty (6dp)
        SerialBatchBundleValidationService.RequiresSelectorQuantityMismatchConfirmation(
            totalSelectedQty: 10.0000004m, itemTargetQty: 10m).ShouldBeFalse();
        SerialBatchBundleValidationService.RequiresSelectorQuantityMismatchConfirmation(
            totalSelectedQty: 8m, itemTargetQty: 10m).ShouldBeTrue();

        // PR #66259: Work Order prompt defaults to overproduction allowance when max == 0, omits allowance when explicit qty passed
        var onlyAllowanceLeft = WorkOrderManager.ResolveManufacturePromptQuantities(
            isManufacturePurpose: true, baseMaxQty: 0m, workOrderQty: 100m, overproductionPercentage: 10m);
        onlyAllowanceLeft.AllowedQty.ShouldBe(10m);
        onlyAllowanceLeft.DefaultQty.ShouldBe(10m);
        onlyAllowanceLeft.DefaultFinishedGoodQty.ShouldBe(10m);

        var explicitCap = WorkOrderManager.ResolveManufacturePromptQuantities(
            isManufacturePurpose: true, baseMaxQty: 5m, workOrderQty: 100m, overproductionPercentage: 10m, hasExplicitCallerQty: true);
        explicitCap.AllowedQty.ShouldBe(5m);
        explicitCap.DefaultQty.ShouldBe(5m);

        // PR #66260: Rejected return bundle converts rejectedQty by conversionFactor; returnFromRejectedWarehouse uses stockQty
        var whMain = Guid.NewGuid();
        var whRej = Guid.NewGuid();
        var normalRejected = SerialBatchBundleValidationService.ResolveReturnRejectedBundleQtyAndWarehouse(
            returnQtyFromRejectedWarehouse: false, stockQty: 0m, rejectedQty: -2m, conversionFactor: 12m,
            warehouseId: whMain, rejectedWarehouseId: whRej);
        normalRejected.BundleStockQty.ShouldBe(-24m);
        normalRejected.WarehouseId.ShouldBe(whRej);

        var fromRejectedWh = SerialBatchBundleValidationService.ResolveReturnRejectedBundleQtyAndWarehouse(
            returnQtyFromRejectedWarehouse: true, stockQty: -36m, rejectedQty: 0m, conversionFactor: 12m,
            warehouseId: whMain, rejectedWarehouseId: whRej);
        fromRejectedWh.BundleStockQty.ShouldBe(-36m);
        fromRejectedWh.WarehouseId.ShouldBe(whMain);
    }

    [Fact]
    public void Pr65637_Pr65601_Pr65444_Pr65650_WholeStockUomMr_PickListKeptSerialRow_MovedEntriesDeduct_StaleBatchRowStockQty_Parity()
    {
        // PR #65637: Fractional stock qty under whole-number stock UOM falls back to stock UOM (cf=1, ceil qty)
        var fixedRow = ProductionPlanMaterialPlanningService.KeepMaterialRequestStockQtyWhole(
            rowUom: "Box", conversionFactor: 0.5m, quantity: 1.5m, stockUom: "Nos",
            stockUomMustBeWholeNumber: true, requiredStockQty: 0.75m, isTransfer: false);
        fixedRow.Uom.ShouldBe("Nos");
        fixedRow.ConversionFactor.ShouldBe(1m);
        fixedRow.Quantity.ShouldBe(1m);

        // Transfer request leaves row untouched
        var transferRow = ProductionPlanMaterialPlanningService.KeepMaterialRequestStockQtyWhole(
            rowUom: "Box", conversionFactor: 0.5m, quantity: 1.5m, stockUom: "Nos",
            stockUomMustBeWholeNumber: true, requiredStockQty: 0.75m, isTransfer: true);
        transferRow.Uom.ShouldBe("Box");
        transferRow.Quantity.ShouldBe(1.5m);

        // PR #65601: Pending Pick List row whose serials overlap keptSerialNos wins over earlier pending row
        var loc1 = Guid.NewGuid();
        var loc2 = Guid.NewGuid();
        var picked = PickListManager.SelectPickListRowForPackedItem(
            new (Guid, decimal, decimal, IReadOnlyList<string>?)[]
            {
                (loc1, 2m, 0m, new[] { "SN-001", "SN-002" }),
                (loc2, 2m, 0m, new[] { "SN-003", "SN-004" }),
            },
            keptSerialNos: new[] { "sn-004" });
        picked.ShouldBe(loc2);

        // PR #65444: RemoveDeliveredSerialAndBatchEntries strips moved serials and deducts moved batch qty
        var batchA = Guid.NewGuid();
        var remaining = PickListManager.RemoveDeliveredSerialAndBatchEntries(
            packageEntries: new (string?, Guid?, decimal)[]
            {
                ("SN-1", null, -1m),
                ("SN-2", null, -1m),
                (null, batchA, -10m),
            },
            movedEntries: new (string?, Guid?, decimal)[]
            {
                ("SN-1", null, -1m),
                (null, batchA, -6m),
            });
        remaining.Count.ShouldBe(2);
        remaining[0].ShouldBe(("SN-2", (Guid?)null, -1m));
        remaining[1].ShouldBe(((string?)null, (Guid?)batchA, -4m));

        // PR #65650: Row whose UOM matches stock UOM ignores stale conversionFactor (12) and returns rowQty (3)
        ItemDetailsResolverService.ResolveRowStockQtyForBatchFilter(
            rowQty: 3m, rowUom: "Nos", rowConversionFactor: 12m, itemStockUom: "Nos", itemSalesUom: "Box", uomConversionFactor: 12m)
            .ShouldBe(3m);
        // Blank rowUom falls back to salesUom ("Box") with uomConversionFactor (12) -> 3 * 12 = 36
        ItemDetailsResolverService.ResolveRowStockQtyForBatchFilter(
            rowQty: 3m, rowUom: null, rowConversionFactor: null, itemStockUom: "Nos", itemSalesUom: "Box", uomConversionFactor: 12m)
            .ShouldBe(36m);
    }
}
