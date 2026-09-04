using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using System.Threading.Tasks;

namespace MyERP.Accounting;

// --- Trial Balance ---
public class TrialBalanceRequestDto
{
    [Required] public Guid CompanyId { get; set; }
    [Required] public DateTime AsOfDate { get; set; }
    public Guid? FiscalYearId { get; set; }
    public bool IncludeSubsidiaries { get; set; } = false;
}

public class TrialBalanceRowDto
{
    public Guid AccountId { get; set; }
    public string AccountCode { get; set; } = null!;
    public string AccountName { get; set; } = null!;
    public string AccountType { get; set; } = null!;
    public bool IsGroup { get; set; }
    public int Level { get; set; }
    public decimal OpeningDebit { get; set; }
    public decimal OpeningCredit { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal ClosingDebit { get; set; }
    public decimal ClosingCredit { get; set; }
}

public class TrialBalanceReportDto
{
    public DateTime AsOfDate { get; set; }
    public Guid CompanyId { get; set; }
    public List<TrialBalanceRowDto> Rows { get; set; } = new();
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
}

// --- Profit & Loss ---
public class ProfitLossRequestDto
{
    [Required] public Guid CompanyId { get; set; }
    [Required] public DateTime FromDate { get; set; }
    [Required] public DateTime ToDate { get; set; }
    /// <summary>When true, includes previous period data for comparison (same duration, immediately preceding).</summary>
    public bool IncludeComparison { get; set; }
}

public class ProfitLossRowDto
{
    public Guid AccountId { get; set; }
    public string AccountCode { get; set; } = null!;
    public string AccountName { get; set; } = null!;
    public string AccountType { get; set; } = null!;
    public decimal Amount { get; set; }
    /// <summary>Amount from the previous comparison period (null when comparison not requested).</summary>
    public decimal? PreviousPeriodAmount { get; set; }
    /// <summary>Growth percentage vs previous period. Null when no comparison or previous was zero.</summary>
    public decimal? GrowthPercentage { get; set; }
    public int Level { get; set; }
    public bool IsGroup { get; set; }
}

public class ProfitLossReportDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public Guid CompanyId { get; set; }
    public List<ProfitLossRowDto> RevenueRows { get; set; } = new();
    public List<ProfitLossRowDto> ExpenseRows { get; set; } = new();
    public decimal TotalRevenue { get; set; }
    public decimal TotalExpense { get; set; }
    public decimal NetProfitOrLoss { get; set; }
    /// <summary>Previous period totals (populated when IncludeComparison=true).</summary>
    public decimal? PreviousTotalRevenue { get; set; }
    public decimal? PreviousTotalExpense { get; set; }
    public decimal? PreviousNetProfitOrLoss { get; set; }
    /// <summary>Previous period date range for display.</summary>
    public DateTime? PreviousFromDate { get; set; }
    public DateTime? PreviousToDate { get; set; }
}

// --- Balance Sheet ---
public class BalanceSheetRequestDto
{
    [Required] public Guid CompanyId { get; set; }
    [Required] public DateTime AsOfDate { get; set; }
}

public class BalanceSheetRowDto
{
    public Guid AccountId { get; set; }
    public string AccountCode { get; set; } = null!;
    public string AccountName { get; set; } = null!;
    public string AccountType { get; set; } = null!;
    public decimal Amount { get; set; }
    public int Level { get; set; }
    public bool IsGroup { get; set; }
}

public class BalanceSheetReportDto
{
    public DateTime AsOfDate { get; set; }
    public Guid CompanyId { get; set; }
    public List<BalanceSheetRowDto> AssetRows { get; set; } = new();
    public List<BalanceSheetRowDto> LiabilityRows { get; set; } = new();
    public List<BalanceSheetRowDto> EquityRows { get; set; } = new();
    public decimal TotalAssets { get; set; }
    public decimal TotalLiabilities { get; set; }
    public decimal TotalEquity { get; set; }
}

// --- Monthly P&L Columnar Report (12-month side-by-side) ---
public class MonthlyProfitLossRequestDto
{
    [Required] public Guid CompanyId { get; set; }
    [Required] public int Year { get; set; }
    public int StartMonth { get; set; } = 1;
}

public class MonthlyProfitLossRowDto
{
    public Guid AccountId { get; set; }
    public string AccountCode { get; set; } = null!;
    public string AccountName { get; set; } = null!;
    public string AccountType { get; set; } = null!;
    public decimal[] MonthlyAmounts { get; set; } = new decimal[12];
    public decimal AnnualTotal { get; set; }
}

public class MonthlyProfitLossReportDto
{
    public int Year { get; set; }
    public Guid CompanyId { get; set; }
    public string[] MonthLabels { get; set; } = new string[12];
    public List<MonthlyProfitLossRowDto> RevenueRows { get; set; } = new();
    public List<MonthlyProfitLossRowDto> ExpenseRows { get; set; } = new();
    public decimal[] MonthlyRevenue { get; set; } = new decimal[12];
    public decimal[] MonthlyExpense { get; set; } = new decimal[12];
    public decimal[] MonthlyNetProfit { get; set; } = new decimal[12];
    public decimal AnnualRevenue { get; set; }
    public decimal AnnualExpense { get; set; }
    public decimal AnnualNetProfit { get; set; }
}

// --- Trial Balance for Party ---
public class PartyTrialBalanceRequestDto
{
    [Required] public Guid CompanyId { get; set; }
    [Required] public DateTime FromDate { get; set; }
    [Required] public DateTime ToDate { get; set; }
    public string PartyType { get; set; } = "Customer";
    public Guid? PartyId { get; set; }
    public Guid? AccountId { get; set; }
    public bool ExcludeZeroBalanceParties { get; set; } = false;
    public bool ShowZeroValues { get; set; } = false;
}

public class PartyTrialBalanceRowDto
{
    public Guid PartyId { get; set; }
    public string PartyName { get; set; } = null!;
    public string PartyType { get; set; } = null!;
    public decimal OpeningDebit { get; set; }
    public decimal OpeningCredit { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal ClosingDebit { get; set; }
    public decimal ClosingCredit { get; set; }
    public string Currency { get; set; } = null!;
}

public class PartyTrialBalanceReportDto
{
    public Guid CompanyId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string PartyType { get; set; } = null!;
    public string Currency { get; set; } = null!;
    public List<PartyTrialBalanceRowDto> Rows { get; set; } = new();
    public decimal TotalOpeningDebit { get; set; }
    public decimal TotalOpeningCredit { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public decimal TotalClosingDebit { get; set; }
    public decimal TotalClosingCredit { get; set; }
}

// --- Service Interface ---
public interface IReportingAppService : IApplicationService
{
    Task<TrialBalanceReportDto> GetTrialBalanceAsync(TrialBalanceRequestDto input);
    Task<ProfitLossReportDto> GetProfitLossAsync(ProfitLossRequestDto input);
    Task<BalanceSheetReportDto> GetBalanceSheetAsync(BalanceSheetRequestDto input);
    Task<MonthlyProfitLossReportDto> GetMonthlyProfitLossAsync(MonthlyProfitLossRequestDto input);
    Task<PartyTrialBalanceReportDto> GetTrialBalanceForPartyAsync(PartyTrialBalanceRequestDto input);
}

