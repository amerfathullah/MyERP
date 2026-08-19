using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.Purchasing.Entities;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Purchasing.BackgroundJobs;

/// <summary>
/// Background job that marks expired supplier quotations as Cancelled/Expired.
/// Per ERPNext: supplier_quotation.set_expired_status (daily scheduler).
/// </summary>
public class SupplierQuotationExpiryJob : AsyncBackgroundJob<SupplierQuotationExpiryJobArgs>, ITransientDependency
{
    private readonly IRepository<SupplierQuotation, Guid> _repository;
    private readonly ILogger<SupplierQuotationExpiryJob> _logger;

    public SupplierQuotationExpiryJob(
        IRepository<SupplierQuotation, Guid> repository,
        ILogger<SupplierQuotationExpiryJob> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public override async Task ExecuteAsync(SupplierQuotationExpiryJobArgs args)
    {
        var asOfDate = args.AsOfDate ?? DateTime.UtcNow.Date;
        _logger.LogInformation("SupplierQuotationExpiryJob: Checking expired supplier quotations for company {CompanyId} as of {Date}",
            args.CompanyId, asOfDate);

        var query = await _repository.GetQueryableAsync();
        var expiredQuotations = query
            .Where(q => q.CompanyId == args.CompanyId &&
                        q.Status == Core.DocumentStatus.Submitted &&
                        q.ValidTill.HasValue &&
                        q.ValidTill.Value < asOfDate)
            .ToList();

        if (!expiredQuotations.Any())
        {
            _logger.LogInformation("SupplierQuotationExpiryJob: No expired supplier quotations found");
            return;
        }

        var expiredCount = 0;
        foreach (var quotation in expiredQuotations)
        {
            try
            {
                quotation.Cancel();
                await _repository.UpdateAsync(quotation);
                expiredCount++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SupplierQuotationExpiryJob: Failed to expire supplier quotation {Id}", quotation.Id);
            }
        }

        _logger.LogInformation("SupplierQuotationExpiryJob: Expired {Count} supplier quotations for company {CompanyId}",
            expiredCount, args.CompanyId);
    }
}

public class SupplierQuotationExpiryJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime? AsOfDate { get; set; }
}
