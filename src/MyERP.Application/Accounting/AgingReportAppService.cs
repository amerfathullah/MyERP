using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using MyERP.Notification.Entities;
using MyERP.Notification;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

[Authorize(MyERPPermissions.SalesInvoices.Default)]
public class AgingReportAppService : ApplicationService, IAgingReportAppService
{
    private readonly AgingBucketService _agingService;
    private readonly IRepository<AppNotification, Guid> _notificationRepo;

    public AgingReportAppService(
        AgingBucketService agingService,
        IRepository<AppNotification, Guid> notificationRepo)
    {
        _agingService = agingService;
        _notificationRepo = notificationRepo;
    }

    public async Task<AgingReportDto> GetReceivablesAgingAsync(AgingReportRequestDto input)
    {
        var calculateAgeingWith = string.Equals(input.CalculateAgeingWith, "Today Date", StringComparison.OrdinalIgnoreCase)
            ? "Today Date"
            : "Report Date";
        var asOfDate = calculateAgeingWith == "Today Date"
            ? DateTime.UtcNow.Date
            : (input.AsOfDate ?? DateTime.UtcNow.Date);

        var report = await _agingService.CalculateReceivablesAgingAsync(input.CompanyId, asOfDate, calculateAgeingWith: calculateAgeingWith);
        return MapToDto(report);
    }

    public async Task<AgingReportDto> GetPayablesAgingAsync(AgingReportRequestDto input)
    {
        var calculateAgeingWith = string.Equals(input.CalculateAgeingWith, "Today Date", StringComparison.OrdinalIgnoreCase)
            ? "Today Date"
            : "Report Date";
        var asOfDate = calculateAgeingWith == "Today Date"
            ? DateTime.UtcNow.Date
            : (input.AsOfDate ?? DateTime.UtcNow.Date);

        var report = await _agingService.CalculatePayablesAgingAsync(input.CompanyId, asOfDate, calculateAgeingWith: calculateAgeingWith);
        return MapToDto(report);
    }

    private static AgingReportDto MapToDto(AgingReport report)
    {
        var bucketLabels = new string[report.BucketRanges.Length + 1];
        for (int i = 0; i < report.BucketRanges.Length + 1; i++)
        {
            if (i == 0) bucketLabels[i] = $"0-{report.BucketRanges[0]}";
            else if (i < report.BucketRanges.Length)
                bucketLabels[i] = $"{report.BucketRanges[i - 1] + 1}-{report.BucketRanges[i]}";
            else bucketLabels[i] = $"{report.BucketRanges[^1] + 1}+";
        }

        return new AgingReportDto
        {
            ReportType = report.ReportType,
            AsOfDate = report.AsOfDate,
            CalculateAgeingWith = report.CalculateAgeingWith,
            BucketLabels = bucketLabels,
            BucketTotals = report.BucketTotals,
            TotalOutstanding = report.TotalOutstanding,
            InvoiceCount = report.InvoiceCount,
            Details = report.Details.Select(d => new AgingDetailEntryDto
            {
                PartyId = d.PartyId,
                PartyName = d.PartyName,
                DocumentId = d.DocumentId,
                DocumentNumber = d.DocumentNumber,
                PostingDate = d.PostingDate,
                DueDate = d.DueDate,
                OutstandingAmount = d.OutstandingAmount,
                AgeDays = d.AgeDays,
                BucketLabel = d.BucketLabel,
            }).OrderBy(d => d.PartyName).ThenByDescending(d => d.AgeDays).ToArray(),
        };
    }

    /// <summary>
    /// Sends an on-demand payment reminder notification for a specific party.
    /// Per ERPNext: creates notification visible to accounts/collections team.
    /// </summary>
    public async Task<bool> SendPaymentReminderAsync(SendPaymentReminderInput input)
    {
        var userId = CurrentUser.Id ?? Guid.Empty;
        if (userId == Guid.Empty) return false;

        var notification = new AppNotification(
            GuidGenerator.Create(),
            userId,
            $"Payment reminder sent to {input.PartyName}",
            CurrentTenant.Id
        );
        notification.Body = $"Overdue amount: {input.OverdueAmount:N2} ({input.InvoiceCount} invoice(s)). Reminder initiated manually from Aging Report.";
        notification.Severity = NotificationSeverity.Warning;

        await _notificationRepo.InsertAsync(notification);
        return true;
    }
}
