using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Tax.DomainServices;
using MyERP.Tax.Entities;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for:
/// 1. Tax item-wise error diffusion (gotcha #585)
/// 2. JournalEntry.AddLineWithDimensions (exchange GL dimension propagation)
/// 3. PaymentEntry CostCenterId/ProjectId fields
/// </summary>
public class TaxDimensionAndErrorDiffusionTests
{
    private static TransactionTaxRow MakeTax(decimal rate, string chargeType = "On Net Total",
        string category = "Total", bool inclusive = false, Guid? accountId = null)
    {
        var tax = new TransactionTaxRow(Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(),
            1, $"Tax {rate}%", chargeType, rate);
        tax.TaxCategory = category;
        tax.IncludedInPrintRate = inclusive;
        if (accountId.HasValue) tax.AccountId = accountId;
        return tax;
    }

    // ── Tax Error Diffusion ─────────────────────────────────────────

    [Fact]
    public void TaxCalculation_MultiItem_ErrorDiffusion_TotalMatches()
    {
        var items = new List<TransactionItem>
        {
            new() { ItemId = Guid.NewGuid(), Qty = 3, Rate = 33.33m, NetAmount = 99.99m },
            new() { ItemId = Guid.NewGuid(), Qty = 2, Rate = 50.01m, NetAmount = 100.02m },
            new() { ItemId = Guid.NewGuid(), Qty = 1, Rate = 99.99m, NetAmount = 99.99m },
        };
        var taxes = new List<TransactionTaxRow> { MakeTax(6m) };

        var svc = new TaxesAndTotalsService();
        var result = svc.Calculate(items, taxes);

        Assert.True(result.TotalTax > 0);
        Assert.Equal(result.GrandTotal, result.NetTotal + result.TotalTax);
    }

    [Fact]
    public void TaxCalculation_SingleItem_NoDiffusionNeeded()
    {
        var items = new List<TransactionItem>
        {
            new() { ItemId = Guid.NewGuid(), Qty = 1, Rate = 100m, NetAmount = 100m },
        };
        var taxes = new List<TransactionTaxRow> { MakeTax(6m) };

        var result = new TaxesAndTotalsService().Calculate(items, taxes);

        Assert.Equal(6m, result.TotalTax);
        Assert.Equal(106m, result.GrandTotal);
    }

    [Fact]
    public void TaxCalculation_ThreeItems_RoundingHandled()
    {
        var items = new List<TransactionItem>
        {
            new() { ItemId = Guid.NewGuid(), Qty = 1, Rate = 33.33m, NetAmount = 33.33m },
            new() { ItemId = Guid.NewGuid(), Qty = 1, Rate = 33.33m, NetAmount = 33.33m },
            new() { ItemId = Guid.NewGuid(), Qty = 1, Rate = 33.34m, NetAmount = 33.34m },
        };
        var taxes = new List<TransactionTaxRow> { MakeTax(6m) };

        var result = new TaxesAndTotalsService().Calculate(items, taxes);

        Assert.Equal(100.00m, result.NetTotal);
        Assert.Equal(6.00m, result.TotalTax);
        Assert.Equal(106.00m, result.GrandTotal);
    }

    [Fact]
    public void TaxCalculation_InclusiveTax_BackCalculatesExclusiveRate()
    {
        var items = new List<TransactionItem>
        {
            new() { ItemId = Guid.NewGuid(), Qty = 1, Rate = 106m, NetAmount = 106m },
        };
        var taxes = new List<TransactionTaxRow> { MakeTax(6m, inclusive: true) };

        var result = new TaxesAndTotalsService().Calculate(items, taxes);

        Assert.Equal(100m, result.NetTotal);
        Assert.Equal(6m, result.TotalTax);
    }

    [Fact]
    public void TaxCalculation_NAsentinel_ExcludesItemFromTax()
    {
        var taxAccountId = Guid.NewGuid();
        var items = new List<TransactionItem>
        {
            new() { ItemId = Guid.NewGuid(), Qty = 1, Rate = 100m, NetAmount = 100m,
                     ItemTaxRateOverrides = new() { { taxAccountId, decimal.MinValue } } },
            new() { ItemId = Guid.NewGuid(), Qty = 1, Rate = 200m, NetAmount = 200m },
        };
        var taxes = new List<TransactionTaxRow> { MakeTax(10m, accountId: taxAccountId) };

        var result = new TaxesAndTotalsService().Calculate(items, taxes);

        Assert.Equal(20m, result.TotalTax);
        Assert.Equal(320m, result.GrandTotal);
    }

    [Fact]
    public void TaxCalculation_DiscountOnNetTotal_ReducesBeforeTax()
    {
        var items = new List<TransactionItem>
        {
            new() { ItemId = Guid.NewGuid(), Qty = 1, Rate = 1000m, NetAmount = 1000m },
        };
        var taxes = new List<TransactionTaxRow> { MakeTax(6m) };

        var result = new TaxesAndTotalsService().Calculate(items, taxes, discountAmount: 100m, applyDiscountOn: "Net Total");

        Assert.Equal(900m, result.NetTotal);
        Assert.Equal(54m, result.TotalTax);
        Assert.Equal(954m, result.GrandTotal);
    }

    [Fact]
    public void TaxCalculation_DiscountOnGrandTotal_ReducesAfterTax()
    {
        var items = new List<TransactionItem>
        {
            new() { ItemId = Guid.NewGuid(), Qty = 1, Rate = 1000m, NetAmount = 1000m },
        };
        var taxes = new List<TransactionTaxRow> { MakeTax(6m) };

        var result = new TaxesAndTotalsService().Calculate(items, taxes, discountAmount: 50m, applyDiscountOn: "Grand Total");

        Assert.Equal(1000m, result.NetTotal);
        Assert.Equal(60m, result.TotalTax);
        Assert.Equal(1010m, result.GrandTotal);
    }

    [Fact]
    public void TaxCalculation_MultiCurrency_BaseAmounts()
    {
        var items = new List<TransactionItem>
        {
            new() { ItemId = Guid.NewGuid(), Qty = 1, Rate = 100m, NetAmount = 100m },
        };
        var taxes = new List<TransactionTaxRow> { MakeTax(6m) };

        var result = new TaxesAndTotalsService().Calculate(items, taxes, exchangeRate: 4.72m);

        Assert.Equal(472m, result.BaseNetTotal);
        Assert.Equal(28.32m, result.BaseTotalTax);
        Assert.Equal(500.32m, result.BaseGrandTotal);
    }

    // ── JournalEntry AddLineWithDimensions ──────────────────────────

    [Fact]
    public void JournalEntry_AddLineWithDimensions_SetsAllFields()
    {
        var ccId = Guid.NewGuid();
        var projId = Guid.NewGuid();
        var acctId = Guid.NewGuid();
        var je = new JournalEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);

        je.AddLineWithDimensions(acctId, 1000m, true, ccId, projId, "Book1", "Test debit");
        je.AddLineWithDimensions(acctId, 1000m, false, ccId, projId, "Book1", "Test credit");

        Assert.Equal(2, je.Lines.Count);
        var debit = je.Lines.First(l => l.IsDebit);
        Assert.Equal(ccId, debit.CostCenterId);
        Assert.Equal(projId, debit.ProjectId);
        Assert.Equal("Book1", debit.FinanceBook);
    }

    [Fact]
    public void JournalEntry_AddLineWithDimensions_NullDimensions_OK()
    {
        var je = new JournalEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        je.AddLineWithDimensions(Guid.NewGuid(), 500m, true, description: "No dims");
        je.AddLineWithDimensions(Guid.NewGuid(), 500m, false);

        Assert.Equal(2, je.Lines.Count);
        Assert.Null(je.Lines.First().CostCenterId);
    }

    [Fact]
    public void JournalEntry_AddLineWithDimensions_BlockedAfterPost()
    {
        var je = new JournalEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        je.AddLineWithDimensions(Guid.NewGuid(), 100m, true);
        je.AddLineWithDimensions(Guid.NewGuid(), 100m, false);
        je.Validate();
        je.Post();

        Assert.Throws<Volo.Abp.BusinessException>(() =>
            je.AddLineWithDimensions(Guid.NewGuid(), 50m, true, Guid.NewGuid()));
    }

    [Fact]
    public void JournalEntry_AddLine_Original_StillWorks()
    {
        var je = new JournalEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        je.AddLine(Guid.NewGuid(), 200m, true, "Debit");
        je.AddLine(Guid.NewGuid(), 200m, false, "Credit");

        Assert.Equal(2, je.Lines.Count);
        Assert.Null(je.Lines.First().CostCenterId);
    }

    // ── PaymentEntry CostCenterId/ProjectId ─────────────────────────

    [Fact]
    public void PaymentEntry_CostCenterId_DefaultsNull()
    {
        var pe = new PaymentEntry(Guid.NewGuid(), Guid.NewGuid(), PaymentType.Receive,
            DateTime.UtcNow, 1000m, Guid.NewGuid(), Guid.NewGuid());

        Assert.Null(pe.CostCenterId);
        Assert.Null(pe.ProjectId);
    }

    [Fact]
    public void PaymentEntry_CostCenterId_CanBeSet()
    {
        var ccId = Guid.NewGuid();
        var projId = Guid.NewGuid();
        var pe = new PaymentEntry(Guid.NewGuid(), Guid.NewGuid(), PaymentType.Pay,
            DateTime.UtcNow, 500m, Guid.NewGuid(), Guid.NewGuid());

        pe.CostCenterId = ccId;
        pe.ProjectId = projId;

        Assert.Equal(ccId, pe.CostCenterId);
        Assert.Equal(projId, pe.ProjectId);
    }

    [Fact]
    public void PaymentEntry_ExchangeGainLoss_DimensionPropagation_Concept()
    {
        var pe = new PaymentEntry(Guid.NewGuid(), Guid.NewGuid(), PaymentType.Receive,
            DateTime.UtcNow, 1000m, Guid.NewGuid(), Guid.NewGuid());
        pe.ExchangeRate = 4.80m;
        pe.SourceExchangeRate = 4.72m;
        pe.CostCenterId = Guid.NewGuid();
        pe.ProjectId = Guid.NewGuid();

        Assert.Equal(80m, pe.ExchangeGainLoss);
        Assert.NotNull(pe.CostCenterId);
        Assert.NotNull(pe.ProjectId);
    }

    // ── Error diffusion with many items ─────────────────────────────

    [Fact]
    public void TaxCalculation_FiveItems_OddAmounts_NoRoundingDrift()
    {
        var items = new List<TransactionItem>
        {
            new() { ItemId = Guid.NewGuid(), Qty = 1, Rate = 17.99m, NetAmount = 17.99m },
            new() { ItemId = Guid.NewGuid(), Qty = 3, Rate = 22.33m, NetAmount = 66.99m },
            new() { ItemId = Guid.NewGuid(), Qty = 2, Rate = 45.67m, NetAmount = 91.34m },
            new() { ItemId = Guid.NewGuid(), Qty = 1, Rate = 83.33m, NetAmount = 83.33m },
            new() { ItemId = Guid.NewGuid(), Qty = 4, Rate = 11.11m, NetAmount = 44.44m },
        };
        var taxes = new List<TransactionTaxRow> { MakeTax(6m) };

        var result = new TaxesAndTotalsService().Calculate(items, taxes);

        Assert.Equal(result.GrandTotal, result.NetTotal + result.TotalTax);
        Assert.True(result.TotalTax > 0);
    }

    [Fact]
    public void TaxCalculation_ValuationOnly_ExcludedFromGrandTotal()
    {
        var items = new List<TransactionItem>
        {
            new() { ItemId = Guid.NewGuid(), Qty = 1, Rate = 1000m, NetAmount = 1000m },
        };
        var taxes = new List<TransactionTaxRow> { MakeTax(5m, category: "Valuation") };

        var result = new TaxesAndTotalsService().Calculate(items, taxes);

        // Valuation-only tax doesn't add to grand total
        Assert.Equal(1000m, result.GrandTotal);
        Assert.Equal(0m, result.TotalTax);
    }
}
