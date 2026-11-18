using System;
using System.Collections.Generic;

namespace MyERP.Accounting;

public class DeferredRevenueAndExpenseRequestDto
{
    public Guid CompanyId { get; set; }
    public string FilterBasedOn { get; set; } = "Date Range";
    public Guid? FromFiscalYearId { get; set; }
    public Guid? ToFiscalYearId { get; set; }
    public DateTime? PeriodStartDate { get; set; }
    public DateTime? PeriodEndDate { get; set; }
    public string Periodicity { get; set; } = "Monthly";
    public string Type { get; set; } = "Revenue";
    public bool WithUpcomingPostings { get; set; } = true;
}

public class DeferredReportPeriodDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public decimal ActualTotal { get; set; }
    public decimal ExpectedTotal { get; set; }
}

public class DeferredRevenueAndExpenseRowDto
{
    public Guid? InvoiceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsTotalRow { get; set; }
    public int Indent { get; set; }
    public DateTime? ServiceStartDate { get; set; }
    public DateTime? ServiceEndDate { get; set; }
    public decimal Amount { get; set; }
    public decimal[] PeriodValues { get; set; } = Array.Empty<decimal>();
    public decimal[] ActualPeriodValues { get; set; } = Array.Empty<decimal>();
}

public class DeferredRevenueAndExpenseReportDto
{
    public Guid CompanyId { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public string Type { get; set; } = "Revenue";
    public string Periodicity { get; set; } = "Monthly";
    public DateTime PeriodStartDate { get; set; }
    public DateTime PeriodEndDate { get; set; }
    public bool WithUpcomingPostings { get; set; }
    public List<DeferredReportPeriodDto> Periods { get; set; } = new();
    public List<DeferredRevenueAndExpenseRowDto> Rows { get; set; } = new();
}
