namespace MyERP.HumanResources;

/// <summary>
/// Loan type: term (scheduled repayments) or demand (on-demand repayment).
/// </summary>
public enum LoanType
{
    TermLoan = 0,
    DemandLoan = 1
}

/// <summary>
/// How interest is calculated on the loan.
/// </summary>
public enum InterestCalculationMethod
{
    /// <summary>Interest on reducing outstanding balance (EMI formula).</summary>
    DiminishingBalance = 0,

    /// <summary>Interest on original principal for full tenure (flat rate).</summary>
    FlatRate = 1
}

/// <summary>
/// Loan document status.
/// </summary>
public enum LoanStatus
{
    Draft = 0,
    Sanctioned = 1,
    Disbursed = 2,
    PartiallyRepaid = 3,
    FullyRepaid = 4,
    Cancelled = 5
}
