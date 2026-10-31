using System;
using System.Collections.Generic;
using MyERP.Accounting.DomainServices;
using MyERP.Manufacturing.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Unit tests for upstream ERPNext parity batch 4:
/// - 4a66be27ac / 4628e8735d (PR #62650): Cost Center Allocation currency-specific precision &amp; cumulative percentage carry-forward rounding.
/// - 95704805df / e011efe379 (PR #62678): Payment Entry cumulative invoice &amp; payment-term allocation validation + mixed-sign rejection.
/// - 7fb16ab015: BillOfMaterials Finished Goods Based Operating Cost (fg_based_operating_cost &amp; operating_cost_per_bom_quantity).
/// </summary>
public class UpstreamBatch4ParityTests
{
    [Fact]
    public void CostCenterAllocation_DistributeAmountByPercentages_HonorsCurrencyPrecisionAndCumulativeRounding_4a66be27ac()
    {
        var ccA = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var ccB = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var ccC = Guid.Parse("00000000-0000-0000-0000-000000000003");

        CostCenterAllocationService.GetCurrencyPrecision("JPY").ShouldBe(0);
        CostCenterAllocationService.GetCurrencyPrecision("KWD").ShouldBe(3);
        CostCenterAllocationService.GetCurrencyPrecision("USD").ShouldBe(2);

        // 1) USD 100.01 split [50%, 50%] at 2 decimals -> [50.01, 50.00], exact sum = 100.01
        var usdHalf = CostCenterAllocationService.DistributeAmountByPercentages(
            100.01m,
            new[] { (ccA, 50m), (ccB, 50m) },
            precision: CostCenterAllocationService.GetCurrencyPrecision("USD"));
        usdHalf[0].Amount.ShouldBe(50.01m);
        usdHalf[1].Amount.ShouldBe(50.00m);
        (usdHalf[0].Amount + usdHalf[1].Amount).ShouldBe(100.01m);

        // 2) JPY 1 split [60%, 40%] at 0 decimals -> [1, 0], exact sum = 1
        var jpy6040 = CostCenterAllocationService.DistributeAmountByPercentages(
            1m,
            new[] { (ccA, 60m), (ccB, 40m) },
            precision: CostCenterAllocationService.GetCurrencyPrecision("JPY"));
        jpy6040[0].Amount.ShouldBe(1m);
        jpy6040[1].Amount.ShouldBe(0m);

        // 3) JPY 1 split [40%, 60%] at 0 decimals -> [0, 1], exact sum = 1
        var jpy4060 = CostCenterAllocationService.DistributeAmountByPercentages(
            1m,
            new[] { (ccA, 40m), (ccB, 60m) },
            precision: CostCenterAllocationService.GetCurrencyPrecision("JPY"));
        jpy4060[0].Amount.ShouldBe(0m);
        jpy4060[1].Amount.ShouldBe(1m);

        // 4) KWD 1.001 split [60%, 40%] at 3 decimals -> [0.601, 0.400], exact sum = 1.001
        var kwdSplit = CostCenterAllocationService.DistributeAmountByPercentages(
            1.001m,
            new[] { (ccA, 60m), (ccB, 40m) },
            precision: CostCenterAllocationService.GetCurrencyPrecision("KWD"));
        kwdSplit[0].Amount.ShouldBe(0.601m);
        kwdSplit[1].Amount.ShouldBe(0.400m);
        (kwdSplit[0].Amount + kwdSplit[1].Amount).ShouldBe(1.001m);

        // 5) 3-way [33.33%, 33.33%, 33.34%] on 100.00 -> [33.33, 33.33, 33.34]
        var threeWay = CostCenterAllocationService.DistributeAmountByPercentages(
            100.00m,
            new[] { (ccA, 33.33m), (ccB, 33.33m), (ccC, 33.34m) },
            precision: 2);
        threeWay[0].Amount.ShouldBe(33.33m);
        threeWay[1].Amount.ShouldBe(33.33m);
        threeWay[2].Amount.ShouldBe(33.34m);
    }

    [Fact]
    public void PaymentEntry_ValidateCumulativeReferenceAllocations_RejectsMixedSignsAndSplitOverAllocations_95704805df()
    {
        var invId = Guid.NewGuid();
        var term1 = Guid.NewGuid();
        var term2 = Guid.NewGuid();

        // 1) Mixed-sign [+80, -10] against positive +118 invoice throws OverAllocation
        var mixedPositiveEx = Should.Throw<BusinessException>(() =>
            PaymentEntryManager.ValidateCumulativeReferenceAllocations(new List<ReferenceAllocationSnapshot>
            {
                new("SalesInvoice", invId, "SINV-001", AllocatedAmount: 80m, InvoiceOutstandingAmount: 118m, IsTermBasedAllocation: true, PaymentTermId: term1, TermOutstandingAmount: 80m),
                new("SalesInvoice", invId, "SINV-001", AllocatedAmount: -10m, InvoiceOutstandingAmount: 118m, IsTermBasedAllocation: false)
            }));
        mixedPositiveEx.Code.ShouldBe(MyERPDomainErrorCodes.OverAllocation);

        // 2) Mixed-sign [-80, +10] against negative -118 credit note throws OverAllocation
        var mixedReturnEx = Should.Throw<BusinessException>(() =>
            PaymentEntryManager.ValidateCumulativeReferenceAllocations(new List<ReferenceAllocationSnapshot>
            {
                new("SalesInvoice", invId, "CN-001", AllocatedAmount: -80m, InvoiceOutstandingAmount: -118m),
                new("SalesInvoice", invId, "CN-001", AllocatedAmount: 10m, InvoiceOutstandingAmount: -118m)
            }));
        mixedReturnEx.Code.ShouldBe(MyERPDomainErrorCodes.OverAllocation);

        // 3) Split positive [+70, +60] = 130 > 118 against same invoice throws OverAllocation
        var splitInvoiceEx = Should.Throw<BusinessException>(() =>
            PaymentEntryManager.ValidateCumulativeReferenceAllocations(new List<ReferenceAllocationSnapshot>
            {
                new("SalesInvoice", invId, "SINV-002", AllocatedAmount: 70m, InvoiceOutstandingAmount: 118m, IsTermBasedAllocation: true, PaymentTermId: term1, TermOutstandingAmount: 70m),
                new("SalesInvoice", invId, "SINV-002", AllocatedAmount: 60m, InvoiceOutstandingAmount: 118m, IsTermBasedAllocation: true, PaymentTermId: term2, TermOutstandingAmount: 60m)
            }));
        splitInvoiceEx.Code.ShouldBe(MyERPDomainErrorCodes.OverAllocation);

        // 4) Split term [+40, +30] = 70 > 59 against same term throws PaymentTermOutstandingExceeded
        var splitTermEx = Should.Throw<BusinessException>(() =>
            PaymentEntryManager.ValidateCumulativeReferenceAllocations(new List<ReferenceAllocationSnapshot>
            {
                new("SalesInvoice", invId, "SINV-003", AllocatedAmount: 40m, InvoiceOutstandingAmount: 118m, IsTermBasedAllocation: true, PaymentTermId: term1, TermOutstandingAmount: 59m, PaymentTermDescription: "50% Advance"),
                new("SalesInvoice", invId, "SINV-003", AllocatedAmount: 30m, InvoiceOutstandingAmount: 118m, IsTermBasedAllocation: true, PaymentTermId: term1, TermOutstandingAmount: 59m, PaymentTermDescription: "50% Advance")
            }));
        splitTermEx.Code.ShouldBe(MyERPDomainErrorCodes.PaymentTermOutstandingExceeded);

        // 5) Valid two-term split [+59 (Term 1), +59 (Term 2)] = 118 <= 118 succeeds
        Should.NotThrow(() =>
            PaymentEntryManager.ValidateCumulativeReferenceAllocations(new List<ReferenceAllocationSnapshot>
            {
                new("SalesInvoice", invId, "SINV-004", AllocatedAmount: 59m, InvoiceOutstandingAmount: 118m, IsTermBasedAllocation: true, PaymentTermId: term1, TermOutstandingAmount: 59m, PaymentTermDescription: "Term 1"),
                new("SalesInvoice", invId, "SINV-004", AllocatedAmount: 59m, InvoiceOutstandingAmount: 118m, IsTermBasedAllocation: true, PaymentTermId: term2, TermOutstandingAmount: 59m, PaymentTermDescription: "Term 2")
            }));
    }

    [Fact]
    public void BillOfMaterials_FgBasedOperatingCost_ComputesFromBomQty_UnlessWithOperationsOrPhantom_7fb16ab015()
    {
        var companyId = Guid.NewGuid();
        var fgItemId = Guid.NewGuid();
        var rmItemId = Guid.NewGuid();

        var bom = new BillOfMaterials(Guid.NewGuid(), companyId, "BOM-FG-OP-001", fgItemId)
        {
            Quantity = 4m,
            WithOperations = false,
            IsPhantomBom = false,
            FgBasedOperatingCost = true,
            OperatingCostPerBomQuantity = 15m
        };
        bom.AddItem(new BomItem(Guid.NewGuid(), bom.Id, rmItemId, "Raw Material A", quantity: 4m, rate: 10m));

        bom.RecalculateCost();
        bom.TotalMaterialCost.ShouldBe(40m);
        bom.OperatingCost.ShouldBe(60m);
        bom.TotalCost.ShouldBe(100m);

        // Phantom BOM ignores FgBasedOperatingCost per ERPNext 7fb16ab015
        bom.IsPhantomBom = true;
        bom.RecalculateCost();
        bom.OperatingCost.ShouldBe(0m);
        bom.TotalCost.ShouldBe(40m);

        // WithOperations overrides FgBasedOperatingCost with routing operation costs
        bom.IsPhantomBom = false;
        bom.WithOperations = true;
        var op = new BomOperation(Guid.NewGuid(), bom.Id, Guid.NewGuid(), sequenceId: 10, timeInMins: 30m, workstationId: null);
        op.CalculateCost(50m); // 0.5h * 50 = 25
        bom.AddOperation(op);
        bom.RecalculateCost();
        bom.OperatingCost.ShouldBe(25m);
        bom.TotalCost.ShouldBe(65m);
    }
}
