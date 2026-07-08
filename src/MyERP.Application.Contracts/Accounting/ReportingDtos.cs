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
}

public class ProfitLossRowDto
{
    public Guid AccountId { get; set; }
    public string AccountCode { get; set; } = null!;
    public string AccountName { get; set; } = null!;
    public string AccountType { get; set; } = null!;
    public decimal Amount { get; set; }
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

// --- Service Interface ---
public interface IReportingAppService : IApplicationService
{
    Task<TrialBalanceReportDto> GetTrialBalanceAsync(TrialBalanceRequestDto input);
    Task<ProfitLossReportDto> GetProfitLossAsync(ProfitLossRequestDto input);
    Task<BalanceSheetReportDto> GetBalanceSheetAsync(BalanceSheetRequestDto input);
}
