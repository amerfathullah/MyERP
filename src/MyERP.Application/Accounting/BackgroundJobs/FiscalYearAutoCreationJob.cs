using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.Accounting.Entities;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace MyERP.Accounting.BackgroundJobs;

/// <summary>
/// Background job that auto-creates the next fiscal year 3 days before current fiscal year ends.
/// Per ERPNext: fiscal_year.auto_create_fiscal_year (daily scheduler).
/// </summary>
public class FiscalYearAutoCreationJob : AsyncBackgroundJob<FiscalYearAutoCreationJobArgs>, ITransientDependency
{
    private readonly IRepository<FiscalYear, Guid> _repository;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ILogger<FiscalYearAutoCreationJob> _logger;

    public FiscalYearAutoCreationJob(
        IRepository<FiscalYear, Guid> repository,
        IGuidGenerator guidGenerator,
        ILogger<FiscalYearAutoCreationJob> logger)
    {
        _repository = repository;
        _guidGenerator = guidGenerator;
        _logger = logger;
    }

    public override async Task ExecuteAsync(FiscalYearAutoCreationJobArgs args)
    {
        var asOfDate = args.AsOfDate ?? DateTime.UtcNow.Date;
        var query = await _repository.GetQueryableAsync();

        var companyIds = args.CompanyId != Guid.Empty
            ? new List<Guid> { args.CompanyId }
            : query.Select(f => f.CompanyId).Distinct().ToList();

        _logger.LogInformation("FiscalYearAutoCreationJob: Checking upcoming fiscal year end for {Count} companies as of {Date}",
            companyIds.Count, asOfDate);

        foreach (var companyId in companyIds)
        {
            var companyFiscalYears = query
                .Where(f => f.CompanyId == companyId)
                .OrderByDescending(f => f.EndDate)
                .ToList();

            if (!companyFiscalYears.Any())
                continue;

            var latestFy = companyFiscalYears.First();

            // Skip short fiscal years per ERPNext fiscal_year.auto_create_fiscal_year (#5979)
            if (latestFy.IsShortYear)
            {
                _logger.LogInformation("FiscalYearAutoCreationJob: Latest fiscal year '{Name}' for company {CompanyId} is a Short Year. Skipping auto-creation.",
                    latestFy.Name, companyId);
                continue;
            }

            // If latest FY ends within 3 days or has already ended, create the next FY
            if (latestFy.EndDate <= asOfDate.AddDays(3))
            {
                var nextStartDate = latestFy.EndDate.AddDays(1);
                var nextEndDate = nextStartDate.AddYears(1).AddDays(-1);
                var nextFyName = nextStartDate.Year == nextEndDate.Year
                    ? nextStartDate.Year.ToString()
                    : $"{nextStartDate.Year}-{nextEndDate.Year}";

                var exists = companyFiscalYears.Any(f => f.StartDate == nextStartDate || f.Name == nextFyName);
                if (!exists)
                {
                    var nextFy = new FiscalYear(
                        _guidGenerator.Create(),
                        companyId,
                        nextFyName,
                        nextStartDate,
                        nextEndDate,
                        isShortYear: false,
                        args.TenantId ?? latestFy.TenantId);

                    await _repository.InsertAsync(nextFy);
                    _logger.LogInformation("FiscalYearAutoCreationJob: Auto-created next fiscal year '{Name}' ({StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}) for company {CompanyId}",
                        nextFyName, nextStartDate, nextEndDate, companyId);
                }
            }
        }
    }
}

public class FiscalYearAutoCreationJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime? AsOfDate { get; set; }
}
