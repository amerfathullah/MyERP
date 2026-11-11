using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting.BackgroundJobs;

/// <summary>
/// Background job that processes deferred revenue recognition for a company.
/// Generates Journal Entries for service periods that have elapsed.
/// Per ERPNext: runs monthly to recognize revenue for completed service periods.
/// 
/// Per ERPNext process_deferred_accounting(): books through the day before the run date, and only
/// when Accounts Settings has automatic processing switched on. The booking rules themselves live
/// in <see cref="DeferredAccountingService"/>.
/// </summary>
public class DeferredRevenueJob : AsyncBackgroundJob<DeferredRevenueJobArgs>, ITransientDependency
{
    private readonly DeferredAccountingService _deferredService;
    private readonly IRepository<AccountsSettings, Guid> _accountsSettingsRepository;
    private readonly ILogger<DeferredRevenueJob> _logger;

    public DeferredRevenueJob(
        DeferredAccountingService deferredService,
        IRepository<AccountsSettings, Guid> accountsSettingsRepository,
        ILogger<DeferredRevenueJob> logger)
    {
        _deferredService = deferredService;
        _accountsSettingsRepository = accountsSettingsRepository;
        _logger = logger;
    }

    public override async Task ExecuteAsync(DeferredRevenueJobArgs args)
    {
        _logger.LogInformation("DeferredRevenueJob: Processing deferred revenue+expense for company {CompanyId}, as of {AsOfDate}",
            args.CompanyId, args.AsOfDate.ToString("yyyy-MM-dd"));

        var settings = (await _accountsSettingsRepository.GetQueryableAsync()).FirstOrDefault();
        if (settings is { AutomaticallyProcessDeferredAccountingEntry: false })
        {
            _logger.LogInformation("DeferredRevenueJob: automatic deferred accounting is switched off; skipping company {CompanyId}", args.CompanyId);
            return;
        }

        try
        {
            var bookThrough = args.AsOfDate.Date.AddDays(-1);

            var revenueCount = await _deferredService.ProcessDeferredRevenueAsync(
                args.CompanyId, bookThrough, args.TenantId);

            var expenseCount = await _deferredService.ProcessDeferredExpenseAsync(
                args.CompanyId, bookThrough, args.TenantId);

            _logger.LogInformation(
                "DeferredRevenueJob: Created {RevenueCount} revenue + {ExpenseCount} expense recognition JEs for company {CompanyId}",
                revenueCount, expenseCount, args.CompanyId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeferredRevenueJob: Error processing company {CompanyId}", args.CompanyId);
            throw; // ABP will handle retry
        }
    }
}

public class DeferredRevenueJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime AsOfDate { get; set; } = DateTime.UtcNow.Date;
}
