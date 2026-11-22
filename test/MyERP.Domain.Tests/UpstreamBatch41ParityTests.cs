using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Core.DomainServices;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing;
using MyERP.Manufacturing.DomainServices;
using MyERP.Manufacturing.Entities;
using MyERP.Purchasing.DomainServices;
using MyERP.Sales.DomainServices;
using Shouldly;
using Xunit;

namespace MyERP;

/// <summary>
/// Parity tests for upstream ERPNext PRs #67149, #67334, #67381, #67382, #67396, #67397,
/// #67402, #67406, #67407, #67430, #67447, #67449, #67460, #67461, #67471, #67483.
/// </summary>
public class UpstreamBatch41ParityTests
{
    [Fact]
    public void Pr67407_Bom_ClearingProcessLossPercentage_ResetsProcessLossQty()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-67407", Guid.NewGuid())
        {
            Quantity = 10m,
            ProcessLossPercentage = 15m
        };
        bom.ProcessLossQty.ShouldBe(1.5m);
        bom.GoodQuantity.ShouldBe(8.5m);

        var sec = new BomSecondaryItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), SecondaryItemType.ByProduct, 4m)
        {
            ProcessLossPercentage = 25m
        };
        sec.ProcessLossQty.ShouldBe(1m);

        bom.ProcessLossPercentage = 0m;
        sec.ProcessLossPercentage = 0m;

        bom.ProcessLossQty.ShouldBe(0m);
        bom.GoodQuantity.ShouldBe(10m);
        sec.ProcessLossQty.ShouldBe(0m);
    }

    [Fact]
    public void Pr67334_WorkOrder_OperatingCost_UsesWorkstationTypeComponents_AndKeepsOpsWithoutComponents()
    {
        var opWithType = Guid.NewGuid();
        var opNoComponents = Guid.NewGuid();
        var opZeroRateComponents = Guid.NewGuid();
        var wsTypeId = Guid.NewGuid();
        var wsZeroId = Guid.NewGuid();
        var elecAcc = Guid.NewGuid();
        var rentAcc = Guid.NewGuid();
        var defaultAcc = Guid.NewGuid();

        var ops = new[]
        {
            new WorkOrderOperationCostInput(opWithType, "Machining", WorkstationId: null, WorkstationTypeId: wsTypeId, ActualOperationTime: 60m, ActualOperatingCost: 100m),
            new WorkOrderOperationCostInput(opNoComponents, "Manual Polish", WorkstationId: null, WorkstationTypeId: null, ActualOperationTime: 30m, ActualOperatingCost: 45m),
            new WorkOrderOperationCostInput(opZeroRateComponents, "Inspection", WorkstationId: wsZeroId, WorkstationTypeId: null, ActualOperationTime: 20m, ActualOperatingCost: 25m),
        };

        var result = WorkOrderManager.CalculateOperationOperatingCosts(
            ops,
            getWorkstationComponents: id => id == wsZeroId
                ? new[] { ("ZeroComp", 0m, (Guid?)null) }
                : Array.Empty<(string, decimal, Guid?)>(),
            getWorkstationTypeComponents: id => id == wsTypeId
                ? new[] { ("Electricity", 30m, (Guid?)elecAcc), ("Rent", 70m, (Guid?)rentAcc) }
                : Array.Empty<(string, decimal, Guid?)>(),
            defaultOperatingExpenseAccountId: defaultAcc);

        result.Count.ShouldBe(4);
        result.ShouldContain(r => r.OperationId == opWithType && r.IsComponentWise && r.OperatingComponent == "Electricity" && r.Amount == 30m && r.ExpenseAccountId == elecAcc);
        result.ShouldContain(r => r.OperationId == opWithType && r.IsComponentWise && r.OperatingComponent == "Rent" && r.Amount == 70m && r.ExpenseAccountId == rentAcc);
        result.ShouldContain(r => r.OperationId == opNoComponents && !r.IsComponentWise && r.Amount == 45m && r.ExpenseAccountId == defaultAcc);
        result.ShouldContain(r => r.OperationId == opZeroRateComponents && !r.IsComponentWise && r.Amount == 25m && r.ExpenseAccountId == defaultAcc);
    }

    [Fact]
    public void Pr67447_JobCard_RunningTimeLog_OccupiesWorkstationSlot_AndQtyOnlyLogSavesBesideRunning()
    {
        var companyId = Guid.NewGuid();
        var woId = Guid.NewGuid();
        var opId = Guid.NewGuid();
        var t0 = new DateTime(2026, 11, 22, 9, 0, 0);
        var t1 = new DateTime(2026, 11, 22, 10, 0, 0);
        var t2 = new DateTime(2026, 11, 22, 11, 0, 0);

        var runningCard = new JobCard(Guid.NewGuid(), companyId, woId, opId, 5m, 1);
        runningCard.Start();
        // Open/running time log: FromTime set, ToTime == default
        runningCard.StartTimeLog(t0);

        // Single-capacity workstation: running card overlaps later candidate window
        var logs = JobCardManager.ExtractOverlappingLogs(new[] { runningCard }, t1, t2, includeRunning: true);
        logs.Count.ShouldBe(1);
        logs[0].ToTime.ShouldBe(JobCardManager.RunningTimeLogEnd);
        JobCardManager.HasCapacityOverlap(productionCapacity: 1, logs).ShouldBeTrue();

        // Multi-capacity (2 slots): one running card takes 1 slot, leaving 1 slot free
        JobCardManager.HasCapacityOverlap(productionCapacity: 2, logs).ShouldBeFalse();

        // Two running cards occupy both slots of capacity-2 workstation
        var runningCard2 = new JobCard(Guid.NewGuid(), companyId, woId, opId, 5m, 1);
        runningCard2.Start();
        runningCard2.StartTimeLog(t0.AddMinutes(10));
        var twoLogs = JobCardManager.ExtractOverlappingLogs(new[] { runningCard, runningCard2 }, t1, t2, includeRunning: true);
        JobCardManager.HasCapacityOverlap(productionCapacity: 2, twoLogs).ShouldBeTrue();

        // Qty-only time log (FromTime == default) skips overlap check and saves beside running job card
        var qtyOnlyLogs = JobCardManager.ExtractOverlappingLogs(new[] { runningCard }, default, default, includeRunning: true);
        qtyOnlyLogs.ShouldBeEmpty();
    }

    [Fact]
    public void Pr67396_ProductionPlan_PlannedSubAssembliesExplodeComponents_AndDoNotExplodeLineIsRequested()
    {
        var planItemId = Guid.NewGuid();
        var subItemId = Guid.NewGuid();
        var subBomId = Guid.NewGuid();
        var subContractItemId = Guid.NewGuid();
        var subContractBomId = Guid.NewGuid();
        var rawItemId = Guid.NewGuid();

        var (requested, planned) = ProductionPlanMaterialPlanningService.ClassifySubAssembliesByPlanRow(new[]
        {
            (planItemId, subItemId, (Guid?)subBomId, SubAssemblyType.InHouseManufacturing),
            (planItemId, subContractItemId, (Guid?)subContractBomId, SubAssemblyType.Subcontracting),
        });

        var lines = new[]
        {
            // Line 1: planned InHouse sub-assembly (with line BOM) -> explodes, NOT requested as raw material
            (1, subItemId, false, false, false, 2m, "SubAsm", (Guid?)null, (Guid?)subBomId, (Guid?)subBomId),
            // Line 2: DoNotExplode line of same sub-assembly (no line BOM) -> NOT matched as planned, requested directly
            (2, subItemId, true, false, false, 1m, "SubAsm Direct", (Guid?)null, (Guid?)null, (Guid?)subBomId),
            // Line 3: planned Subcontract sub-assembly -> explodes when includeSubcontractedItems = true, skipped when false
            (3, subContractItemId, false, false, true, 1m, "SubCon", (Guid?)null, (Guid?)subContractBomId, (Guid?)subContractBomId),
            // Line 4: plain raw material
            (4, rawItemId, false, false, false, 3m, "Steel", (Guid?)null, (Guid?)null, (Guid?)null),
        };

        var withSubcontract = ProductionPlanMaterialPlanningService.EvaluateProductionPlanBomSubitemGroups(
            includeExplodedItems: true,
            includeSubcontractedItems: true,
            bomLines: lines,
            plannedParentQty: 5m,
            requestedSubAssemblies: requested.GetValueOrDefault(planItemId),
            plannedSubAssemblies: planned.GetValueOrDefault(planItemId));

        withSubcontract.ShouldContain(r => r.ItemId == subItemId && !r.DoNotExplode && r.ChildBomIdToExplode == subBomId && !r.ShouldMergeIntoRawMaterials);
        withSubcontract.ShouldContain(r => r.ItemId == subItemId && r.DoNotExplode && r.ChildBomIdToExplode == null && r.ShouldMergeIntoRawMaterials && r.Qty == 5m);
        withSubcontract.ShouldContain(r => r.ItemId == subContractItemId && r.ChildBomIdToExplode == subContractBomId && !r.ShouldMergeIntoRawMaterials);

        var withoutSubcontract = ProductionPlanMaterialPlanningService.EvaluateProductionPlanBomSubitemGroups(
            includeExplodedItems: true,
            includeSubcontractedItems: false,
            bomLines: lines,
            plannedParentQty: 5m,
            requestedSubAssemblies: requested.GetValueOrDefault(planItemId),
            plannedSubAssemblies: planned.GetValueOrDefault(planItemId));

        withoutSubcontract.ShouldNotContain(r => r.ItemId == subContractItemId);
    }

    [Fact]
    public void Pr67406_And_Pr67430_StockEntry_JobCardPendingQty_AndWholeNumberFloatNoiseRounding()
    {
        var bomItem = Guid.NewGuid();
        var altItem = Guid.NewGuid();
        var mfgEntry1 = Guid.NewGuid();

        // Same item used by two operations -> mfgEntry1 recorded on both alternative/warehouse rows, counted ONCE (fgQty = 2)
        var available = new (Guid ItemId, Guid? OriginalItemId, IReadOnlyDictionary<Guid, decimal> ConsumedFor)[]
        {
            (bomItem, null, new Dictionary<Guid, decimal> { [mfgEntry1] = 2m }),
            (altItem, bomItem, new Dictionary<Guid, decimal> { [mfgEntry1] = 2m }),
        };

        // WO qty = 10, required = 20, transferred = 10 -> transferred_for_qty = 5 -> pending = 5 - 2 = 3
        var pending = StockEntryManager.CalculateJobCardMaterialPendingQty(
            bomItemId: bomItem,
            workOrderQty: 10m,
            workOrderTransferredQty: 10m,
            totalRequiredQtyForItem: 20m,
            totalTransferredQtyForItem: 10m,
            availableMaterials: available);
        pending.ShouldBe(3m);

        // PR #67430: float noise 7.0000000001 rounds to 7 (NOT ceiled to 8), while 0.004 still ceils to 1
        StockEntryManager.CalculateTransferBasedRawMaterialQty(
            availableWipQty: 7.0000000001m, fgCompletedQty: 1m, pendingQtyToMfg: 1m, isReturn: false, uomMustBeWholeNumber: true)
            .ShouldBe(7m);
        StockEntryManager.CalculateTransferBasedRawMaterialQty(
            availableWipQty: 0.004m, fgCompletedQty: 1m, pendingQtyToMfg: 1m, isReturn: false, uomMustBeWholeNumber: true)
            .ShouldBe(1m);
    }

    [Fact]
    public void Pr67449_SubAssemblyCoProduct_ValuedOnSubAssemblyComponents_AndPhantomIgnoresProcessLoss()
    {
        var phantomBom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-PHANTOM", Guid.NewGuid())
        {
            Quantity = 10m,
            IsPhantomBom = true,
            ProcessLossPercentage = 20m
        };
        phantomBom.GoodQuantity.ShouldBe(8m);
        phantomBom.ExplodedGoodQuantity.ShouldBe(10m);

        var compA = Guid.NewGuid();
        var compB = Guid.NewGuid();
        var consumedRates = new Dictionary<Guid, decimal>
        {
            [compA] = 10m,
            [compB] = 5m
        };

        // Sub-assembly BOM (qty=2, processLoss=0, non-phantom) consumes 3 of compA (30) + 2 of compB (10) = 40 per BOM,
        // minus own-cost scrap value 6 = 34 net per sub-assembly BOM.
        // Making 4 FG units needs 4 sub-assemblies = 2 sub-assembly BOMs -> 80 - 12 = 68 net basis.
        var basis = StockEntryManager.CalculateSubAssemblyComponentCost(
            subAssemblySecondaryQtyPerBom: 1m,
            subAssemblySecondaryQtyPerFinishedGood: 0.5m,
            fgCompletedQty: 4m,
            subAssemblyBomQuantity: 2m,
            subAssemblyBomProcessLossQty: 0m,
            subAssemblyBomIsPhantom: false,
            subAssemblyExplodedComponents: new[] { (compA, 3m), (compB, 2m) },
            consumedRatesByBomItemId: consumedRates,
            subAssemblyOwnCostSecondaryValue: 12m);
        basis.ShouldBe(68m);
    }

    [Fact]
    public void Pr67461_SubcontractingOrder_MappedProcessLoss_WholeNumberRunningCumulative()
    {
        // Whole-number UOM, 15% process loss, 3 partial receipts of 3 units each (total 9 -> round(1.35)=1):
        // Receipt 1 (0 -> 3): round(0.45) - round(0) = 0
        // Receipt 2 (3 -> 6): round(0.90) - round(0.45) = 1 - 0 = 1
        // Receipt 3 (6 -> 9): round(1.35) - round(0.90) = 1 - 1 = 0
        SubcontractingManager.CalculateMappedProcessLossQty(3m, 0m, 15m, mustBeWholeNumber: true).ShouldBe(0m);
        SubcontractingManager.CalculateMappedProcessLossQty(3m, 3m, 15m, mustBeWholeNumber: true).ShouldBe(1m);
        SubcontractingManager.CalculateMappedProcessLossQty(3m, 6m, 15m, mustBeWholeNumber: true).ShouldBe(0m);

        // Fractional UOM keeps per-receipt precision rounding
        SubcontractingManager.CalculateMappedProcessLossQty(3m, 0m, 15m, mustBeWholeNumber: false, precision: 4).ShouldBe(0.45m);
    }

    [Fact]
    public void Pr67460_SubcontractingReceipt_ServiceCostGl_IncludesRejectedAndLoss_AndReversesFullyOnReturn()
    {
        // Receipt: 7 accepted + 2 rejected + 1 process loss = 10 units @ 50 service cost = 500.00
        var receiptServiceGl = SubcontractingManager.CalculateSubcontractingServiceCostForGl(
            serviceCostPerQty: 50m, acceptedQty: 7m, rejectedQty: 2m, processLossQty: 1m, isReturn: false);
        receiptServiceGl.ShouldBe(500.00m);

        // Return of 7 accepted units with original received/accepted ratio = 10/7 reverses full 500.00
        var returnServiceGl = SubcontractingManager.CalculateSubcontractingServiceCostForGl(
            serviceCostPerQty: 50m, acceptedQty: -7m, rejectedQty: 0m, processLossQty: 0m, isReturn: true, returnReceivedQtyRatio: 10m / 7m);
        returnServiceGl.ShouldBe(-500.00m);
    }

    [Fact]
    public void Pr67471_SubcontractingReceipt_SetsRmCostBeforePercentageSecondary_AndValuesAtPostingTime()
    {
        var ownSecRow = Guid.NewGuid();
        var pctSecRow = Guid.NewGuid();
        var scrapItem = Guid.NewGuid();
        var coProdItem = Guid.NewGuid();
        var whId = Guid.NewGuid();

        // FG: 10 accepted, 10 received, RM total = 800 (80/unit), Service = 20/unit -> total 1000
        // Own-cost scrap (qty 2) valued at posting-time warehouse rate 50 -> 100 deducted -> 900 basis
        // Percentage co-product (20% of 900 = 180 across qty 3 -> rate 60/unit)
        // Total secondary = 280 (28/FG unit) -> FG rate = 20 + 80 - 28 = 72
        var result = SubcontractingManager.CalculateReceiptFinishedGoodAndSecondaryRates(
            fgAcceptedQty: 10m,
            fgReceivedQty: 10m,
            suppliedRmTotalAmount: 800m,
            serviceCostPerQty: 20m,
            additionalCostPerQty: 0m,
            lcvAmount: 0m,
            secondaryRows: new[]
            {
                new SubcontractingSecondaryRowInput(ownSecRow, scrapItem, whId, 2m, SecondaryItemValuationType.ValuationRate),
                new SubcontractingSecondaryRowInput(pctSecRow, coProdItem, whId, 3m, SecondaryItemValuationType.PercentageOfComponentCost, CostAllocationPercentage: 20m),
            },
            resolveValuationRateAtPosting: (itemId, _) => itemId == scrapItem ? 50m : 0m);

        result.RmCostPerQty.ShouldBe(80m);
        result.SecondaryRatesByRowId[ownSecRow].Rate.ShouldBe(50m);
        result.SecondaryRatesByRowId[pctSecRow].Rate.ShouldBe(60m);
        result.FinishedGoodRate.ShouldBe(72m);
    }

    [Fact]
    public void Pr67483_And_Pr67402_SubcontractedPlanReceivedQty_AndServicePoQuantityLimit()
    {
        var planSub1 = Guid.NewGuid();
        var planSub2 = Guid.NewGuid();
        var poItem1 = Guid.NewGuid();
        var poItem2 = Guid.NewGuid();

        var receivedByPlanRow = SubcontractingManager.CalculateSubcontractedPlanReceivedQuantities(
            affectedPlanSubAssemblyItemIds: new[] { planSub1, planSub2 },
            submittedPoItems: new[] { (poItem1, planSub1), (poItem2, planSub1) },
            subcontractedReceivedQtyByPoItemId: new Dictionary<Guid, decimal>
            {
                [poItem1] = 4.12346m,
                [poItem2] = 2.00001m,
            },
            precision: 4);

        receivedByPlanRow[planSub1].ShouldBe(6.1235m);
        receivedByPlanRow[planSub2].ShouldBe(0m);

        // PR #67402: non-stock subcontracted service PO row with FgItemId enforces over-receipt limit
        PurchaseOrderManager.IsQuantityLimitEnforcedForPurchaseOrderItem(false, false, fgItemId: Guid.NewGuid()).ShouldBeTrue();
        PurchaseOrderManager.IsQuantityLimitEnforcedForPurchaseOrderItem(false, false, fgItemId: null).ShouldBeFalse();
    }

    [Fact]
    public void Pr67397_ItemPrice_PrefersPartyPriceInStockUom_OverGeneralPriceInRowUom()
    {
        var customerId = Guid.NewGuid();
        var candidates = new[]
        {
            new ItemPriceCandidate(PriceListRate: 120m, Uom: "Box", PackingUnit: 0m, CustomerId: null),
            new ItemPriceCandidate(PriceListRate: 8.5m, Uom: "Unit", PackingUnit: 6m, CustomerId: customerId),
        };

        // Row is 1 Box (1 Box = 12 Units via conversion factor resolver -> stockQty = 12, divisible by packing unit 6)
        // Must pick customer's Unit price (8.5) instead of general Box price (120)
        var resolved = ItemDetailsResolverService.ResolvePriceListRateWithPartyStockUomPriority(
            candidates,
            rowUom: "Box",
            stockUom: "Unit",
            qty: 1m,
            conversionFactor: 0m,
            partyId: customerId,
            isBuying: false,
            resolveConversionFactor: () => 12m);

        resolved.IsPartyPrice.ShouldBeTrue();
        resolved.PriceListRate.ShouldBe(8.5m);
        resolved.MatchedUom.ShouldBe("Unit");
        resolved.EffectiveConversionFactor.ShouldBe(12m);
        resolved.StockQty.ShouldBe(12m);

        // When stockQty does NOT fit party's packing unit (e.g. 5 Units vs packing unit 6), falls back to general Box price
        var fallback = ItemDetailsResolverService.ResolvePriceListRateWithPartyStockUomPriority(
            candidates,
            rowUom: "Box",
            stockUom: "Unit",
            qty: 1m,
            conversionFactor: 5m,
            partyId: customerId,
            isBuying: false);

        fallback.IsPartyPrice.ShouldBeFalse();
        fallback.PriceListRate.ShouldBe(120m);
        fallback.MatchedUom.ShouldBe("Box");
    }

    [Fact]
    public void Pr67381_Pr67382_Pr67149_PickListSerialCap_BundleRate_RejectedSelectorQty_AndDeletionIgnores()
    {
        // PR #67382: Pick List location never offers more qty than its free serial nos
        var whId = Guid.NewGuid();
        var locations = new[]
        {
            new PickLocationAllocation { WarehouseId = whId, Qty = 5m, SerialNumbers = new List<string> { "SN-1", "SN-2", "SN-3", "SN-4", "SN-5" } }
        };
        var picked = new Dictionary<(Guid WarehouseId, Guid? BatchId), (decimal HeldQty, IReadOnlySet<string>? PickedSerialNos)>
        {
            [(whId, null)] = (1m, new HashSet<string> { "SN-1", "SN-2", "SN-3" }) // only 2 free serials left despite heldQty=1
        };
        var filtered = PickListManager.FilterLocationsByPickedMaterials(locations, picked);
        filtered.Count.ShouldBe(1);
        filtered[0].Qty.ShouldBe(2m);
        filtered[0].SerialNumbers.ShouldBe(new[] { "SN-4", "SN-5" });

        // PR #67382: Product bundle rate from components divides by row qty (not stock qty), and returns keep original rate
        var bundleRate = ProductBundleDecompositionService.CalculateProductBundleRateFromComponents(
            parentRowQty: 2m,
            isReturn: false,
            originalParentRate: 999m,
            editableBundleItemRates: true,
            packedComponents: new[] { (15m, 4m), (10m, 2m) }); // 60 + 20 = 80 / 2 = 40 per row UOM
        bundleRate.ShouldBe(40m);

        var returnBundleRate = ProductBundleDecompositionService.CalculateProductBundleRateFromComponents(
            parentRowQty: -2m,
            isReturn: true,
            originalParentRate: 40m,
            editableBundleItemRates: true,
            packedComponents: new[] { (99m, -4m) });
        returnBundleRate.ShouldBe(40m);

        // PR #67381: Serial/batch selector converts rejected qty into stock UOM and rounds at stock qty precision
        SerialBatchBundleValidationService.ResolveSelectorTargetStockQty(
            stockQty: 24m, transferQty: 0m, qty: 2m, isRejected: true, rejectedQty: 1.5m, conversionFactor: 12m, stockQtyPrecision: 4)
            .ShouldBe(18m);

        // PR #67149: Transaction deletion ignore list preserves Communication and Email Account
        var ignored = CompanyRestrictionValidationService.GetDefaultTransactionDeletionIgnoredDoctypes();
        ignored.ShouldContain("Communication");
        ignored.ShouldContain("Email Account");
    }

    [Fact]
    public void Loan_ReverseRepayment_RestoresPrincipalInterestAndReopensFullyRepaidLoan()
    {
        var loan = new MyERP.HumanResources.Entities.Loan(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "LN-001",
            MyERP.HumanResources.LoanType.TermLoan,
            MyERP.HumanResources.InterestCalculationMethod.FlatRate,
            loanAmount: 1000m,
            annualInterestRate: 6m,
            tenureMonths: 2);
        loan.Sanction();
        loan.Disburse(DateTime.Today, DateTime.Today);

        loan.RecordRepayment(500m, 5m);
        loan.Status.ShouldBe(MyERP.HumanResources.LoanStatus.PartiallyRepaid);

        loan.RecordRepayment(500m, 5m);
        loan.Status.ShouldBe(MyERP.HumanResources.LoanStatus.FullyRepaid);
        loan.TotalPrincipalRepaid.ShouldBe(1000m);
        loan.TotalInterestCharged.ShouldBe(10m);
        loan.TotalAmountRepaid.ShouldBe(1010m);

        // Reversing second repayment reopens loan to PartiallyRepaid
        loan.ReverseRepayment(500m, 5m);
        loan.Status.ShouldBe(MyERP.HumanResources.LoanStatus.PartiallyRepaid);
        loan.TotalPrincipalRepaid.ShouldBe(500m);
        loan.TotalInterestCharged.ShouldBe(5m);
        loan.TotalAmountRepaid.ShouldBe(505m);

        // Reversing first repayment restores loan to Disbursed with zero repaid
        loan.ReverseRepayment(500m, 5m);
        loan.Status.ShouldBe(MyERP.HumanResources.LoanStatus.Disbursed);
        loan.TotalPrincipalRepaid.ShouldBe(0m);
        loan.TotalInterestCharged.ShouldBe(0m);
        loan.TotalAmountRepaid.ShouldBe(0m);
    }

    [Fact]
    public void MyInvois_InvoiceDocumentBuilder_NormalizesClassificationForeignPartiesAndConsolidatedCodes()
    {
        var builder = new MyERP.EInvoice.Services.InvoiceDocumentBuilder();
        var data = new MyERP.EInvoice.Services.EInvoiceDocumentData
        {
            InvoiceNumber = "INV-MYINVOIS-01",
            IssueDate = new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc),
            DocumentTypeCode = "01",
            CurrencyCode = "MYR",
            IsConsolidated = false,
            Supplier = new MyERP.EInvoice.Services.EInvoicePartyData
            {
                Name = "Foreign Supplier Pte Ltd",
                Tin = "EI00000000030",
                CountryCode = "SG",
                State = "Singapore",
            },
            Buyer = new MyERP.EInvoice.Services.EInvoicePartyData
            {
                Name = "Overseas Buyer Inc",
                Tin = "",
                CountryCode = "US",
                State = "California",
            },
            Delivery = new MyERP.EInvoice.Services.EInvoiceDeliveryData
            {
                RecipientName = "Overseas Warehouse",
                CountryCode = "GB",
                State = "London",
            },
            NetTotal = 300m,
            TaxAmount = 0m,
            GrandTotal = 300m,
            Lines = new List<MyERP.EInvoice.Services.EInvoiceLineData>
            {
                new() { Description = "Blank Code Item", Quantity = 1m, UnitPrice = 100m, ClassificationCode = "" },
                new() { Description = "Composite Code Item", Quantity = 1m, UnitPrice = 200m, ClassificationCode = "003 : Computer, smartphone or tablet" },
            }
        };

        var xml = System.Xml.Linq.XDocument.Parse(builder.Build(data));
        System.Xml.Linq.XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
        System.Xml.Linq.XNamespace cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";

        // Foreign supplier & delivery get state "17" and ISO3 country codes
        var supplierAddr = xml.Root!.Element(cac + "AccountingSupplierParty")!.Element(cac + "Party")!.Element(cac + "PostalAddress")!;
        supplierAddr.Element(cbc + "CountrySubentityCode")!.Value.ShouldBe("17");
        supplierAddr.Element(cac + "Country")!.Element(cbc + "IdentificationCode")!.Value.ShouldBe("SGP");

        var deliveryAddr = xml.Root!.Element(cac + "Delivery")!.Element(cac + "DeliveryAddress")!;
        deliveryAddr.Element(cbc + "CountrySubentityCode")!.Value.ShouldBe("17");
        deliveryAddr.Element(cac + "Country")!.Element(cbc + "IdentificationCode")!.Value.ShouldBe("GBR");

        // Foreign buyer with blank TIN gets EI00000000030, state "17", country "USA"
        var buyerParty = xml.Root!.Element(cac + "AccountingCustomerParty")!.Element(cac + "Party")!;
        buyerParty.Elements(cac + "PartyIdentification")
            .First(x => x.Element(cbc + "ID")!.Attribute("schemeID")!.Value == "TIN")
            .Element(cbc + "ID")!.Value.ShouldBe("EI00000000030");
        buyerParty.Element(cac + "PostalAddress")!.Element(cbc + "CountrySubentityCode")!.Value.ShouldBe("17");
        buyerParty.Element(cac + "PostalAddress")!.Element(cac + "Country")!.Element(cbc + "IdentificationCode")!.Value.ShouldBe("USA");

        // Blank classification defaults to "022", composite strips description to "003"
        var classCodes = xml.Root!.Elements(cac + "InvoiceLine")
            .Select(l => l.Element(cac + "Item")!.Element(cac + "CommodityClassification")!.Element(cbc + "ItemClassificationCode")!.Value)
            .ToList();
        classCodes.ShouldBe(new[] { "022", "003" });

        // Consolidated invoice forces "004" on every line
        data.IsConsolidated = true;
        var consolidatedXml = System.Xml.Linq.XDocument.Parse(builder.Build(data));
        var consolidatedCodes = consolidatedXml.Root!.Elements(cac + "InvoiceLine")
            .Select(l => l.Element(cac + "Item")!.Element(cac + "CommodityClassification")!.Element(cbc + "ItemClassificationCode")!.Value)
            .ToList();
        consolidatedCodes.ShouldBe(new[] { "004", "004" });
    }
}
