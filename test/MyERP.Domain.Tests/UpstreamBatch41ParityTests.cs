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

    [Fact]
    public void MyInvois_InvoiceDocumentBuilder_EmitsTaxExchangeRateItemPriceExtensionTaxExemptionAndVersion11()
    {
        var builder = new MyERP.EInvoice.Services.InvoiceDocumentBuilder();
        var usdData = new MyERP.EInvoice.Services.EInvoiceDocumentData
        {
            InvoiceNumber = "INV-USD-01",
            IssueDate = new DateTime(2026, 7, 1, 9, 0, 0, DateTimeKind.Utc),
            DocumentTypeCode = "01",
            DocumentVersion = "1.1",
            CurrencyCode = "USD",
            ExchangeRate = 4.45m,
            Supplier = new MyERP.EInvoice.Services.EInvoicePartyData { Name = "MY Seller Sdn Bhd", Tin = "C1234567890", CountryCode = "MYS", State = "14" },
            Buyer = new MyERP.EInvoice.Services.EInvoicePartyData { Name = "US Buyer LLC", Tin = "EI00000000030", CountryCode = "USA" },
            NetTotal = 250m,
            TaxAmount = 0m,
            GrandTotal = 250m,
            TaxBreakdown = new List<MyERP.EInvoice.Services.EInvoiceTaxBreakdown>
            {
                new() { TaxCategoryCode = "E", TaxRate = 0m, TaxExemptionReason = "Export Goods", TaxableAmount = 250m, TaxAmount = 0m }
            },
            Lines = new List<MyERP.EInvoice.Services.EInvoiceLineData>
            {
                new() { Description = "Export Widget", Quantity = 2m, UnitPrice = 125m, TaxAmount = 0m, TaxCategoryCode = "E", TaxRate = 0m }
            }
        };

        var xml = System.Xml.Linq.XDocument.Parse(builder.Build(usdData));
        System.Xml.Linq.XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
        System.Xml.Linq.XNamespace cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";

        xml.Root!.Element(cbc + "InvoiceTypeCode")!.Attribute("listVersionID")!.Value.ShouldBe("1.1");

        var taxRateElem = xml.Root!.Element(cac + "TaxExchangeRate");
        taxRateElem.ShouldNotBeNull();
        taxRateElem!.Element(cbc + "SourceCurrencyCode")!.Value.ShouldBe("USD");
        taxRateElem.Element(cbc + "TargetCurrencyCode")!.Value.ShouldBe("MYR");
        taxRateElem.Element(cbc + "CalculationRate")!.Value.ShouldBe("4.4500");

        xml.Root!.Element(cac + "TaxTotal")!
            .Element(cac + "TaxSubtotal")!
            .Element(cac + "TaxCategory")!
            .Element(cbc + "TaxExemptionReason")!.Value.ShouldBe("Export Goods");

        var invLine = xml.Root!.Element(cac + "InvoiceLine")!;
        invLine.Element(cac + "TaxTotal")!
            .Element(cac + "TaxSubtotal")!
            .Element(cac + "TaxCategory")!
            .Element(cbc + "TaxExemptionReason")!.Value.ShouldBe("NA");
        invLine.Element(cac + "ItemPriceExtension")!
            .Element(cbc + "Amount")!.Value.ShouldBe("250.00");

        // MYR invoice omits TaxExchangeRate
        usdData.CurrencyCode = "MYR";
        var myrXml = System.Xml.Linq.XDocument.Parse(builder.Build(usdData));
        myrXml.Root!.Element(cac + "TaxExchangeRate").ShouldBeNull();
    }

    [Fact]
    public void MyInvois_InvoiceDocumentBuilder_MapsTaxTypesBuildsBreakdownAndEmitsInvoicePeriod()
    {
        MyERP.EInvoice.Services.InvoiceDocumentBuilder.MapTaxTypeToLhdnCategoryCode(MyERP.Tax.TaxType.Sales, 6m).ShouldBe("01");
        MyERP.EInvoice.Services.InvoiceDocumentBuilder.MapTaxTypeToLhdnCategoryCode(MyERP.Tax.TaxType.Service, 8m).ShouldBe("02");
        MyERP.EInvoice.Services.InvoiceDocumentBuilder.MapTaxTypeToLhdnCategoryCode(MyERP.Tax.TaxType.Exempt, 0m).ShouldBe("E");
        MyERP.EInvoice.Services.InvoiceDocumentBuilder.MapTaxTypeToLhdnCategoryCode(MyERP.Tax.TaxType.ZeroRated, 0m).ShouldBe("06");
        MyERP.EInvoice.Services.InvoiceDocumentBuilder.MapTaxTypeToLhdnCategoryCode(MyERP.Tax.TaxType.OutOfScope, 0m).ShouldBe("06");
        MyERP.EInvoice.Services.InvoiceDocumentBuilder.MapTaxTypeToLhdnCategoryCode(null, 10m).ShouldBe("01");
        MyERP.EInvoice.Services.InvoiceDocumentBuilder.MapTaxTypeToLhdnCategoryCode(null, 0m).ShouldBe("06");

        var lines = new List<MyERP.EInvoice.Services.EInvoiceLineData>
        {
            new() { Description = "Item A", Quantity = 1m, UnitPrice = 100m, TaxAmount = 6m, TaxCategoryCode = "01", TaxRate = 6m },
            new() { Description = "Item B", Quantity = 2m, UnitPrice = 50m, TaxAmount = 6m, TaxCategoryCode = "01", TaxRate = 6m },
            new() { Description = "Service C", Quantity = 1m, UnitPrice = 200m, TaxAmount = 16m, TaxCategoryCode = "02", TaxRate = 8m },
        };
        var breakdown = MyERP.EInvoice.Services.InvoiceDocumentBuilder.BuildTaxBreakdown(lines);
        breakdown.Count.ShouldBe(2);
        breakdown.Single(b => b.TaxCategoryCode == "01").TaxableAmount.ShouldBe(200m);
        breakdown.Single(b => b.TaxCategoryCode == "01").TaxAmount.ShouldBe(12m);
        breakdown.Single(b => b.TaxCategoryCode == "02").TaxableAmount.ShouldBe(200m);
        breakdown.Single(b => b.TaxCategoryCode == "02").TaxAmount.ShouldBe(16m);

        var builder = new MyERP.EInvoice.Services.InvoiceDocumentBuilder();
        var xml = System.Xml.Linq.XDocument.Parse(builder.Build(new MyERP.EInvoice.Services.EInvoiceDocumentData
        {
            InvoiceNumber = "INV-PERIOD-01",
            IssueDate = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            InvoicePeriodStartDate = new DateTime(2026, 8, 1),
            InvoicePeriodEndDate = new DateTime(2026, 8, 31),
            InvoicePeriodDescription = "Monthly",
            Supplier = new MyERP.EInvoice.Services.EInvoicePartyData { Name = "Seller", Tin = "C111", CountryCode = "MYS", State = "14" },
            Buyer = new MyERP.EInvoice.Services.EInvoicePartyData { Name = "Buyer", Tin = "C222", CountryCode = "MYS", State = "14" },
            NetTotal = 400m,
            TaxAmount = 28m,
            GrandTotal = 428m,
            Lines = lines,
            TaxBreakdown = breakdown,
        }));

        System.Xml.Linq.XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
        System.Xml.Linq.XNamespace cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
        var period = xml.Root!.Element(cac + "InvoicePeriod");
        period.ShouldNotBeNull();
        period!.Element(cbc + "StartDate")!.Value.ShouldBe("2026-08-01");
        period.Element(cbc + "EndDate")!.Value.ShouldBe("2026-08-31");
        period.Element(cbc + "Description")!.Value.ShouldBe("Monthly");
        xml.Root!.Element(cac + "TaxTotal")!.Elements(cac + "TaxSubtotal").Count().ShouldBe(2);
    }

    [Fact]
    public async System.Threading.Tasks.Task MyInvois_EInvoiceConsolidationService_RejectsCancelledSubmittedReturnsAndMixedCurrencies()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        static MyERP.EInvoice.Services.EInvoiceConsolidationService BuildSvc(List<MyERP.Sales.Entities.SalesInvoice> list)
        {
            var invRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Sales.Entities.SalesInvoice, Guid>>();
            var consolRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.EInvoice.Entities.EInvoiceConsolidation, Guid>>();
            var custRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Sales.Entities.Customer, Guid>>();
            var guidGen = NSubstitute.Substitute.For<Volo.Abp.Guids.IGuidGenerator>();
            NSubstitute.SubstituteExtensions.Returns(guidGen.Create(), _ => Guid.NewGuid());
            NSubstitute.SubstituteExtensions.Returns(
                invRepo.WithDetailsAsync(NSubstitute.Arg.Any<System.Linq.Expressions.Expression<Func<MyERP.Sales.Entities.SalesInvoice, object>>[]>()),
                System.Threading.Tasks.Task.FromResult(list.AsQueryable()));
            NSubstitute.SubstituteExtensions.Returns(custRepo.GetQueryableAsync(), System.Threading.Tasks.Task.FromResult(new List<MyERP.Sales.Entities.Customer>().AsQueryable()));
            NSubstitute.SubstituteExtensions.Returns(
                consolRepo.GetListAsync(NSubstitute.Arg.Any<System.Linq.Expressions.Expression<Func<MyERP.EInvoice.Entities.EInvoiceConsolidation, bool>>>()),
                System.Threading.Tasks.Task.FromResult(new List<MyERP.EInvoice.Entities.EInvoiceConsolidation>()));
            return new MyERP.EInvoice.Services.EInvoiceConsolidationService(invRepo, consolRepo, custRepo, guidGen);
        }

        // 1. Cancelled invoice rejected
        var ok1 = new MyERP.Sales.Entities.SalesInvoice(Guid.NewGuid(), companyId, customerId, "SI-OK-1", DateTime.Today);
        ok1.AddItem(Guid.NewGuid(), "Item", 1m, 100m, 0m);
        var cancelled = new MyERP.Sales.Entities.SalesInvoice(Guid.NewGuid(), companyId, customerId, "SI-CANC", DateTime.Today);
        cancelled.AddItem(Guid.NewGuid(), "Item", 1m, 100m, 0m);
        cancelled.Submit();
        cancelled.Post();
        cancelled.Cancel();
        var ex1 = await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
            BuildSvc(new() { ok1, cancelled }).ConsolidateInvoicesAsync(new() { ok1.Id, cancelled.Id }, companyId));
        ex1.Data["detail"]!.ToString()!.ShouldContain("cancelled");

        // 2. Already submitted to LHDN rejected
        var submitted = new MyERP.Sales.Entities.SalesInvoice(Guid.NewGuid(), companyId, customerId, "SI-VALID", DateTime.Today)
        {
            EInvoiceStatus = MyERP.Sales.EInvoiceStatus.Valid
        };
        submitted.AddItem(Guid.NewGuid(), "Item", 1m, 100m, 0m);
        var ex2 = await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
            BuildSvc(new() { ok1, submitted }).ConsolidateInvoicesAsync(new() { ok1.Id, submitted.Id }, companyId));
        ex2.Data["detail"]!.ToString()!.ShouldContain("already submitted");

        // 3. Return / Credit Note rejected
        var retInv = new MyERP.Sales.Entities.SalesInvoice(Guid.NewGuid(), companyId, customerId, "SI-RET", DateTime.Today)
        {
            IsReturn = true
        };
        retInv.AddItem(Guid.NewGuid(), "Item", -1m, 100m, 0m);
        var ex3 = await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
            BuildSvc(new() { ok1, retInv }).ConsolidateInvoicesAsync(new() { ok1.Id, retInv.Id }, companyId));
        ex3.Data["detail"]!.ToString()!.ShouldContain("Credit/Debit Note");

        // 4. Mixed currencies rejected
        var usdInv = new MyERP.Sales.Entities.SalesInvoice(Guid.NewGuid(), companyId, customerId, "SI-USD", DateTime.Today)
        {
            CurrencyCode = "USD"
        };
        usdInv.AddItem(Guid.NewGuid(), "Item", 1m, 100m, 0m);
        var ex4 = await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
            BuildSvc(new() { ok1, usdInv }).ConsolidateInvoicesAsync(new() { ok1.Id, usdInv.Id }, companyId));
        ex4.Data["detail"]!.ToString()!.ShouldContain("mixed currencies");
    }

    [Fact]
    public async System.Threading.Tasks.Task MyInvois_EInvoiceValidationService_BlocksAlreadyValidAndHandlesBlankSupplierTinFallback()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();

        var company = new MyERP.Core.Entities.Company(companyId, "LHDN Co")
        {
            EnableLhdnInvoice = true,
            TaxId = "C1234567890",
            MsicCode = "46510",
            RegistrationNumber = "202001012345",
        };
        var customer = new MyERP.Sales.Entities.Customer(customerId, companyId, "Buyer") { Tin = "C9876543210" };
        var supplier = new MyERP.Purchasing.Entities.Supplier(supplierId, companyId, "Supplier") { Tin = "C5555555550" };

        var companyRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Core.Entities.Company, Guid>>();
        var custRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Sales.Entities.Customer, Guid>>();
        var suppRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Purchasing.Entities.Supplier, Guid>>();
        var siRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Sales.Entities.SalesInvoice, Guid>>();
        var piRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Purchasing.Entities.PurchaseInvoice, Guid>>();

        NSubstitute.SubstituteExtensions.Returns(companyRepo.GetAsync(companyId), System.Threading.Tasks.Task.FromResult(company));
        NSubstitute.SubstituteExtensions.Returns(custRepo.FindAsync(customerId), System.Threading.Tasks.Task.FromResult<MyERP.Sales.Entities.Customer?>(customer));
        NSubstitute.SubstituteExtensions.Returns(suppRepo.FindAsync(supplierId), System.Threading.Tasks.Task.FromResult<MyERP.Purchasing.Entities.Supplier?>(supplier));

        var validator = new MyERP.EInvoice.Services.EInvoiceValidationService(companyRepo, custRepo, suppRepo, siRepo, piRepo);

        // 1. SalesInvoice already Valid rejected
        var si = new MyERP.Sales.Entities.SalesInvoice(Guid.NewGuid(), companyId, customerId, "SI-V1", DateTime.Today)
        {
            IsSubmitToLhdn = true,
            EInvoiceStatus = MyERP.Sales.EInvoiceStatus.Valid,
        };
        si.AddItem(Guid.NewGuid(), "Service", 1m, 100m, 0m);
        si.Submit();
        var siErrors = await validator.ValidateForSubmissionAsync(si, companyId);
        siErrors.ShouldContain(e => e.Contains("Valid LHDN"));

        // 2. PurchaseInvoice whitespace SupplierTin falls back to Supplier.Tin (no TIN error), blank currency rejected, Valid status rejected
        var pi = new MyERP.Purchasing.Entities.PurchaseInvoice(Guid.NewGuid(), companyId, supplierId, "PI-V1", DateTime.Today)
        {
            IsSubmitToLhdn = true,
            SupplierTin = "   ",
            CurrencyCode = "",
            EInvoiceStatus = MyERP.Sales.EInvoiceStatus.Valid,
        };
        pi.AddItem(Guid.NewGuid(), "Goods", 1m, 100m, 0m);
        pi.Submit();
        var piErrors = await validator.ValidatePurchaseInvoiceForSubmissionAsync(pi, companyId);
        piErrors.ShouldNotContain(e => e.Contains("Supplier TIN is required"));
        piErrors.ShouldContain(e => e.Contains("Currency code is required"));
        piErrors.ShouldContain(e => e.Contains("Valid LHDN"));
    }

    [Fact]
    public async System.Threading.Tasks.Task UpstreamPRs_67479_67481_67413_And_LhdnAcceptedCancel_Parity()
    {
        // 1. ERPNext PR #67479: stock-reserved part of Sales Order excluded from Production Plan pending qty cap
        // stockQty=10, stockReservedQty=4 (all from stock, not own WO), productionPlanQty=3 => qty=6, pending=min(6, 6-3)=3 (not 6)
        var pendingWithReservation = MyERP.Manufacturing.DomainServices.ProductionPlanMaterialPlanningService
            .CalculateSalesOrderItemPendingQtyForProductionPlan(
                stockQty: 10m,
                stockReservedQty: 4m,
                reservedOwnWorkOrderOutputQty: 0m,
                workOrderQty: 0m,
                deliveredQtyInOrderUom: 0m,
                conversionFactor: 1m,
                productionPlanQty: 3m);
        pendingWithReservation.ShouldBe(3m);

        // 2. ERPNext PR #67481: closed Work Order counts only its made qty (grossed up for planned loss), never exceeding open count
        var openCommitted = MyERP.Manufacturing.DomainServices.ProductionPlanMaterialPlanningService
            .CalculateWorkOrderCommittedQtyForPlan(
                workOrderQty: 10m,
                producedQty: 4m,
                processLossQty: 0m,
                plannedLossPercentage: 20m,
                creditProducedQty: true,
                isClosed: false);
        openCommitted.ShouldBe(10m);

        var closedCommitted = MyERP.Manufacturing.DomainServices.ProductionPlanMaterialPlanningService
            .CalculateWorkOrderCommittedQtyForPlan(
                workOrderQty: 10m,
                producedQty: 4m,
                processLossQty: 0m,
                plannedLossPercentage: 20m,
                creditProducedQty: true,
                isClosed: true);
        closedCommitted.ShouldBe(5m); // 4 / (1 - 0.2) = 5

        // 3. ERPNext PR #67413: purpose-aware pending FG qty for Stock Entry against Work Order
        MyERP.Inventory.DomainServices.StockEntryManager.CalculatePendingFgQtyForPurpose(
            woQty: 10m, producedQty: 6m, disassembledQty: 2m, materialTransferredQty: 8m, processLossQty: 1m,
            skipTransfer: false, purpose: MyERP.Inventory.StockEntryType.Disassemble)
            .ShouldBe(4m); // 6 - 2

        MyERP.Inventory.DomainServices.StockEntryManager.CalculatePendingFgQtyForPurpose(
            woQty: 10m, producedQty: 6m, disassembledQty: 0m, materialTransferredQty: 7m, processLossQty: 0m,
            skipTransfer: false, purpose: MyERP.Inventory.StockEntryType.MaterialTransferForManufacture)
            .ShouldBe(3m); // 10 - 7

        // Manufacture caps transferred qty at woQty and subtracts produced + process loss
        MyERP.Inventory.DomainServices.StockEntryManager.CalculatePendingFgQtyForPurpose(
            woQty: 10m, producedQty: 4m, disassembledQty: 0m, materialTransferredQty: 12m, processLossQty: 1m,
            skipTransfer: false, purpose: MyERP.Inventory.StockEntryType.Manufacture)
            .ShouldBe(5m); // min(12, 10) - 4 - 1 = 5

        // Manufacture with partial transfer uses transferredQty, unless skipTransfer is enabled
        MyERP.Inventory.DomainServices.StockEntryManager.CalculatePendingFgQtyForPurpose(
            woQty: 10m, producedQty: 2m, disassembledQty: 0m, materialTransferredQty: 6m, processLossQty: 1m,
            skipTransfer: false, purpose: MyERP.Inventory.StockEntryType.Manufacture)
            .ShouldBe(3m); // 6 - 2 - 1

        MyERP.Inventory.DomainServices.StockEntryManager.CalculatePendingFgQtyForPurpose(
            woQty: 10m, producedQty: 2m, disassembledQty: 0m, materialTransferredQty: 6m, processLossQty: 1m,
            skipTransfer: true, purpose: MyERP.Inventory.StockEntryType.Manufacture)
            .ShouldBe(7m); // 10 - 2 - 1

        // 4. MyInvois: EInvoiceService.CancelAsync allows cancelling submissions in "Accepted" status within 72h
        var subId = Guid.NewGuid();
        var sub = new MyERP.EInvoice.Entities.EInvoiceSubmission(subId, Guid.NewGuid(), "SalesInvoice", Guid.NewGuid())
        {
            SubmissionUid = "SUB-ACC-1",
            DocumentUuid = "UUID-ACC-1",
            Status = "Accepted",
            SubmittedAt = DateTime.UtcNow.AddHours(-2),
        };
        var lhdnClient = NSubstitute.Substitute.For<MyERP.EInvoice.Services.ILhdnApiClient>();
        var subRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.EInvoice.Entities.EInvoiceSubmission, Guid>>();
        var logRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.EInvoice.Entities.LhdnSuccessLog, Guid>>();

        NSubstitute.SubstituteExtensions.Returns(subRepo.GetAsync(subId), System.Threading.Tasks.Task.FromResult(sub));
        NSubstitute.SubstituteExtensions.Returns(
            lhdnClient.CancelDocumentAsync("tok", "UUID-ACC-1", "Duplicate", MyERP.EInvoice.LhdnEnvironment.Sandbox),
            System.Threading.Tasks.Task.FromResult(new MyERP.EInvoice.Services.LhdnCancelResponse { IsSuccess = true, StatusCode = 200 }));

        var eInvoiceService = new MyERP.EInvoice.Services.EInvoiceService(lhdnClient, subRepo, logRepo);
        var cancelled = await eInvoiceService.CancelAsync(subId, "Duplicate", "tok", MyERP.EInvoice.LhdnEnvironment.Sandbox);
        cancelled.Status.ShouldBe("Cancelled");
        cancelled.Reason.ShouldBe("Duplicate");
    }

    [Fact]
    public void UpstreamPRs_68803_67414_67428_67443_WholeUnitCeilAndUomRate_Parity()
    {
        // 1. PR #68803 & #67414: pre-round to 9 decimals before ceiling so float/decimal noise never adds an extra unit
        MyERP.Manufacturing.DomainServices.ProductionPlanMaterialPlanningService
            .CeilWholeUnit(7.0000000001m, mustBeWholeNumber: true)
            .ShouldBe(7m);
        MyERP.Manufacturing.DomainServices.ProductionPlanMaterialPlanningService
            .CeilWholeUnit(7.001m, mustBeWholeNumber: true)
            .ShouldBe(8m);
        MyERP.Manufacturing.DomainServices.ProductionPlanMaterialPlanningService
            .CeilWholeUnit(7.25m, mustBeWholeNumber: false)
            .ShouldBe(7.25m);

        // 2. PR #67428: last purchase rate fallback converts from source purchase UOM to target row UOM
        MyERP.Inventory.DomainServices.ItemDetailsResolverService
            .ConvertLastPurchaseRateToRowUom(lastPurchaseRateInSourceUom: 100m, lastPurchaseConversionFactor: 10m, rowConversionFactor: 2m)
            .ShouldBe(20m);
        MyERP.Inventory.DomainServices.ItemDetailsResolverService
            .ConvertLastPurchaseRateToRowUom(lastPurchaseRateInSourceUom: 12.5m, lastPurchaseConversionFactor: 1m, rowConversionFactor: 12m)
            .ShouldBe(150m);

        // 3. PR #67443: buying Item Price in non-stock UOM converts to stock UOM valuation rate
        MyERP.Inventory.DomainServices.ItemDetailsResolverService
            .ConvertBuyingItemPriceToStockUom(buyingPriceListRate: 120m, buyingUomConversionFactor: 12m)
            .ShouldBe(10m);
    }

    [Fact]
    public void UpstreamPRs_67357_67372_67398_68876_SubcontractingAndScheduling_Parity()
    {
        // 1. PR #67357: receipt takes only its proportional share of remaining SCO additional costs
        var row1 = Guid.NewGuid();
        var row2 = Guid.NewGuid();
        var orderRows = new[]
        {
            (row1, 10m, 4m, 5m), // 6 pending * 5 = 30 pending cost; ordered cost = 50
            (row2, 10m, 0m, 5m), // 10 pending * 5 = 50 pending cost; ordered cost = 50 (total = 100)
        };
        // Earlier receipt took 20 of 100 => remainingShare = 0.8. This receipt takes 4 of row1 (20 of 80 pending = 0.25)
        // Expected share = 0.8 * 0.25 = 0.20
        var share = MyERP.Purchasing.DomainServices.SubcontractingManager.CalculateReceiptAdditionalCostShare(
            orderRows,
            additionalCostTakenByEarlierReceipts: 20m,
            receiptQtyByOrderRowId: new System.Collections.Generic.Dictionary<Guid, decimal> { [row1] = 4m });
        share.ShouldBe(0.2m);

        // Zero total additional cost keeps 1.0 share
        MyERP.Purchasing.DomainServices.SubcontractingManager.CalculateReceiptAdditionalCostShare(
            new[] { (row1, 10m, 0m, 0m) },
            additionalCostTakenByEarlierReceipts: 0m).ShouldBe(1m);

        // 2. PR #67398: FG keeps share of BOM % co-products omitted or zero-qty on receipt
        var (allocatedPct, fgAmount, fgRate) = MyERP.Purchasing.DomainServices.SubcontractingManager
            .CalculateFinishedGoodShareAfterReceiptPercentageCoProducts(
                fgGrossAmount: 1000m,
                fgQty: 10m,
                receiptPercentageRows: new[] { (2m, 20m), (0m, 10m) }); // 10% co-product has 0 net qty -> excluded
        allocatedPct.ShouldBe(20m);
        fgAmount.ShouldBe(800m);
        fgRate.ShouldBe(80m);

        // 3. PR #67372: pooled pending RM transfer across SCO rows sharing same material
        var rmId = Guid.NewGuid();
        var fg1 = Guid.NewGuid();
        var fg2 = Guid.NewGuid();
        var suppliedRows = new (Guid, Guid?, decimal, decimal)[]
        {
            (rmId, fg1, 10m, 14m), // over-supplied by 4
            (rmId, fg2, 10m, 0m),  // needs 10, but order-wide pool only has 6 left
        };
        var bomPooled = MyERP.Purchasing.DomainServices.SubcontractingRmTransferService
            .CalculatePooledPendingRmTransfers(suppliedRows, poolPerFinishedGood: false);
        bomPooled.Count.ShouldBe(1);
        bomPooled[0].FinishedGoodId.ShouldBe(fg2);
        bomPooled[0].TransferQty.ShouldBe(6m);

        var perFgPooled = MyERP.Purchasing.DomainServices.SubcontractingRmTransferService
            .CalculatePooledPendingRmTransfers(suppliedRows, poolPerFinishedGood: true);
        perFgPooled.Count.ShouldBe(1);
        perFgPooled[0].TransferQty.ShouldBe(10m);

        // 4. PR #68876: Production Plan schedules per-batch operation by batch_size, fixed time stays fixed
        MyERP.Manufacturing.Entities.BomOperation.CalculateScheduledDurationMins(
            timeInMins: 30m, qty: 15m, bomQty: 1m, isFixedTime: false,
            createJobCardBasedOnBatchSize: true, batchSize: 5m).ShouldBe(90m);
        MyERP.Manufacturing.Entities.BomOperation.CalculateScheduledDurationMins(
            timeInMins: 30m, qty: 15m, bomQty: 1m, isFixedTime: true,
            createJobCardBasedOnBatchSize: true, batchSize: 5m).ShouldBe(30m);
    }

    [Fact]
    public void UpstreamPRs_67498_63784_MrpForecastConsumptionAndReservedSerials_Parity()
    {
        // 1. PR #67498: MPS demand across entire forecast period consumes forecast, earlier-ending periods first
        var forecasts = new[]
        {
            ("FG-1", new DateTime(2026, 5, 1), new DateTime(2026, 6, 1), 100m),
            ("FG-1", new DateTime(2026, 5, 1), new DateTime(2026, 5, 15), 40m), // ends earlier -> consumes first
        };
        var mpsDemands = new[]
        {
            ("FG-1", new DateTime(2026, 5, 10), 60m), // 40 goes to [May 1, May 15), remaining 20 goes to [May 1, Jun 1)
            ("FG-1", new DateTime(2026, 5, 25), 30m), // 30 goes to [May 1, Jun 1) -> leaves 50
        };
        var remaining = MyERP.Manufacturing.DomainServices.MasterProductionScheduleService
            .ConsumeForecastFromMpsDemand(forecasts, mpsDemands);
        remaining.Count.ShouldBe(1);
        remaining[0].PeriodEnd.ShouldBe(new DateTime(2026, 6, 1));
        remaining[0].RemainingForecastQty.ShouldBe(50m);

        MyERP.Manufacturing.DomainServices.MasterProductionScheduleService
            .CalculateCombinedMrpDemand(mpsPlannedQty: 30m, remainingForecastQty: 50m, adhocQty: 5m)
            .ShouldBe(85m);

        // 2. PR #63784: pick serial numbers reserved for target SO row first, then fill from unreserved stock
        var picked = MyERP.Inventory.DomainServices.StockReservationManager.PickSerialNosPrioritizingSalesOrderRow(
            requiredQty: 3,
            reservedForSoRowSerials: new[] { "SR-RES-1", "SR-RES-2" },
            unreservedAvailableSerials: new[] { "SR-FREE-1", "SR-RES-1", "SR-FREE-2" },
            ignoreSerials: new[] { "SR-RES-2" });
        picked.ShouldBe(new[] { "SR-RES-1", "SR-FREE-1", "SR-FREE-2" });

        // Voucher OR row/packed-item detail matching
        var soId = Guid.NewGuid();
        var soRowId = Guid.NewGuid();
        var targets = new System.Collections.Generic.HashSet<Guid> { soRowId };
        MyERP.Inventory.DomainServices.StockReservationManager
            .IsReservationForVoucherOrRow(soId, soRowId, targets)
            .ShouldBeTrue();
        MyERP.Inventory.DomainServices.StockReservationManager
            .IsReservationForVoucherOrRow(soId, Guid.NewGuid(), targets)
            .ShouldBeFalse();
    }
}
