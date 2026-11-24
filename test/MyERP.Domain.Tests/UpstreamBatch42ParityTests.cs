using System;
using System.Collections.Generic;
using MyERP.Core.DomainServices;
using MyERP.Inventory.DomainServices;
using MyERP.Manufacturing;
using MyERP.Manufacturing.DomainServices;
using MyERP.Manufacturing.Entities;
using MyERP.Projects.Entities;
using MyERP.Purchasing.DomainServices;
using MyERP.Sales.DomainServices;
using MyERP.Tax.DomainServices;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP;

/// <summary>
/// Parity tests for upstream ERPNext PRs #68311, #69299, #69301, #69312, #69314,
/// #69315, #69322, #69324, #69327, #69337, #69350, #69351.
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
}
