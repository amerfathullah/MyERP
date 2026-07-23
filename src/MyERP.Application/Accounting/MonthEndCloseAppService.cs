using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Accounting.DomainServices;
using MyERP.Permissions;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

[Authorize(MyERPPermissions.Accounts.Default)]
public class MonthEndCloseAppService : ApplicationService
{
    private readonly MonthEndCloseService _monthEndService;

    public MonthEndCloseAppService(MonthEndCloseService monthEndService)
    {
        _monthEndService = monthEndService;
    }

    /// <summary>Validates readiness for month-end close — returns checklist.</summary>
    public async Task<MonthEndReadinessDto> ValidateReadinessAsync(MonthEndCloseRequestDto input)
    {
        var report = await _monthEndService.ValidateReadinessAsync(input.CompanyId, input.PeriodEndDate);
        return new MonthEndReadinessDto
        {
            CompanyId = report.CompanyId,
            PeriodEndDate = report.PeriodEndDate,
            IsReady = report.IsReady,
            PassedCount = report.PassedCount,
            TotalChecks = report.TotalChecks,
            Checks = report.Checks.Select(c => new MonthEndCheckDto
            {
                Name = c.Name,
                Passed = c.Passed,
                Details = c.Details
            }).ToList()
        };
    }

    /// <summary>Gets current close status for a period.</summary>
    public async Task<MonthEndCloseStatusDto> GetCloseStatusAsync(MonthEndCloseRequestDto input)
    {
        var status = await _monthEndService.GetCloseStatusAsync(input.CompanyId, input.PeriodEndDate);
        return new MonthEndCloseStatusDto
        {
            CompanyId = status.CompanyId,
            PeriodEndDate = status.PeriodEndDate,
            IsTrialBalanceBalanced = status.IsTrialBalanceBalanced,
            HasPeriodClosingVoucher = status.HasPeriodClosingVoucher,
            IsPeriodClosed = status.IsPeriodClosed,
            IsFullyClosed = status.IsFullyClosed
        };
    }

    /// <summary>Freezes accounting period up to the specified date.</summary>
    [Authorize(MyERPPermissions.Accounts.Edit)]
    public async Task FreezeAsync(FreezeAccountingPeriodDto input)
    {
        await _monthEndService.FreezeAccountingPeriodAsync(input.CompanyId, input.FreezeUpTo);
    }
}

// --- DTOs ---

public class MonthEndCloseRequestDto
{
    public Guid CompanyId { get; set; }
    public DateTime PeriodEndDate { get; set; }
}

public class FreezeAccountingPeriodDto
{
    public Guid CompanyId { get; set; }
    public DateTime FreezeUpTo { get; set; }
}

public class MonthEndReadinessDto
{
    public Guid CompanyId { get; set; }
    public DateTime PeriodEndDate { get; set; }
    public bool IsReady { get; set; }
    public int PassedCount { get; set; }
    public int TotalChecks { get; set; }
    public List<MonthEndCheckDto> Checks { get; set; } = new();
}

public class MonthEndCheckDto
{
    public string Name { get; set; } = null!;
    public bool Passed { get; set; }
    public string? Details { get; set; }
}

public class MonthEndCloseStatusDto
{
    public Guid CompanyId { get; set; }
    public DateTime PeriodEndDate { get; set; }
    public bool IsTrialBalanceBalanced { get; set; }
    public bool HasPeriodClosingVoucher { get; set; }
    public bool IsPeriodClosed { get; set; }
    public bool IsFullyClosed { get; set; }
}
