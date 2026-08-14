namespace MyERP.Accounting;

/// <summary>Financial report types.</summary>
public enum FinancialReportType
{
    ProfitAndLoss = 0,
    BalanceSheet = 1,
    CashFlow = 2,
    Custom = 3
}

/// <summary>Data source for a financial report row.</summary>
public enum FinancialReportDataSource
{
    /// <summary>Row pulls GL data filtered by account categories.</summary>
    AccountData = 0,
    /// <summary>Row calculates value from formula referencing other rows.</summary>
    CalculatedAmount = 1,
    /// <summary>Row fetches data from a custom API endpoint.</summary>
    CustomApi = 2,
    /// <summary>Visual separator — blank line.</summary>
    BlankLine = 3,
    /// <summary>Visual separator — column break (multi-segment layout).</summary>
    ColumnBreak = 4,
    /// <summary>Visual separator — section break (multi-segment layout).</summary>
    SectionBreak = 5
}
