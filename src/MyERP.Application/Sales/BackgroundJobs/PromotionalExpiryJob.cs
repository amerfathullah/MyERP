using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.Sales.Entities;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales.BackgroundJobs;

/// <summary>
/// Background job that automatically disables expired promotional coupons and promotional schemes.
/// Per ERPNext: coupon_code.update_coupon_code_status and promotional_scheme scheduler (daily).
/// </summary>
public class PromotionalExpiryJob : AsyncBackgroundJob<PromotionalExpiryJobArgs>, ITransientDependency
{
    private readonly IRepository<CouponCode, Guid> _couponRepository;
    private readonly IRepository<PromotionalScheme, Guid> _schemeRepository;
    private readonly ILogger<PromotionalExpiryJob> _logger;

    public PromotionalExpiryJob(
        IRepository<CouponCode, Guid> couponRepository,
        IRepository<PromotionalScheme, Guid> schemeRepository,
        ILogger<PromotionalExpiryJob> logger)
    {
        _couponRepository = couponRepository;
        _schemeRepository = schemeRepository;
        _logger = logger;
    }

    public override async Task ExecuteAsync(PromotionalExpiryJobArgs args)
    {
        var asOfDate = args.AsOfDate ?? DateTime.UtcNow.Date;
        _logger.LogInformation("PromotionalExpiryJob: Checking expired promotional items for company {CompanyId} as of {Date}",
            args.CompanyId, asOfDate);

        // 1. Expire coupon codes
        var couponQuery = await _couponRepository.GetQueryableAsync();
        var expiredCoupons = couponQuery
            .Where(c => (c.CompanyId == null || c.CompanyId == args.CompanyId) &&
                        c.IsEnabled &&
                        ((c.ValidUpto.HasValue && c.ValidUpto.Value.Date < asOfDate.Date) ||
                         (c.MaximumUse > 0 && c.Used >= c.MaximumUse)))
            .ToList();

        var expiredCouponsCount = 0;
        foreach (var coupon in expiredCoupons)
        {
            coupon.IsEnabled = false;
            await _couponRepository.UpdateAsync(coupon);
            expiredCouponsCount++;
        }

        // 2. Expire promotional schemes
        var schemeQuery = await _schemeRepository.GetQueryableAsync();
        var expiredSchemes = schemeQuery
            .Where(s => s.CompanyId == args.CompanyId &&
                        !s.IsDisabled &&
                        s.ValidUpto.HasValue &&
                        s.ValidUpto.Value.Date < asOfDate.Date)
            .ToList();

        var expiredSchemesCount = 0;
        foreach (var scheme in expiredSchemes)
        {
            scheme.IsDisabled = true;
            await _schemeRepository.UpdateAsync(scheme);
            expiredSchemesCount++;
        }

        _logger.LogInformation("PromotionalExpiryJob: Disabled {Coupons} expired coupons and {Schemes} expired schemes for company {CompanyId}",
            expiredCouponsCount, expiredSchemesCount, args.CompanyId);
    }
}

public class PromotionalExpiryJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime? AsOfDate { get; set; }
}
