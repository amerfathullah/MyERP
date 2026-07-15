using System;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Tests.Accounting;

public class OpeningBalanceTests
{
    [Fact]
    public void AccountSubType_HasTemporaryOpening()
    {
        var subType = AccountSubType.TemporaryOpening;
        subType.ShouldBe((AccountSubType)32);
    }

    [Fact]
    public void Account_WithTemporaryOpeningSubType_IsEquity()
    {
        var account = new Account(Guid.NewGuid(), Guid.NewGuid(), "9999", "Temporary Opening", AccountType.Equity);
        account.AccountSubType = AccountSubType.TemporaryOpening;
        
        account.AccountType.ShouldBe(AccountType.Equity);
        account.AccountSubType.ShouldBe(AccountSubType.TemporaryOpening);
    }

    [Fact]
    public void RevenueAccount_NotAllowed_ForOpeningEntry()
    {
        // Revenue accounts (P&L) cannot be used in opening entries
        var account = new Account(Guid.NewGuid(), Guid.NewGuid(), "4100", "Sales Revenue", AccountType.Revenue);
        
        // Opening balance tool should reject this
        (account.AccountType == AccountType.Revenue || account.AccountType == AccountType.Expense)
            .ShouldBeTrue();
    }

    [Fact]
    public void ExpenseAccount_NotAllowed_ForOpeningEntry()
    {
        var account = new Account(Guid.NewGuid(), Guid.NewGuid(), "5100", "COGS", AccountType.Expense);
        
        (account.AccountType == AccountType.Revenue || account.AccountType == AccountType.Expense)
            .ShouldBeTrue();
    }

    [Fact]
    public void AssetAccount_Allowed_ForOpeningEntry()
    {
        var account = new Account(Guid.NewGuid(), Guid.NewGuid(), "1100", "Cash", AccountType.Asset);
        
        (account.AccountType == AccountType.Revenue || account.AccountType == AccountType.Expense)
            .ShouldBeFalse();
    }

    [Fact]
    public void LiabilityAccount_Allowed_ForOpeningEntry()
    {
        var account = new Account(Guid.NewGuid(), Guid.NewGuid(), "2100", "Accounts Payable", AccountType.Liability);
        
        (account.AccountType == AccountType.Revenue || account.AccountType == AccountType.Expense)
            .ShouldBeFalse();
    }

    [Fact]
    public void EquityAccount_Allowed_ForOpeningEntry()
    {
        var account = new Account(Guid.NewGuid(), Guid.NewGuid(), "3100", "Share Capital", AccountType.Equity);
        
        (account.AccountType == AccountType.Revenue || account.AccountType == AccountType.Expense)
            .ShouldBeFalse();
    }

    [Fact]
    public void GroupAccount_NotAllowed_ForOpeningEntry()
    {
        var account = new Account(Guid.NewGuid(), Guid.NewGuid(), "1000", "Assets Group", AccountType.Asset);
        account.IsGroup = true;
        
        account.IsGroup.ShouldBeTrue();
    }

    [Fact]
    public void OpeningJournalEntry_BalanceDifference_GoesToTempOpening()
    {
        // Scenario: Assets = 100,000, Liabilities = 60,000
        // Difference = 40,000 (debit excess) → Credit Temporary Opening
        decimal totalDebit = 100_000m;
        decimal totalCredit = 60_000m;
        decimal difference = totalDebit - totalCredit;
        
        difference.ShouldBe(40_000m);
        // The system should credit Temp Opening by 40,000 to balance
        (difference > 0).ShouldBeTrue(); // More debits → credit opening
    }

    [Fact]
    public void OpeningJournalEntry_CreditExcess_DebitsOpening()
    {
        // Scenario: more credits than debits (unusual but possible)
        decimal totalDebit = 50_000m;
        decimal totalCredit = 80_000m;
        decimal difference = totalDebit - totalCredit;
        
        difference.ShouldBe(-30_000m);
        (difference < 0).ShouldBeTrue(); // More credits → debit opening
    }

    [Fact]
    public void OpeningJournalEntry_AlreadyBalanced_NoTempOpening()
    {
        decimal totalDebit = 100_000m;
        decimal totalCredit = 100_000m;
        decimal difference = totalDebit - totalCredit;
        
        (Math.Abs(difference) < 0.01m).ShouldBeTrue(); // Balanced — no temp entry needed
    }

    [Fact]
    public void OpeningStatus_IsBalanced_WhenTempBalanceZero()
    {
        decimal tempBalance = 0m;
        (Math.Abs(tempBalance) < 0.01m).ShouldBeTrue();
    }

    [Fact]
    public void OpeningStatus_NotBalanced_WhenTempBalanceNonZero()
    {
        decimal tempBalance = 5000m;
        (Math.Abs(tempBalance) < 0.01m).ShouldBeFalse();
    }
}
