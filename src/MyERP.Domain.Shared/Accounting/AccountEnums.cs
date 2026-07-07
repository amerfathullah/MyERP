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

    // Liability
    CurrentLiability = 20,
    LongTermLiability = 21,
    AccountsPayable = 22,
    TaxPayable = 23,

    // Equity
    ShareCapital = 30,
    RetainedEarnings = 31,

    // Revenue
    OperatingRevenue = 40,
    OtherIncome = 41,

    // Expense
    OperatingExpense = 50,
    CostOfGoodsSold = 51,
    DepreciationExpense = 52,
    TaxExpense = 53
}
