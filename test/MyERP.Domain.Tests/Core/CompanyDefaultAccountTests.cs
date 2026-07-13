using System;
using MyERP.Core.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Core;

public class CompanyDefaultAccountTests
{
    private static Company CreateCompany() => new(Guid.NewGuid(), "Test Company");

    [Fact]
    public void DefaultReceivableAccountId_Defaults_ToNull()
    {
        var company = CreateCompany();
        company.DefaultReceivableAccountId.ShouldBeNull();
    }

    [Fact]
    public void DefaultPayableAccountId_Defaults_ToNull()
    {
        var company = CreateCompany();
        company.DefaultPayableAccountId.ShouldBeNull();
    }

    [Fact]
    public void DefaultIncomeAccountId_CanBeSet()
    {
        var company = CreateCompany();
        var accountId = Guid.NewGuid();
        company.DefaultIncomeAccountId = accountId;
        company.DefaultIncomeAccountId.ShouldBe(accountId);
    }

    [Fact]
    public void DefaultExpenseAccountId_CanBeSet()
    {
        var company = CreateCompany();
        var accountId = Guid.NewGuid();
        company.DefaultExpenseAccountId = accountId;
        company.DefaultExpenseAccountId.ShouldBe(accountId);
    }

    [Fact]
    public void DefaultBankAccountId_CanBeSet()
    {
        var company = CreateCompany();
        var accountId = Guid.NewGuid();
        company.DefaultBankAccountId = accountId;
        company.DefaultBankAccountId.ShouldBe(accountId);
    }

    [Fact]
    public void DefaultInventoryAccountId_CanBeSet()
    {
        var company = CreateCompany();
        var accountId = Guid.NewGuid();
        company.DefaultInventoryAccountId = accountId;
        company.DefaultInventoryAccountId.ShouldBe(accountId);
    }

    [Fact]
    public void DepreciationExpenseAccountId_CanBeSet()
    {
        var company = CreateCompany();
        var accountId = Guid.NewGuid();
        company.DepreciationExpenseAccountId = accountId;
        company.DepreciationExpenseAccountId.ShouldBe(accountId);
    }

    [Fact]
    public void AccumulatedDepreciationAccountId_CanBeSet()
    {
        var company = CreateCompany();
        var accountId = Guid.NewGuid();
        company.AccumulatedDepreciationAccountId = accountId;
        company.AccumulatedDepreciationAccountId.ShouldBe(accountId);
    }

    [Fact]
    public void AllDefaultAccounts_AreNullable()
    {
        var company = CreateCompany();
        company.DefaultReceivableAccountId.ShouldBeNull();
        company.DefaultPayableAccountId.ShouldBeNull();
        company.DefaultIncomeAccountId.ShouldBeNull();
        company.DefaultExpenseAccountId.ShouldBeNull();
        company.DefaultBankAccountId.ShouldBeNull();
        company.DefaultInventoryAccountId.ShouldBeNull();
        company.DepreciationExpenseAccountId.ShouldBeNull();
        company.AccumulatedDepreciationAccountId.ShouldBeNull();
    }

    [Fact]
    public void FallbackPattern_UsesCompanyId_WhenNoDefault()
    {
        var company = CreateCompany();
        // Pattern used in AppServices: company.DefaultReceivableAccountId ?? fallback
        var receivable = company.DefaultReceivableAccountId ?? company.Id;
        receivable.ShouldBe(company.Id); // Falls back to company ID when null
    }

    [Fact]
    public void FallbackPattern_UsesAccount_WhenSet()
    {
        var company = CreateCompany();
        var receivableId = Guid.NewGuid();
        company.DefaultReceivableAccountId = receivableId;
        var resolved = company.DefaultReceivableAccountId ?? company.Id;
        resolved.ShouldBe(receivableId); // Uses the actual account
    }
}
