using System;

namespace MyERP.Accounting;

public class AgingReportDto
{
    public string ReportType { get; set; } = null!;
    public DateTime AsOfDate { get; set; }
    public string CalculateAgeingWith { get; set; } = "Report Date";
    public string AgeingBasedOn { get; set; } = "Due Date";
    public string[] BucketLabels { get; set; } = [];
    public decimal[] BucketTotals { get; set; } = [];
    public decimal TotalOutstanding { get; set; }
    public int InvoiceCount { get; set; }
    public AgingDetailEntryDto[] Details { get; set; } = [];
}

public class AgingDetailEntryDto
{
    public Guid PartyId { get; set; }
    public string? PartyName { get; set; }
    public Guid DocumentId { get; set; }
    public string DocumentNumber { get; set; } = null!;
    public DateTime PostingDate { get; set; }
    public DateTime DueDate { get; set; }
    public decimal OutstandingAmount { get; set; }
    public int AgeDays { get; set; }
    public string BucketLabel { get; set; } = null!;
}

public class AgingReportRequestDto
{
    public Guid CompanyId { get; set; }
    public DateTime? AsOfDate { get; set; }
    /// <summary>
    /// Per ERPNext PR #47580 / commit c67ba2d49b: "Report Date" (default) or "Today Date".
    /// </summary>
    public string? CalculateAgeingWith { get; set; } = "Report Date";

    /// <summary>
    /// "Due Date" (default) or "Posting Date".
    /// </summary>
    public string? AgeingBasedOn { get; set; } = "Due Date";

    /// <summary>Optional party filter (Customer for AR, Supplier for AP).</summary>
    public Guid? PartyId { get; set; }

    /// <summary>Optional invoice posting date range filter.</summary>
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public class SendPaymentReminderInput
{
    public Guid PartyId { get; set; }
    public string PartyName { get; set; } = null!;
    public string PartyType { get; set; } = "Customer";
    public decimal OverdueAmount { get; set; }
    public int InvoiceCount { get; set; }
}
