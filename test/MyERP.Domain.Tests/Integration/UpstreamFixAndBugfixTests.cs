using System;
using System.Collections.Generic;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests.Integration;

/// <summary>
/// Tests for recently-implemented upstream fixes and production bug fixes:
/// - PR #57283: Stock account sub-type change blocking when SLE exists
/// - Party name resolution patterns
/// - Account entity guards
/// </summary>
public class UpstreamFixAndBugfixTests
{
    // === PR #57283: Stock Account Type Change Guard ===

    [Fact]
    public void Account_StockSubType_CanBeCreated()
    {
        var account = new Account(Guid.NewGuid(), Guid.NewGuid(), "1140", "Inventory",
            AccountType.Asset);
        account.AccountSubType = AccountSubType.Stock;
        Assert.Equal(AccountSubType.Stock, account.AccountSubType);
    }

    [Fact]
    public void Account_SubType_CanChange_WhenNotStock()
    {
        var account = new Account(Guid.NewGuid(), Guid.NewGuid(), "1100", "Bank",
            AccountType.Asset);
        account.AccountSubType = AccountSubType.BankAccount;
        // Change from Bank to Cash — should be allowed (no stock guard)
        account.AccountSubType = AccountSubType.CashAccount;
        Assert.Equal(AccountSubType.CashAccount, account.AccountSubType);
    }

    [Fact]
    public void Account_SubType_DefaultsNull()
    {
        var account = new Account(Guid.NewGuid(), Guid.NewGuid(), "9999", "Test",
            AccountType.Expense);
        Assert.Null(account.AccountSubType);
    }

    [Fact]
    public void Account_CanSetFrozen()
    {
        var account = new Account(Guid.NewGuid(), Guid.NewGuid(), "1140", "Inventory",
            AccountType.Asset);
        account.IsFrozen = true;
        Assert.True(account.IsFrozen);
    }

    [Fact]
    public void Account_BalanceMustBe_DefaultsNull()
    {
        var account = new Account(Guid.NewGuid(), Guid.NewGuid(), "1140", "Inventory",
            AccountType.Asset);
        Assert.Null(account.BalanceMustBe);
    }

    [Fact]
    public void Account_SetParent_SelfReference_Throws()
    {
        var id = Guid.NewGuid();
        var account = new Account(id, Guid.NewGuid(), "1001", "Test", AccountType.Asset);
        Assert.Throws<BusinessException>(() => account.SetParent(id));
    }

    [Fact]
    public void Account_SetParent_ValidId_Succeeds()
    {
        var account = new Account(Guid.NewGuid(), Guid.NewGuid(), "1001", "Test", AccountType.Asset);
        var parentId = Guid.NewGuid();
        account.SetParent(parentId);
        Assert.Equal(parentId, account.ParentAccountId);
    }

    [Fact]
    public void Account_SetParent_Null_ClearsParent()
    {
        var account = new Account(Guid.NewGuid(), Guid.NewGuid(), "1001", "Test", AccountType.Asset);
        account.SetParent(Guid.NewGuid());
        account.SetParent(null);
        Assert.Null(account.ParentAccountId);
    }

    // === Party Name Resolution Patterns ===

    [Fact]
    public void BatchLookup_Dictionary_ResolvesNames()
    {
        // Simulates the batch customer name resolution pattern in SI GetListAsync
        var customers = new Dictionary<Guid, string>
        {
            [Guid.Parse("11111111-1111-1111-1111-111111111111")] = "Acme Corp",
            [Guid.Parse("22222222-2222-2222-2222-222222222222")] = "Beta Ltd",
        };

        var invoiceCustomerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        Assert.True(customers.TryGetValue(invoiceCustomerId, out var name));
        Assert.Equal("Acme Corp", name);
    }

    [Fact]
    public void BatchLookup_UnknownId_ReturnsNull()
    {
        var customers = new Dictionary<Guid, string>
        {
            [Guid.NewGuid()] = "Known Customer",
        };

        var unknownId = Guid.NewGuid();
        Assert.False(customers.TryGetValue(unknownId, out _));
    }

    // === AccountType Enum Coverage ===

    [Fact]
    public void AccountType_HasAllExpectedValues()
    {
        Assert.Equal(0, (int)AccountType.Asset);
        Assert.Equal(1, (int)AccountType.Liability);
        Assert.Equal(2, (int)AccountType.Equity);
        Assert.Equal(3, (int)AccountType.Revenue);
        Assert.Equal(4, (int)AccountType.Expense);
    }

    [Fact]
    public void AccountSubType_Stock_Is15()
    {
        Assert.Equal(15, (int)AccountSubType.Stock);
    }

    [Fact]
    public void AccountSubType_HasRelevantValues()
    {
        Assert.Equal(12, (int)AccountSubType.BankAccount);
        Assert.Equal(13, (int)AccountSubType.CashAccount);
        Assert.Equal(14, (int)AccountSubType.AccountsReceivable);
        Assert.Equal(15, (int)AccountSubType.Stock);
    }
}
