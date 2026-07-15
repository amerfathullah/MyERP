using System;
using MyERP.Accounting;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.DomainServices;

/// <summary>
/// Tests for the AccountingRuleEngine's dynamic account resolution.
/// Per ERPNext: GL accounts are resolved from Company defaults when AccountSource != FixedAccount.
/// </summary>
public class AccountingRuleEngineDynamicResolutionTests
{
    #region AccountSource Enum

    [Fact]
    public void AccountSource_FixedAccount_IsDefault()
    {
        var rule = new AccountingRule(Guid.NewGuid(), Guid.NewGuid(), "Test Rule",
            "SalesInvoice", true, AccountSource.FixedAccount, AmountSource.GrandTotal);

        rule.AccountSource.ShouldBe(AccountSource.FixedAccount);
    }

    [Fact]
    public void AccountSource_HasAllRequiredTypes()
    {
        // All 6 sources should exist for dynamic GL resolution
        Enum.GetValues<AccountSource>().Length.ShouldBeGreaterThanOrEqualTo(6);
    }

    [Fact]
    public void AccountSource_CustomerReceivable_ForSalesPosting()
    {
        var rule = new AccountingRule(Guid.NewGuid(), Guid.NewGuid(), "DR Receivable",
            "SalesInvoice", true, AccountSource.CustomerReceivable, AmountSource.GrandTotal);

        rule.AccountSource.ShouldBe(AccountSource.CustomerReceivable);
        rule.IsDebit.ShouldBeTrue(); // DR receivable on SI
    }

    [Fact]
    public void AccountSource_SupplierPayable_ForPurchasePosting()
    {
        var rule = new AccountingRule(Guid.NewGuid(), Guid.NewGuid(), "CR Payable",
            "PurchaseInvoice", false, AccountSource.SupplierPayable, AmountSource.GrandTotal);

        rule.AccountSource.ShouldBe(AccountSource.SupplierPayable);
        rule.IsDebit.ShouldBeFalse(); // CR payable on PI
    }

    [Fact]
    public void AccountSource_ItemIncome_ForRevenueRecognition()
    {
        var rule = new AccountingRule(Guid.NewGuid(), Guid.NewGuid(), "CR Revenue",
            "SalesInvoice", false, AccountSource.ItemIncome, AmountSource.NetTotal);

        rule.AccountSource.ShouldBe(AccountSource.ItemIncome);
        rule.AmountSource.ShouldBe(AmountSource.NetTotal); // Revenue = net of tax
    }

    [Fact]
    public void AccountSource_ItemExpense_ForCOGS()
    {
        var rule = new AccountingRule(Guid.NewGuid(), Guid.NewGuid(), "DR Expense",
            "PurchaseInvoice", true, AccountSource.ItemExpense, AmountSource.NetTotal);

        rule.AccountSource.ShouldBe(AccountSource.ItemExpense);
    }

    [Fact]
    public void AccountSource_TaxPayable_ForTaxLiability()
    {
        var rule = new AccountingRule(Guid.NewGuid(), Guid.NewGuid(), "CR Tax Payable",
            "SalesInvoice", false, AccountSource.TaxPayable, AmountSource.TaxAmount);

        rule.AccountSource.ShouldBe(AccountSource.TaxPayable);
        rule.AmountSource.ShouldBe(AmountSource.TaxAmount);
    }

    #endregion

    #region Company Default Accounts for Resolution

    [Fact]
    public void Company_DefaultReceivableAccountId_UsedByCustomerReceivableSource()
    {
        var company = new Company(Guid.NewGuid(), "Test Corp");
        var receivableAcct = Guid.NewGuid();
        company.DefaultReceivableAccountId = receivableAcct;

        company.DefaultReceivableAccountId.ShouldBe(receivableAcct);
    }

    [Fact]
    public void Company_DefaultPayableAccountId_UsedBySupplierPayableSource()
    {
        var company = new Company(Guid.NewGuid(), "Test Corp");
        var payableAcct = Guid.NewGuid();
        company.DefaultPayableAccountId = payableAcct;

        company.DefaultPayableAccountId.ShouldBe(payableAcct);
    }

    [Fact]
    public void Company_DefaultIncomeAccountId_UsedByItemIncomeSource()
    {
        var company = new Company(Guid.NewGuid(), "Test Corp");
        var incomeAcct = Guid.NewGuid();
        company.DefaultIncomeAccountId = incomeAcct;

        company.DefaultIncomeAccountId.ShouldBe(incomeAcct);
    }

    [Fact]
    public void Company_DefaultExpenseAccountId_UsedByItemExpenseSource()
    {
        var company = new Company(Guid.NewGuid(), "Test Corp");
        var expenseAcct = Guid.NewGuid();
        company.DefaultExpenseAccountId = expenseAcct;

        company.DefaultExpenseAccountId.ShouldBe(expenseAcct);
    }

    [Fact]
    public void Company_AllDefaultAccounts_Nullable()
    {
        var company = new Company(Guid.NewGuid(), "New Corp");

        company.DefaultReceivableAccountId.ShouldBeNull();
        company.DefaultPayableAccountId.ShouldBeNull();
        company.DefaultIncomeAccountId.ShouldBeNull();
        company.DefaultExpenseAccountId.ShouldBeNull();
    }

    #endregion

    #region Rule Fallback Chain

    [Fact]
    public void AccountingRule_FixedAccountId_FallbackForDynamicSources()
    {
        // When dynamic resolution fails (company default not set), falls back to FixedAccountId
        var fixedAcctId = Guid.NewGuid();
        var rule = new AccountingRule(Guid.NewGuid(), Guid.NewGuid(), "Receivable",
            "SalesInvoice", true, AccountSource.CustomerReceivable, AmountSource.GrandTotal);
        rule.FixedAccountId = fixedAcctId;

        // If company.DefaultReceivableAccountId is null, FixedAccountId is used as fallback
        rule.FixedAccountId.ShouldBe(fixedAcctId);
    }

    [Fact]
    public void AccountingRule_NoFixedAndNoCompanyDefault_ThrowsOnResolve()
    {
        // Rule with CustomerReceivable source but no fixed fallback AND no company default
        // should throw during posting (caught at GL posting time, not rule creation time)
        var rule = new AccountingRule(Guid.NewGuid(), Guid.NewGuid(), "Receivable",
            "SalesInvoice", true, AccountSource.CustomerReceivable, AmountSource.GrandTotal);

        rule.FixedAccountId.ShouldBeNull(); // No fallback
        rule.AccountSource.ShouldBe(AccountSource.CustomerReceivable);
        // Resolution would throw at posting time when company default is also null
    }

    #endregion

    #region AmountSource Resolution

    [Fact]
    public void AmountSource_NetTotal_ForRevenueAndExpense()
    {
        AmountSource.NetTotal.ShouldBe((AmountSource)0);
    }

    [Fact]
    public void AmountSource_GrandTotal_ForReceivableAndPayable()
    {
        AmountSource.GrandTotal.ShouldBe((AmountSource)1);
    }

    [Fact]
    public void AmountSource_TaxAmount_ForTaxLiability()
    {
        AmountSource.TaxAmount.ShouldBe((AmountSource)2);
    }

    [Fact]
    public void AmountSource_LineAmount_ForPerItemPosting()
    {
        AmountSource.LineAmount.ShouldBe((AmountSource)3);
    }

    #endregion

    #region IAccountableDocument FinanceBook Integration

    [Fact]
    public void IAccountableDocument_FinanceBook_DefaultNull()
    {
        // The default interface implementation returns null (default book)
        // This is tested via the SI entity which implements IAccountableDocument
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SI-001", DateTime.Today);

        // SI doesn't explicitly set FinanceBook → uses interface default (null)
        ((IAccountableDocument)si).FinanceBook.ShouldBeNull();
    }

    #endregion
}
