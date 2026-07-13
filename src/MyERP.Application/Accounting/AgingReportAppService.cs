using System;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public class AgingReportDto
{
    public string ReportType { get; set; } = null!;
    public DateTime AsOfDate { get; set; }
    public string[] BucketLabels { get; set; } = [];
    public decimal[] BucketTotals { get; set; } = [];
    public decimal TotalOutstanding { get; set; }
    public int InvoiceCount { get; set; }
}

public class AgingReportRequestDto
{
    public Guid CompanyId { get; set; }
    public DateTime? AsOfDate { get; set; }
}

[Authorize(MyERPPermissions.SalesInvoices.Default)]
public class AgingReportAppService : ApplicationService
{
    private readonly AgingBucketService _agingService;

    public AgingReportAppService(AgingBucketService agingService) => _agingService = agingService;

    public async Task<AgingReportDto> GetReceivablesAgingAsync(AgingReportRequestDto input)
    {
        var asOfDate = input.AsOfDate ?? DateTime.UtcNow.Date;
        var report = await _agingService.CalculateReceivablesAgingAsync(input.CompanyId, asOfDate);
        return MapToDto(report);
    }

    public async Task<AgingReportDto> GetPayablesAgingAsync(AgingReportRequestDto input)
    {
        var asOfDate = input.AsOfDate ?? DateTime.UtcNow.Date;
        var report = await _agingService.CalculatePayablesAgingAsync(input.CompanyId, asOfDate);
        return MapToDto(report);
    }

    private static AgingReportDto MapToDto(AgingReport report) => new()
    {
        ReportType = report.ReportType,
        AsOfDate = report.AsOfDate,
        BucketLabels = GenerateBucketLabels(report.BucketRanges),
        BucketTotals = report.BucketTotals,
        TotalOutstanding = report.TotalOutstanding,
        InvoiceCount = report.InvoiceCount,
    };

    private static string[] GenerateBucketLabels(int[] ranges)
    {
        var labels = new string[ranges.Length + 1];
        labels[0] = $"0-{ranges[0]}";
        for (int i = 1; i < ranges.Length; i++)
            labels[i] = $"{ranges[i - 1] + 1}-{ranges[i]}";
        labels[ranges.Length] = $"{ranges[^1]}+";
        return labels;
    }
}
