using System;
using MyERP.Accounting.Entities;
using MyERP.Assets.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Accounting;

public class MultiCurrencyPostingTests
{
    #region IAccountableDocument Multi-Currency Fields

    [Fact]
    public void DeliveryNote_ExchangeRate_DefaultsToOne()
    {
        var dn = new MyERP.Sales.Entities.DeliveryNote(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "DN-001", DateTime.Today);
        dn.ExchangeRate.ShouldBe(1m);
    }

    [Fact]
    public void DeliveryNote_ExchangeRate_CanBeSet()
    {
        var dn = new MyERP.Sales.Entities.DeliveryNote(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "DN-001", DateTime.Today);
        dn.ExchangeRate = 4.72m;
        dn.ExchangeRate.ShouldBe(4.72m);
    }

    [Fact]
    public void PurchaseReceipt_ExchangeRate_DefaultsToOne()
    {
        var pr = new MyERP.Purchasing.Entities.PurchaseReceipt(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "PR-001", DateTime.Today);
        pr.ExchangeRate.ShouldBe(1m);
    }

    [Fact]
    public void PurchaseReceipt_ExchangeRate_CanBeSet()
    {
        var pr = new MyERP.Purchasing.Entities.PurchaseReceipt(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "PR-001", DateTime.Today);
        pr.ExchangeRate = 0.24m;
        pr.ExchangeRate.ShouldBe(0.24m);
    }

    [Fact]
    public void StockEntry_CurrencyCode_DefaultsMYR()
    {
        var se = new MyERP.Inventory.Entities.StockEntry(
            Guid.NewGuid(), Guid.NewGuid(), MyERP.Inventory.StockEntryType.MaterialReceipt, DateTime.Today);
        se.CurrencyCode.ShouldBe("MYR");
    }

    [Fact]
    public void StockEntry_ExchangeRate_DefaultsToOne()
    {
        var se = new MyERP.Inventory.Entities.StockEntry(
            Guid.NewGuid(), Guid.NewGuid(), MyERP.Inventory.StockEntryType.MaterialReceipt, DateTime.Today);
        se.ExchangeRate.ShouldBe(1m);
    }

    #endregion

    #region Multi-Currency JournalEntry Line Creation

    [Fact]
    public void MultiCurrency_JournalEntryLine_CompanyAndAccountCurrencyDiffer()
    {
        // USD invoice for MYR company at 4.72 rate
        var line = new JournalEntryLine(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            amountInCompanyCurrency: 4720m, // MYR
            isDebit: true,
            accountCurrency: "USD",
            amountInAccountCurrency: 1000m, // USD
            exchangeRate: 4.72m);

        line.Amount.ShouldBe(4720m); // Company currency (MYR)
        line.AmountInAccountCurrency.ShouldBe(1000m); // Account currency (USD)
        line.ExchangeRate.ShouldBe(4.72m);
        line.AccountCurrency.ShouldBe("USD");
    }

    [Fact]
    public void SameCurrency_JournalEntryLine_AmountsEqual()
    {
        var line = new JournalEntryLine(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            500m, true, "MYR", 500m, 1m);

        line.Amount.ShouldBe(500m);
        line.AmountInAccountCurrency.ShouldBe(500m);
        line.ExchangeRate.ShouldBe(1m);
    }

    [Fact]
    public void JournalEntry_MultiCurrency_StillBalances()
    {
        // An invoice in USD at rate 4.72: Grand Total = 1000 USD = 4720 MYR
        var je = new JournalEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.Today);

        je.AddLine(Guid.NewGuid(), 4720m, true); // DR Receivable (MYR)
        je.AddLine(Guid.NewGuid(), 4720m, false); // CR Revenue (MYR)

        // Balance check is ALWAYS in company currency
        je.Validate(); // Should not throw
        je.TotalDebit.ShouldBe(4720m);
        je.TotalCredit.ShouldBe(4720m);
    }

    #endregion

    #region FinanceBook Entity

    [Fact]
    public void FinanceBook_Create_SetsNameAndCompany()
    {
        var companyId = Guid.NewGuid();
        var book = new FinanceBook(Guid.NewGuid(), companyId, "Tax Depreciation");

        book.Name.ShouldBe("Tax Depreciation");
        book.CompanyId.ShouldBe(companyId);
        book.IsDefault.ShouldBeFalse();
        book.Description.ShouldBeNull();
    }

    [Fact]
    public void FinanceBook_IsDefault_CanBeSet()
    {
        var book = new FinanceBook(Guid.NewGuid(), Guid.NewGuid(), "Primary");
        book.IsDefault = true;
        book.IsDefault.ShouldBeTrue();
    }

    [Fact]
    public void FinanceBook_Description_CanBeSet()
    {
        var book = new FinanceBook(Guid.NewGuid(), Guid.NewGuid(), "IFRS Book");
        book.Description = "For international reporting standards";
        book.Description.ShouldBe("For international reporting standards");
    }

    [Fact]
    public void FinanceBook_NullName_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new FinanceBook(Guid.NewGuid(), Guid.NewGuid(), null!));
    }

    #endregion

    #region DepreciationScheduleEntry — FinanceBookId

    [Fact]
    public void DepreciationScheduleEntry_FinanceBookId_DefaultNull()
    {
        var entry = new DepreciationScheduleEntry(
            Guid.NewGuid(), Guid.NewGuid(), DateTime.Today, 1000m, 5000m);
        entry.FinanceBookId.ShouldBeNull();
    }

    [Fact]
    public void DepreciationScheduleEntry_FinanceBookId_CanBeSet()
    {
        var bookId = Guid.NewGuid();
        var entry = new DepreciationScheduleEntry(
            Guid.NewGuid(), Guid.NewGuid(), DateTime.Today, 1000m, 5000m);
        entry.FinanceBookId = bookId;
        entry.FinanceBookId.ShouldBe(bookId);
    }

    [Fact]
    public void DepreciationScheduleEntry_DifferentBooksCanHaveDifferentAmounts()
    {
        var assetId = Guid.NewGuid();
        var taxBookId = Guid.NewGuid();
        var mgmtBookId = Guid.NewGuid();

        // Tax book: accelerated depreciation (higher amount)
        var taxEntry = new DepreciationScheduleEntry(
            Guid.NewGuid(), assetId, DateTime.Today, 5000m, 5000m);
        taxEntry.FinanceBookId = taxBookId;

        // Management book: straight-line (lower amount)
        var mgmtEntry = new DepreciationScheduleEntry(
            Guid.NewGuid(), assetId, DateTime.Today, 2500m, 2500m);
        mgmtEntry.FinanceBookId = mgmtBookId;

        taxEntry.DepreciationAmount.ShouldBeGreaterThan(mgmtEntry.DepreciationAmount);
        taxEntry.FinanceBookId.ShouldNotBe(mgmtEntry.FinanceBookId);
    }

    #endregion
}
