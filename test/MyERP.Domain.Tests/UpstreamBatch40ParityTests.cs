using System;
using System.Collections.Generic;
using MyERP.Manufacturing;
using MyERP.Manufacturing.DomainServices;
using MyERP.Manufacturing.Entities;
using MyERP.Purchasing.DomainServices;
using MyERP.Purchasing.Entities;
using MyERP.Sales.DomainServices;
using Shouldly;
using Xunit;

namespace MyERP;

/// <summary>
/// Parity tests for upstream ERPNext PRs #67361, #67427, #68338, #67338, #68701, #67472.
/// </summary>
public class UpstreamBatch40ParityTests
{
    [Fact]
    public void Pr67361_SubcontractingOrder_DistributeAdditionalCostsByPreCostAmount_IsIdempotentAcrossSaves()
    {
        var rowA = Guid.NewGuid();
        var rowB = Guid.NewGuid();
        var rows = new List<(Guid RowId, decimal Qty, decimal RmCostPerQty, decimal ServiceCostPerQty)>
        {
            (rowA, 10m, 20m, 10m), // pre-cost amount = 300
            (rowB, 5m, 100m, 20m), // pre-cost amount = 600
        };

        var firstSave = SubcontractingManager.DistributeOrderAdditionalCostsByPreCostAmount(rows, totalAdditionalCost: 90m, distributeByQty: false);
        var secondSave = SubcontractingManager.DistributeOrderAdditionalCostsByPreCostAmount(rows, totalAdditionalCost: 90m, distributeByQty: false);

        // Split 300:600 = 1:2 -> 30 on rowA (3/qty), 60 on rowB (12/qty)
        firstSave[0].AdditionalCostPerQty.ShouldBe(3m);
        firstSave[0].FinalRate.ShouldBe(33m);
        firstSave[0].FinalAmount.ShouldBe(330m);

        firstSave[1].AdditionalCostPerQty.ShouldBe(12m);
        firstSave[1].FinalRate.ShouldBe(132m);
        firstSave[1].FinalAmount.ShouldBe(660m);

        secondSave.ShouldBe(firstSave);
    }

    [Fact]
    public void Pr67427_GrossProfit_RowInAnotherUom_AndZeroValuationRate()
    {
        // Selling 2 Boxes (1 Box = 12 Units -> stockQty = 24) at USD 60/Box, FX = 4.0 -> MYR 240/Box = MYR 20/Unit.
        // Valuation rate = MYR 15/Unit -> Gross profit = (20 - 15) * 24 = 120.00
        var gp = GrossProfitService.CalculateRowGrossProfit(
            rate: 60m,
            exchangeRate: 4.0m,
            conversionFactor: 12m,
            valuationRate: 15m,
            stockQty: 24m,
            precision: 2);
        gp.ShouldBe(120.00m);

        // Zero valuation rate still computes full gross profit (PR #67427 commit 418ab200e8)
        var gpZeroVal = GrossProfitService.CalculateRowGrossProfit(
            rate: 120m,
            exchangeRate: 1m,
            conversionFactor: 12m,
            valuationRate: 0m,
            stockQty: 24m,
            precision: 2);
        gpZeroVal.ShouldBe(240.00m);
    }

    [Fact]
    public void Pr68338_PurchaseInvoice_ResetReceiptQty_ClearsOnlyNonPrRowsWhenUpdateStockOff()
    {
        var pi = new PurchaseInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PINV-68338", DateTime.Today)
        {
            UpdateStock = true
        };
        pi.AddItem(Guid.NewGuid(), "Direct Row", 5m, 10m, 0m, "Unit", receivedQty: 7m, rejectedQty: 2m);
        pi.AddItem(Guid.NewGuid(), "PR Linked Row", 4m, 10m, 0m, "Unit", receivedQty: 4m, rejectedQty: 0m);
        pi.Items[1].PurchaseReceiptItemId = Guid.NewGuid();

        // While UpdateStock is true, ResetReceiptQty does nothing
        pi.ResetReceiptQty();
        pi.Items[0].ReceivedQty.ShouldBe(7m);
        pi.Items[0].RejectedQty.ShouldBe(2m);

        // Once UpdateStock is turned off, non-PR row resets to 0 while PR-linked row is preserved
        pi.UpdateStock = false;
        pi.ResetReceiptQty();
        pi.Items[0].ReceivedQty.ShouldBe(0m);
        pi.Items[0].RejectedQty.ShouldBe(0m);
        pi.Items[1].ReceivedQty.ShouldBe(4m);
    }

    [Fact]
    public void Pr67338_Bom_CurrencyChangeWithStaleConversionRate_TriggersRefreshAtFieldPrecision()
    {
        // Currency changed USD -> EUR while conversion rate unchanged within 4-decimal precision
        BomValidationService.HasStaleConversionRate("USD", 4.45124m, "EUR", 4.45121m, precision: 4).ShouldBeTrue();
        BomValidationService.HasStaleConversionRate("USD", 4.4512m, "EUR", 4.8200m, precision: 4).ShouldBeFalse();
        BomValidationService.HasStaleConversionRate("USD", 4.4512m, "USD", 4.4512m, precision: 4).ShouldBeFalse();

        var fetched = false;
        var resolved = BomValidationService.ResolveBomConversionRate(
            currency: "EUR",
            companyCurrency: "MYR",
            currentConversionRate: 4.45121m,
            fetchExchangeRate: () => { fetched = true; return 4.85m; },
            previousCurrency: "USD",
            previousConversionRate: 4.45124m,
            precision: 4);
        fetched.ShouldBeTrue();
        resolved.ShouldBe(4.85m);

        // Matching company currency always returns 1 without fetching
        var fetchedSame = false;
        var companyRate = BomValidationService.ResolveBomConversionRate(
            currency: "MYR",
            companyCurrency: "MYR",
            currentConversionRate: 4.45m,
            fetchExchangeRate: () => { fetchedSame = true; return 9.99m; });
        fetchedSame.ShouldBeFalse();
        companyRate.ShouldBe(1m);
    }

    [Fact]
    public void Pr68701_WorkOrder_CorrectiveCostBookedOnFgEntryOnly_AndSpreadOverFgQtyOnly()
    {
        var companyId = Guid.NewGuid();
        var fgItemId = Guid.NewGuid();
        var semiFgItemId = Guid.NewGuid();
        var opSemiId = Guid.NewGuid();
        var opFgId = Guid.NewGuid();

        var wo = new WorkOrder(Guid.NewGuid(), companyId, "WO-68701", fgItemId, Guid.NewGuid(), 10m)
        {
            TrackSemiFinishedGoods = true
        };

        var semiJc = new JobCard(Guid.NewGuid(), companyId, wo.Id, opSemiId, 20m, 1)
        {
            FinishedGoodItemId = semiFgItemId,
            CompletedQty = 20m
        };
        semiJc.Start();
        semiJc.Complete();

        var fgJc = new JobCard(Guid.NewGuid(), companyId, wo.Id, opFgId, 10m, 2)
        {
            FinishedGoodItemId = fgItemId,
            CompletedQty = 10m
        };
        fgJc.Start();
        fgJc.Complete();

        var submittedCards = new[] { semiJc, fgJc };

        // Semi-finished good entry gets 0 corrective cost
        WorkOrderManager.MakesSemiFinishedGood(wo.ItemId, semiJc.FinishedGoodItemId).ShouldBeTrue();
        var semiAlloc = WorkOrderManager.CalculateCorrectiveOperationCostForManufacture(
            wo, correctiveOperationCost: 100m, utilisedCorrectiveCost: 0m, fgCompletedQty: 20m,
            submittedJobCards: submittedCards, currentJobCard: semiJc);
        semiAlloc.ShouldBe(0m);

        // Max operation qty ignores semi-FG (20) and counts FG only (10)
        WorkOrderManager.CalculateMaxOperationQuantity(wo, submittedCards).ShouldBe(10m);

        // Producing 4 of 10 FG units allocates 40 of 100 corrective cost
        var fgAlloc = WorkOrderManager.CalculateCorrectiveOperationCostForManufacture(
            wo, correctiveOperationCost: 100m, utilisedCorrectiveCost: 0m, fgCompletedQty: 4m,
            submittedJobCards: submittedCards, currentJobCard: fgJc);
        fgAlloc.ShouldBe(40m);
    }

    [Fact]
    public void Pr67472_CorrectiveJobCard_KeepsManualRateUntilWorkstationChanges()
    {
        var wsA = Guid.NewGuid();
        var wsB = Guid.NewGuid();

        // Unchanged workstation keeps manual hour rate (e.g. amended or saved corrective card)
        JobCardManager.ResolveCorrectiveJobCardHourRate(125m, wsA, wsA, newWorkstationHourRate: 60m)
            .ShouldBe(125m);

        // Changed workstation switches to new workstation hour rate
        JobCardManager.ResolveCorrectiveJobCardHourRate(125m, wsB, wsA, newWorkstationHourRate: 85m)
            .ShouldBe(85m);

        // First-time assignment from null previous workstation takes workstation hour rate
        JobCardManager.ResolveCorrectiveJobCardHourRate(0m, wsA, null, newWorkstationHourRate: 60m)
            .ShouldBe(60m);
    }
}
