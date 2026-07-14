using System;
using System.Linq;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests.Accounting;

public class BankTransactionRuleTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _accountId = Guid.NewGuid();

    private BankTransaction CreateTransaction(decimal amount, string description = "Test payment", string? refNo = null)
    {
        return new BankTransaction(Guid.NewGuid(), _companyId, Guid.NewGuid(),
            DateTime.Today, description, amount)
        {
            ReferenceNumber = refNo
        };
    }

    private BankTransactionRule CreateRule(string name = "Test Rule", int priority = 1)
    {
        return new BankTransactionRule(Guid.NewGuid(), _companyId, name, priority);
    }

    [Fact]
    public void Rule_DefaultState()
    {
        var rule = CreateRule();
        Assert.True(rule.IsEnabled);
        Assert.Equal(BankTransactionType.Any, rule.TransactionType);
        Assert.Equal(BankRuleClassifyAs.BankEntry, rule.ClassifyAs);
        Assert.Equal(BankEntryMode.SingleAccount, rule.BankEntryMode);
        Assert.Null(rule.MinAmount);
        Assert.Null(rule.MaxAmount);
        Assert.Empty(rule.Conditions);
        Assert.Empty(rule.Accounts);
    }

    [Fact]
    public void Rule_Matches_ContainsCondition()
    {
        var rule = CreateRule();
        rule.AddCondition(BankRuleMatchType.Contains, "salary");

        var tx = CreateTransaction(-5000m, "Monthly salary payment TRF");
        Assert.True(rule.Matches(tx));
    }

    [Fact]
    public void Rule_Matches_CaseInsensitive()
    {
        var rule = CreateRule();
        rule.AddCondition(BankRuleMatchType.Contains, "SALARY");

        var tx = CreateTransaction(-5000m, "Monthly salary payment");
        Assert.True(rule.Matches(tx));
    }

    [Fact]
    public void Rule_Matches_StartsWithCondition()
    {
        var rule = CreateRule();
        rule.AddCondition(BankRuleMatchType.StartsWith, "FPX");

        var txMatch = CreateTransaction(1000m, "FPX Payment from customer");
        var txNoMatch = CreateTransaction(1000m, "Payment via FPX");

        Assert.True(rule.Matches(txMatch));
        Assert.False(rule.Matches(txNoMatch));
    }

    [Fact]
    public void Rule_Matches_EndsWithCondition()
    {
        var rule = CreateRule();
        rule.AddCondition(BankRuleMatchType.EndsWith, "AUTO-DEBIT");

        var txMatch = CreateTransaction(-200m, "Insurance premium AUTO-DEBIT");
        var txNoMatch = CreateTransaction(-200m, "AUTO-DEBIT insurance charge paid");

        Assert.True(rule.Matches(txMatch));
        Assert.False(rule.Matches(txNoMatch));
    }

    [Fact]
    public void Rule_Matches_RegexCondition()
    {
        var rule = CreateRule();
        rule.AddCondition(BankRuleMatchType.Regex, @"INV-\d{4,}");

        var txMatch = CreateTransaction(5000m, "Payment for INV-20230001");
        var txNoMatch = CreateTransaction(5000m, "Payment for order 12345");

        Assert.True(rule.Matches(txMatch));
        Assert.False(rule.Matches(txNoMatch));
    }

    [Fact]
    public void Rule_Matches_OrLogic_AnyConditionSuffices()
    {
        var rule = CreateRule();
        rule.AddCondition(BankRuleMatchType.Contains, "salary");
        rule.AddCondition(BankRuleMatchType.Contains, "payroll");

        var txSalary = CreateTransaction(-5000m, "Monthly salary");
        var txPayroll = CreateTransaction(-5000m, "Payroll batch");
        var txNeither = CreateTransaction(-5000m, "Office rent");

        Assert.True(rule.Matches(txSalary));
        Assert.True(rule.Matches(txPayroll));
        Assert.False(rule.Matches(txNeither));
    }

    [Fact]
    public void Rule_NoConditions_NeverMatches()
    {
        var rule = CreateRule();
        // No conditions added

        var tx = CreateTransaction(1000m, "Any transaction");
        Assert.False(rule.Matches(tx));
    }

    [Fact]
    public void Rule_DisabledRule_NeverMatches()
    {
        var rule = CreateRule();
        rule.IsEnabled = false;
        rule.AddCondition(BankRuleMatchType.Contains, "salary");

        var tx = CreateTransaction(-5000m, "Monthly salary");
        Assert.False(rule.Matches(tx));
    }

    [Fact]
    public void Rule_TransactionType_WithdrawalFilter()
    {
        var rule = CreateRule();
        rule.TransactionType = BankTransactionType.Withdrawal;
        rule.AddCondition(BankRuleMatchType.Contains, "payment");

        var withdrawal = CreateTransaction(-1000m, "Vendor payment");
        var deposit = CreateTransaction(1000m, "Customer payment");

        Assert.True(rule.Matches(withdrawal));
        Assert.False(rule.Matches(deposit));
    }

    [Fact]
    public void Rule_TransactionType_DepositFilter()
    {
        var rule = CreateRule();
        rule.TransactionType = BankTransactionType.Deposit;
        rule.AddCondition(BankRuleMatchType.Contains, "payment");

        var withdrawal = CreateTransaction(-1000m, "Vendor payment");
        var deposit = CreateTransaction(1000m, "Customer payment");

        Assert.False(rule.Matches(withdrawal));
        Assert.True(rule.Matches(deposit));
    }

    [Fact]
    public void Rule_AmountRange_WithinRange()
    {
        var rule = CreateRule();
        rule.MinAmount = 100m;
        rule.MaxAmount = 5000m;
        rule.AddCondition(BankRuleMatchType.Contains, "transfer");

        var within = CreateTransaction(2000m, "Bank transfer");
        var below = CreateTransaction(50m, "Small transfer");
        var above = CreateTransaction(10000m, "Large transfer");

        Assert.True(rule.Matches(within));
        Assert.False(rule.Matches(below));
        Assert.False(rule.Matches(above));
    }

    [Fact]
    public void Rule_AmountRange_UsesAbsoluteValue()
    {
        var rule = CreateRule();
        rule.MinAmount = 100m;
        rule.MaxAmount = 5000m;
        rule.TransactionType = BankTransactionType.Any;
        rule.AddCondition(BankRuleMatchType.Contains, "payment");

        // Withdrawal: amount is negative, but abs value is within range
        var tx = CreateTransaction(-2000m, "Vendor payment");
        Assert.True(rule.Matches(tx));
    }

    [Fact]
    public void Rule_Validate_MinGreaterThanMax_Throws()
    {
        var rule = CreateRule();
        rule.MinAmount = 5000m;
        rule.MaxAmount = 1000m;

        var ex = Assert.Throws<BusinessException>(() => rule.Validate());
        Assert.Equal("MyERP:02014", ex.Code);
    }

    [Fact]
    public void Rule_Validate_PaymentEntry_RequiresPartyAndAccount()
    {
        var rule = CreateRule();
        rule.ClassifyAs = BankRuleClassifyAs.PaymentEntry;
        // Missing party type, party ID, and account

        var ex = Assert.Throws<BusinessException>(() => rule.Validate());
        Assert.Equal("MyERP:02015", ex.Code);
    }

    [Fact]
    public void Rule_Validate_PaymentEntry_WithAllFields_Succeeds()
    {
        var rule = CreateRule();
        rule.ClassifyAs = BankRuleClassifyAs.PaymentEntry;
        rule.PartyType = "Customer";
        rule.PartyId = Guid.NewGuid();
        rule.AccountId = _accountId;

        rule.Validate(); // Should not throw
    }

    [Fact]
    public void Rule_Validate_SingleAccount_RequiresAccount()
    {
        var rule = CreateRule();
        rule.ClassifyAs = BankRuleClassifyAs.BankEntry;
        rule.BankEntryMode = BankEntryMode.SingleAccount;
        rule.AccountId = null;

        var ex = Assert.Throws<BusinessException>(() => rule.Validate());
        Assert.Equal("MyERP:02015", ex.Code);
    }

    [Fact]
    public void Rule_Validate_MultipleAccounts_RequiresAtLeastOne()
    {
        var rule = CreateRule();
        rule.ClassifyAs = BankRuleClassifyAs.BankEntry;
        rule.BankEntryMode = BankEntryMode.MultipleAccounts;
        // No accounts added

        var ex = Assert.Throws<BusinessException>(() => rule.Validate());
        Assert.Equal("MyERP:02015", ex.Code);
    }

    [Fact]
    public void Rule_Validate_MultipleAccounts_LastRowMustBeRemainder()
    {
        var rule = CreateRule();
        rule.ClassifyAs = BankRuleClassifyAs.BankEntry;
        rule.BankEntryMode = BankEntryMode.MultipleAccounts;
        rule.AddAccount(Guid.NewGuid(), "transaction_amount * 0.6", null);
        rule.AddAccount(Guid.NewGuid(), "transaction_amount * 0.4", null); // Should be remainder (null formulas)

        var ex = Assert.Throws<BusinessException>(() => rule.Validate());
        Assert.Equal("MyERP:02016", ex.Code);
    }

    [Fact]
    public void Rule_Validate_MultipleAccounts_ValidConfig()
    {
        var rule = CreateRule();
        rule.ClassifyAs = BankRuleClassifyAs.BankEntry;
        rule.BankEntryMode = BankEntryMode.MultipleAccounts;
        rule.AddAccount(Guid.NewGuid(), "transaction_amount * 0.6", null);
        rule.AddAccount(Guid.NewGuid(), null, null); // Remainder row

        rule.Validate(); // Should not throw
    }

    [Fact]
    public void Rule_AddCondition_InvalidRegex_Throws()
    {
        var rule = CreateRule();

        var ex = Assert.Throws<BusinessException>(() =>
            rule.AddCondition(BankRuleMatchType.Regex, "[invalid(regex"));
        Assert.Equal("MyERP:02013", ex.Code);
    }

    [Fact]
    public void Rule_AddCondition_ValidRegex_Succeeds()
    {
        var rule = CreateRule();
        rule.AddCondition(BankRuleMatchType.Regex, @"^REF:\s*\d+$");

        Assert.Single(rule.Conditions);
    }

    [Fact]
    public void BankTransaction_RuleEvaluation_DefaultValues()
    {
        var tx = CreateTransaction(1000m);
        Assert.False(tx.IsRuleEvaluated);
        Assert.Null(tx.MatchedTransactionRuleId);
    }

    [Fact]
    public void Rule_Priority_AssignedOnCreate()
    {
        var rule1 = new BankTransactionRule(Guid.NewGuid(), _companyId, "Rule 1", 1);
        var rule2 = new BankTransactionRule(Guid.NewGuid(), _companyId, "Rule 2", 2);

        Assert.Equal(1, rule1.Priority);
        Assert.Equal(2, rule2.Priority);
    }

    [Fact]
    public void Rule_FirstMatch_WinsInPriorityOrder()
    {
        var rule1 = CreateRule("High Priority", priority: 1);
        rule1.AddCondition(BankRuleMatchType.Contains, "payment");

        var rule2 = CreateRule("Low Priority", priority: 2);
        rule2.AddCondition(BankRuleMatchType.Contains, "payment");

        var tx = CreateTransaction(1000m, "Customer payment received");
        var rules = new[] { rule1, rule2 }.OrderBy(r => r.Priority);

        // First match wins
        var match = rules.First(r => r.Matches(tx));
        Assert.Equal("High Priority", match.RuleName);
    }
}
