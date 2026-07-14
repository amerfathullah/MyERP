namespace MyERP.Accounting;

/// <summary>
/// Type of bank transaction that a rule applies to.
/// </summary>
public enum BankTransactionType
{
    Any = 0,
    Withdrawal = 1,
    Deposit = 2
}

/// <summary>
/// What action to take when a bank transaction rule matches.
/// </summary>
public enum BankRuleClassifyAs
{
    /// <summary>Create a Journal Entry (Bank Entry type).</summary>
    BankEntry = 0,

    /// <summary>Match against an existing Payment Entry.</summary>
    PaymentEntry = 1,

    /// <summary>Internal bank-to-bank transfer.</summary>
    Transfer = 2
}

/// <summary>
/// How the bank entry should be structured when classify_as = BankEntry.
/// </summary>
public enum BankEntryMode
{
    /// <summary>Single target account (simple posting).</summary>
    SingleAccount = 0,

    /// <summary>Multiple target accounts with formula-based amounts.</summary>
    MultipleAccounts = 1
}

/// <summary>
/// Description matching mode for bank transaction rule conditions.
/// </summary>
public enum BankRuleMatchType
{
    Contains = 0,
    StartsWith = 1,
    EndsWith = 2,
    Regex = 3
}
