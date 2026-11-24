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

    [Fact]
    public void UpstreamPRs_65784_67462_68832_PosReturnStock_JobCardNonStock_NestedSubBom_Parity()
    {
        // 1. PR #65784: POS closing splits chronologically by IsReturn so returns precede later sales
        var companyId = Guid.NewGuid();
        var custId = Guid.NewGuid();
        var sale1 = new MyERP.Sales.Entities.SalesInvoice(Guid.NewGuid(), companyId, custId, "POS-1", new DateTime(2026, 6, 1, 9, 0, 0));
        var ret1 = new MyERP.Sales.Entities.SalesInvoice(Guid.NewGuid(), companyId, custId, "POS-RET-1", new DateTime(2026, 6, 1, 10, 0, 0)) { IsReturn = true };
        var sale2 = new MyERP.Sales.Entities.SalesInvoice(Guid.NewGuid(), companyId, custId, "POS-2", new DateTime(2026, 6, 1, 11, 0, 0));

        var batches = MyERP.Sales.DomainServices.PosConsolidationService.SplitChronologicalReturnBatches(
            new[] { sale2, ret1, sale1 });
        batches.Count.ShouldBe(3);
        batches[0].Single().InvoiceNumber.ShouldBe("POS-1");
        batches[1].Single().InvoiceNumber.ShouldBe("POS-RET-1");
        batches[2].Single().InvoiceNumber.ShouldBe("POS-2");

        // 2. PR #67462: Job Card output entry only charges its own operation's non-stock items; unassigned goes to final FG card
        var op1 = Guid.NewGuid();
        var op2 = Guid.NewGuid();
        var fgItem = Guid.NewGuid();
        var semiFgItem = Guid.NewGuid();

        MyERP.Manufacturing.DomainServices.JobCardManager.IsNonStockBomRowAttributedToJobCard(
            rowOperationId: op1, rowOperationRowId: null,
            jobCardOperationId: op1, jobCardOperationRowId: null,
            jobCardFinishedGoodId: semiFgItem, workOrderProductionItemId: fgItem).ShouldBeTrue();

        MyERP.Manufacturing.DomainServices.JobCardManager.IsNonStockBomRowAttributedToJobCard(
            rowOperationId: op2, rowOperationRowId: null,
            jobCardOperationId: op1, jobCardOperationRowId: null,
            jobCardFinishedGoodId: semiFgItem, workOrderProductionItemId: fgItem).ShouldBeFalse();

        // Unassigned non-stock row goes only to final FG Job Card, NOT intermediate semi-FG card
        MyERP.Manufacturing.DomainServices.JobCardManager.IsNonStockBomRowAttributedToJobCard(
            rowOperationId: null, rowOperationRowId: null,
            jobCardOperationId: op1, jobCardOperationRowId: null,
            jobCardFinishedGoodId: semiFgItem, workOrderProductionItemId: fgItem).ShouldBeFalse();

        MyERP.Manufacturing.DomainServices.JobCardManager.IsNonStockBomRowAttributedToJobCard(
            rowOperationId: null, rowOperationRowId: null,
            jobCardOperationId: op2, jobCardOperationRowId: null,
            jobCardFinishedGoodId: fgItem, workOrderProductionItemId: fgItem).ShouldBeTrue();

        // 3. PR #68832: Nested sub-assembly secondary cost deducts own-cost rows + proportional share of inner sub-assembly secondary items
        var outerSubBomId = Guid.NewGuid();
        var innerSecId = Guid.NewGuid();
        var ownSecId = Guid.NewGuid();
        var withinCost = MyERP.Inventory.DomainServices.StockEntryManager.CalculateNestedSubAssemblySecondaryCostWithin(
            subBomId: outerSubBomId,
            subAssembliesMade: 2m,
            fgCompletedQty: 1m,
            qtyWithinSubBomBySecondaryId: new System.Collections.Generic.Dictionary<Guid, decimal> { [innerSecId] = 1m },
            qtyPerFinishedGoodBySecondaryId: new System.Collections.Generic.Dictionary<Guid, decimal> { [innerSecId] = 4m },
            entryItems: new (Guid?, Guid?, bool, decimal)[]
            {
                (innerSecId, Guid.NewGuid(), false, 200m), // 200 * 1 * 2 / (4 * 1) = 100
                (ownSecId, outerSubBomId, true, 35m),      // own-cost row of outerSubBom = 35
            });
        withinCost.ShouldBe(135m);
    }

    [Fact]
    public async System.Threading.Tasks.Task UpstreamPRs_67348_67489_68802_And_ConsolidationDoubleMergeGuard_Parity()
    {
        // 1. PR #67348: pending consumption qty based on BOM (floors at precision so proposal never exceeds remaining)
        var rm1 = Guid.NewGuid();
        var rm2 = Guid.NewGuid();
        var bomConsumptionQty = MyERP.Inventory.DomainServices.StockEntryManager.CalculatePendingConsumptionQtyBasedOnBom(
            workOrderQty: 10m,
            producedQty: 2m,
            processLossQty: 0m,
            skipTransfer: false,
            requiredItems: new[]
            {
                (rm1, 30m, 20m, 10m, true), // left=10 -> covers 10 * 10 / 30 = 3.333333... -> floored to 3.3333
                (rm2, 10m, 10m, 2m, true),  // left=8 -> covers 8
            },
            precision: 4);
        bomConsumptionQty.ShouldBe(3.3333m);

        // PR #67348: pending consumption qty based on transfer
        var transferConsumptionQty = MyERP.Inventory.DomainServices.StockEntryManager.CalculatePendingConsumptionQtyBasedOnTransfer(
            materialTransferredQty: 10m,
            producedQty: 3m,
            processLossQty: 1m,
            requiredItems: new[] { (rm1, 10m, 4m), (rm2, 10m, 10m) },
            coveredFgQtyByItemId: new System.Collections.Generic.Dictionary<Guid, decimal> { [rm1] = 6m });
        transferConsumptionQty.ShouldBe(4m); // min(10 - 6, 10 - 3 - 1) = 4

        // 2. PR #67489: monthly forecast spreads from its own delivery date [May 15, Jun 15), not May 1
        var (start, end) = MyERP.Manufacturing.DomainServices.MasterProductionScheduleService
            .GetForecastPeriod(new DateTime(2026, 5, 15), "Monthly");
        start.ShouldBe(new DateTime(2026, 5, 15));
        end.ShouldBe(new DateTime(2026, 6, 15));

        var dailyBuckets = MyERP.Manufacturing.DomainServices.MasterProductionScheduleService.SpreadForecastToDailyBuckets(
            new[] { ("FG-1", new DateTime(2026, 5, 15), (string?)"Monthly", 310m) },
            fromDate: new DateTime(2026, 6, 1),
            toDate: new DateTime(2026, 6, 10));
        dailyBuckets.Count.ShouldBe(10); // June 1..10 at 10/day
        dailyBuckets[0].DailyQty.ShouldBe(10m);

        // 3. PR #68802: standalone debit note stock valuation rate converts to row UOM
        MyERP.Inventory.DomainServices.ItemDetailsResolverService
            .ConvertStockValuationRateToRowUom(stockValuationRate: 15m, rowConversionFactor: 12m)
            .ShouldBe(180m);

        // 4. MyInvois: EInvoiceConsolidationService blocks re-consolidating an IsConsolidated parent invoice
        var companyId = Guid.NewGuid();
        var custId = Guid.NewGuid();
        var ok1 = new MyERP.Sales.Entities.SalesInvoice(Guid.NewGuid(), companyId, custId, "SI-OK-32", DateTime.Today);
        ok1.AddItem(Guid.NewGuid(), "Item", 1m, 100m, 0m);
        var alreadyCons = new MyERP.Sales.Entities.SalesInvoice(Guid.NewGuid(), companyId, custId, "SI-CONS-32", DateTime.Today)
        {
            IsConsolidated = true,
        };
        alreadyCons.AddItem(Guid.NewGuid(), "Item", 1m, 100m, 0m);

        var siRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Sales.Entities.SalesInvoice, Guid>>();
        var consRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.EInvoice.Entities.EInvoiceConsolidation, Guid>>();
        var custRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Sales.Entities.Customer, Guid>>();
        var guidGen = NSubstitute.Substitute.For<Volo.Abp.Guids.IGuidGenerator>();
        NSubstitute.SubstituteExtensions.Returns(
            siRepo.WithDetailsAsync(NSubstitute.Arg.Any<System.Linq.Expressions.Expression<Func<MyERP.Sales.Entities.SalesInvoice, object>>[]>()),
            System.Threading.Tasks.Task.FromResult(new[] { ok1, alreadyCons }.AsQueryable()));
        NSubstitute.SubstituteExtensions.Returns(
            custRepo.GetQueryableAsync(),
            System.Threading.Tasks.Task.FromResult(new MyERP.Sales.Entities.Customer[0].AsQueryable()));

        var svc = new MyERP.EInvoice.Services.EInvoiceConsolidationService(siRepo, consRepo, custRepo, guidGen);
        var ex = await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
            svc.ConsolidateInvoicesAsync(new() { ok1.Id, alreadyCons.Id }, companyId));
        ex.Code.ShouldBe(MyERP.MyERPDomainErrorCodes.EInvoiceConsolidationAlreadyConsolidated);
    }

    [Fact]
    public void UpstreamPRs_67500_68828_67344_67345_68805_67379_And_MyInvoisCreditNoteTax_Parity()
    {
        // 1. MyInvois: Credit Note with negative taxAmount maps to "01" (not "06" Not Applicable)
        MyERP.EInvoice.Services.InvoiceDocumentBuilder
            .MapTaxTypeToLhdnCategoryCode(null, -6m)
            .ShouldBe("01");
        var cnBreakdown = MyERP.EInvoice.Services.InvoiceDocumentBuilder.BuildTaxBreakdown(new[]
        {
            new MyERP.EInvoice.Services.EInvoiceLineData
            {
                Description = "Return",
                Quantity = -1m,
                UnitPrice = 100m,
                TaxAmount = -6m,
                TaxRate = 6m,
            }
        });
        cnBreakdown.Single().TaxCategoryCode.ShouldBe("01");

        // 2. PR #67500: SCIO cumulative return/delivery validation catches split lines exceeding max allowed
        var scioRowId = Guid.NewGuid();
        var scioEx = Should.Throw<Volo.Abp.BusinessException>(() =>
            MyERP.Purchasing.DomainServices.SubcontractingManager.ValidateCumulativeInwardOrderQuantities(
                rows: new[]
                {
                    (1, scioRowId, "RM-A", 4m),
                    (2, scioRowId, "RM-A", 3m), // cumulative 7 > max 6
                },
                maxAllowedByScioDetailId: new System.Collections.Generic.Dictionary<Guid, decimal> { [scioRowId] = 6m }));
        scioEx.Code.ShouldBe(MyERP.MyERPDomainErrorCodes.ValidationFailed);
        scioEx.Data["detail"]!.ToString()!.ShouldContain("Row #2");

        // 3. PR #68828: split SCO RM return across order rows up to each row's remaining qty
        var r1 = Guid.NewGuid();
        var r2 = Guid.NewGuid();
        var splitReturns = MyERP.Purchasing.DomainServices.SubcontractingRmTransferService
            .SplitReturnQtyAcrossOrderRows(9m, new[] { (r1, 4m), (r2, 6m) });
        splitReturns.ShouldBe(new[] { (r1, 4m), (r2, 5m) });

        // 4. PR #67345: available FG qty subtracts BOTH disassembledQty and convertedFgQty
        MyERP.Manufacturing.Entities.WorkOrder
            .CalculateAvailableFgQtyForDisassemblyOrConversion(producedQty: 10m, disassembledQty: 3m, convertedFgQty: 2m)
            .ShouldBe(5m);

        // 5. PR #67344: Work Order material transferred counts Pick List transfers alongside direct transfers
        var itemA = Guid.NewGuid();
        var itemB = Guid.NewGuid();
        MyERP.Manufacturing.Entities.WorkOrder.CalculateMaterialTransferredQtyWithPickList(
            woQty: 10m,
            sumFgCompletedQty: 4m,
            hasTransferWithoutForQuantity: true,
            requiredItems: new[] { (itemA, 10m, 8m), (itemB, 20m, 14m) }) // min fraction = 14/20 = 0.7 -> 7 > 4
            .ShouldBe(7m);

        // 6. PR #68805: Work Order unreserved qty excludes subcontracted qty
        MyERP.Manufacturing.Entities.WorkOrder.CalculateWorkOrderUnreservedQty(
            requiredQty: 10m, subcontractedQty: 4m, stockReservedQty: 2m, consumedQty: 1m, transferredQty: 0m)
            .ShouldBe(3m); // 10 - 4 - max(2+1, 0) = 3

        // 7. PR #67379: inter-company outgoing delivery queries negative stock qty, return/purchase queries positive
        MyERP.Inventory.DomainServices.ItemDetailsResolverService
            .ResolveInternalTransactionRateQueryQty(stockQty: 5m, isInternalCustomer: true, isReturn: false)
            .ShouldBe(-5m);
        MyERP.Inventory.DomainServices.ItemDetailsResolverService
            .ResolveInternalTransactionRateQueryQty(stockQty: 5m, isInternalCustomer: true, isReturn: true)
            .ShouldBe(5m);
    }

    [Fact]
    public async System.Threading.Tasks.Task UpstreamBatch7_LhdnJsonEnvelope_Subcontracting_BomPlc_StandaloneDebitNote_Parity()
    {
        // 1. MyInvois original.py: LhdnApiClient wraps XML in LHDN JSON submission envelope with SHA-256 hex & Base64
        const string sampleXml = "<Invoice xmlns:cbc=\"urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2\"><cbc:ID>SI-2026-0099</cbc:ID></Invoice>";
        var payloadJson = MyERP.EInvoice.Services.LhdnApiClient.BuildSubmissionPayloadJson(sampleXml);
        using (var parsed = System.Text.Json.JsonDocument.Parse(payloadJson))
        {
            var doc0 = parsed.RootElement.GetProperty("documents")[0];
            doc0.GetProperty("format").GetString().ShouldBe("XML");
            doc0.GetProperty("codeNumber").GetString().ShouldBe("SI-2026-0099");
            var expectedBytes = System.Text.Encoding.UTF8.GetBytes(sampleXml);
            doc0.GetProperty("documentHash").GetString().ShouldBe(
                Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(expectedBytes)).ToLowerInvariant());
            doc0.GetProperty("document").GetString().ShouldBe(Convert.ToBase64String(expectedBytes));
        }

        // Verify SubmitDocumentAsync actually sends application/json envelope over HTTP
        string? capturedMediaType = null;
        string? capturedBody = null;
        var handler = new CaptureHttpHandler(req =>
        {
            capturedMediaType = req.Content?.Headers.ContentType?.MediaType;
            capturedBody = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.Accepted)
            {
                Content = new System.Net.Http.StringContent(
                    """{"submissionUid":"SUB-7","acceptedDocuments":[{"uuid":"UUID-7","longId":"LONG-7"}]}""",
                    System.Text.Encoding.UTF8,
                    "application/json")
            };
        });
        var factory = NSubstitute.Substitute.For<System.Net.Http.IHttpClientFactory>();
        NSubstitute.SubstituteExtensions.Returns(factory.CreateClient("LhdnApi"), new System.Net.Http.HttpClient(handler));
        var client = new MyERP.EInvoice.Services.LhdnApiClient(
            factory,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<MyERP.EInvoice.Services.LhdnApiClient>.Instance);
        var submitRes = await client.SubmitDocumentAsync("tok", sampleXml, MyERP.EInvoice.LhdnEnvironment.Sandbox);
        submitRes.IsSuccess.ShouldBeTrue();
        capturedMediaType.ShouldBe("application/json");
        capturedBody.ShouldNotBeNull();
        capturedBody!.ShouldContain("\"codeNumber\":\"SI-2026-0099\"");

        // 2. ERPNext e2d286d688 + 61bb973b59: Subcontracting Receipt return reserves only non-zero % co-product rows
        var retShare = MyERP.Purchasing.DomainServices.SubcontractingManager
            .CalculateReturnedFinishedGoodShareFromReceiptRows(
                fgReturnGrossAmount: -1000m,
                fgReturnQty: -10m,
                originalReceiptSecondaryRows: new[]
                {
                    (2m, MyERP.Manufacturing.SecondaryItemValuationType.PercentageOfComponentCost, 20m),
                    (3m, MyERP.Manufacturing.SecondaryItemValuationType.ValuationRate, 15m), // ignored
                    (1m, MyERP.Manufacturing.SecondaryItemValuationType.Manual, 10m),        // ignored
                    (0m, MyERP.Manufacturing.SecondaryItemValuationType.PercentageOfComponentCost, 25m), // zero qty ignored
                });
        retShare.AllocatedCoProductPercentage.ShouldBe(20m);
        retShare.FinishedGoodAmount.ShouldBe(-800m);
        retShare.FinishedGoodRate.ShouldBe(80m);

        // 3. ERPNext 5b56de6ee4 + acdd0590d1 + bace342ab3: SCIO delivery/return Difference Account resolution
        var scioRow1 = Guid.NewGuid();
        var scioRow2 = Guid.NewGuid();
        var accA = Guid.NewGuid();
        var accB = Guid.NewGuid();
        var companyDefaultAcc = Guid.NewGuid();
        var deliveryDefaults = MyERP.Purchasing.DomainServices.SubcontractingManager
            .ResolveSubcontractingReturnDifferenceAccounts(new[]
            {
                (scioRow1, (Guid?)accA),
                (scioRow1, (Guid?)accA), // consistent single account -> defaults to accA
                (scioRow2, (Guid?)accA),
                (scioRow2, (Guid?)accB), // mixed accounts -> no default
            });
        deliveryDefaults.ShouldContainKeyAndValue(scioRow1, accA);
        deliveryDefaults.ShouldNotContainKey(scioRow2);
        MyERP.Purchasing.DomainServices.SubcontractingManager
            .ResolveSubcontractingEntryExpenseAccount(accB, accA, companyDefaultAcc)
            .ShouldBe(accB); // explicit account preserved
        MyERP.Purchasing.DomainServices.SubcontractingManager
            .ResolveSubcontractingEntryExpenseAccount(null, accA, companyDefaultAcc)
            .ShouldBe(accA);

        // 4. ERPNext 3fd104156f: RM consumption beyond what was sent for one FG counts on other rows of the same SCO
        var scoId = Guid.NewGuid();
        var rmId = Guid.NewGuid();
        var fg1 = Guid.NewGuid();
        var fg2 = Guid.NewGuid();
        var rowFg1 = Guid.NewGuid();
        var rowFg2 = Guid.NewGuid();
        var consumedAlloc = MyERP.Purchasing.DomainServices.SubcontractingManager
            .AllocateSubcontractingRmConsumptionAcrossOrderRows(
                orderRows: new[]
                {
                    (rowFg1, scoId, fg1, rmId, 5m),
                    (rowFg2, scoId, fg2, rmId, 5m),
                },
                itemwiseConsumedQty: new System.Collections.Generic.Dictionary<(Guid, Guid, Guid), decimal>
                {
                    [(rmId, fg1, scoId)] = 8m, // 5 on rowFg1 + 3 overflow absorbed by rowFg2
                });
        consumedAlloc[rowFg1].ShouldBe(5m);
        consumedAlloc[rowFg2].ShouldBe(3m);

        // 5. ERPNext 324bc5ffa6 + 9ff6216212 + ead69ffe56 + 4768171999: BOM price-list conversion rate
        MyERP.Manufacturing.DomainServices.BomCostPropagationService
            .ResolveBomPriceListConversionRate("MYR", "MYR", "USD", 4.5m, 4.4m, 4.2m)
            .ShouldBe(1m);
        MyERP.Manufacturing.DomainServices.BomCostPropagationService
            .ResolveBomPriceListConversionRate("USD", "MYR", "USD", 4.5m, 4.4m, 4.2m)
            .ShouldBe(4.5m);
        MyERP.Manufacturing.DomainServices.BomCostPropagationService
            .ResolveBomPriceListConversionRate("EUR", "MYR", "USD", 4.5m, 5.1m, 4.9m)
            .ShouldBe(5.1m);
        MyERP.Manufacturing.DomainServices.BomCostPropagationService
            .ResolveBomPriceListConversionRate("EUR", "MYR", "USD", 4.5m, null, 4.9m)
            .ShouldBe(4.9m); // keeps stored rate when lookup finds nothing

        // 6. ERPNext f2588c879a + 9ef6e8bef4 + 023c44198d: standalone debit note fallback conversion factor & qty precision
        decimal seenLookupQty = 0m;
        var dnVal = MyERP.Inventory.DomainServices.ItemDetailsResolverService
            .ResolveStandaloneDebitNoteValuation(
                rowQty: -1.33336m,
                rowConversionFactor: 0m,         // missing on row -> fallback to item UOM factor 12
                itemUomConversionFactor: 12m,
                lookupStockValuationRate: q =>
                {
                    seenLookupQty = q;
                    return 5m; // stock UOM rate
                },
                stockQtyPrecision: 4,
                ratePrecision: 4);
        dnVal.ConversionFactor.ShouldBe(12m);
        seenLookupQty.ShouldBe(-16.0003m);
        dnVal.LookupStockQty.ShouldBe(-16.0003m);
        dnVal.RowUomRate.ShouldBe(60m);
    }

    [Fact]
    public void UpstreamBatch8_MyInvoisConsolidatedBuyer_ReservationCancelReplay_NestedSubAssemblyCoProducts_Parity()
    {
        // 1. MyInvois consolidate_invoice.py: consolidated buyer emits TIN EI00000000010, BRN/SST/TTX/Address/Contact = NA, MYS
        var builder = new MyERP.EInvoice.Services.InvoiceDocumentBuilder();
        var consXml = System.Xml.Linq.XDocument.Parse(builder.Build(new MyERP.EInvoice.Services.EInvoiceDocumentData
        {
            InvoiceNumber = "CONS-2026-01",
            IssueDate = new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc),
            DocumentTypeCode = "01",
            CurrencyCode = "MYR",
            IsConsolidated = true,
            Supplier = new MyERP.EInvoice.Services.EInvoicePartyData { Name = "Seller Sdn Bhd", Tin = "C1000", CountryCode = "MYS", State = "14" },
            Buyer = new MyERP.EInvoice.Services.EInvoicePartyData(), // blank buyer fields
            NetTotal = 100m,
            TaxAmount = 0m,
            GrandTotal = 100m,
            Lines = new System.Collections.Generic.List<MyERP.EInvoice.Services.EInvoiceLineData>
            {
                new() { Description = "Receipt #1", Quantity = 1m, UnitPrice = 100m }
            }
        }));
        System.Xml.Linq.XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
        System.Xml.Linq.XNamespace cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
        var buyerParty = consXml.Root!.Element(cac + "AccountingCustomerParty")!.Element(cac + "Party")!;
        string GetPartyId(string scheme) => buyerParty.Elements(cac + "PartyIdentification")
            .First(x => x.Element(cbc + "ID")!.Attribute("schemeID")!.Value == scheme)
            .Element(cbc + "ID")!.Value;
        GetPartyId("TIN").ShouldBe("EI00000000010");
        GetPartyId("BRN").ShouldBe("NA");
        GetPartyId("SST").ShouldBe("NA");
        GetPartyId("TTX").ShouldBe("NA");
        buyerParty.Element(cac + "PartyLegalEntity")!.Element(cbc + "RegistrationName")!.Value.ShouldBe("Consolidated Buyers");
        buyerParty.Element(cac + "Contact")!.Element(cbc + "Telephone")!.Value.ShouldBe("NA");
        buyerParty.Element(cac + "Contact")!.Element(cbc + "ElectronicMail")!.Value.ShouldBe("NA");
        buyerParty.Element(cac + "PostalAddress")!.Element(cac + "AddressLine")!.Element(cbc + "Line")!.Value.ShouldBe("NA");

        // 2. ERPNext a2c2bc92c2 + f4a3af2745: replay submits & earlier cancels in order; cancelling V1 reopens ONLY SRE1, keeping V2 on SRE2
        var t0 = new DateTime(2026, 7, 1, 8, 0, 0, DateTimeKind.Utc);
        var sre1 = Guid.NewGuid();
        var sre2 = Guid.NewGuid();
        var v1 = Guid.NewGuid();
        var v2 = Guid.NewGuid();
        var v0CancelledEarlier = Guid.NewGuid();
        var replayed = MyERP.Inventory.DomainServices.StockReservationManager.ReplayReservationUsageOnCancel(
            reservations: new[]
            {
                (sre1, t0, 5m),
                (sre2, t0.AddMinutes(1), 5m),
            },
            usages: new[]
            {
                (v0CancelledEarlier, t0.AddMinutes(2), (DateTime?)t0.AddMinutes(3), 5m), // took SRE1 then released before V1
                (v1, t0.AddMinutes(4), (DateTime?)t0.AddMinutes(10), 5m),               // took SRE1, now cancelling
                (v2, t0.AddMinutes(5), (DateTime?)null, 5m),                            // took SRE2, stays on SRE2
            },
            cancelledVoucherId: v1);
        replayed[sre1].ShouldBe(0m);
        replayed[sre2].ShouldBe(5m);

        // 3. ERPNext 07915b5632 + f0483b53a5 + 83976ff833: subcontracted qty excluded ONLY on new WO reservations
        MyERP.Inventory.DomainServices.StockReservationManager
            .CalculateWorkOrderAllowedReservationQty(10m, subcontractedQty: 4m, alreadyReservedQty: 2m, consumedQty: 0m, transferredQty: 0m, isNewReservation: true)
            .ShouldBe(4m); // 10 - 4 - 2 = 4
        MyERP.Inventory.DomainServices.StockReservationManager
            .CalculateWorkOrderAllowedReservationQty(10m, subcontractedQty: 4m, alreadyReservedQty: 2m, consumedQty: 0m, transferredQty: 0m, isNewReservation: false)
            .ShouldBe(8m); // existing reservation keeps full 10 - 2 = 8

        // 4. ERPNext 05b5ca5548: nested sub-assembly secondary items scale by parent qty at every level
        var rootBom = Guid.NewGuid();
        var midBom = Guid.NewGuid();
        var innerBom = Guid.NewGuid();
        var midSecItem = Guid.NewGuid();
        var innerSecItem = Guid.NewGuid();
        var scaledSecs = MyERP.Inventory.DomainServices.StockEntryManager.ScaleNestedSubAssemblySecondaryItems(
            rootBomId: rootBom,
            rootQty: 3m,
            subAssembliesByBom: new System.Collections.Generic.Dictionary<Guid, System.Collections.Generic.IReadOnlyList<(Guid, decimal)>>
            {
                [rootBom] = new[] { (midBom, 2m) },  // 3 * 2 = 6 midBom
                [midBom] = new[] { (innerBom, 4m) }, // 6 * 4 = 24 innerBom
            },
            secondaryItemsByBom: new System.Collections.Generic.Dictionary<Guid, System.Collections.Generic.IReadOnlyList<(Guid, decimal)>>
            {
                [midBom] = new[] { (midSecItem, 0.5m) },     // 6 * 0.5 = 3
                [innerBom] = new[] { (innerSecItem, 0.25m) }, // 24 * 0.25 = 6
            });
        scaledSecs.Single(x => x.ItemId == midSecItem).ScaledQty.ShouldBe(3m);
        scaledSecs.Single(x => x.ItemId == innerSecItem).ScaledQty.ShouldBe(6m);

        // 5. ERPNext 7df8a18e13 + 7949464d7e: outer sub-assembly co-product basis deducts ONLY own ValuationRate/Manual + inner sub-assembly rows (not sibling BOMs)
        var siblingBom = Guid.NewGuid();
        var netBasis = MyERP.Inventory.DomainServices.StockEntryManager.CalculateOuterCoProductNetCostBasis(
            bomId: midBom,
            grossComponentCost: 1000m,
            subAssemblyBomIdsUnderBom: new System.Collections.Generic.HashSet<Guid> { innerBom },
            secondaryRows: new[]
            {
                (innerBom, MyERP.Manufacturing.SecondaryItemValuationType.PercentageOfComponentCost, 120m), // deducted (nested under midBom)
                (midBom, MyERP.Manufacturing.SecondaryItemValuationType.Manual, 80m),                        // deducted (own Manual)
                (midBom, MyERP.Manufacturing.SecondaryItemValuationType.PercentageOfComponentCost, 200m),    // NOT deducted (own %)
                (siblingBom, MyERP.Manufacturing.SecondaryItemValuationType.PercentageOfComponentCost, 300m),// NOT deducted (sibling BOM)
            });
        netBasis.ShouldBe(800m); // 1000 - 120 - 80
    }

    [Fact]
    public void UpstreamBatch9_MyInvoisCreditNoteAbsAmounts_CorrectiveCost_WholeNumberShare_InterCompanyRateLock_Parity()
    {
        // 1. MyInvois createxml.py: Credit Note (02) emits non-negative absolute amounts & AllowanceTotalAmount
        var builder = new MyERP.EInvoice.Services.InvoiceDocumentBuilder();
        var cnXml = System.Xml.Linq.XDocument.Parse(builder.Build(new MyERP.EInvoice.Services.EInvoiceDocumentData
        {
            InvoiceNumber = "CN-2026-01",
            IssueDate = new DateTime(2026, 7, 2, 10, 0, 0, DateTimeKind.Utc),
            DocumentTypeCode = "02",
            CurrencyCode = "MYR",
            BillingReferenceNumber = "SI-2026-01",
            BillingReferenceUuid = "UUID-ORIG-01",
            Supplier = new MyERP.EInvoice.Services.EInvoicePartyData { Name = "Seller Sdn Bhd", Tin = "C1000", CountryCode = "MYS", State = "14" },
            Buyer = new MyERP.EInvoice.Services.EInvoicePartyData { Name = "Buyer Sdn Bhd", Tin = "C2000", CountryCode = "MYS", State = "14" },
            DiscountAmount = -10m,
            NetTotal = -100m,
            TaxAmount = -6m,
            GrandTotal = -106m,
            TaxBreakdown = new System.Collections.Generic.List<MyERP.EInvoice.Services.EInvoiceTaxBreakdown>
            {
                new() { TaxCategoryCode = "01", TaxRate = 6m, TaxableAmount = -100m, TaxAmount = -6m }
            },
            Lines = new System.Collections.Generic.List<MyERP.EInvoice.Services.EInvoiceLineData>
            {
                new()
                {
                    Description = "Returned Widget",
                    Quantity = -2m,
                    UnitPrice = 50m,
                    DiscountAmount = -10m,
                    TaxCategoryCode = "01",
                    TaxRate = 6m,
                    TaxAmount = -6m,
                }
            }
        }));
        System.Xml.Linq.XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
        System.Xml.Linq.XNamespace cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
        var lmt = cnXml.Root!.Element(cac + "LegalMonetaryTotal")!;
        lmt.Element(cbc + "LineExtensionAmount")!.Value.ShouldBe("100.00");
        lmt.Element(cbc + "TaxExclusiveAmount")!.Value.ShouldBe("100.00");
        lmt.Element(cbc + "TaxInclusiveAmount")!.Value.ShouldBe("106.00");
        lmt.Element(cbc + "AllowanceTotalAmount")!.Value.ShouldBe("10.00");
        lmt.Element(cbc + "PayableAmount")!.Value.ShouldBe("106.00");
        cnXml.Root!.Element(cac + "TaxTotal")!.Element(cbc + "TaxAmount")!.Value.ShouldBe("6.00");
        var line0 = cnXml.Root!.Element(cac + "InvoiceLine")!;
        line0.Element(cbc + "InvoicedQuantity")!.Value.ShouldBe("2.0000");
        line0.Element(cbc + "LineExtensionAmount")!.Value.ShouldBe("100.00");
        line0.Element(cac + "AllowanceCharge")!.Element(cbc + "Amount")!.Value.ShouldBe("10.00");
        line0.Element(cac + "TaxTotal")!.Element(cbc + "TaxAmount")!.Value.ShouldBe("6.00");

        // 2. ERPNext ac5cc09181: corrective operation cost spreads over finished-good qty only when tracking semi-FG
        var fgItemId = Guid.NewGuid();
        var semiFgItemId = Guid.NewGuid();
        var op1 = Guid.NewGuid();
        var op2 = Guid.NewGuid();
        MyERP.Manufacturing.DomainServices.JobCardManager.CalculateCorrectiveOperationCostBasisQty(
            workOrderProductionItemId: fgItemId,
            trackSemiFinishedGoods: true,
            jobCardCompletions: new[]
            {
                (op1, (Guid?)semiFgItemId, 2m), // semi-FG ignored
                (op2, (Guid?)fgItemId, 10m),    // FG counted
            }).ShouldBe(10m);

        // 3. ERPNext a88ca080d7: transfer-based Manufacture raw material qty keeps small share < qty precision for whole-number UOM
        MyERP.Inventory.DomainServices.StockEntryManager.CalculateTransferBasedManufactureRawMaterialQty(
            rowQty: 1m, fgCompletedQty: 1m, pendingQtyToManufacture: 250m, isReturn: false, uomMustBeWholeNumber: true, qtyPrecision: 2)
            .ShouldBe(1m); // 0.004 rounds to 0.00 at precision 2, but 9-dec pre-round keeps 0.004 -> ceil to 1
        MyERP.Inventory.DomainServices.StockEntryManager.CalculateTransferBasedManufactureRawMaterialQty(
            rowQty: 7m, fgCompletedQty: 3m, pendingQtyToManufacture: 3m, isReturn: false, uomMustBeWholeNumber: true, qtyPrecision: 2)
            .ShouldBe(7m); // exact 7 not ceiled to 8

        // 4. ERPNext 8eeacf1177: stock qty for party packing unit rounds to row stock_qty precision
        MyERP.Inventory.DomainServices.ItemDetailsResolverService
            .RoundStockQtyForPartyPackingUnit(rowQty: 1.33336m, conversionFactor: 3m, rowStockQtyPrecision: 4)
            .ShouldBe(4.0001m);

        // 5. ERPNext 2de31e743c + 453051e1c7: rate lock & same-rate check apply to inter-company sales/purchases, skip only internal transfers
        MyERP.Inventory.DomainServices.ItemDetailsResolverService
            .ShouldEnforceMaintainSameRateAndSourceRateLock(maintainSameRateEnabled: true, isReturn: false, isInternalTransfer: false)
            .ShouldBeTrue();
        MyERP.Inventory.DomainServices.ItemDetailsResolverService
            .ShouldEnforceMaintainSameRateAndSourceRateLock(maintainSameRateEnabled: true, isReturn: false, isInternalTransfer: true)
            .ShouldBeFalse();

        // 6. ERPNext 5b9c476c29: packed row keeps incoming rate when serial numbers are valued as a pool (avg_rate = 0)
        MyERP.Inventory.DomainServices.ItemDetailsResolverService
            .ResolvePackedRowIncomingRateFromBundle(currentIncomingRate: 45m, bundleAvgRate: 0m, isSerialNoWiseValuationDisabled: true)
            .ShouldBe(45m);
        MyERP.Inventory.DomainServices.ItemDetailsResolverService
            .ResolvePackedRowIncomingRateFromBundle(currentIncomingRate: 45m, bundleAvgRate: 52.125m, isSerialNoWiseValuationDisabled: false)
            .ShouldBe(52.125m);
    }

    [Fact]
    public void UpstreamBatch10_XAdesSigner_InwardReservationReleaseRestore_RejectedReturnGL_PlannedSubAssembly_Parity()
    {
        // 1. MyInvois XAdES v1.1 signer: single-hashed RSA-SHA256 signature, base-10 decimal serial, UBL ExtensionURI & Transforms
        using (var rsa = System.Security.Cryptography.RSA.Create(2048))
        {
            var req = new System.Security.Cryptography.X509Certificates.CertificateRequest(
                "CN=MyERP Test Signer", rsa, System.Security.Cryptography.HashAlgorithmName.SHA256,
                System.Security.Cryptography.RSASignaturePadding.Pkcs1);
            using var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
            var pfx = cert.Export(System.Security.Cryptography.X509Certificates.X509ContentType.Pfx, "test-pw");

            var signer = new MyERP.EInvoice.Services.InvoiceDocumentSigner();
            const string rawXml = "<Invoice xmlns=\"urn:oasis:names:specification:ubl:schema:xsd:Invoice-2\" xmlns:cbc=\"urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2\"><cbc:ID>SI-SIGN-01</cbc:ID></Invoice>";
            var signedXml = signer.Sign(rawXml, pfx, "test-pw");

            signedXml.ShouldContain("urn:oasis:names:specification:ubl:dsig:enveloped:xades");
            signedXml.ShouldContain("<sbc:ReferencedSignatureID>urn:oasis:names:specification:ubl:signature:Invoice</sbc:ReferencedSignatureID>");
            signedXml.ShouldContain("not(//ancestor-or-self::ext:UBLExtensions)");

            var parsed = System.Xml.Linq.XDocument.Parse(signedXml);
            System.Xml.Linq.XNamespace ds = "http://www.w3.org/2000/09/xmldsig#";
            var serialText = parsed.Descendants(ds + "X509SerialNumber").Single().Value;
            serialText.All(char.IsDigit).ShouldBeTrue(); // base-10 decimal integer, never hex
            serialText.ShouldBe(MyERP.EInvoice.Services.InvoiceDocumentSigner.GetDecimalSerialNumber(cert));

            var sigB64 = parsed.Descendants(ds + "SignatureValue").Single().Value;
            var sigBytes = Convert.FromBase64String(sigB64);
            var canonicalBytes = System.Text.Encoding.UTF8.GetBytes(rawXml);
            rsa.VerifyData(
                canonicalBytes,
                sigBytes,
                System.Security.Cryptography.HashAlgorithmName.SHA256,
                System.Security.Cryptography.RSASignaturePadding.Pkcs1).ShouldBeTrue();
        }

        // 2. ERPNext a442d94273 + e00d5c12b7 + d6d9be7765: release never dips below UsedQty; cancel restores up to VoucherQty
        var scioRow = Guid.NewGuid();
        var woSre = Guid.NewGuid();
        var released = MyERP.Inventory.DomainServices.StockReservationManager.ReleaseUnusedInwardWorkOrderReservations(
            reservations: new[] { (woSre, scioRow, 10m, 7m) }, // 7 already used -> max releasable = 3
            shortQtyByScioDetailId: new System.Collections.Generic.Dictionary<Guid, decimal> { [scioRow] = 5m });
        released[woSre].ShouldBe(3m);

        var restored = MyERP.Inventory.DomainServices.StockReservationManager.RestoreInwardWorkOrderReservationsOnReturnCancel(
            reservations: new[] { (woSre, scioRow, 10m, 7m) }, // VoucherQty=10, ReservedQty=7 -> gap=3
            freeInwardQtyByScioDetailId: new System.Collections.Generic.Dictionary<Guid, decimal> { [scioRow] = 5m },
            returnQtyByScioDetailId: new System.Collections.Generic.Dictionary<Guid, decimal> { [scioRow] = 2m });
        restored[woSre].ShouldBe(2m); // capped by returnQty=2

        // 3. ERPNext 097411fb47 + 3837f7b46a: rejected warehouse return debits at receipt net rate when original had accepted qty
        MyERP.Purchasing.DomainServices.PurchaseReceiptManager.ResolveRejectedWarehouseReturnDebitAmount(
            isReturn: true,
            returnQtyFromRejectedWarehouse: true,
            isInternalTransfer: false,
            originalAcceptedQty: 5m,
            returnStockQty: -4m,
            baseNetRate: 120m,
            conversionFactor: 2m, // stock unit net rate = 60 -> -4 * 60 = -240
            fallbackOutgoingAmount: -275m)
            .ShouldBe(-240m);
        MyERP.Purchasing.DomainServices.PurchaseReceiptManager.ResolveRejectedWarehouseReturnDebitAmount(
            isReturn: true,
            returnQtyFromRejectedWarehouse: true,
            isInternalTransfer: false,
            originalAcceptedQty: 0m, // fully rejected receipt -> fallback
            returnStockQty: -4m,
            baseNetRate: 120m,
            conversionFactor: 2m,
            fallbackOutgoingAmount: -275m)
            .ShouldBe(-275m);

        // 4. ERPNext 1449380676 + c3437b2eba + 07c5dfcab8: Production Plan explodes planned Subcontract sub-assembly when subcontracting enabled
        var subItem = Guid.NewGuid();
        var subBom = Guid.NewGuid();
        var plannedTypes = new System.Collections.Generic.Dictionary<(Guid, Guid?), string>
        {
            [(subItem, subBom)] = "Subcontract"
        };
        var whenSubIncluded = MyERP.Manufacturing.DomainServices.ProductionPlanMaterialPlanningService
            .EvaluatePlannedSubAssemblyExplosion(
                subItem, subBom, qty: 2m, isPhantomItem: false,
                includeExplodedItems: true, includeSubcontractedItems: true, defaultShouldExplode: false,
                requestedSubAssemblies: null, plannedSubAssemblyTypes: plannedTypes);
        whenSubIncluded.SkipSubitem.ShouldBeFalse();
        whenSubIncluded.ShouldExplode.ShouldBeTrue();

        var whenSubExcluded = MyERP.Manufacturing.DomainServices.ProductionPlanMaterialPlanningService
            .EvaluatePlannedSubAssemblyExplosion(
                subItem, subBom, qty: 2m, isPhantomItem: false,
                includeExplodedItems: true, includeSubcontractedItems: false, defaultShouldExplode: true,
                requestedSubAssemblies: null, plannedSubAssemblyTypes: plannedTypes);
        whenSubExcluded.SkipSubitem.ShouldBeTrue();
        whenSubExcluded.ShouldExplode.ShouldBeFalse();
    }

    [Fact]
    public async System.Threading.Tasks.Task UpstreamBatch11_LhdnCaseInsensitiveStatus_SemiFgCost_PpClosure_ScioRatio_ClosedWoConsumption_Parity()
    {
        // 1. MyInvois get_status.py: case-insensitive Valid/Cancelled status matching in EInvoiceService.RefreshStatusAsync
        var subId = Guid.NewGuid();
        var sub = new MyERP.EInvoice.Entities.EInvoiceSubmission(subId, Guid.NewGuid(), "SalesInvoice", Guid.NewGuid())
        {
            SubmissionUid = "SUB-11",
            Status = "Pending"
        };

        var subRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.EInvoice.Entities.EInvoiceSubmission, Guid>>();
        NSubstitute.SubstituteExtensions.Returns(subRepo.GetAsync(subId), sub);
        var logRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.EInvoice.Entities.LhdnSuccessLog, Guid>>();
        var apiClient = NSubstitute.Substitute.For<MyERP.EInvoice.Services.ILhdnApiClient>();
        NSubstitute.SubstituteExtensions.Returns(
            apiClient.GetDocumentStatusAsync("tok", "SUB-11", MyERP.EInvoice.LhdnEnvironment.Sandbox),
            new MyERP.EInvoice.Services.LhdnStatusResponse
            {
                IsSuccess = true,
                StatusCode = 200,
                Status = " valid ",
                DocumentUuid = "UUID-11",
                LongId = "LONG-11"
            });

        var einvSvc = new MyERP.EInvoice.Services.EInvoiceService(apiClient, subRepo, logRepo);
        var refreshed = await einvSvc.RefreshStatusAsync(subId, "tok", MyERP.EInvoice.LhdnEnvironment.Sandbox);
        refreshed.Status.ShouldBe("valid");
        refreshed.ValidatedAt.ShouldNotBeNull();
        refreshed.QrCodeUrl.ShouldBe("https://preprod.myinvois.hasil.gov.my/UUID-11/share/LONG-11");

        // 2. ERPNext a96f709df4: WO-level operating/corrective cost booked on FG entry only, not semi-FG Job Card
        var fgId = Guid.NewGuid();
        var semiId = Guid.NewGuid();
        MyERP.Manufacturing.DomainServices.JobCardManager
            .ShouldBookWorkOrderLevelOperatingCostOnEntry(fgId, semiId).ShouldBeFalse();
        MyERP.Manufacturing.DomainServices.JobCardManager
            .ShouldBookWorkOrderLevelOperatingCostOnEntry(fgId, fgId).ShouldBeTrue();
        MyERP.Manufacturing.DomainServices.JobCardManager
            .ShouldBookWorkOrderLevelOperatingCostOnEntry(fgId, null).ShouldBeTrue();

        // 3. ERPNext d6c1294e97 + 0b4b095b8d: Production Plan checks only SO rows receiving positive allocated qty
        var refKey = Guid.NewGuid();
        var openSoRow = Guid.NewGuid();
        var closedZeroAllocSoRow = Guid.NewGuid();
        var checkedSoRows = MyERP.Manufacturing.DomainServices.ProductionPlanMaterialPlanningService
            .ResolvePlannedSalesOrderRowsForClosureCheck(
                planRows: new[] { ((Guid?)null, refKey, 5m) },
                combinedReferences: new[]
                {
                    (refKey, openSoRow, 5m),            // gets all 5
                    (refKey, closedZeroAllocSoRow, 3m), // gets 0 -> excluded from closed-row check
                });
        checkedSoRows.ShouldContain(openSoRow);
        checkedSoRows.ShouldNotContain(closedZeroAllocSoRow);

        // 4. ERPNext a36fc2fd7a: SCIO keeps existing conversion ratio when Sales Order row qty is zero
        MyERP.Purchasing.DomainServices.SubcontractingManager
            .ResolveInwardOrderSalesConversionFactor(existingRatio: 2.5m, salesOrderStockQty: 0m, salesOrderFgItemQty: 10m)
            .ShouldBe(2.5m);
        MyERP.Purchasing.DomainServices.SubcontractingManager
            .ResolveInwardOrderSalesConversionFactor(existingRatio: 2.5m, salesOrderStockQty: 20m, salesOrderFgItemQty: 5m)
            .ShouldBe(4m);

        // 5. ERPNext 7faed55958: Closed Work Order rejects return-flagged MaterialConsumptionForManufacture, allows only return transfer
        MyERP.Inventory.DomainServices.StockEntryManager
            .ShouldRejectStockEntryAgainstClosedWorkOrder(true, MyERP.Inventory.StockEntryType.MaterialConsumptionForManufacture, isReturn: true)
            .ShouldBeTrue();
        MyERP.Inventory.DomainServices.StockEntryManager
            .ShouldRejectStockEntryAgainstClosedWorkOrder(true, MyERP.Inventory.StockEntryType.MaterialTransferForManufacture, isReturn: true)
            .ShouldBeFalse();
        MyERP.Inventory.DomainServices.StockEntryManager
            .ShouldRejectStockEntryAgainstClosedWorkOrder(true, MyERP.Inventory.StockEntryType.MaterialTransferForManufacture, isReturn: false)
            .ShouldBeTrue();
    }

    [Fact]
    public void UpstreamBatch12_BomParentPropagation_ComponentCostSplit_CopqOpenLogs_PiResetQty_Parity()
    {
        // 1. ERPNext 2dde60cc73 + 42a3a8342d: parent BOM propagation fires when EITHER TotalCost OR BaseTotalCost changes
        MyERP.Manufacturing.DomainServices.BomCostPropagationService
            .ShouldPropagateCostToParentBoms(true, 100m, 100m, 450m, 460m).ShouldBeTrue();  // base changed
        MyERP.Manufacturing.DomainServices.BomCostPropagationService
            .ShouldPropagateCostToParentBoms(true, 100m, 105m, 450m, 450m).ShouldBeTrue();  // total changed
        MyERP.Manufacturing.DomainServices.BomCostPropagationService
            .ShouldPropagateCostToParentBoms(true, 100m, 100m, 450m, 450m).ShouldBeFalse(); // unchanged
        MyERP.Manufacturing.DomainServices.BomCostPropagationService
            .ShouldPropagateCostToParentBoms(false, 100m, 105m, 450m, 460m).ShouldBeFalse(); // disabled

        // 2. ERPNext 1bc6b681ff + 21589f031f + 6688a76b2e: cumulative rounding keeps component shares equal to exact amount
        var shares = MyERP.Manufacturing.DomainServices.JobCardManager.SplitOperationCostByWorkstationComponents(
            amount: 100m,
            workstationComponents: new[]
            {
                ("Electricity", 1m),
                ("Rent", 1m),
                ("Wages", 1m),
            },
            precision: 2);
        shares.Select(s => s.Amount).Sum().ShouldBe(100m); // 33.33 + 33.34 + 33.33 = 100.00 (no penny loss)
        shares.Select(s => s.OperatingComponent).ShouldBe(new[] { "Electricity", "Rent", "Wages" });

        // 3. ERPNext d507648396: Cost of Poor Quality date-range clipping skips open time logs (null FromTime or ToTime)
        var t0 = new DateTime(2026, 7, 1, 8, 0, 0, DateTimeKind.Utc);
        var clippedMins = MyERP.Manufacturing.DomainServices.JobCardManager.CalculateClippedTimeLogMinutesInDateRange(
            timeLogs: new[]
            {
                ((DateTime?)t0.AddHours(-1), (DateTime?)t0.AddHours(1)), // clipped to [t0, t0+1h] -> 60m
                ((DateTime?)t0.AddHours(1), (DateTime?)null),           // open log -> skipped
                ((DateTime?)null, (DateTime?)t0.AddHours(2)),           // invalid open log -> skipped
            },
            rangeStart: t0,
            rangeEnd: t0.AddHours(4));
        clippedMins.ShouldBe(60m);

        // 4. ERPNext 3496853eae: turning off UpdateStock on PI clears received/rejected qty on unlinked rows only
        var unlinkedRow = Guid.NewGuid();
        var prLinkedRow = Guid.NewGuid();
        var prDetailId = Guid.NewGuid();
        var resetRows = MyERP.Purchasing.DomainServices.PurchaseReceiptManager.ResetUnlinkedPurchaseInvoiceReceiptQuantities(
            updateStock: false,
            rows: new[]
            {
                (unlinkedRow, (Guid?)null, 10m, 2m),
                (prLinkedRow, (Guid?)prDetailId, 8m, 1m),
            });
        resetRows.Single(r => r.RowId == unlinkedRow).ShouldBe((unlinkedRow, 0m, 0m));
        resetRows.Single(r => r.RowId == prLinkedRow).ShouldBe((prLinkedRow, 8m, 1m));
    }

    [Fact]
    public void UpstreamBatch13_PiPendingReceiptDebitNotes_SoUnreservedPrecision_StockRepostStop_Parity()
    {
        // 1. ERPNext 4dd309aec7 + 3e5f527cfd + d82d54cf42: sum debit notes in stock UOM before dividing by row conversion factor
        // 3 debit notes of -1/3 each in Box UOM (factor 1) against 1 Box billed (factor 3): summed stock = 3 -> 3/3 = 1 Box debited -> 0 pending
        var pendingZero = MyERP.Purchasing.DomainServices.PurchaseReceiptManager.CalculatePurchaseInvoicePendingReceiptQty(
            invoiceQty: 1m,
            invoiceConversionFactor: 3m,
            receivedAndMappedQty: 0m,
            submittedDebitNotes: new[] { (-1m, 1m), (-1m, 1m), (-1m, 1m) },
            qtyPrecision: 4);
        pendingZero.ShouldBe(0m);

        // 12 Nos billed (factor = 1/12), 5 Nos debited -> exact 7 Nos remaining (not 6.96 from rounded stock_qty)
        var pendingExact = MyERP.Purchasing.DomainServices.PurchaseReceiptManager.CalculatePurchaseInvoicePendingReceiptQty(
            invoiceQty: 12m,
            invoiceConversionFactor: 1m / 12m,
            receivedAndMappedQty: 0m,
            submittedDebitNotes: new[] { (-5m, 1m / 12m) },
            qtyPrecision: 4);
        pendingExact.ShouldBe(7m);

        // 2. ERPNext 072fe92f40: SO unreserved stock qty rounds at stock_qty precision before positivity check
        MyERP.Inventory.DomainServices.StockReservationManager
            .CalculateSalesOrderUnreservedStockQty(10m, 5m, 4.99999m, stockQtyPrecision: 4)
            .ShouldBe(0m); // 0.00001 rounds to 0 at precision 4
        MyERP.Inventory.DomainServices.StockReservationManager
            .CalculateSalesOrderUnreservedStockQty(10m, 5m, 3m, stockQtyPrecision: 4)
            .ShouldBe(2m);

        // 3. ERPNext 41d5d3c132 + 2ec98826fc: repost skips cancelled vouchers and stops at Stock Reconciliation or unchanged balance
        var cancelledAction = MyERP.Inventory.DomainServices.StockEntryManager.EvaluateStockRepostEntryAction(
            isVoucherCancelled: true, isStockReconciliation: false,
            previousQtyAfterTx: 10m, newQtyAfterTx: 12m, previousStockValue: 100m, newStockValue: 120m);
        cancelledAction.ShouldBe((true, false));

        var recoAction = MyERP.Inventory.DomainServices.StockEntryManager.EvaluateStockRepostEntryAction(
            isVoucherCancelled: false, isStockReconciliation: true,
            previousQtyAfterTx: 10m, newQtyAfterTx: 12m, previousStockValue: 100m, newStockValue: 120m);
        recoAction.ShouldBe((false, true));

        var unchangedAction = MyERP.Inventory.DomainServices.StockEntryManager.EvaluateStockRepostEntryAction(
            isVoucherCancelled: false, isStockReconciliation: false,
            previousQtyAfterTx: 10m, newQtyAfterTx: 10m, previousStockValue: 100m, newStockValue: 100m);
        unchangedAction.ShouldBe((false, true));

        var changedAction = MyERP.Inventory.DomainServices.StockEntryManager.EvaluateStockRepostEntryAction(
            isVoucherCancelled: false, isStockReconciliation: false,
            previousQtyAfterTx: 10m, newQtyAfterTx: 10m, previousStockValue: 100m, newStockValue: 105m);
        changedAction.ShouldBe((false, false));
    }

    [Fact]
    public void UpstreamBatch14_SupplierTtx_PickListMatch_OverduePoDate_BlanketOrderQuotation_SalvageRecompute_Parity()
    {
        // 1. MyInvois purchase_invoice.py: BuildSupplierParty emits schemeID="TTX" when TourismTaxNumber is set
        var builder = new MyERP.EInvoice.Services.InvoiceDocumentBuilder();
        var xml = System.Xml.Linq.XDocument.Parse(builder.Build(new MyERP.EInvoice.Services.EInvoiceDocumentData
        {
            InvoiceNumber = "SI-TTX-01",
            IssueDate = new DateTime(2026, 7, 5, 10, 0, 0, DateTimeKind.Utc),
            DocumentTypeCode = "01",
            CurrencyCode = "MYR",
            Supplier = new MyERP.EInvoice.Services.EInvoicePartyData
            {
                Name = "Hotel Resort Sdn Bhd",
                Tin = "C9999",
                SstRegistration = "W10-1234",
                TourismTaxNumber = "TTX-8888",
                CountryCode = "MYS",
                State = "14",
            },
            Buyer = new MyERP.EInvoice.Services.EInvoicePartyData { Name = "Guest", Tin = "EI00000000020", CountryCode = "MYS", State = "14" },
            NetTotal = 200m,
            TaxAmount = 0m,
            GrandTotal = 200m,
            Lines = new System.Collections.Generic.List<MyERP.EInvoice.Services.EInvoiceLineData>
            {
                new() { Description = "Room Stay", Quantity = 1m, UnitPrice = 200m }
            }
        }));
        System.Xml.Linq.XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
        System.Xml.Linq.XNamespace cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
        var supplierParty = xml.Root!.Element(cac + "AccountingSupplierParty")!.Element(cac + "Party")!;
        supplierParty.Elements(cac + "PartyIdentification")
            .First(x => x.Element(cbc + "ID")!.Attribute("schemeID")!.Value == "TTX")
            .Element(cbc + "ID")!.Value.ShouldBe("TTX-8888");

        // 2. ERPNext ca335b6613: Delivery row delivering wrong item against Pick List row is rejected
        var plRowId = Guid.NewGuid();
        var pickedItemId = Guid.NewGuid();
        var wrongItemId = Guid.NewGuid();
        var plEx = Should.Throw<Volo.Abp.BusinessException>(() =>
            MyERP.Sales.DomainServices.DeliveryNoteManager.ValidateDeliveryItemsMatchPickListItems(
                deliveryRows: new[] { (1, wrongItemId, (Guid?)plRowId) },
                pickedItemIdByPickListItemId: new System.Collections.Generic.Dictionary<Guid, Guid> { [plRowId] = pickedItemId }));
        plEx.Code.ShouldBe(MyERP.MyERPDomainErrorCodes.ValidationFailed);
        plEx.Data["detail"]!.ToString()!.ShouldContain("Row #1");

        // 3. ERPNext 3a7d1a09b6: Overdue Sales Order delivery date falls back to today for mapped PO schedule date
        var today = new DateTime(2026, 7, 10);
        MyERP.Sales.DomainServices.SalesOrderManager
            .ResolvePurchaseOrderScheduleDateFromSalesOrder(new DateTime(2026, 7, 1), today)
            .ShouldBe(today);
        MyERP.Sales.DomainServices.SalesOrderManager
            .ResolvePurchaseOrderScheduleDateFromSalesOrder(new DateTime(2026, 7, 20), today)
            .ShouldBe(new DateTime(2026, 7, 20));

        // 4. ERPNext 138a0e0c0a: Quotation accepts only Selling Blanket Order of same customer (and skips qty-limit check)
        var custA = Guid.NewGuid();
        var custB = Guid.NewGuid();
        MyERP.Sales.DomainServices.SalesOrderManager
            .ValidateBlanketOrderMatchesDocument("Quotation", "Selling", custA, custA)
            .ShouldBeFalse(); // skip qty limit on Quotation
        MyERP.Sales.DomainServices.SalesOrderManager
            .ValidateBlanketOrderMatchesDocument("SalesOrder", "Selling", custA, custA)
            .ShouldBeTrue();  // enforce qty limit on SalesOrder
        Should.Throw<Volo.Abp.BusinessException>(() =>
            MyERP.Sales.DomainServices.SalesOrderManager.ValidateBlanketOrderMatchesDocument("Quotation", "Purchasing", custA, custA));
        Should.Throw<Volo.Abp.BusinessException>(() =>
            MyERP.Sales.DomainServices.SalesOrderManager.ValidateBlanketOrderMatchesDocument("Quotation", "Selling", custA, custB));

        // 5. ERPNext 1211552f18: Asset salvage value recomputed from percentage when net purchase amount changes
        MyERP.Assets.DomainServices.AssetLifecycleManager
            .RecomputeSalvageValueFromPercentage(netPurchaseAmount: 15000m, salvageValuePercentage: 10m, currentSalvageValue: 1000m)
            .ShouldBe(1500m);
        MyERP.Assets.DomainServices.AssetLifecycleManager
            .RecomputeSalvageValueFromPercentage(netPurchaseAmount: 15000m, salvageValuePercentage: 0m, currentSalvageValue: 1000m)
            .ShouldBe(1000m);
    }

    [Fact]
    public void UpstreamBatch15_DeliveryPartyId_BomHourRate_ScrFgShare_SeCancel_WoPickList_Parity()
    {
        // 1. MyInvois: DeliveryParty emits PartyIdentification (TIN, ID) when provided, omitted when absent
        var builder = new MyERP.EInvoice.Services.InvoiceDocumentBuilder();
        var xml = System.Xml.Linq.XDocument.Parse(builder.Build(new MyERP.EInvoice.Services.EInvoiceDocumentData
        {
            InvoiceNumber = "SI-2026-0015",
            IssueDate = new DateTime(2026, 7, 15, 10, 0, 0, DateTimeKind.Utc),
            DocumentTypeCode = "01",
            CurrencyCode = "MYR",
            Supplier = new MyERP.EInvoice.Services.EInvoicePartyData { Name = "Supplier Sdn Bhd", Tin = "C1111111111", Address = "Jalan 1", City = "KL", State = "14", CountryCode = "MYS" },
            Buyer = new MyERP.EInvoice.Services.EInvoicePartyData { Name = "Buyer Sdn Bhd", Tin = "C2222222222", Address = "Jalan 2", City = "KL", State = "14", CountryCode = "MYS" },
            Delivery = new MyERP.EInvoice.Services.EInvoiceDeliveryData
            {
                RecipientName = "Warehouse A",
                Tin = "C3333333333",
                IdType = "BRN",
                IdValue = "202001012345",
                Address = "Lot 9",
                City = "Shah Alam",
                State = "10",
                PostalCode = "40000",
                CountryCode = "MYS"
            },
            NetTotal = 100m,
            TaxAmount = 0m,
            GrandTotal = 100m,
            Lines = new System.Collections.Generic.List<MyERP.EInvoice.Services.EInvoiceLineData>
            {
                new() { Description = "Item A", Quantity = 1m, UnitPrice = 100m }
            }
        }));
        System.Xml.Linq.XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
        System.Xml.Linq.XNamespace cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
        var deliveryParty = xml.Root!.Element(cac + "Delivery")!.Element(cac + "DeliveryParty")!;
        deliveryParty.Elements(cac + "PartyIdentification")
            .First(x => x.Element(cbc + "ID")!.Attribute("schemeID")!.Value == "TIN")
            .Element(cbc + "ID")!.Value.ShouldBe("C3333333333");
        deliveryParty.Elements(cac + "PartyIdentification")
            .First(x => x.Element(cbc + "ID")!.Attribute("schemeID")!.Value == "BRN")
            .Element(cbc + "ID")!.Value.ShouldBe("202001012345");

        // 2. ERPNext 6d04024f6d / 07106a9125 / 0af88d7771 / 85124767ea / e86eeba24d / db5a3ce30f:
        //    BOM operation hour_rate refetch on workstation change, currency rescale, precision rounding, null==empty, zero-rate guard
        // Workstation changed (rate unchanged from previous) -> refetch new workstation rate & divide by conversion rate, rounded
        MyERP.Manufacturing.Entities.BomOperation.ResolveUpdatedHourRate(
            currentHourRate: 50m, previousHourRate: 50m,
            previousWorkstation: "WS-1", currentWorkstation: "WS-2",
            previousWorkstationType: null, currentWorkstationType: "",
            previousConversionRate: 1m, currentConversionRate: 3m,
            fetchWorkstationHourRateInCompanyCurrency: () => 100m,
            precision: 2).ShouldBe(33.33m);
        // Only conversion rate changed -> rescale to preserve company-currency rate (even manual rate)
        MyERP.Manufacturing.Entities.BomOperation.ResolveUpdatedHourRate(
            currentHourRate: 100m, previousHourRate: 100m,
            previousWorkstation: "WS-1", currentWorkstation: "WS-1",
            previousWorkstationType: null, currentWorkstationType: "",
            previousConversionRate: 4m, currentConversionRate: 2m,
            fetchWorkstationHourRateInCompanyCurrency: () => 999m,
            precision: 2).ShouldBe(200m);
        // Null vs empty workstation treated as unchanged (do not clear manual rate)
        MyERP.Manufacturing.Entities.BomOperation.ResolveUpdatedHourRate(
            currentHourRate: 75m, previousHourRate: 75m,
            previousWorkstation: null, currentWorkstation: "",
            previousWorkstationType: "", currentWorkstationType: null,
            previousConversionRate: 1m, currentConversionRate: 1m,
            fetchWorkstationHourRateInCompanyCurrency: () => 999m).ShouldBe(75m);
        // Zero old or new conversion rate -> skip rescaling
        MyERP.Manufacturing.Entities.BomOperation.ResolveUpdatedHourRate(
            currentHourRate: 80m, previousHourRate: 80m,
            previousWorkstation: "WS-1", currentWorkstation: "WS-1",
            previousWorkstationType: null, currentWorkstationType: null,
            previousConversionRate: 0m, currentConversionRate: 4m).ShouldBe(80m);

        // 3. ERPNext 7938233f41: Non-return SCR gives FG 100 - sum(present % co-products); return keeps BOM share
        MyERP.Purchasing.DomainServices.SubcontractingManager
            .ResolveFinishedGoodCostAllocationPercentage(isReturn: false, bomCostAllocationPercentage: 60m, receiptPercentageSecondaryAllocations: new[] { 15m })
            .ShouldBe(85m);
        MyERP.Purchasing.DomainServices.SubcontractingManager
            .ResolveFinishedGoodCostAllocationPercentage(isReturn: false, bomCostAllocationPercentage: 60m, receiptPercentageSecondaryAllocations: Array.Empty<decimal>())
            .ShouldBe(100m);
        MyERP.Purchasing.DomainServices.SubcontractingManager
            .ResolveFinishedGoodCostAllocationPercentage(isReturn: true, bomCostAllocationPercentage: 60m, receiptPercentageSecondaryAllocations: new[] { 15m })
            .ShouldBe(60m);

        // 4. ERPNext d12484abad: Block cancelling SendToSubcontractor OR reserved-components return when later entry moved reserved stock
        var seEx1 = Should.Throw<Volo.Abp.BusinessException>(() =>
            MyERP.Inventory.DomainServices.StockEntryManager.ValidateReservedStockNotMovedOn(
                MyERP.Inventory.StockEntryType.SendToSubcontractor,
                isReservedComponentsReturn: false,
                hasLaterActiveReservationFromThisEntry: true,
                subcontractingOrderNumber: "SCO-0001"));
        seEx1.Code.ShouldBe(MyERP.MyERPDomainErrorCodes.ValidationFailed);
        seEx1.Data["detail"]!.ToString()!.ShouldContain("SCO-0001");

        var seEx2 = Should.Throw<Volo.Abp.BusinessException>(() =>
            MyERP.Inventory.DomainServices.StockEntryManager.ValidateReservedStockNotMovedOn(
                MyERP.Inventory.StockEntryType.MaterialTransfer,
                isReservedComponentsReturn: true,
                hasLaterActiveReservationFromThisEntry: true,
                subcontractingOrderNumber: "SCO-0001"));
        seEx2.Code.ShouldBe(MyERP.MyERPDomainErrorCodes.ValidationFailed);

        Should.NotThrow(() =>
            MyERP.Inventory.DomainServices.StockEntryManager.ValidateReservedStockNotMovedOn(
                MyERP.Inventory.StockEntryType.SendToSubcontractor,
                isReservedComponentsReturn: false,
                hasLaterActiveReservationFromThisEntry: false,
                subcontractingOrderNumber: "SCO-0001"));

        // 5. ERPNext 2511433013: Additional transfer entries excluded from Work Order Pick List transfer check
        MyERP.Manufacturing.DomainServices.WorkOrderManager
            .IsEligibleForWorkOrderPickListTransferCheck(
                isSubmitted: true,
                entryType: MyERP.Inventory.StockEntryType.MaterialTransferForManufacture,
                isReturn: false,
                isAdditionalTransferEntry: false,
                fgCompletedQty: 0m,
                jobCardId: null)
            .ShouldBeTrue();
        MyERP.Manufacturing.DomainServices.WorkOrderManager
            .IsEligibleForWorkOrderPickListTransferCheck(
                isSubmitted: true,
                entryType: MyERP.Inventory.StockEntryType.MaterialTransferForManufacture,
                isReturn: false,
                isAdditionalTransferEntry: true,
                fgCompletedQty: 0m,
                jobCardId: null)
            .ShouldBeFalse();
    }

    [Fact]
    public void UpstreamBatch16_AdditionalDocRef_PaymentTerms_PoScoClose_ReturnUom_MpsLead_Parity()
    {
        // 1. MyInvois: AdditionalDocumentReference (Customs/Incoterms) & PaymentTerms emitted when supplied
        var builder = new MyERP.EInvoice.Services.InvoiceDocumentBuilder();
        var xml = System.Xml.Linq.XDocument.Parse(builder.Build(new MyERP.EInvoice.Services.EInvoiceDocumentData
        {
            InvoiceNumber = "SI-2026-0016",
            IssueDate = new DateTime(2026, 7, 16, 10, 0, 0, DateTimeKind.Utc),
            DocumentTypeCode = "01",
            CurrencyCode = "MYR",
            Supplier = new MyERP.EInvoice.Services.EInvoicePartyData { Name = "Supplier Sdn Bhd", Tin = "C1111111111", Address = "Jalan 1", City = "KL", State = "14", CountryCode = "MYS" },
            Buyer = new MyERP.EInvoice.Services.EInvoicePartyData { Name = "Buyer Sdn Bhd", Tin = "C2222222222", Address = "Jalan 2", City = "KL", State = "14", CountryCode = "MYS" },
            AdditionalDocumentReferences = new System.Collections.Generic.List<MyERP.EInvoice.Services.EInvoiceAdditionalDocumentReference>
            {
                new() { Id = "CF1-998877", DocumentType = "CustomsImportForm", DocumentDescription = "Import Form 1" },
                new() { Id = "CIF", DocumentType = "Incoterms" }
            },
            PaymentTermsNote = "Payment method is Bank Transfer",
            NetTotal = 100m,
            TaxAmount = 0m,
            GrandTotal = 100m,
            Lines = new System.Collections.Generic.List<MyERP.EInvoice.Services.EInvoiceLineData>
            {
                new() { Description = "Item B", Quantity = 1m, UnitPrice = 100m }
            }
        }));
        System.Xml.Linq.XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
        System.Xml.Linq.XNamespace cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
        var addRefs = xml.Root!.Elements(cac + "AdditionalDocumentReference").ToList();
        addRefs.Count.ShouldBe(2);
        addRefs[0].Element(cbc + "ID")!.Value.ShouldBe("CF1-998877");
        addRefs[0].Element(cbc + "DocumentType")!.Value.ShouldBe("CustomsImportForm");
        addRefs[0].Element(cbc + "DocumentDescription")!.Value.ShouldBe("Import Form 1");
        addRefs[1].Element(cbc + "ID")!.Value.ShouldBe("CIF");
        xml.Root!.Element(cac + "PaymentTerms")!.Element(cbc + "Note")!.Value.ShouldBe("Payment method is Bank Transfer");

        // 2. ERPNext 0a1e80e7da: Refuse closing PO row still on an open Subcontracting Order
        var poRowId = Guid.NewGuid();
        var poCloseEx = Should.Throw<Volo.Abp.BusinessException>(() =>
            MyERP.Purchasing.DomainServices.PurchaseOrderManager.ValidatePurchaseOrderItemsNotOnOpenSubcontractingOrder(
                isSubcontracted: true,
                itemsToClose: new[] { (1, poRowId, "SVC-ITEM") },
                openSubcontractingOrderByPoItemId: new System.Collections.Generic.Dictionary<Guid, string> { [poRowId] = "SCO-2026-0001" }));
        poCloseEx.Code.ShouldBe(MyERP.MyERPDomainErrorCodes.ValidationFailed);
        poCloseEx.Data["detail"]!.ToString()!.ShouldContain("SCO-2026-0001");

        Should.NotThrow(() =>
            MyERP.Purchasing.DomainServices.PurchaseOrderManager.ValidatePurchaseOrderItemsNotOnOpenSubcontractingOrder(
                isSubcontracted: false,
                itemsToClose: new[] { (1, poRowId, "SVC-ITEM") },
                openSubcontractingOrderByPoItemId: new System.Collections.Generic.Dictionary<Guid, string> { [poRowId] = "SCO-2026-0001" }));

        // 3. ERPNext 4b0cbc8ec1 & 7f085027e1: Refuse submitting or re-opening SCO with unreceived row on closed PO row
        var scoEx = Should.Throw<Volo.Abp.BusinessException>(() =>
            MyERP.Purchasing.DomainServices.SubcontractingManager.ValidateSubcontractingOrderPurchaseOrderRowsOpen(
                purchaseOrderNumber: "PO-2026-0001",
                scoItems: new[] { (1, poRowId, 10m, 4m) },
                poItemsById: new System.Collections.Generic.Dictionary<Guid, (string, bool)> { [poRowId] = ("SVC-ITEM", true) }));
        scoEx.Code.ShouldBe(MyERP.MyERPDomainErrorCodes.ValidationFailed);
        scoEx.Data["detail"]!.ToString()!.ShouldContain("PO-2026-0001");

        // Fully received SCO row over closed PO row is allowed
        Should.NotThrow(() =>
            MyERP.Purchasing.DomainServices.SubcontractingManager.ValidateSubcontractingOrderPurchaseOrderRowsOpen(
                purchaseOrderNumber: "PO-2026-0001",
                scoItems: new[] { (1, poRowId, 10m, 10m) },
                poItemsById: new System.Collections.Generic.Dictionary<Guid, (string, bool)> { [poRowId] = ("SVC-ITEM", true) }));

        // 4. ERPNext 2812f1bf12: Stock UOM return row counts at factor 1 even if stale conversion_factor != 1
        MyERP.Purchasing.DomainServices.PurchaseReceiptManager
            .CalculateReturnWarehouseCheckStockQty(qty: -5m, uom: "Nos", stockUom: "Nos", conversionFactor: 12m)
            .ShouldBe(5m);
        MyERP.Purchasing.DomainServices.PurchaseReceiptManager
            .CalculateReturnWarehouseCheckStockQty(qty: -5m, uom: "Box", stockUom: "Nos", conversionFactor: 12m)
            .ShouldBe(60m);

        // 5. ERPNext 3d452749fa & 4fe3d19743: MPS cumulative lead time uses purchase lead days for purchased items and own+buffer+max(child) for BOM items
        MyERP.Manufacturing.DomainServices.MasterProductionScheduleService
            .CalculateMpsCumulativeLeadTimeDays(hasBom: false, purchaseLeadDays: 7m, manufacturingLeadDays: 2m, bufferTimeDays: 1m)
            .ShouldBe(7m);
        MyERP.Manufacturing.DomainServices.MasterProductionScheduleService
            .CalculateMpsCumulativeLeadTimeDays(hasBom: true, purchaseLeadDays: 99m, manufacturingLeadDays: 1.5m, bufferTimeDays: 0.5m, componentCumulativeLeadDays: new[] { 4m, 7m })
            .ShouldBe(9m);
    }

    [Fact]
    public void UpstreamBatch17_CountryIso_SreStatusRound_ReturnReReserve_DeletedBin_BackflushSerial_Parity()
    {
        // 1. MyInvois: Country/IdentificationCode emits listID="ISO3166-1" and listAgencyID="6" on Supplier, Buyer, and Delivery
        var builder = new MyERP.EInvoice.Services.InvoiceDocumentBuilder();
        var xml = System.Xml.Linq.XDocument.Parse(builder.Build(new MyERP.EInvoice.Services.EInvoiceDocumentData
        {
            InvoiceNumber = "SI-2026-0017",
            IssueDate = new DateTime(2026, 7, 17, 10, 0, 0, DateTimeKind.Utc),
            DocumentTypeCode = "01",
            CurrencyCode = "MYR",
            Supplier = new MyERP.EInvoice.Services.EInvoicePartyData { Name = "Supplier", Tin = "C1111111111", Address = "A", City = "KL", State = "14", CountryCode = "MYS" },
            Buyer = new MyERP.EInvoice.Services.EInvoicePartyData { Name = "Buyer", Tin = "C2222222222", Address = "B", City = "KL", State = "14", CountryCode = "MYS" },
            Delivery = new MyERP.EInvoice.Services.EInvoiceDeliveryData { RecipientName = "Wh", Address = "C", City = "KL", State = "14", CountryCode = "MYS" },
            NetTotal = 100m,
            TaxAmount = 0m,
            GrandTotal = 100m,
            Lines = new System.Collections.Generic.List<MyERP.EInvoice.Services.EInvoiceLineData>
            {
                new() { Description = "Item", Quantity = 1m, UnitPrice = 100m }
            }
        }));
        System.Xml.Linq.XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
        System.Xml.Linq.XNamespace cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
        var countryCodes = xml.Root!.Descendants(cac + "Country").Select(c => c.Element(cbc + "IdentificationCode")!).ToList();
        countryCodes.Count.ShouldBe(3);
        foreach (var code in countryCodes)
        {
            code.Attribute("listID")!.Value.ShouldBe("ISO3166-1");
            code.Attribute("listAgencyID")!.Value.ShouldBe("6");
            code.Value.ShouldBe("MYS");
        }

        // 2. ERPNext 0f3a1a2ed3 & b14a643fb4: SRE status rounds (transferred + consumed) vs reserved_qty at precision
        var (statusClosed, stillHoldsClosed) = MyERP.Inventory.DomainServices.StockReservationManager
            .EvaluateStockReservationEntryStatus(
                MyERP.Core.DocumentStatus.Submitted,
                voucherQty: 10m,
                reservedQty: 10m,
                deliveredQty: 0m,
                transferredQty: 3.33333m,
                consumedQty: 6.66666m,
                reservedQtyPrecision: 4);
        statusClosed.ShouldBe("Closed");
        stillHoldsClosed.ShouldBeFalse();

        var (statusPartial, stillHoldsPartial) = MyERP.Inventory.DomainServices.StockReservationManager
            .EvaluateStockReservationEntryStatus(
                MyERP.Core.DocumentStatus.Submitted,
                voucherQty: 10m,
                reservedQty: 10m,
                deliveredQty: 0m,
                transferredQty: 3m,
                consumedQty: 4m,
                reservedQtyPrecision: 4);
        statusPartial.ShouldBe("Partially Used");
        stillHoldsPartial.ShouldBeTrue();

        // 3. ERPNext b14a643fb4 & 2cec6e36eb: Returned components re-reserved per destination warehouse up to order shortfall
        var rowId = Guid.NewGuid();
        var whA = Guid.NewGuid();
        var whB = Guid.NewGuid();
        var reReserved = MyERP.Inventory.DomainServices.StockReservationManager.AllocateReturnedComponentReReservations(
            returnedRows: new[] { (rowId, whA, 4m), (rowId, whB, 5m) },
            orderRowDemand: new System.Collections.Generic.Dictionary<Guid, (decimal, decimal)> { [rowId] = (10m, 4m) });
        reReserved.Count.ShouldBe(2);
        reReserved[0].ShouldBe((rowId, whA, 4m));
        reReserved[1].ShouldBe((rowId, whB, 2m)); // capped at remaining shortfall (6 - 4 = 2), surplus 3 unreserved

        // 4. ERPNext 3c92ab4303: Update Items deleted row refreshes bundle component bins (SO) & MR item warehouse (PO)
        var bundleId = Guid.NewGuid();
        var compId = Guid.NewGuid();
        var soWh = Guid.NewGuid();
        var compWh = Guid.NewGuid();
        var (soBins, soMrIds) = MyERP.Inventory.DomainServices.StockReservationManager.ResolveDeletedRowBinRefreshTargets(
            parentDocType: "SalesOrder",
            rowItemId: bundleId,
            rowWarehouseId: soWh,
            linkedPackedItems: new[] { (compId, (Guid?)compWh) });
        soBins.ShouldContain((bundleId, soWh, "reserved_qty"));
        soBins.ShouldContain((compId, compWh, "reserved_qty"));
        soMrIds.ShouldBeEmpty();

        var mrItemId = Guid.NewGuid();
        var (poBins, poMrIds) = MyERP.Inventory.DomainServices.StockReservationManager.ResolveDeletedRowBinRefreshTargets(
            parentDocType: "PurchaseOrder",
            rowItemId: compId,
            rowWarehouseId: soWh,
            linkedMaterialRequestItemId: mrItemId);
        poBins.ShouldContain((compId, soWh, "ordered_qty"));
        poMrIds.ShouldBe(new[] { mrItemId });

        // 5. ERPNext 94d3fa6598: Backflush serial/batch assignment uses whole-number-ceilinged qty (2, not 1.5 -> 1)
        MyERP.Inventory.DomainServices.StockEntryManager
            .ResolveBackflushSerialBatchAssignmentQty(
                rowQty: 3m,
                fgCompletedQty: 1m,
                pendingQtyToManufacture: 2m,
                isReturn: false,
                uomMustBeWholeNumber: true)
            .ShouldBe(2m);
    }

    [Fact]
    public async System.Threading.Tasks.Task UpstreamBatch18_DocTypeNorm_CancelCase_WoConsumeFloor_ScioSum_JobCard_Parity()
    {
        // 1. MyInvois: Composite DocumentTypeCode ("02 : Credit Note") normalized to "02" in UBL InvoiceTypeCode
        var builder = new MyERP.EInvoice.Services.InvoiceDocumentBuilder();
        var xml = System.Xml.Linq.XDocument.Parse(builder.Build(new MyERP.EInvoice.Services.EInvoiceDocumentData
        {
            InvoiceNumber = "CN-2026-0018",
            IssueDate = new DateTime(2026, 7, 18, 10, 0, 0, DateTimeKind.Utc),
            DocumentTypeCode = "02 : Credit Note",
            CurrencyCode = "MYR",
            Supplier = new MyERP.EInvoice.Services.EInvoicePartyData { Name = "Supplier", Tin = "C1111111111", Address = "A", City = "KL", State = "14", CountryCode = "MYS" },
            Buyer = new MyERP.EInvoice.Services.EInvoicePartyData { Name = "Buyer", Tin = "C2222222222", Address = "B", City = "KL", State = "14", CountryCode = "MYS" },
            NetTotal = 100m,
            TaxAmount = 0m,
            GrandTotal = 100m,
            Lines = new System.Collections.Generic.List<MyERP.EInvoice.Services.EInvoiceLineData>
            {
                new() { Description = "Item", Quantity = 1m, UnitPrice = 100m }
            }
        }));
        System.Xml.Linq.XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
        xml.Root!.Element(cbc + "InvoiceTypeCode")!.Value.ShouldBe("02");

        // 1b. MyInvois: EInvoiceService.CancelAsync handles lowercase "cancelled" / "valid" case-insensitively
        var subRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.EInvoice.Entities.EInvoiceSubmission, Guid>>();
        var logRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.EInvoice.Entities.LhdnSuccessLog, Guid>>();
        var lhdnClient = NSubstitute.Substitute.For<MyERP.EInvoice.Services.ILhdnApiClient>();
        var subId = Guid.NewGuid();
        var alreadyCancelled = new MyERP.EInvoice.Entities.EInvoiceSubmission(subId, Guid.NewGuid(), "SalesInvoice", Guid.NewGuid())
        {
            SubmissionUid = "SUB-18",
            DocumentUuid = "DOC-18",
            Status = "cancelled",
            SubmittedAt = DateTime.UtcNow.AddHours(-1)
        };
        NSubstitute.SubstituteExtensions.Returns(subRepo.GetAsync(subId), System.Threading.Tasks.Task.FromResult(alreadyCancelled));
        var svc = new MyERP.EInvoice.Services.EInvoiceService(lhdnClient, subRepo, logRepo);
        var cancelEx = await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
            svc.CancelAsync(subId, "Duplicate", "tok", MyERP.EInvoice.LhdnEnvironment.Sandbox));
        cancelEx.Data["reason"]!.ToString()!.ShouldContain("already cancelled");

        // 2. ERPNext 9ebd625556: Work Order BOM-based consumption proposal floors to qty precision (never rounds up past remaining)
        MyERP.Manufacturing.DomainServices.WorkOrderManager
            .CalculatePendingConsumptionQtyBasedOnBom(
                leftForQty: 1.66669m,
                workOrderQty: 5m,
                producedQty: 0m,
                processLossQty: 0m,
                qtyPrecision: 4)
            .ShouldBe(1.6666m);

        // 3. ERPNext a7fe007c52 & 4311634e8e: Subcontracting Inward Order sums split rows per detail and rounds before limit check
        var scioDetail = Guid.NewGuid();
        var scioEx = Should.Throw<Volo.Abp.BusinessException>(() =>
            MyERP.Purchasing.DomainServices.SubcontractingManager.ValidateInwardOrderCumulativeRowQuantities(
                rows: new[] { (1, scioDetail, "FG-1", 6m), (2, scioDetail, "FG-1", 5m) },
                maxAllowedQtyByScioDetailId: new System.Collections.Generic.Dictionary<Guid, decimal> { [scioDetail] = 10m },
                orderNumber: "SCIO-2026-0001"));
        scioEx.Code.ShouldBe(MyERP.MyERPDomainErrorCodes.ValidationFailed);
        scioEx.Data["detail"]!.ToString()!.ShouldContain("Row #2");

        // Sub-precision dust rounds cleanly within cap
        Should.NotThrow(() =>
            MyERP.Purchasing.DomainServices.SubcontractingManager.ValidateInwardOrderCumulativeRowQuantities(
                rows: new[] { (1, scioDetail, "FG-1", 3.33333m), (2, scioDetail, "FG-1", 6.66666m) },
                maxAllowedQtyByScioDetailId: new System.Collections.Generic.Dictionary<Guid, decimal> { [scioDetail] = 10m },
                orderNumber: "SCIO-2026-0001",
                qtyPrecision: 4));

        // 4. ERPNext 51abb93bdb & 38b5cbe21a: Corrective Job Card keeps manual rate on amendment until workstation changes
        var wsOld = Guid.NewGuid();
        var wsNew = Guid.NewGuid();
        MyERP.Manufacturing.DomainServices.JobCardManager
            .ResolveCorrectiveJobCardHourRate(
                currentHourRate: 125m,
                currentWorkstationId: wsOld,
                previousSavedWorkstationId: null,
                amendedFromWorkstationId: wsOld,
                sourceJobCardWorkstationId: wsNew,
                fetchWorkstationHourRate: _ => 500m)
            .ShouldBe(125m); // amended card keeps manual rate when workstation == amendedFrom
        MyERP.Manufacturing.DomainServices.JobCardManager
            .ResolveCorrectiveJobCardHourRate(
                currentHourRate: 125m,
                currentWorkstationId: wsNew,
                previousSavedWorkstationId: null,
                amendedFromWorkstationId: wsOld,
                sourceJobCardWorkstationId: wsOld,
                fetchWorkstationHourRate: _ => 500m)
            .ShouldBe(500m); // changed workstation fetches new workstation rate

        // 5. ERPNext c530d6f106 & 3b6942f4dd: Job Card rescale floors RequiredQty at TransferredQty & drops untransferred stale rows
        var itemKept = Guid.NewGuid();
        var itemStaleUntransferred = Guid.NewGuid();
        var itemStaleTransferred = Guid.NewGuid();
        var reconciled = MyERP.Manufacturing.DomainServices.JobCardManager.ReconcileJobCardItemsOnRescale(
            existingRows: new[]
            {
                (itemKept, 10m, 8m, 0m),
                (itemStaleUntransferred, 5m, 0m, 0m),
                (itemStaleTransferred, 4m, 2m, 0m)
            },
            newRequiredQtyByItemId: new System.Collections.Generic.Dictionary<Guid, decimal>
            {
                [itemKept] = 6m // lowered below transferred 8 -> floored at 8
            });
        reconciled.Count.ShouldBe(2);
        reconciled.First(r => r.ItemId == itemKept).RequiredQty.ShouldBe(8m);
        reconciled.Any(r => r.ItemId == itemStaleUntransferred).ShouldBeFalse();
        reconciled.Any(r => r.ItemId == itemStaleTransferred).ShouldBeTrue();
    }

    [Fact]
    public async System.Threading.Tasks.Task UpstreamBatch19_LhdnTinStatus_ProjectsTasks_WoSubcontract_SreLock_OrderRow_Parity()
    {
        // 1a. MyInvois search_taxpayer.py: HTTP 200 JSON without TIN returns IsFound=false with exact error message
        var handler = new CaptureHttpHandler(_ => new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new System.Net.Http.StringContent("{\"name\":\"Test Sdn Bhd\"}", System.Text.Encoding.UTF8, "application/json")
        });
        var httpFactory = NSubstitute.Substitute.For<System.Net.Http.IHttpClientFactory>();
        NSubstitute.SubstituteExtensions.Returns(httpFactory.CreateClient("LhdnApi"), new System.Net.Http.HttpClient(handler));
        var lhdnLogger = NSubstitute.Substitute.For<Microsoft.Extensions.Logging.ILogger<MyERP.EInvoice.Services.LhdnApiClient>>();
        var apiClient = new MyERP.EInvoice.Services.LhdnApiClient(httpFactory, lhdnLogger);
        var tinRes = await apiClient.SearchTaxpayerAsync("tok", "BRN", "202001001234", MyERP.EInvoice.LhdnEnvironment.Sandbox);
        tinRes.IsFound.ShouldBeFalse();
        tinRes.Tin.ShouldBeNull();
        (tinRes.ErrorMessage ?? string.Empty).ShouldContain("TIN not found in API response.");

        // 1b. MyInvois get_status.py: missing SubmissionUid marks submission Failed, persists update, and throws
        var subRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.EInvoice.Entities.EInvoiceSubmission, Guid>>();
        var logRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.EInvoice.Entities.LhdnSuccessLog, Guid>>();
        var mockLhdn = NSubstitute.Substitute.For<MyERP.EInvoice.Services.ILhdnApiClient>();
        var subId = Guid.NewGuid();
        var missingUidSub = new MyERP.EInvoice.Entities.EInvoiceSubmission(subId, Guid.NewGuid(), "SalesInvoice", Guid.NewGuid())
        {
            SubmissionUid = "",
            Status = "Pending"
        };
        NSubstitute.SubstituteExtensions.Returns(subRepo.GetAsync(subId), System.Threading.Tasks.Task.FromResult(missingUidSub));
        var einvSvc = new MyERP.EInvoice.Services.EInvoiceService(mockLhdn, subRepo, logRepo);
        var statusEx = await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
            einvSvc.RefreshStatusAsync(subId, "tok", MyERP.EInvoice.LhdnEnvironment.Sandbox));
        statusEx.Code.ShouldBe(MyERP.MyERPDomainErrorCodes.EInvoiceSubmissionFailed);
        missingUidSub.Status.ShouldBe("Failed");
        await NSubstitute.SubstituteExtensions.Received(subRepo, 1).UpdateAsync(missingUidSub);

        // 2. ERPNext PR #69135 & #69136: ProjectTask schedule end + dependent rescheduling forward and backward
        var projId = Guid.NewGuid();
        var pred1 = new MyERP.Projects.Entities.ProjectTask(Guid.NewGuid(), projId, "TASK-1", "Predecessor 1")
        {
            ExpectedStartDate = new DateTime(2026, 8, 1, 9, 0, 0),
            ExpectedEndDate = new DateTime(2026, 8, 5, 17, 0, 0),
            ActualEndDate = new DateTime(2026, 8, 7, 17, 0, 0) // overran expected end!
        };
        pred1.GetScheduleEndDate().ShouldBe(new DateTime(2026, 8, 7, 17, 0, 0));

        var depNoEnd = new MyERP.Projects.Entities.ProjectTask(Guid.NewGuid(), projId, "TASK-2", "Dependent No End")
        {
            ExpectedStartDate = new DateTime(2026, 8, 7, 10, 30, 0), // starts AT pred1 schedule end
            ExpectedEndDate = null
        };
        depNoEnd.Start(); // Working status must also be rescheduled
        depNoEnd.AddDependency(pred1.Id);

        var pred2 = new MyERP.Projects.Entities.ProjectTask(Guid.NewGuid(), projId, "TASK-3", "Predecessor 2")
        {
            ExpectedStartDate = new DateTime(2026, 8, 1),
            ExpectedEndDate = new DateTime(2026, 8, 4)
        };
        var depMoveBack = new MyERP.Projects.Entities.ProjectTask(Guid.NewGuid(), projId, "TASK-4", "Dependent Move Back")
        {
            ExpectedStartDate = new DateTime(2026, 8, 8, 8, 0, 0), // day after old end (Aug 7)
            ExpectedEndDate = new DateTime(2026, 8, 10, 8, 0, 0)
        };
        depMoveBack.AddDependency(pred1.Id);
        depMoveBack.AddDependency(pred2.Id);

        var allTasks = new List<MyERP.Projects.Entities.ProjectTask> { pred1, depNoEnd, pred2, depMoveBack };
        var taskRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Projects.Entities.ProjectTask, Guid>>();
        NSubstitute.SubstituteExtensions.Returns(taskRepo.GetQueryableAsync(), System.Threading.Tasks.Task.FromResult(allTasks.AsQueryable()));
        var taskDepSvc = new MyERP.Projects.DomainServices.TaskDependencyValidationService(taskRepo);

        // Overrun pushes depNoEnd to Aug 8 preserving 10:30 time-of-day even with null ExpectedEndDate
        await taskDepSvc.RescheduleDependentTasksAsync(pred1);
        depNoEnd.ExpectedStartDate.ShouldBe(new DateTime(2026, 8, 8, 10, 30, 0));
        depNoEnd.ExpectedEndDate.ShouldBeNull();

        // Moving pred1 earlier from Aug 7 -> Aug 2 pulls depMoveBack back to Aug 5 (respecting pred2 end Aug 4)
        var oldEnd = pred1.GetScheduleEndDate();
        pred1.ActualEndDate = null;
        pred1.ExpectedEndDate = new DateTime(2026, 8, 2, 17, 0, 0);
        await taskDepSvc.RescheduleDependentTasksAsync(pred1, previousEndDate: oldEnd);
        depMoveBack.ExpectedStartDate.ShouldBe(new DateTime(2026, 8, 5, 8, 0, 0));
        depMoveBack.ExpectedEndDate.ShouldBe(new DateTime(2026, 8, 7, 8, 0, 0));

        // 3. ERPNext PR #69181 & #69182: Group task validations & recursive ExtendGroupToCover
        var grandGroup = new MyERP.Projects.Entities.ProjectTask(Guid.NewGuid(), projId, "GRP-0", "Grand Group")
        {
            IsGroup = true,
            ExpectedEndDate = new DateTime(2026, 8, 10)
        };
        var groupTask = new MyERP.Projects.Entities.ProjectTask(Guid.NewGuid(), projId, "GRP-1", "Parent Group")
        {
            IsGroup = true,
            ParentTaskId = grandGroup.Id,
            ExpectedEndDate = new DateTime(2026, 8, 10)
        };
        var childTask = new MyERP.Projects.Entities.ProjectTask(Guid.NewGuid(), projId, "CHILD-1", "Child Task")
        {
            ParentTaskId = groupTask.Id,
            ExpectedStartDate = new DateTime(2026, 8, 8),
            ExpectedHours = 72m // derives Aug 11 (> group Aug 10)
        };
        var cancelledChild = new MyERP.Projects.Entities.ProjectTask(Guid.NewGuid(), projId, "CHILD-2", "Cancelled Child")
        {
            ParentTaskId = groupTask.Id,
            ExpectedEndDate = new DateTime(2026, 8, 20)
        };
        cancelledChild.Cancel();
        allTasks.AddRange(new[] { grandGroup, groupTask, childTask, cancelledChild });

        childTask.SetDefaultEndDateIfMissing();
        Should.Throw<Volo.Abp.BusinessException>(() =>
            MyERP.Projects.DomainServices.TaskDependencyValidationService.ValidateParentExpectedEndDate(childTask, groupTask));
        await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
            taskDepSvc.ValidateGroupHasNoChildrenAsync(groupTask, newIsGroup: false));
        await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
            taskDepSvc.ValidateChildrenExpectedEndDateAsync(groupTask, new DateTime(2026, 8, 9)));

        // Cancelled child on Aug 20 does not block setting group end to Aug 12
        await Should.NotThrowAsync(() =>
            taskDepSvc.ValidateChildrenExpectedEndDateAsync(groupTask, new DateTime(2026, 8, 12)));

        // PR #69182: ExtendGroupToCover extends both parent and grandparent groups
        MyERP.Projects.DomainServices.TaskDependencyValidationService.ExtendGroupToCover(
            groupTask, new DateTime(2026, 8, 15), new[] { grandGroup, groupTask, childTask });
        groupTask.ExpectedEndDate.ShouldBe(new DateTime(2026, 8, 15));
        grandGroup.ExpectedEndDate.ShouldBe(new DateTime(2026, 8, 15));

        // 4. ERPNext PR #69243: WorkOrder status counts subcontracted qty as reserved
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-69243", Guid.NewGuid(), Guid.NewGuid(), 10m)
        {
            ReserveStock = true
        };
        var woRow = new WorkOrderItem(Guid.NewGuid(), wo.Id, Guid.NewGuid(), "RM-1", 10m);
        wo.RequiredItems.Add(woRow);
        woRow.SetStockReservedQty(4m);
        wo.GetReservationStatus(WorkOrderStatus.NotStarted, new Dictionary<Guid, decimal> { [woRow.Id] = 6m })
            .ShouldBe(WorkOrderStatus.StockReserved);
        wo.GetReservationStatus(WorkOrderStatus.NotStarted, new Dictionary<Guid, decimal> { [woRow.Id] = 5m })
            .ShouldBe(WorkOrderStatus.StockPartiallyReserved);

        // 5. ERPNext PR #68930: StockReservationEntry blocks serial/batch updates when consumed, transferred, delivered, or from Pick List
        var sre = new StockReservationEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "WorkOrder", Guid.NewGuid(), 10m);
        sre.CanUpdateSerialAndBatchEntries().ShouldBeTrue();
        sre.TransferredQty = 2m;
        sre.CanUpdateSerialAndBatchEntries().ShouldBeFalse();
        var sreEx = Should.Throw<Volo.Abp.BusinessException>(() => sre.ValidateCanUpdateSerialAndBatchEntries(serialBatchEntriesChanged: true));
        (sreEx.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("delivered, consumed or transferred");
        Should.NotThrow(() => sre.ValidateCanUpdateSerialAndBatchEntries(serialBatchEntriesChanged: false));

        var pickListSre = new StockReservationEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SalesOrder", Guid.NewGuid(), 5m)
        {
            FromVoucherType = "Pick List"
        };
        pickListSre.CanUpdateSerialAndBatchEntries().ShouldBeFalse();
        Should.Throw<Volo.Abp.BusinessException>(() => pickListSre.ValidateCanUpdateSerialAndBatchEntries(serialBatchEntriesChanged: true));

        // 6. ERPNext PR #69227: ChildItemUpdateService rejects item row IDs not belonging to target order
        var childUpdater = new ChildItemUpdateService();
        var ownRowId = Guid.NewGuid();
        var foreignRowId = Guid.NewGuid();
        Should.NotThrow(() => childUpdater.ValidateRowsBelongToParent(new[] { ownRowId }, new[] { ownRowId }, "Sales Order", "SO-001"));
        var rowEx = Should.Throw<Volo.Abp.BusinessException>(() =>
            childUpdater.ValidateRowsBelongToParent(new[] { ownRowId }, new[] { foreignRowId }, "Sales Order", "SO-001"));
        (rowEx.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("does not belong to Sales Order SO-001");
    }

    [Fact]
    public void UpstreamBatch20_LhdnPrepaidMonetary_SplitReturnCap_ScrTargetedCost_ScioMultiRow_Parity()
    {
        // 1. MyInvois UBL 2.1: PrepaidPayment + ChargeTotalAmount + PayableRoundingAmount + deducted PayableAmount
        var builder = new MyERP.EInvoice.Services.InvoiceDocumentBuilder();
        var xml = System.Xml.Linq.XDocument.Parse(builder.Build(new MyERP.EInvoice.Services.EInvoiceDocumentData
        {
            InvoiceNumber = "INV-2026-0020",
            IssueDate = new DateTime(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc),
            DocumentTypeCode = "01",
            CurrencyCode = "MYR",
            Supplier = new MyERP.EInvoice.Services.EInvoicePartyData { Name = "Sup", Tin = "C1111111111", Address = "A", City = "KL", State = "14", CountryCode = "MYS" },
            Buyer = new MyERP.EInvoice.Services.EInvoicePartyData { Name = "Buy", Tin = "C2222222222", Address = "B", City = "KL", State = "14", CountryCode = "MYS" },
            ChargeTotalAmount = 15m,
            PayableRoundingAmount = -0.02m,
            PrepaidPayment = new MyERP.EInvoice.Services.EInvoicePrepaidPaymentData
            {
                Id = "ADV-001",
                PaidAmount = 40m,
                PaidDate = new DateTime(2026, 8, 19, 9, 30, 0, DateTimeKind.Utc)
            },
            NetTotal = 100m,
            TaxAmount = 6m,
            GrandTotal = 121m,
            Lines = new System.Collections.Generic.List<MyERP.EInvoice.Services.EInvoiceLineData>
            {
                new() { Description = "Item", Quantity = 1m, UnitPrice = 100m, TaxAmount = 6m }
            }
        }));
        System.Xml.Linq.XNamespace cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
        System.Xml.Linq.XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
        var prepaidEl = xml.Root!.Element(cac + "PrepaidPayment");
        prepaidEl.ShouldNotBeNull();
        prepaidEl!.Element(cbc + "ID")!.Value.ShouldBe("ADV-001");
        prepaidEl.Element(cbc + "PaidAmount")!.Value.ShouldBe("40.00");
        prepaidEl.Element(cbc + "PaidDate")!.Value.ShouldBe("2026-08-19");
        prepaidEl.Element(cbc + "PaidTime")!.Value.ShouldBe("09:30:00Z");
        var monetary = xml.Root.Element(cac + "LegalMonetaryTotal")!;
        monetary.Element(cbc + "ChargeTotalAmount")!.Value.ShouldBe("15.00");
        monetary.Element(cbc + "PrepaidAmount")!.Value.ShouldBe("40.00");
        monetary.Element(cbc + "PayableRoundingAmount")!.Value.ShouldBe("-0.02");
        monetary.Element(cbc + "PayableAmount")!.Value.ShouldBe("81.00");

        // 2. ERPNext PR #67207: Delivery return across warehouses caps cumulative stock qty at original SLE qty
        var origRowId = Guid.NewGuid();
        var priorRetRowId = Guid.NewGuid();
        var whMain = Guid.NewGuid();
        var whReturn = Guid.NewGuid();
        // Original delivery: qty 1 Box * 0.33333 = 0.3333 stock qty posted as -0.3333 in whMain;
        // Prior return to whReturn: qty -0.5 * 0.33333 rounded to +0.1667 in whReturn -> only 0.1666 left to return!
        var (unroundedLeft, postedLeft) = DeliveryNoteManager.CalculateRowLeftToReturn(
            submittedOriginalAndPriorReturnRows: new[]
            {
                (origRowId, 1m, 0.33333m, whMain, 0m, (Guid?)null),
                (priorRetRowId, -0.5m, 0.33333m, whReturn, 0m, (Guid?)null),
            },
            postedSleActualQtyByRowId: new Dictionary<Guid, decimal>
            {
                [origRowId] = -0.3333m,
                [priorRetRowId] = 0.1667m,
            },
            isStockReceipt: false,
            targetWarehouseId: whReturn,
            stockQtyPrecision: 4);
        postedLeft.ShouldBe(0.1666m);

        // Final return of -0.5 Box (which alone would round to -0.1667) is capped at -0.1666 so total == original 0.3333
        DeliveryNoteManager.CalculateCappedReturnStockQty(
            returnQty: -0.5m,
            conversionFactor: 0.33333m,
            unroundedLeftToReturn: unroundedLeft,
            postedLeftToReturnStockQty: postedLeft,
            stockQtyPrecision: 4).ShouldBe(-0.1666m);

        // Cancellation returns exact posted SLE qty
        DeliveryNoteManager.CalculateCappedReturnStockQty(
            returnQty: -0.5m,
            conversionFactor: 0.33333m,
            unroundedLeftToReturn: 0m,
            postedLeftToReturnStockQty: 0m,
            isCancelled: true,
            postedReturnSleQty: -0.1666m).ShouldBe(-0.1666m);

        // 3. ERPNext PR #68870: SCR targeted additional cost + re-save weight strip + return allocation
        var fg1 = Guid.NewGuid();
        var fg2 = Guid.NewGuid();
        var secRow = Guid.NewGuid();
        SubcontractingManager.GetSubcontractingReceiptAmountForCosting(
            rowAmount: 250m, qtyForCosting: 10m, previousAdditionalCostPerQty: 5m, bomCostAllocationPercentage: 100m)
            .ShouldBe(200m);

        var dist = SubcontractingManager.CalculateTargetedAndSpreadAdditionalCosts(
            distributeBasedOn: "Qty",
            rows: new[]
            {
                (fg1, false, 100m, 2m),
                (fg2, false, 100m, 2m),
                (secRow, true, 10m, 1m),
            },
            additionalCostRows: new[]
            {
                (1, (Guid?)fg1, 60m),  // targeted to fg1 only
                (2, (Guid?)null, 40m), // untargeted -> 20 each
            });
        dist.First(r => r.RowId == fg1).TotalShare.ShouldBe(80m);
        dist.First(r => r.RowId == fg1).AdditionalCostPerQty.ShouldBe(40m);
        dist.First(r => r.RowId == fg2).TotalShare.ShouldBe(20m);
        dist.First(r => r.RowId == fg2).AdditionalCostPerQty.ShouldBe(10m);

        // Targeting a secondary (non-FG) row throws
        Should.Throw<Volo.Abp.BusinessException>(() =>
            SubcontractingManager.CalculateTargetedAndSpreadAdditionalCosts(
                distributeBasedOn: "Qty",
                rows: new[] { (fg1, false, 100m, 2m), (secRow, true, 10m, 1m) },
                additionalCostRows: new[] { (1, (Guid?)secRow, 50m) }));

        // Returning 1 of 2 units of fg1 only takes back 50% of targeted cost (30) and 25% of untargeted cost (10)
        var retCosts = SubcontractingManager.CalculateSubcontractingReturnAdditionalCosts(
            originalFgRows: new Dictionary<Guid, (decimal, decimal, decimal, decimal)>
            {
                [fg1] = (2m, 2m, 40m, 2m),
                [fg2] = (2m, 2m, 10m, 2m),
            },
            returnRows: new[] { (fg1, -1m) },
            originalAdditionalCosts: new[]
            {
                (1, (Guid?)fg1, 60m),
                (2, (Guid?)null, 40m),
            });
        retCosts.First(r => r.RowIdx == 1).ReturnedAmount.ShouldBe(-30m);
        retCosts.First(r => r.RowIdx == 2).ReturnedAmount.ShouldBe(-10m);

        // 4. ERPNext PR #67501: SCIO multi-row manufacture consumption & scoped cancel check
        var cpiItem = Guid.NewGuid();
        var ownItem = Guid.NewGuid();
        var scio1 = Guid.NewGuid();
        var scio2 = Guid.NewGuid();
        var whStore = Guid.NewGuid();

        // Two rows of same customer-provided item summing to 10 <= available 10 pass
        Should.NotThrow(() =>
            SubcontractingManager.ValidateInwardManufactureMultiRowConsumption(
                rows: new[] { (1, cpiItem, "CPI-1", 6m), (2, cpiItem, "CPI-1", 4m) },
                availableByItemId: new Dictionary<Guid, (decimal, decimal)> { [cpiItem] = (0m, 10m) }));

        // Two rows summing to 11 > available 10 throw at row #2
        var overEx = Should.Throw<Volo.Abp.BusinessException>(() =>
            SubcontractingManager.ValidateInwardManufactureMultiRowConsumption(
                rows: new[] { (1, cpiItem, "CPI-1", 6m), (2, cpiItem, "CPI-1", 5m) },
                availableByItemId: new Dictionary<Guid, (decimal, decimal)> { [cpiItem] = (0m, 10m) }));
        (overEx.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("Row #2");

        // Cancel check: billed extra material on ANOTHER order (scio2) does NOT block cancelling scio1
        Should.NotThrow(() =>
            SubcontractingManager.ValidateInwardManufactureCancelAgainstBilledExtraMaterial(
                scioId: scio1,
                fgItemId: fg1,
                ownMaterialRows: new[] { (1, ownItem, "OWN-1", whStore, 3m), (2, ownItem, "OWN-1", whStore, 3m) },
                scioReceivedOwnMaterials: new Dictionary<(Guid, Guid, Guid, Guid), (decimal, decimal, decimal)>
                {
                    [(scio2, fg1, ownItem, whStore)] = (6m, 6m, 0m)
                }));

        // Cancel check: billed own material on SAME order across two Manufacture rows (3 + 3 = 6) passes row #1 (6-3 >= 2) and blocks at row #2 (6-6 < 2)
        var cancelOwnEx = Should.Throw<Volo.Abp.BusinessException>(() =>
            SubcontractingManager.ValidateInwardManufactureCancelAgainstBilledExtraMaterial(
                scioId: scio1,
                fgItemId: fg1,
                ownMaterialRows: new[] { (1, ownItem, "OWN-1", whStore, 3m), (2, ownItem, "OWN-1", whStore, 3m) },
                scioReceivedOwnMaterials: new Dictionary<(Guid, Guid, Guid, Guid), (decimal, decimal, decimal)>
                {
                    [(scio1, fg1, ownItem, whStore)] = (6m, 2m, 0m)
                }));
        (cancelOwnEx.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("Row #2");
    }

    [Fact]
    public void UpstreamBatch21_BomSecondaryLoss_BatchOp_InterCompanyLcv_BundleVal_LhdnIcv_Parity()
    {
        // 1. ERPNext PR #67332: BOM secondary process loss capped at row qty, ValuationRate cost net of loss, SCR fallback rate
        var secCap = new BomSecondaryItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), SecondaryItemType.ByProduct, 0.006m)
        {
            ProcessLossPercentage = 99m
        };
        // 0.006 * 0.99 = 0.00594 -> rounds to 0.01 at precision 2 -> MUST cap at 0.006
        secCap.GetCappedProcessLossQty(precision: 2).ShouldBe(0.006m);

        var (lossQty, cost) = BomCostPropagationService.CalculateValuationRateSecondaryCost(
            unitRate: 100m, stockQty: 10m, processLossPercentage: 20m, lossQtyPrecision: 4, costPrecision: 2);
        lossQty.ShouldBe(2m);
        cost.ShouldBe(800m); // 100 * (10 - 2)

        BomCostPropagationService.CalculateScrFallbackSecondaryRate(baseCost: 800m, stockQty: 10m, processLossQty: 2m)
            .ShouldBe(100m); // 800 / (10 - 2), not 800 / 10

        // 2. ERPNext PR #67331: Batch operation costing & Work Order time per unit
        BomCostPropagationService.ResolveSetCostBasedOnBomQty(fixedTime: true, operationCreateJobCardBasedOnBatchSize: true, currentFlag: true)
            .ShouldBeFalse();
        BomCostPropagationService.ResolveSetCostBasedOnBomQty(fixedTime: false, operationCreateJobCardBasedOnBatchSize: true, currentFlag: false)
            .ShouldBeTrue();
        WorkOrderManager.CalculateOperationTimePerUnit(
            timeInMins: 30m, qty: 10m, units: 2m, exploded: false,
            operationBatchSizeFlag: false, setCostBasedOnBomQty: true, batchSize: 5m)
            .ShouldBe(12m); // 30 * 2 / 5

        // 3. ERPNext PR #67439: Landed cost stays off transit outward leg & rejected warehouse leg
        var acceptedWh = Guid.NewGuid();
        var rejectedWh = Guid.NewGuid();
        var transitWh = Guid.NewGuid();
        PurchaseReceiptManager.CalculateInterCompanyIncomingRate(100m, sleActualQty: -10m, transitWh, rejectedWh, landedCostVoucherAmount: 50m)
            .ShouldBe(100m); // transit outward leg -> no LCV
        PurchaseReceiptManager.CalculateInterCompanyIncomingRate(100m, sleActualQty: 2m, rejectedWh, rejectedWh, landedCostVoucherAmount: 50m)
            .ShouldBe(100m); // rejected leg -> no LCV
        PurchaseReceiptManager.CalculateInterCompanyIncomingRate(100m, sleActualQty: 10m, acceptedWh, rejectedWh, landedCostVoucherAmount: 50m)
            .ShouldBe(105m); // accepted leg -> +5/unit
        PurchaseReceiptManager.CalculateInternalReceiptValuationRate(100m, landedCostVoucherAmount: 50m, acceptedStockQty: 10m)
            .ShouldBe(105m);

        // 4. ERPNext PR #67434: Product Bundle rows checked against valuation rate when editableBundleItemRates == false
        ProductBundleDecompositionService.ShouldValidateRowAgainstValuationRate(
            isStockItem: false, isInternalCustomer: false, editableBundleItemRates: false, hasBundleVersion: true)
            .ShouldBeTrue();
        ProductBundleDecompositionService.ShouldValidateRowAgainstValuationRate(
            isStockItem: false, isInternalCustomer: false, editableBundleItemRates: true, hasBundleVersion: true)
            .ShouldBeFalse();
        ProductBundleDecompositionService.ShouldValidateRowAgainstValuationRate(
            isStockItem: false, isInternalCustomer: true, editableBundleItemRates: false, hasBundleVersion: true)
            .ShouldBeFalse();
        ProductBundleDecompositionService.CalculateProductBundleValuationRate(new[] { (25m, 2m), (10m, 3m) })
            .ShouldBe(80m);

        // 5. MyInvois createxml.py: ICV code extraction and standard invoice BillingReference emission
        MyERP.EInvoice.Services.InvoiceDocumentBuilder.ExtractIcvCode("SI-2026-00042").ShouldBe("00042");
        var builder = new MyERP.EInvoice.Services.InvoiceDocumentBuilder();
        var xml = System.Xml.Linq.XDocument.Parse(builder.Build(new MyERP.EInvoice.Services.EInvoiceDocumentData
        {
            InvoiceNumber = "SI-2026-00042",
            IssueDate = new DateTime(2026, 8, 21, 10, 0, 0, DateTimeKind.Utc),
            DocumentTypeCode = "01",
            CurrencyCode = "MYR",
            EmitIcvBillingReference = true,
            Supplier = new MyERP.EInvoice.Services.EInvoicePartyData { Name = "Sup", Tin = "C1111111111", Address = "A", City = "KL", State = "14", CountryCode = "MYS" },
            Buyer = new MyERP.EInvoice.Services.EInvoicePartyData { Name = "Buy", Tin = "C2222222222", Address = "B", City = "KL", State = "14", CountryCode = "MYS" },
            NetTotal = 100m,
            TaxAmount = 0m,
            GrandTotal = 100m,
            Lines = new System.Collections.Generic.List<MyERP.EInvoice.Services.EInvoiceLineData>
            {
                new() { Description = "Item", Quantity = 1m, UnitPrice = 100m }
            }
        }));
        System.Xml.Linq.XNamespace cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
        System.Xml.Linq.XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
        xml.Root!.Element(cac + "BillingReference")!.Element(cac + "InvoiceDocumentReference")!.Element(cbc + "ID")!.Value
            .ShouldBe("00042");
    }

    [Fact]
    public void UpstreamBatch22_MfgTransferredCap_JcSecondaryLink_ScioBatchTake_LhdnContact_Parity()
    {
        // 1. ERPNext PR #67354: Manufacture refuses qty exceeding transferred + overproduction - produced - processLoss
        Should.NotThrow(() =>
            StockEntryManager.ValidateManufactureQtyAgainstTransferredQty(
                fgCompletedQty: 6m, workOrderQty: 10m, transferredQty: 8m, producedQty: 2m, processLossQty: 0m,
                workOrderNumber: "WO-67354", overproductionAllowancePercentage: 0m));

        var mfgEx = Should.Throw<Volo.Abp.BusinessException>(() =>
            StockEntryManager.ValidateManufactureQtyAgainstTransferredQty(
                fgCompletedQty: 7m, workOrderQty: 10m, transferredQty: 8m, producedQty: 2m, processLossQty: 0m,
                workOrderNumber: "WO-67354", overproductionAllowancePercentage: 0m));
        (mfgEx.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("cannot be more than 6");

        // 10% overproduction allowance (1 unit) lets qty 7 pass
        Should.NotThrow(() =>
            StockEntryManager.ValidateManufactureQtyAgainstTransferredQty(
                fgCompletedQty: 7m, workOrderQty: 10m, transferredQty: 8m, producedQty: 2m, processLossQty: 0m,
                workOrderNumber: "WO-67354", overproductionAllowancePercentage: 10m));

        // 2. ERPNext PR #67394: Job Card secondary relinking, BOM dedup, and scrap warehouse routing
        var itemA = Guid.NewGuid();
        var itemB = Guid.NewGuid();
        var bomRowA = Guid.NewGuid();
        var bomRowB = Guid.NewGuid();
        var jcRow1 = Guid.NewGuid();
        var jcRow2 = Guid.NewGuid();
        var linked = JobCardManager.LinkSecondaryItemsToBom(
            jobCardSecondaryRows: new[]
            {
                (jcRow1, itemA, SecondaryItemType.Scrap, (Guid?)bomRowA), // still matches -> keep
                (jcRow2, itemB, SecondaryItemType.Scrap, (Guid?)bomRowA), // item changed -> relink to bomRowB
            },
            bomSecondaryRows: new[]
            {
                (bomRowA, itemA, SecondaryItemType.Scrap),
                (bomRowB, itemB, SecondaryItemType.Scrap),
            });
        linked.First(r => r.RowId == jcRow1).ResolvedBomSecondaryItemId.ShouldBe(bomRowA);
        linked.First(r => r.RowId == jcRow2).ResolvedBomSecondaryItemId.ShouldBe(bomRowB);

        var filteredBomSec = StockEntryManager.FilterBomSecondaryItemsNotRecordedOnJobCard(
            bomSecondaryItems: new[] { (itemA, SecondaryItemType.Scrap, 1m), (itemB, SecondaryItemType.ByProduct, 2m) },
            jobCardSecondaryItems: new[] { (itemA, SecondaryItemType.Scrap) });
        filteredBomSec.Count.ShouldBe(1);
        filteredBomSec[0].ItemId.ShouldBe(itemB);

        var scrapWh = Guid.NewGuid();
        var fgWh = Guid.NewGuid();
        StockEntryManager.ResolveSecondaryItemTargetWarehouse(SecondaryItemType.Scrap, scrapWh, fgWh).ShouldBe(scrapWh);
        StockEntryManager.ResolveSecondaryItemTargetWarehouse(SecondaryItemType.ByProduct, scrapWh, fgWh).ShouldBe(fgWh);

        // 3. ERPNext PR #67503: Take pending customer batches only up to row qty, mutating remaining slots FIFO
        var slots = new List<ReservedSerialBatchSlot>
        {
            new() { BatchNo = "B-1", AvailableQty = 4m },
            new() { BatchNo = "B-2", AvailableQty = 6m },
        };
        var (_, row1Batches) = StockEntryManager.TakeReservedSerialAndBatchEntries(slots, requestedQty: 5m);
        row1Batches["B-1"].ShouldBe(4m);
        row1Batches["B-2"].ShouldBe(1m);
        slots[0].AvailableQty.ShouldBe(0m);
        slots[1].AvailableQty.ShouldBe(5m);

        var (_, row2Batches) = StockEntryManager.TakeReservedSerialAndBatchEntries(slots, requestedQty: 3m);
        row2Batches.Count.ShouldBe(1);
        row2Batches["B-2"].ShouldBe(3m);
        slots[1].AvailableQty.ShouldBe(2m);

        // 4. MyInvois createxml.py: IsNotApplicable / IsValidEmail and XML contact sanitization
        MyERP.EInvoice.Services.InvoiceDocumentBuilder.IsNotApplicable("N/A").ShouldBeTrue();
        MyERP.EInvoice.Services.InvoiceDocumentBuilder.IsNotApplicable("NA").ShouldBeTrue();
        MyERP.EInvoice.Services.InvoiceDocumentBuilder.IsValidEmail("finance@company.my").ShouldBeTrue();
        MyERP.EInvoice.Services.InvoiceDocumentBuilder.IsValidEmail("not-an-email").ShouldBeFalse();

        var builder = new MyERP.EInvoice.Services.InvoiceDocumentBuilder();
        var xml = System.Xml.Linq.XDocument.Parse(builder.Build(new MyERP.EInvoice.Services.EInvoiceDocumentData
        {
            InvoiceNumber = "SI-2026-00022",
            IssueDate = new DateTime(2026, 8, 22, 10, 0, 0, DateTimeKind.Utc),
            DocumentTypeCode = "01",
            CurrencyCode = "MYR",
            Supplier = new MyERP.EInvoice.Services.EInvoicePartyData
            {
                Name = "Sup", Tin = "C1111111111", Address = "A", City = "KL", State = "14", CountryCode = "MYS",
                Phone = "+60312345678", Email = "invalid-email"
            },
            Buyer = new MyERP.EInvoice.Services.EInvoicePartyData
            {
                Name = "Buy", Tin = "C2222222222", Address = "B", City = "KL", State = "14", CountryCode = "MYS",
                Phone = "N/A", Email = "NA"
            },
            NetTotal = 100m,
            TaxAmount = 0m,
            GrandTotal = 100m,
            Lines = new System.Collections.Generic.List<MyERP.EInvoice.Services.EInvoiceLineData>
            {
                new() { Description = "Item", Quantity = 1m, UnitPrice = 100m }
            }
        }));
        System.Xml.Linq.XNamespace cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
        System.Xml.Linq.XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
        var supContact = xml.Root!.Element(cac + "AccountingSupplierParty")!.Element(cac + "Party")!.Element(cac + "Contact");
        supContact.ShouldNotBeNull();
        supContact!.Element(cbc + "Telephone")!.Value.ShouldBe("+60312345678");
        supContact.Element(cbc + "ElectronicMail").ShouldBeNull(); // invalid email omitted
        xml.Root.Element(cac + "AccountingCustomerParty")!.Element(cac + "Party")!.Element(cac + "Contact").ShouldBeNull(); // N/A contact omitted
    }

    [Fact]
    public void UpstreamBatch23_FutureSleSameTime_CoProductManualFg_ReturnQueueGuard_LhdnVersion_Parity()
    {
        // 1. ERPNext PR #68732: Same-time SLE created BEFORE voucher does not trigger repost; created AFTER does
        var tPost = new DateTime(2026, 8, 23, 12, 0, 0, DateTimeKind.Utc);
        var tCreate1 = new DateTime(2026, 8, 23, 12, 0, 1, DateTimeKind.Utc);
        var tCreate2 = new DateTime(2026, 8, 23, 12, 0, 2, DateTimeKind.Utc);
        var tCreate3 = new DateTime(2026, 8, 23, 12, 0, 3, DateTimeKind.Utc);

        StockRepostGuardService.IsLaterStockLedgerEntry(tPost, tCreate1, tPost, tCreate2).ShouldBeFalse();
        StockRepostGuardService.IsLaterStockLedgerEntry(tPost, tCreate3, tPost, tCreate2).ShouldBeTrue();
        StockRepostGuardService.IsLaterStockLedgerEntry(tPost.AddSeconds(1), tCreate1, tPost, tCreate2).ShouldBeTrue();
        StockRepostGuardService.IsLaterStockLedgerEntry(tPost, tCreate3, tPost, null).ShouldBeFalse();

        // 2. ERPNext PR #67383: Co-product percentage split deducts manually rated FG cost first (floored at 0)
        StockEntryManager.CalculateCoProductRateAfterManualFinishedGoods(
            secondaryItemsCostBasis: 1000m,
            costedOutItemsCost: 100m,
            manuallyRatedFinishedItemsCost: 300m,
            finishedItemsQty: 5m,
            costAllocationPercentage: 25m,
            secondaryItemsQty: 5m)
            .ShouldBe(30m); // (1000 - 100 - 300) * 25% / 5 = 30

        StockEntryManager.CalculateCoProductRateAfterManualFinishedGoods(
            secondaryItemsCostBasis: 200m,
            costedOutItemsCost: 0m,
            manuallyRatedFinishedItemsCost: 500m,
            finishedItemsQty: 2m,
            costAllocationPercentage: 50m,
            secondaryItemsQty: 1m)
            .ShouldBe(0m); // floored at 0

        // 3. ERPNext PR #67530: FIFO/LIFO return from non-receipt warehouse verifies rate-matched queue qty (same-wh rows take first)
        var whReceipt = Guid.NewGuid();
        var whOther = Guid.NewGuid();
        var queue = new Dictionary<(string, Guid, decimal), decimal>
        {
            [("FIFO-1", whOther, 50m)] = 5m
        };

        // Returning 5 from whOther succeeds when whOther holds 5 at rate 50
        Should.NotThrow(() =>
            PurchaseReceiptManager.ValidateReturnWarehouseHoldsFifoLifoReceiptStock(
                returnRows: new[]
                {
                    (1, "FIFO-1", whOther, "Other WH", -5m, (string?)"Nos", (string?)"Nos", 1m, 50m, (IReadOnlyCollection<Guid>)new[] { whReceipt }, "Main WH", true)
                },
                queueQtyByKey: queue,
                returnAgainstNumber: "PR-2026-0001"));

        // Same-wh row returning 3 from whOther takes first, leaving only 2 for moved row returning 4 -> throws
        var fifoEx = Should.Throw<Volo.Abp.BusinessException>(() =>
            PurchaseReceiptManager.ValidateReturnWarehouseHoldsFifoLifoReceiptStock(
                returnRows: new[]
                {
                    (1, "FIFO-1", whOther, "Other WH", -4m, (string?)"Nos", (string?)"Nos", 1m, 50m, (IReadOnlyCollection<Guid>)new[] { whReceipt }, "Main WH", true),
                    (2, "FIFO-1", whOther, "Other WH", -3m, (string?)"Nos", (string?)"Nos", 1m, 50m, (IReadOnlyCollection<Guid>)new[] { whOther }, "Other WH", true),
                },
                queueQtyByKey: queue,
                returnAgainstNumber: "PR-2026-0001"));
        (fifoEx.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("Move the goods back to Main WH");

        // 4. MyInvois createxml.py: ResolveDocumentVersion requires certificate for 1.1, otherwise falls back to 1.0
        MyERP.EInvoice.Services.InvoiceDocumentBuilder.ResolveDocumentVersion(hasCertificateFile: true, configuredVersion: "1.1").ShouldBe("1.1");
        MyERP.EInvoice.Services.InvoiceDocumentBuilder.ResolveDocumentVersion(hasCertificateFile: false, configuredVersion: "1.1").ShouldBe("1.0");
        MyERP.EInvoice.Services.InvoiceDocumentBuilder.ResolveDocumentVersion(hasCertificateFile: true, configuredVersion: "1.0").ShouldBe("1.0");
    }

    [Fact]
    public async System.Threading.Tasks.Task UpstreamBatch24_PaymentLedgerPreview_IcBatchPkg_ScoReturnSre_LhdnBuyer_Parity()
    {
        // 1. ERPNext PR #68176: Payment Ledger view/preview visibility & voucher PLE query excluding delinked unless requested
        MyERP.Accounting.DomainServices.PaymentLedgerService.ShouldShowPaymentLedgerForVoucher("PaymentEntry", hasParty: false, hasReceivableOrPayableAccount: false).ShouldBeFalse();
        MyERP.Accounting.DomainServices.PaymentLedgerService.ShouldShowPaymentLedgerForVoucher("PaymentEntry", hasParty: true, hasReceivableOrPayableAccount: false).ShouldBeTrue();
        MyERP.Accounting.DomainServices.PaymentLedgerService.ShouldShowPaymentLedgerForVoucher("JournalEntry", hasParty: false, hasReceivableOrPayableAccount: false).ShouldBeFalse();
        MyERP.Accounting.DomainServices.PaymentLedgerService.ShouldShowPaymentLedgerForVoucher("JournalEntry", hasParty: false, hasReceivableOrPayableAccount: true).ShouldBeTrue();
        MyERP.Accounting.DomainServices.PaymentLedgerService.ShouldShowPaymentLedgerForVoucher("SalesInvoice", hasParty: false, hasReceivableOrPayableAccount: false).ShouldBeTrue();

        var voucherId = Guid.NewGuid();
        var activePle = new MyERP.Accounting.Entities.PaymentLedgerEntry(
            Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), "Customer", Guid.NewGuid(),
            "SalesInvoice", voucherId, "SalesInvoice", voucherId, 100m, 100m, "MYR");
        var delinkedPle = new MyERP.Accounting.Entities.PaymentLedgerEntry(
            Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), "Customer", Guid.NewGuid(),
            "SalesInvoice", voucherId, "SalesInvoice", voucherId, -100m, -100m, "MYR")
        {
            Delinked = true
        };
        var pleRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Accounting.Entities.PaymentLedgerEntry, Guid>>();
        NSubstitute.SubstituteExtensions.Returns(pleRepo.GetQueryableAsync(), System.Threading.Tasks.Task.FromResult(new[] { activePle, delinkedPle }.AsQueryable()));
        var pleSvc = new MyERP.Accounting.DomainServices.PaymentLedgerService(pleRepo);
        (await pleSvc.GetVoucherPaymentLedgerEntriesAsync("SalesInvoice", voucherId, showCancelledEntries: false)).Count.ShouldBe(1);
        (await pleSvc.GetVoucherPaymentLedgerEntriesAsync("SalesInvoice", voucherId, showCancelledEntries: true)).Count.ShouldBe(2);

        // 2. ERPNext PR #68787: Inter-company receipt never keeps transit package; rejected bundle moves on warehouse change
        PurchaseReceiptManager.KeepsTransitPackage(isSameCompanyInternalTransfer: true, isInterCompanyReceipt: false, hasRejectedStock: false).ShouldBeTrue();
        PurchaseReceiptManager.KeepsTransitPackage(isSameCompanyInternalTransfer: true, isInterCompanyReceipt: false, hasRejectedStock: true).ShouldBeFalse();
        PurchaseReceiptManager.KeepsTransitPackage(isSameCompanyInternalTransfer: false, isInterCompanyReceipt: true, hasRejectedStock: false).ShouldBeFalse();

        var rejWh1 = Guid.NewGuid();
        var rejWh2 = Guid.NewGuid();
        var existingBundle = Guid.NewGuid();
        PurchaseReceiptManager.ResolveRejectedPackageAction(0m, existingBundle, rejWh1, rejWh1).ShouldBe(RejectedPackageAction.Remove);
        PurchaseReceiptManager.ResolveRejectedPackageAction(5m, existingBundle, rejWh1, rejWh1).ShouldBe(RejectedPackageAction.Resize);
        PurchaseReceiptManager.ResolveRejectedPackageAction(5m, existingBundle, rejWh1, rejWh2).ShouldBe(RejectedPackageAction.RecreateAtNewWarehouse);

        // 3. ERPNext PR #67522: SCO return of components uses full open qty (deducting delivered+transferred+consumed) & replays supplier WH on cancel
        var sre = new StockReservationEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SubcontractingOrder", Guid.NewGuid(), 10m)
        {
            TransferredQty = 3m,
            ConsumedQty = 4m
        };
        StockReservationManager.GetReservationOpenQty(sre).ShouldBe(3m);

        var scoId = Guid.NewGuid();
        var rmItemId = Guid.NewGuid();
        var srcWh = Guid.NewGuid();
        var supWh = Guid.NewGuid();
        var submitScope = StockReservationManager.ResolveSubcontractingReservationReplayScope(
            isComponentsReturn: true, isCancel: false, scoId, rmItemId, srcWh, supWh);
        submitScope.FilterByVoucherDetail.ShouldBeTrue();
        submitScope.TargetWarehouseId.ShouldBe(srcWh);
        var cancelScope = StockReservationManager.ResolveSubcontractingReservationReplayScope(
            isComponentsReturn: true, isCancel: true, scoId, rmItemId, srcWh, supWh);
        cancelScope.FilterByVoucherDetail.ShouldBeFalse();
        cancelScope.ReplayGroupKey.ShouldBe((scoId, rmItemId, srcWh));

        // 4. MyInvois createxml.py: Buyer address/contact validator enforces fields on B2B and skips on Consolidated
        MyERP.EInvoice.Services.EInvoiceValidationService
            .ValidateBuyerAddressAndContactForLhdn(null, null, null, "NA", "bad-email", isConsolidated: true)
            .ShouldBeEmpty();
        var buyerErrors = MyERP.EInvoice.Services.EInvoiceValidationService
            .ValidateBuyerAddressAndContactForLhdn(null, "123 Jalan", null, "NA", "bad-email", isConsolidated: false);
        buyerErrors.Count.ShouldBe(4);
    }

    [Fact]
    public void UpstreamBatch25_PricingContext_AccountingDimUserScope_TdsDefault_AppointmentAssignee_MyInvoisSelfBilledContact_Parity()
    {
        // 1. ERPNext PR #69273: Pricing context authorization (doctype allow-list, company scope, party permission, price list side)
        var allowedCompany = Guid.NewGuid();
        var otherCompany = Guid.NewGuid();
        var allowedSet = new HashSet<Guid> { allowedCompany };

        Should.Throw<Volo.Abp.BusinessException>(() =>
            MyERP.Sales.DomainServices.PricingRuleApplicationService.ValidatePricingContext(
                "User", hasTransactionReadPermission: true)).Code.ShouldBe("MyERP:11002");

        Should.Throw<Volo.Abp.BusinessException>(() =>
            MyERP.Sales.DomainServices.PricingRuleApplicationService.ValidatePricingContext(
                "SalesOrder", hasTransactionReadPermission: false)).Code.ShouldBe("MyERP:11003");

        Should.Throw<Volo.Abp.BusinessException>(() =>
            MyERP.Sales.DomainServices.PricingRuleApplicationService.ValidatePricingContext(
                "SalesOrder", hasTransactionReadPermission: true, companyId: otherCompany, allowedCompanyIds: allowedSet))
            .Code.ShouldBe("MyERP:11004");

        Should.Throw<Volo.Abp.BusinessException>(() =>
            MyERP.Sales.DomainServices.PricingRuleApplicationService.ValidatePricingContext(
                "SalesOrder", hasTransactionReadPermission: true, companyId: allowedCompany, allowedCompanyIds: allowedSet,
                hasParty: true, hasPartySelectPermission: false))
            .Code.ShouldBe("MyERP:11005");

        Should.Throw<Volo.Abp.BusinessException>(() =>
            MyERP.Sales.DomainServices.PricingRuleApplicationService.ValidatePricingContext(
                "SalesInvoice", hasTransactionReadPermission: true, companyId: allowedCompany, allowedCompanyIds: allowedSet,
                priceListName: "Standard Buying", priceListMatchesSide: false, isInternalParty: false, isReturnAgainstSamePriceList: false))
            .Code.ShouldBe("MyERP:11006");

        // Internal customer or return-against same price list or BOM skips/passes price list side check
        Should.NotThrow(() =>
            MyERP.Sales.DomainServices.PricingRuleApplicationService.ValidatePricingContext(
                "SalesInvoice", hasTransactionReadPermission: true, companyId: allowedCompany, allowedCompanyIds: allowedSet,
                priceListName: "Standard Buying", priceListMatchesSide: false, isInternalParty: true));
        Should.NotThrow(() =>
            MyERP.Sales.DomainServices.PricingRuleApplicationService.ValidatePricingContext(
                "SalesInvoice", hasTransactionReadPermission: true, companyId: allowedCompany, allowedCompanyIds: allowedSet,
                priceListName: "Standard Buying", priceListMatchesSide: false, isReturnAgainstSamePriceList: true));
        Should.NotThrow(() =>
            MyERP.Sales.DomainServices.PricingRuleApplicationService.ValidatePricingContext(
                "BOM", hasTransactionReadPermission: true, priceListName: "Standard Buying", priceListMatchesSide: false));

        // 2. ERPNext PR #67760: Accounting dimension defaults scoped to companies user can see
        var defaultsByCompany = new Dictionary<Guid, string>
        {
            [allowedCompany] = "CC-A",
            [otherCompany] = "CC-B"
        };
        Should.Throw<Volo.Abp.BusinessException>(() =>
            MyERP.Accounting.DomainServices.AccountingDimensionService.FilterDimensionDefaultsForUser(
                defaultsByCompany, allowedSet, hasCompanyReadPermission: false));
        var scopedDefaults = MyERP.Accounting.DomainServices.AccountingDimensionService.FilterDimensionDefaultsForUser(
            defaultsByCompany, allowedSet, hasCompanyReadPermission: true);
        scopedDefaults.Count.ShouldBe(1);
        scopedDefaults[allowedCompany].ShouldBe("CC-A");

        // 3. ERPNext PR #68477: Purchase Invoice mapped from PO/PR keeps apply_tds ticked when supplier has category or group
        MyERP.Tax.DomainServices.TaxWithholdingService.ShouldApplyTaxWithholdingByDefault("Section 107A", null).ShouldBeTrue();
        MyERP.Tax.DomainServices.TaxWithholdingService.ShouldApplyTaxWithholdingByDefault(null, "Non-Resident Group").ShouldBeTrue();
        MyERP.Tax.DomainServices.TaxWithholdingService.ShouldApplyTaxWithholdingByDefault(null, null).ShouldBeFalse();
        MyERP.Tax.DomainServices.TaxWithholdingService.ShouldApplyTaxWithholdingByDefault("Section 107A", null, forValidate: true).ShouldBeFalse();

        // 4. ERPNext PR #69097: Appointment assignee lookup requires Opportunity read permission and ignores unpermitted opportunities
        var permittedAgent = Guid.NewGuid();
        var hiddenAgent = Guid.NewGuid();
        var opps = new List<(Guid? AssignedUserId, DateTime CreationTime, bool IsPermitted)>
        {
            (hiddenAgent, new DateTime(2026, 11, 20), false),
            (permittedAgent, new DateTime(2026, 11, 18), true)
        };
        MyERP.CRM.Entities.Appointment.ResolveAssigneeFromLatestOpportunity(isLinkedLead: false, hasOpportunityReadPermission: true, opps).ShouldBeNull();
        MyERP.CRM.Entities.Appointment.ResolveAssigneeFromLatestOpportunity(isLinkedLead: true, hasOpportunityReadPermission: false, opps).ShouldBeNull();
        MyERP.CRM.Entities.Appointment.ResolveAssigneeFromLatestOpportunity(isLinkedLead: true, hasOpportunityReadPermission: true, opps).ShouldBe(permittedAgent);

        // 5. MyInvois purchase_invoice.py & createxml.py: self-billed contact fallbacks & company address/contact validator
        var builder = new MyERP.EInvoice.Services.InvoiceDocumentBuilder();
        var selfBilledXml = builder.Build(new MyERP.EInvoice.Services.EInvoiceDocumentData
        {
            InvoiceNumber = "SB-2026-001",
            DocumentTypeCode = "11",
            IssueDate = new DateTime(2026, 11, 24),
            CurrencyCode = "MYR",
            Supplier = new MyERP.EInvoice.Services.EInvoicePartyData
            {
                Name = "Foreign Supplier",
                Tin = "EI00000000030",
                IdType = "PASSPORT",
                IdValue = "P12345",
                Phone = "NA",
                Email = "not-an-email"
            },
            Buyer = new MyERP.EInvoice.Services.EInvoicePartyData
            {
                Name = "My Company Sdn Bhd",
                Tin = "C1234567890",
                IdType = "BRN",
                IdValue = "202001000001"
            },
            Lines = new List<MyERP.EInvoice.Services.EInvoiceLineData>
            {
                new() { Description = "Consulting", Quantity = 1m, UnitPrice = 100m, TaxAmount = 0m }
            },
            NetTotal = 100m,
            TaxAmount = 0m,
            GrandTotal = 100m
        });
        selfBilledXml.ShouldContain("<cbc:Telephone>60100000000</cbc:Telephone>");
        selfBilledXml.ShouldContain("<cbc:ElectronicMail>noemail@noemail.com</cbc:ElectronicMail>");

        var companyErrors = MyERP.EInvoice.Services.EInvoiceValidationService
            .ValidateCompanyAddressAndContactForLhdn(null, "", "NA", "bad");
        companyErrors.Count.ShouldBe(4);
        MyERP.EInvoice.Services.EInvoiceValidationService
            .ValidateCompanyAddressAndContactForLhdn("10 Jalan Ampang", "14", "+60312345678", "billing@company.my")
            .ShouldBeEmpty();
    }

    [Fact]
    public async System.Threading.Tasks.Task UpstreamBatch26_TemplateHolidayDependents_PosProfileFilter_AppointmentMail_InvoiceDiscountingPerm_ConsolidatedDelivery_Parity()
    {
        // 1. ERPNext PR #69137: Holiday-shifted template task reschedules its dependent tasks after dependency mapping
        var projId = Guid.NewGuid();
        var projectStart = new DateTime(2026, 9, 1);
        // Template task A: begin=0, duration=2 => naive Sep 1..Sep 3, but Sep 1 holiday shifted it to Sep 2..Sep 4
        var taskA = new MyERP.Projects.Entities.ProjectTask(Guid.NewGuid(), projId, "T-A", "Holiday Shifted")
        {
            ExpectedStartDate = new DateTime(2026, 9, 2),
            ExpectedEndDate = new DateTime(2026, 9, 4)
        };
        // Template task B: begin=3, duration=1 => naive Sep 4..Sep 5, depends on A (which now ends Sep 4)
        var taskB = new MyERP.Projects.Entities.ProjectTask(Guid.NewGuid(), projId, "T-B", "Dependent Task")
        {
            ExpectedStartDate = new DateTime(2026, 9, 4),
            ExpectedEndDate = new DateTime(2026, 9, 5)
        };
        taskB.AddDependency(taskA.Id);

        var taskRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Projects.Entities.ProjectTask, Guid>>();
        NSubstitute.SubstituteExtensions.Returns(taskRepo.GetQueryableAsync(),
            System.Threading.Tasks.Task.FromResult(new List<MyERP.Projects.Entities.ProjectTask> { taskA, taskB }.AsQueryable()));
        var depSvc = new MyERP.Projects.DomainServices.TaskDependencyValidationService(taskRepo);

        await depSvc.RescheduleTemplateTasksMovedByHolidaysAsync(
            projectStart,
            new List<(MyERP.Projects.Entities.ProjectTask Task, int BeginDays, int DurationDays)>
            {
                (taskA, 0, 2),
                (taskB, 3, 1)
            });

        taskB.ExpectedStartDate.ShouldBe(new DateTime(2026, 9, 5));
        taskB.ExpectedEndDate.ShouldBe(new DateTime(2026, 9, 6));

        // 2. ERPNext PR #69213: POS Profile filter permission check refuses unentitled callers before leaking existence
        Should.NotThrow(() => MyERP.Sales.Entities.PosProfile.ValidatePosProfileFilterPermission(null, false, false));
        Should.NotThrow(() => MyERP.Sales.Entities.PosProfile.ValidatePosProfileFilterPermission("POS-1", true, true));
        Should.Throw<Volo.Abp.BusinessException>(() =>
            MyERP.Sales.Entities.PosProfile.ValidatePosProfileFilterPermission("POS-1", false, true));
        Should.Throw<Volo.Abp.BusinessException>(() =>
            MyERP.Sales.Entities.PosProfile.ValidatePosProfileFilterPermission("POS-Missing", true, false));

        // 3. ERPNext PR #67759: Verification email excludes guest-supplied customer name; confirmed email includes it
        var phishingName = "Customer. Pay invoice INV-777 now at https://pay.example.com";
        var appt = new MyERP.CRM.Entities.Appointment(
            Guid.NewGuid(), Guid.NewGuid(), phishingName, new DateTime(2026, 9, 10, 10, 0, 0), createdThroughPortal: true);
        var verifyBody = appt.BuildVerificationEmailBody("https://erp.example.com/book_appointment/verify?key=abc");
        verifyBody.ShouldContain("/book_appointment/verify?key=abc");
        verifyBody.ShouldNotContain("pay.example.com");
        verifyBody.ShouldNotContain("INV-777");
        appt.BuildConfirmedEmailBody().ShouldContain(phishingName);

        // 4. ERPNext PR #69090: Invoice Discounting mapper checks both source SI read and ID create permissions
        Should.Throw<Volo.Abp.BusinessException>(() =>
            MyERP.Accounting.DomainServices.InvoiceDiscountingService.ValidateCreateFromSalesInvoicePermission(false, true));
        Should.Throw<Volo.Abp.BusinessException>(() =>
            MyERP.Accounting.DomainServices.InvoiceDiscountingService.ValidateCreateFromSalesInvoicePermission(true, false));
        Should.NotThrow(() =>
            MyERP.Accounting.DomainServices.InvoiceDiscountingService.ValidateCreateFromSalesInvoicePermission(true, true));

        // 5. MyInvois consolidate_invoice.py: Consolidated delivery party emits EI00000000010, BRN=NA, MYS, Consolidated Buyers
        var builder = new MyERP.EInvoice.Services.InvoiceDocumentBuilder();
        var consolidatedXml = builder.Build(new MyERP.EInvoice.Services.EInvoiceDocumentData
        {
            InvoiceNumber = "CONS-2026-001",
            DocumentTypeCode = "01",
            IssueDate = new DateTime(2026, 11, 24),
            CurrencyCode = "MYR",
            IsConsolidated = true,
            Supplier = new MyERP.EInvoice.Services.EInvoicePartyData
            {
                Name = "Seller Sdn Bhd",
                Tin = "C1234567890",
                IdType = "BRN",
                IdValue = "202001000001"
            },
            Buyer = new MyERP.EInvoice.Services.EInvoicePartyData
            {
                Name = "General Public",
                Tin = "EI00000000010"
            },
            Delivery = new MyERP.EInvoice.Services.EInvoiceDeliveryData(),
            Lines = new List<MyERP.EInvoice.Services.EInvoiceLineData>
            {
                new() { Description = "POS Consolidated", Quantity = 1m, UnitPrice = 50m, TaxAmount = 0m }
            },
            NetTotal = 50m,
            TaxAmount = 0m,
            GrandTotal = 50m
        });
        consolidatedXml.ShouldContain("<cac:Delivery>");
        consolidatedXml.ShouldContain("<cac:DeliveryParty>");
        consolidatedXml.ShouldContain("<cbc:ID schemeID=\"TIN\">EI00000000010</cbc:ID>");
        consolidatedXml.ShouldContain("<cbc:ID schemeID=\"BRN\">NA</cbc:ID>");
        consolidatedXml.ShouldContain("<cbc:RegistrationName>Consolidated Buyers</cbc:RegistrationName>");
    }

    private sealed class CaptureHttpHandler : System.Net.Http.HttpMessageHandler
    {
        private readonly Func<System.Net.Http.HttpRequestMessage, System.Net.Http.HttpResponseMessage> _fn;
        public CaptureHttpHandler(Func<System.Net.Http.HttpRequestMessage, System.Net.Http.HttpResponseMessage> fn) => _fn = fn;
        protected override System.Threading.Tasks.Task<System.Net.Http.HttpResponseMessage> SendAsync(
            System.Net.Http.HttpRequestMessage request,
            System.Threading.CancellationToken cancellationToken)
            => System.Threading.Tasks.Task.FromResult(_fn(request));
    }
}
