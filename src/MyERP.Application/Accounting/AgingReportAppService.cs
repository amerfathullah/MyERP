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
        return ObjectMapper.Map<AgingReport, AgingReportDto>(report);
    }

    public async Task<AgingReportDto> GetPayablesAgingAsync(AgingReportRequestDto input)
    {
        var asOfDate = input.AsOfDate ?? DateTime.UtcNow.Date;
        var report = await _agingService.CalculatePayablesAgingAsync(input.CompanyId, asOfDate);
        return ObjectMapper.Map<AgingReport, AgingReportDto>(report);
    }
}
