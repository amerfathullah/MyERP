using System;
using System.Collections.Generic;

namespace MyERP.Accounting;

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
