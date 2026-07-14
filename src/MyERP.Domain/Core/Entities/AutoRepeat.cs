using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Core.Entities;

/// <summary>
/// Auto Repeat configuration — automatically creates copies of documents on a schedule.
/// Supports any document type (Sales Invoice, Journal Entry, Purchase Order, etc.).
/// 
/// Documents are always created as Draft (never auto-submitted).
/// The reference document must be in Submitted status.
/// 
/// Source: frappe/automation/doctype/auto_repeat/auto_repeat.py
/// Per DO-NOT: "Allow auto-repeat of cancelled documents"
/// </summary>
public class AutoRepeat : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    /// <summary>The document type to repeat (e.g., "SalesInvoice", "JournalEntry").</summary>
    public string ReferenceDocumentType { get; set; } = null!;

    /// <summary>The ID of the template document to copy.</summary>
    public Guid ReferenceDocumentId { get; set; }

    /// <summary>Display label for the reference document.</summary>
    public string? ReferenceDocumentNumber { get; set; }

    /// <summary>How often to repeat.</summary>
    public RepeatFrequency Frequency { get; set; }

    /// <summary>Day of week for weekly frequency (1=Monday, 7=Sunday).</summary>
    public RepeatDayOfWeek? DayOfWeek { get; set; }

    /// <summary>Day of month for monthly/quarterly/half-yearly/yearly (1-31). If > days in month, uses last day.</summary>
    public int? DayOfMonth { get; set; }

    /// <summary>First date to generate a document.</summary>
    public DateTime StartDate { get; set; }

    /// <summary>Last date to generate (null = indefinite).</summary>
    public DateTime? EndDate { get; set; }

    /// <summary>Next date a document should be generated.</summary>
    public DateTime NextScheduleDate { get; set; }

    /// <summary>Whether this auto-repeat is currently active.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Send email notification after each creation.</summary>
    public bool NotifyByEmail { get; set; }

    /// <summary>Email recipients for notification.</summary>
    public string? NotifyRecipients { get; set; }

    /// <summary>How many documents have been generated so far.</summary>
    public int GeneratedCount { get; set; }

    /// <summary>Date of last document generation.</summary>
    public DateTime? LastGeneratedDate { get; set; }

    protected AutoRepeat() { }

    public AutoRepeat(
        Guid id,
        Guid companyId,
        string referenceDocumentType,
        Guid referenceDocumentId,
        RepeatFrequency frequency,
        DateTime startDate,
        DateTime? endDate = null,
        Guid? tenantId = null) : base(id)
    {
        CompanyId = companyId;
        ReferenceDocumentType = Check.NotNullOrWhiteSpace(referenceDocumentType, nameof(referenceDocumentType));
        ReferenceDocumentId = referenceDocumentId;
        Frequency = frequency;
        StartDate = startDate;
        EndDate = endDate;
        NextScheduleDate = startDate;
        TenantId = tenantId;

        if (endDate.HasValue && endDate.Value < startDate)
        {
            throw new BusinessException("MyERP:01011")
                .WithData("startDate", startDate)
                .WithData("endDate", endDate);
        }
    }

    /// <summary>
    /// Record that a document was generated and advance the schedule.
    /// </summary>
    public void RecordGeneration(DateTime generatedAt)
    {
        GeneratedCount++;
        LastGeneratedDate = generatedAt;
        NextScheduleDate = CalculateNextDate(generatedAt);

        // Auto-disable if past end date
        if (EndDate.HasValue && NextScheduleDate > EndDate.Value)
        {
            IsEnabled = false;
        }
    }

    /// <summary>
    /// Calculate the next schedule date based on frequency from a given date.
    /// Per ERPNext: Monthly uses same day (clamped to last day of shorter months).
    /// </summary>
    public DateTime CalculateNextDate(DateTime fromDate)
    {
        return Frequency switch
        {
            RepeatFrequency.Daily => fromDate.AddDays(1),
            RepeatFrequency.Weekly => fromDate.AddDays(7),
            RepeatFrequency.Monthly => AddMonthsPreserveDay(fromDate, 1),
            RepeatFrequency.Quarterly => AddMonthsPreserveDay(fromDate, 3),
            RepeatFrequency.HalfYearly => AddMonthsPreserveDay(fromDate, 6),
            RepeatFrequency.Yearly => AddMonthsPreserveDay(fromDate, 12),
            _ => fromDate.AddMonths(1)
        };
    }

    /// <summary>
    /// Check if this auto-repeat should fire on a given date.
    /// </summary>
    public bool IsDueOn(DateTime asOfDate)
    {
        if (!IsEnabled) return false;
        if (EndDate.HasValue && asOfDate > EndDate.Value) return false;
        return NextScheduleDate <= asOfDate;
    }

    /// <summary>
    /// Disable this auto-repeat.
    /// </summary>
    public void Disable()
    {
        IsEnabled = false;
    }

    /// <summary>
    /// Re-enable this auto-repeat.
    /// </summary>
    public void Enable()
    {
        if (EndDate.HasValue && DateTime.UtcNow.Date > EndDate.Value)
        {
            throw new BusinessException("MyERP:01012")
                .WithData("endDate", EndDate);
        }
        IsEnabled = true;
    }

    private static DateTime AddMonthsPreserveDay(DateTime date, int months)
    {
        var targetMonth = date.AddMonths(months);
        var dayOfMonth = date.Day;
        var daysInTargetMonth = DateTime.DaysInMonth(targetMonth.Year, targetMonth.Month);

        // Clamp to last day of month if the target month is shorter
        var actualDay = Math.Min(dayOfMonth, daysInTargetMonth);
        return new DateTime(targetMonth.Year, targetMonth.Month, actualDay);
    }
}
