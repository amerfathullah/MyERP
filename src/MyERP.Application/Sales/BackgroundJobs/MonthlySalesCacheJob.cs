using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using MyERP.Core;
using MyERP.Sales.Entities;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales.BackgroundJobs;

/// <summary>
/// Background job that pre-aggregates and caches monthly sales history per company.
/// Per ERPNext: company.cache_companies_monthly_sales_history (daily scheduler).
/// </summary>
public class MonthlySalesCacheJob : AsyncBackgroundJob<MonthlySalesCacheJobArgs>, ITransientDependency
{
    private readonly IRepository<SalesInvoice, Guid> _invoiceRepository;
    private readonly IDistributedCache<MonthlySalesCacheItem> _cache;
    private readonly ILogger<MonthlySalesCacheJob> _logger;

    public MonthlySalesCacheJob(
        IRepository<SalesInvoice, Guid> invoiceRepository,
        IDistributedCache<MonthlySalesCacheItem> cache,
        ILogger<MonthlySalesCacheJob> logger)
    {
        _invoiceRepository = invoiceRepository;
        _cache = cache;
        _logger = logger;
    }

    public override async Task ExecuteAsync(MonthlySalesCacheJobArgs args)
    {
        var asOfDate = args.AsOfDate ?? DateTime.UtcNow.Date;
        var startOfYear = new DateTime(asOfDate.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        _logger.LogInformation("MonthlySalesCacheJob: Caching monthly sales history for company {CompanyId} as of {Date}",
            args.CompanyId, asOfDate);

        var query = await _invoiceRepository.GetQueryableAsync();
        var invoices = query
            .Where(i => i.CompanyId == args.CompanyId &&
                        i.Status == DocumentStatus.Posted &&
                        !i.IsReturn &&
                        i.IssueDate >= startOfYear &&
                        i.IssueDate <= asOfDate)
            .ToList();

        var monthlyTotals = invoices
            .GroupBy(i => new { i.IssueDate.Year, i.IssueDate.Month })
            .Select(g => new MonthlySalesBucket
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                TotalAmount = g.Sum(x => x.GrandTotal),
                InvoiceCount = g.Count(),
            })
            .OrderBy(b => b.Year)
            .ThenBy(b => b.Month)
            .ToList();

        var cacheKey = $"monthly_sales:{args.CompanyId}:{asOfDate.Year}";
        var cacheItem = new MonthlySalesCacheItem
        {
            CompanyId = args.CompanyId,
            Year = asOfDate.Year,
            Buckets = monthlyTotals,
            CachedAt = DateTime.UtcNow,
        };

        await _cache.SetAsync(cacheKey, cacheItem, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
        });

        _logger.LogInformation("MonthlySalesCacheJob: Cached {Count} monthly buckets for company {CompanyId} (Total: {Total:N2})",
            monthlyTotals.Count, args.CompanyId, monthlyTotals.Sum(b => b.TotalAmount));
    }
}

public class MonthlySalesCacheJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime? AsOfDate { get; set; }
}

public class MonthlySalesCacheItem
{
    public Guid CompanyId { get; set; }
    public int Year { get; set; }
    public System.Collections.Generic.List<MonthlySalesBucket> Buckets { get; set; } = new();
    public DateTime CachedAt { get; set; }
}

public class MonthlySalesBucket
{
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal TotalAmount { get; set; }
    public int InvoiceCount { get; set; }
}
