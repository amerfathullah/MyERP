using System;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using Microsoft.Extensions.Caching.Distributed;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Core.DomainServices;

/// <summary>
/// Cached company settings lookup — reduces database load for the most frequently
/// accessed data in the system. Company settings are read on virtually every transaction.
/// 
/// Cache invalidation: when Company entity is updated, the cache entry is evicted.
/// TTL: 5 minutes (balance between freshness and performance).
/// </summary>
public class CompanySettingsCache : ITransientDependency
{
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IDistributedCache<CompanySettingsCacheItem> _cache;

    public CompanySettingsCache(
        IRepository<Company, Guid> companyRepository,
        IDistributedCache<CompanySettingsCacheItem> cache)
    {
        _companyRepository = companyRepository;
        _cache = cache;
    }

    /// <summary>
    /// Get company settings from cache or database.
    /// </summary>
    public async Task<CompanySettingsCacheItem> GetAsync(Guid companyId)
    {
        var cacheKey = $"company:{companyId}";

        return await _cache.GetOrAddAsync(
            cacheKey,
            async () =>
            {
                var company = await _companyRepository.GetAsync(companyId);
                return new CompanySettingsCacheItem
                {
                    Id = company.Id,
                    Name = company.Name,
                    CurrencyCode = company.CurrencyCode,
                    FiscalYearStartMonth = company.FiscalYearStartMonth,
                    StockFrozenUpto = company.StockFrozenUpto,
                    AccountsFrozenTillDate = company.AccountsFrozenTillDate,
                };
            },
            () => new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            });
    }

    /// <summary>
    /// Invalidate cache when company settings change.
    /// Call this from CompanyAppService.UpdateAsync().
    /// </summary>
    public async Task InvalidateAsync(Guid companyId)
    {
        await _cache.RemoveAsync($"company:{companyId}");
    }
}

/// <summary>
/// Cacheable subset of Company data — only the fields needed for transaction validation.
/// </summary>
[Serializable]
public class CompanySettingsCacheItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string CurrencyCode { get; set; } = "MYR";
    public int FiscalYearStartMonth { get; set; }
    public DateTime? StockFrozenUpto { get; set; }
    public DateTime? AccountsFrozenTillDate { get; set; }
}
