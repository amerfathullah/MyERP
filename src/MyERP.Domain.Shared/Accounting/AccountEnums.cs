namespace MyERP.Accounting;

public enum AccountType
{
    Asset = 0,
    Liability = 1,
    Equity = 2,
    Revenue = 3,
    Expense = 4
}

public enum AccountSubType
{
    // Asset
    CurrentAsset = 10,
    FixedAsset = 11,
    BankAccount = 12,
    CashAccount = 13,
    AccountsReceivable = 14,
    Stock = 15,
    AccumulatedDepreciation = 16,
    CapitalWorkInProgress = 17,

    // Liability
    CurrentLiability = 20,
    LongTermLiability = 21,
    AccountsPayable = 22,
    TaxPayable = 23,

    // Equity
    ShareCapital = 30,
    RetainedEarnings = 31,
    TemporaryOpening = 32,

    // Revenue
    OperatingRevenue = 40,
    OtherIncome = 41,

    // Expense
    OperatingExpense = 50,
    CostOfGoodsSold = 51,
    DepreciationExpense = 52,
    TaxExpense = 53
}

/// <summary>
/// Enforces the direction of an account's balance.
/// Debit-normal accounts (Assets, Expenses) should always have debit balance.
/// Credit-normal accounts (Liabilities, Equity, Revenue) should always have credit balance.
/// </summary>
public enum BalanceDirection
{
    /// <summary>Balance must be debit (positive). Asset and Expense accounts.</summary>
    Debit = 0,

    /// <summary>Balance must be credit (negative in accounting terms). Liability, Equity, Revenue accounts.</summary>
    Credit = 1
}
