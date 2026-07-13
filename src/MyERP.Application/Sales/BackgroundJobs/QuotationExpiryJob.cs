using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Sales.Entities;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales.BackgroundJobs;

/// <summary>
/// Background job that marks expired quotations as Lost.
/// Per ERPNext: quotations past ValidUntil and still in Submitted status are auto-expired.
/// Per DO-NOT: "Auto-expire Ordered quotations (they've already fulfilled their purpose)"
///   — only expires Submitted quotations, NOT ones already converted.
/// </summary>
public class QuotationExpiryJob : AsyncBackgroundJob<QuotationExpiryJobArgs>, ITransientDependency
{
    private readonly IRepository<Quotation, Guid> _repository;
    private readonly ILogger<QuotationExpiryJob> _logger;

    public QuotationExpiryJob(
        IRepository<Quotation, Guid> repository,
        ILogger<QuotationExpiryJob> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public override async Task ExecuteAsync(QuotationExpiryJobArgs args)
    {
        _logger.LogInformation("QuotationExpiryJob: Checking expired quotations for company {CompanyId}", args.CompanyId);

        var query = await _repository.GetQueryableAsync();
        var expiredQuotations = query
            .Where(q => q.CompanyId == args.CompanyId
                && q.Status == Core.DocumentStatus.Submitted
                && q.ValidUntil.HasValue
                && q.ValidUntil.Value < args.AsOfDate)
            .ToList();

        if (!expiredQuotations.Any())
        {
            _logger.LogInformation("QuotationExpiryJob: No expired quotations found");
            return;
        }

        var expired = 0;
        foreach (var quotation in expiredQuotations)
        {
            try
            {
                quotation.MarkLost();
                await _repository.UpdateAsync(quotation);
                expired++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "QuotationExpiryJob: Failed to expire quotation {Id}", quotation.Id);
            }
        }

        _logger.LogInformation("QuotationExpiryJob: Expired {Count} quotations for company {CompanyId}",
            expired, args.CompanyId);
    }
}

public class QuotationExpiryJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime AsOfDate { get; set; } = DateTime.UtcNow.Date;
}
