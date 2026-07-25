using System;
using System.Linq;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Assets;
using MyERP.Assets.Entities;
using MyERP.Accounting.DomainServices;
using MyERP.Sales.Entities;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for PE Tax collection, Asset Depreciation Detail, and SI Loyalty Redemption.
/// </summary>
public class PETaxAssetDetailLoyaltyTests
{
    // === PaymentEntry Taxes Collection ===

    [Fact]
    public void PE_Taxes_DefaultEmpty()
    {
        var pe = CreatePE();
        Assert.Empty(pe.Taxes);
        Assert.Equal(0m, pe.TotalTaxes);
        Assert.Equal(0m, pe.TotalIncludedTaxes);
    }

    [Fact]
    public void PE_AddTax_IncreasesCollection()
    {
        var pe = CreatePE();
        var tax = new PaymentEntryTax(Guid.NewGuid(), pe.Id, Guid.NewGuid())
        {
            Rate = 6m,
            ChargeType = PaymentTaxChargeType.OnPaidAmount
        };
        pe.AddTax(tax);
        Assert.Single(pe.Taxes);
    }

    [Fact]
    public void PE_AddTax_BlockedAfterSubmit()
    {
        var pe = CreatePE();
        pe.Submit();
        Assert.Throws<BusinessException>(() =>
            pe.AddTax(new PaymentEntryTax(Guid.NewGuid(), pe.Id, Guid.NewGuid())));
    }

    [Fact]
    public void PE_RecalculateTaxes_CalculatesAll()
    {
        var pe = CreatePE(10000m);
        var tax1 = new PaymentEntryTax(Guid.NewGuid(), pe.Id, Guid.NewGuid())
        {
            Rate = 6m, ChargeType = PaymentTaxChargeType.OnPaidAmount
        };
        var tax2 = new PaymentEntryTax(Guid.NewGuid(), pe.Id, Guid.NewGuid())
        {
            Rate = 2m, ChargeType = PaymentTaxChargeType.OnPaidAmount
        };
        pe.AddTax(tax1);
        pe.AddTax(tax2);

        pe.RecalculateTaxes();

        Assert.Equal(600m, pe.Taxes.First().TaxAmount);
        Assert.Equal(200m, pe.Taxes.Last().TaxAmount);
        Assert.Equal(800m, pe.TotalTaxes);
    }

    [Fact]
    public void PE_TotalTaxes_ExcludesExchangeGainLoss()
    {
        var pe = CreatePE(5000m);
        pe.AddTax(new PaymentEntryTax(Guid.NewGuid(), pe.Id, Guid.NewGuid())
        {
            Rate = 6m, ChargeType = PaymentTaxChargeType.OnPaidAmount
        });
        pe.AddTax(new PaymentEntryTax(Guid.NewGuid(), pe.Id, Guid.NewGuid())
        {
            TaxAmount = 150m, IsExchangeGainLoss = true
        });

        pe.RecalculateTaxes();

        // 6% of 5000 = 300, exchange G/L of 150 excluded from TotalTaxes
        Assert.Equal(300m, pe.TotalTaxes);
    }

    [Fact]
    public void PE_TotalIncludedTaxes_OnlyCountsIncluded()
    {
        var pe = CreatePE(10000m);
        pe.AddTax(new PaymentEntryTax(Guid.NewGuid(), pe.Id, Guid.NewGuid())
        {
            Rate = 6m, ChargeType = PaymentTaxChargeType.OnPaidAmount,
            IncludedInPaidAmount = true
        });
        pe.AddTax(new PaymentEntryTax(Guid.NewGuid(), pe.Id, Guid.NewGuid())
        {
            Rate = 2m, ChargeType = PaymentTaxChargeType.OnPaidAmount,
            IncludedInPaidAmount = false
        });
        pe.RecalculateTaxes();

        Assert.Equal(600m, pe.TotalIncludedTaxes); // Only the 6% included tax
        Assert.Equal(800m, pe.TotalTaxes);          // Both taxes
    }

    [Fact]
    public void PE_IAccountableDocument_GrandTotal_IncludesNonIncludedTax()
    {
        var pe = CreatePE(10000m);
        pe.AddTax(new PaymentEntryTax(Guid.NewGuid(), pe.Id, Guid.NewGuid())
        {
            Rate = 6m, ChargeType = PaymentTaxChargeType.OnPaidAmount,
            IncludedInPaidAmount = false
        });
        pe.RecalculateTaxes();

        var doc = (IAccountableDocument)pe;
        // GrandTotal = PaidAmount + TotalTaxes - TotalIncludedTaxes = 10000 + 600 - 0
        Assert.Equal(10600m, doc.GrandTotal);
        Assert.Equal(600m, doc.TaxAmount);
    }

    [Fact]
    public void PE_IAccountableDocument_GrandTotal_IncludedTax_NoExtra()
    {
        var pe = CreatePE(10000m);
        pe.AddTax(new PaymentEntryTax(Guid.NewGuid(), pe.Id, Guid.NewGuid())
        {
            Rate = 6m, ChargeType = PaymentTaxChargeType.OnPaidAmount,
            IncludedInPaidAmount = true // Deducted from paid amount, not added
        });
        pe.RecalculateTaxes();

        var doc = (IAccountableDocument)pe;
        // GrandTotal = 10000 + 600 - 600 = 10000 (tax included, so grand total = paid amount)
        Assert.Equal(10000m, doc.GrandTotal);
    }

    // === Asset Depreciation Detail ===

    [Fact]
    public void AssetDepreciationDetail_Create_DefaultValues()
    {
        var detail = new AssetDepreciationDetail(
            Guid.NewGuid(), Guid.NewGuid(), DepreciationMethod.StraightLine, 60, 1, 100000m);

        Assert.Equal(DepreciationMethod.StraightLine, detail.DepreciationMethod);
        Assert.Equal(60, detail.TotalNumberOfDepreciations);
        Assert.Equal(1, detail.FrequencyOfDepreciation);
        Assert.Equal(100000m, detail.NetPurchaseAmount);
        Assert.Equal(100000m, detail.ValueAfterDepreciation);
        Assert.Null(detail.FinanceBookId);
        Assert.Equal(0m, detail.ExpectedValueAfterUsefulLife);
        Assert.Equal(0m, detail.OpeningAccumulatedDepreciation);
    }

    [Fact]
    public void AssetDepreciationDetail_DepreciableAmount_Full()
    {
        var detail = new AssetDepreciationDetail(
            Guid.NewGuid(), Guid.NewGuid(), DepreciationMethod.StraightLine, 60, 1, 100000m)
        {
            ExpectedValueAfterUsefulLife = 10000m
        };
        // 100000 - 10000 - 0 (opening) = 90000
        Assert.Equal(90000m, detail.DepreciableAmount);
    }

    [Fact]
    public void AssetDepreciationDetail_DepreciableAmount_WithOpening()
    {
        var detail = new AssetDepreciationDetail(
            Guid.NewGuid(), Guid.NewGuid(), DepreciationMethod.WrittenDownValue, 20, 12, 500000m)
        {
            ExpectedValueAfterUsefulLife = 50000m,
            OpeningAccumulatedDepreciation = 100000m
        };
        // 500000 - 50000 - 100000 = 350000
        Assert.Equal(350000m, detail.DepreciableAmount);
    }

    [Fact]
    public void AssetDepreciationDetail_FinanceBook_CanBeSet()
    {
        var bookId = Guid.NewGuid();
        var detail = new AssetDepreciationDetail(
            Guid.NewGuid(), Guid.NewGuid(), DepreciationMethod.DoubleDecliningBalance, 10, 12, 200000m)
        {
            FinanceBookId = bookId,
            Rate = 20m
        };
        Assert.Equal(bookId, detail.FinanceBookId);
        Assert.Equal(20m, detail.Rate);
    }

    [Fact]
    public void Asset_DepreciationDetails_Collection_Exists()
    {
        var asset = new Asset(Guid.NewGuid(), Guid.NewGuid(), "A-001", "Office Equipment",
            DateTime.Today, 50000m);
        Assert.NotNull(asset.DepreciationDetails);
        Assert.Empty(asset.DepreciationDetails);
    }

    [Fact]
    public void AssetDepreciationDetail_MultiBook_Scenario()
    {
        // Tax book: Straight Line over 5 years
        var taxDetail = new AssetDepreciationDetail(
            Guid.NewGuid(), Guid.NewGuid(), DepreciationMethod.StraightLine, 60, 1, 100000m)
        {
            FinanceBookId = Guid.NewGuid(), // Tax Book
            ExpectedValueAfterUsefulLife = 1m
        };

        // Management book: WDV over 8 years
        var mgmtDetail = new AssetDepreciationDetail(
            Guid.NewGuid(), Guid.NewGuid(), DepreciationMethod.WrittenDownValue, 96, 1, 100000m)
        {
            FinanceBookId = Guid.NewGuid(), // Management Book
            Rate = 15m,
            ExpectedValueAfterUsefulLife = 5000m
        };

        // Tax book depreciates 99999 over 60 months
        Assert.Equal(99999m, taxDetail.DepreciableAmount);
        // Management book depreciates 95000 over 96 months
        Assert.Equal(95000m, mgmtDetail.DepreciableAmount);
    }

    // === SalesInvoice Loyalty Fields ===

    [Fact]
    public void SI_Loyalty_DefaultsZero()
    {
        var si = CreateSI();
        Assert.Equal(0, si.LoyaltyPointsRedeemed);
        Assert.Equal(0m, si.LoyaltyRedemptionAmount);
        Assert.Equal(0, si.LoyaltyPointsEarned);
        Assert.Null(si.LoyaltyProgramId);
    }

    [Fact]
    public void SI_Loyalty_PointsRedeemed_CanBeSet()
    {
        var si = CreateSI();
        si.LoyaltyPointsRedeemed = 500;
        si.LoyaltyRedemptionAmount = 50m; // 500 points × RM 0.10 per point
        Assert.Equal(500, si.LoyaltyPointsRedeemed);
        Assert.Equal(50m, si.LoyaltyRedemptionAmount);
    }

    [Fact]
    public void SI_Loyalty_EarnedPoints_SetOnSubmit()
    {
        var si = CreateSI();
        si.LoyaltyPointsEarned = 100; // Set by AppService after submit
        si.LoyaltyProgramId = Guid.NewGuid();
        Assert.Equal(100, si.LoyaltyPointsEarned);
        Assert.NotNull(si.LoyaltyProgramId);
    }

    [Fact]
    public void SI_Loyalty_RedemptionReducesEffectivePayable()
    {
        var si = CreateSI();
        si.AddItem(Guid.NewGuid(), "Widget", 10, 100m, 60m); // 1000 net + 60 tax
        Assert.Equal(1060m, si.GrandTotal);

        si.LoyaltyRedemptionAmount = 50m;

        var effectivePayable = si.GrandTotal - si.LoyaltyRedemptionAmount;
        Assert.Equal(1010m, effectivePayable);
        Assert.Equal(50m, si.LoyaltyRedemptionAmount);
    }

    // === Integration: PE Tax + Direction ===

    [Fact]
    public void PE_Tax_PayAdd_IsDebit()
    {
        var tax = new PaymentEntryTax(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid())
        {
            AddDeductTax = TaxAddDeduct.Add
        };
        // Pay+Add → debit (tax paid on behalf)
        Assert.True(tax.IsDebit("Pay"));
    }

    [Fact]
    public void PE_Tax_ReceiveDeduct_IsDebit()
    {
        var tax = new PaymentEntryTax(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid())
        {
            AddDeductTax = TaxAddDeduct.Deduct
        };
        // Receive+Deduct → debit (TDS withheld from customer)
        Assert.True(tax.IsDebit("Receive"));
    }

    // === Helpers ===

    private static PaymentEntry CreatePE(decimal amount = 1000m)
    {
        return new PaymentEntry(
            Guid.NewGuid(), Guid.NewGuid(), PaymentType.Receive, DateTime.Today,
            amount, Guid.NewGuid(), Guid.NewGuid());
    }

    private static SalesInvoice CreateSI()
    {
        return new SalesInvoice(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "MYR", DateTime.Today);
    }
}
