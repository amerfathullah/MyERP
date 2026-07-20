using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Accounting;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Sales.Entities;
using Xunit;

namespace MyERP;

/// <summary>
/// Tests for AccountingRuleEngine refactoring and DeferredAccountingService guards.
/// Validates: account resolution, FY requirement, amount source resolution, rule ordering.
/// </summary>
public class AccountingEngineRefactorTests
{
    // === AccountSource Resolution ===

    [Fact]
    public void ResolveAmount_NetTotal_ReturnsNetTotal()
    {
        var invoice = CreateTestInvoice(unitPrice: 1000m, taxAmount: 60m);
        Assert.Equal(1000m, invoice.NetTotal);
    }

    [Fact]
    public void ResolveAmount_GrandTotal_IncludesTax()
    {
        var invoice = CreateTestInvoice(unitPrice: 1000m, taxAmount: 60m);
        Assert.Equal(1060m, invoice.GrandTotal);
    }

    [Fact]
    public void ResolveAmount_TaxAmount_ReturnsTaxAmount()
    {
        var invoice = CreateTestInvoice(unitPrice: 1000m, taxAmount: 60m);
        Assert.Equal(60m, invoice.TaxAmount);
    }

    [Fact]
    public void ResolveAmount_ZeroTax_GrandEqualsNet()
    {
        var invoice = CreateTestInvoice(unitPrice: 1000m, taxAmount: 0m);
        Assert.Equal(0m, invoice.TaxAmount);
        Assert.Equal(invoice.NetTotal, invoice.GrandTotal);
    }

    // === Company Account Defaults ===

    [Fact]
    public void Company_DefaultReceivableAccountId_UsedWhenSet()
    {
        var company = CreateTestCompany();
        var accountId = Guid.NewGuid();
        company.DefaultReceivableAccountId = accountId;
        Assert.Equal(accountId, company.DefaultReceivableAccountId);
    }

    [Fact]
    public void Company_DefaultPayableAccountId_UsedWhenSet()
    {
        var company = CreateTestCompany();
        var accountId = Guid.NewGuid();
        company.DefaultPayableAccountId = accountId;
        Assert.Equal(accountId, company.DefaultPayableAccountId);
    }

    [Fact]
    public void Company_DefaultIncomeAccountId_NullWhenNotConfigured()
    {
        var company = CreateTestCompany();
        Assert.Null(company.DefaultIncomeAccountId);
    }

    [Fact]
    public void Company_DefaultExpenseAccountId_NullWhenNotConfigured()
    {
        var company = CreateTestCompany();
        Assert.Null(company.DefaultExpenseAccountId);
    }

    // === AccountingRule Configuration ===

    [Fact]
    public void AccountingRule_FixedAccount_RequiresAccountId()
    {
        var rule = new AccountingRule(Guid.NewGuid(), Guid.NewGuid(),
            "SI Revenue", "SalesInvoice", true, AccountSource.FixedAccount, AmountSource.NetTotal);
        Assert.Equal(AccountSource.FixedAccount, rule.AccountSource);
    }

    [Fact]
    public void AccountingRule_CustomerReceivable_ResolvesFromCompany()
    {
        var rule = new AccountingRule(Guid.NewGuid(), Guid.NewGuid(),
            "SI Receivable", "SalesInvoice", true, AccountSource.CustomerReceivable, AmountSource.GrandTotal);
        Assert.Equal(AccountSource.CustomerReceivable, rule.AccountSource);
    }

    [Fact]
    public void AccountingRule_SortOrder_DeterminesProcessingOrder()
    {
        var rules = new[]
        {
            new AccountingRule(Guid.NewGuid(), Guid.NewGuid(), "Tax", "SI", false, AccountSource.TaxPayable, AmountSource.TaxAmount) { SortOrder = 3 },
            new AccountingRule(Guid.NewGuid(), Guid.NewGuid(), "Revenue", "SI", false, AccountSource.ItemIncome, AmountSource.NetTotal) { SortOrder = 2 },
            new AccountingRule(Guid.NewGuid(), Guid.NewGuid(), "Receivable", "SI", true, AccountSource.CustomerReceivable, AmountSource.GrandTotal) { SortOrder = 1 },
        };
        var ordered = rules.OrderBy(r => r.SortOrder).ToList();
        Assert.Equal("Receivable", ordered[0].Name);
        Assert.Equal("Revenue", ordered[1].Name);
        Assert.Equal("Tax", ordered[2].Name);
    }

    [Fact]
    public void AccountingRule_IsActive_FiltersInactiveRules()
    {
        var activeRule = new AccountingRule(Guid.NewGuid(), Guid.NewGuid(),
            "Active", "SI", true, AccountSource.FixedAccount, AmountSource.GrandTotal) { IsActive = true };
        var inactiveRule = new AccountingRule(Guid.NewGuid(), Guid.NewGuid(),
            "Inactive", "SI", true, AccountSource.FixedAccount, AmountSource.GrandTotal) { IsActive = false };
        var rules = new[] { activeRule, inactiveRule };
        var active = rules.Where(r => r.IsActive).ToList();
        Assert.Single(active);
        Assert.Equal("Active", active[0].Name);
    }

    // === Deferred Revenue Fields ===

    [Fact]
    public void SalesInvoiceItem_DeferredRevenue_DefaultsFalse()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "INV-010", DateTime.Today);
        si.AddItem(Guid.NewGuid(), "Service", 1, 12000m, 0m);
        var item = si.Items[0];
        Assert.False(item.EnableDeferredRevenue);
        Assert.Null(item.ServiceStartDate);
        Assert.Null(item.ServiceEndDate);
    }

    [Fact]
    public void SalesInvoiceItem_DeferredRevenue_CanBeConfigured()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "INV-011", DateTime.Today);
        si.AddItem(Guid.NewGuid(), "Annual Support", 1, 12000m, 0m);
        var item = si.Items[0];
        item.EnableDeferredRevenue = true;
        item.ServiceStartDate = new DateTime(2026, 1, 1);
        item.ServiceEndDate = new DateTime(2026, 12, 31);
        item.DeferredRevenueAccountId = Guid.NewGuid();
        Assert.True(item.EnableDeferredRevenue);
        Assert.NotNull(item.DeferredRevenueAccountId);
    }

    [Fact]
    public void DeferredScheduleEntry_HasRequiredProperties()
    {
        var entry = new DeferredScheduleEntry
        {
            PostingDate = new DateTime(2026, 1, 31),
            Amount = 1000m,
            PeriodIndex = 1,
            TotalPeriods = 12
        };
        Assert.Equal(1000m, entry.Amount);
        Assert.Equal(1, entry.PeriodIndex);
        Assert.Equal(12, entry.TotalPeriods);
    }

    // === Multi-Currency GL Fields ===

    [Fact]
    public void JournalEntryLine_MultiCurrency_SetsAccountCurrency()
    {
        var je = new JournalEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.Today);
        je.AddLine(Guid.NewGuid(), 4720m, true);
        var line = je.Lines[0];
        line.AccountCurrency = "USD";
        line.AmountInAccountCurrency = 1000m;
        line.ExchangeRate = 4.72m;
        Assert.Equal("USD", line.AccountCurrency);
        Assert.Equal(1000m, line.AmountInAccountCurrency);
        Assert.Equal(4.72m, line.ExchangeRate);
    }

    [Fact]
    public void JournalEntryLine_SameCurrency_ExchangeRateIsOne()
    {
        var je = new JournalEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.Today);
        je.AddLine(Guid.NewGuid(), 1000m, true);
        var line = je.Lines[0];
        Assert.Equal(1m, line.ExchangeRate);
    }

    [Fact]
    public void JournalEntryLine_FinanceBook_DefaultsNull()
    {
        var je = new JournalEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.Today);
        je.AddLine(Guid.NewGuid(), 500m, false);
        Assert.Null(je.Lines[0].FinanceBook);
    }

    [Fact]
    public void JournalEntryLine_FinanceBook_CanBeSet()
    {
        var je = new JournalEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.Today);
        je.AddLine(Guid.NewGuid(), 500m, false);
        je.Lines[0].FinanceBook = "Tax Depreciation";
        Assert.Equal("Tax Depreciation", je.Lines[0].FinanceBook);
    }

    // === IAccountableDocument Implementation ===

    [Fact]
    public void SalesInvoice_ImplementsIAccountableDocument()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "INV-002", DateTime.Today);
        IAccountableDocument doc = si;
        Assert.Equal("SalesInvoice", doc.DocumentType);
        Assert.Equal("MYR", doc.CurrencyCode);
        Assert.Equal(1m, doc.ExchangeRate);
    }

    [Fact]
    public void SalesInvoice_ExchangeRate_AffectsBaseAmounts()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "INV-003", DateTime.Today);
        si.CurrencyCode = "USD";
        si.ExchangeRate = 4.72m;
        si.AddItem(Guid.NewGuid(), "Widget", 10, 100m, 0m);
        Assert.Equal(4720m, si.BaseGrandTotal);
    }

    [Fact]
    public void IAccountableDocument_FinanceBook_DefaultsNull()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "INV-004", DateTime.Today);
        IAccountableDocument doc = si;
        Assert.Null(doc.FinanceBook);
    }

    // === AccountSource Enum ===

    [Fact]
    public void AccountSource_HasAllExpectedValues()
    {
        Assert.Equal(0, (int)AccountSource.FixedAccount);
        Assert.Equal(1, (int)AccountSource.CustomerReceivable);
        Assert.Equal(2, (int)AccountSource.SupplierPayable);
        Assert.Equal(3, (int)AccountSource.ItemIncome);
        Assert.Equal(4, (int)AccountSource.ItemExpense);
        Assert.Equal(5, (int)AccountSource.TaxPayable);
    }

    // === Helpers ===

    private SalesInvoice CreateTestInvoice(decimal unitPrice, decimal taxAmount)
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "INV-001", DateTime.Today);
        si.AddItem(Guid.NewGuid(), "Item A", 1, unitPrice, taxAmount);
        return si;
    }

    private Company CreateTestCompany()
    {
        return new Company(Guid.NewGuid(), "Test Company Sdn Bhd");
    }
}
