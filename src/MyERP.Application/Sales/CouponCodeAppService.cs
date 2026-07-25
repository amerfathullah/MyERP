using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

/// <summary>
/// Application service for Coupon Code management.
/// Coupons link to Pricing Rules to provide discounts.
/// </summary>
[Authorize(MyERPPermissions.SalesInvoices.Default)]
public class CouponCodeAppService : ApplicationService
{
    private readonly IRepository<CouponCode, Guid> _repository;
    private readonly IRepository<PricingRule, Guid> _pricingRuleRepository;

    public CouponCodeAppService(
        IRepository<CouponCode, Guid> repository,
        IRepository<PricingRule, Guid> pricingRuleRepository)
    {
        _repository = repository;
        _pricingRuleRepository = pricingRuleRepository;
    }

    public async Task<PagedResultDto<CouponCodeDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var totalCount = query.Count();
        var items = query
            .OrderByDescending(x => x.CreationTime)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        return new PagedResultDto<CouponCodeDto>(
            totalCount,
            items.Select(MapToDto).ToList());
    }

    public async Task<CouponCodeDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return MapToDto(entity);
    }

    [Authorize(MyERPPermissions.SalesInvoices.Create)]
    public async Task<CouponCodeDto> CreateAsync(CreateCouponCodeDto input)
    {
        // Validate pricing rule exists
        var rule = await _pricingRuleRepository.FindAsync(input.PricingRuleId);
        if (rule == null)
            throw new BusinessException("MyERP:03018").WithData("pricingRuleId", input.PricingRuleId);

        // Generate code if not provided
        var code = input.Code;
        if (string.IsNullOrWhiteSpace(code))
        {
            code = input.CouponType == CouponType.GiftCard
                ? CouponCode.GenerateGiftCardCode()
                : CouponCode.GeneratePromotionalCode(input.CouponName);
        }

        var entity = new CouponCode(
            GuidGenerator.Create(),
            code,
            input.CouponName,
            input.CouponType,
            input.PricingRuleId,
            CurrentTenant.Id);

        entity.CompanyId = input.CompanyId;
        entity.MaximumUse = input.MaximumUse;
        entity.MaximumUsePerCustomer = input.MaximumUsePerCustomer;
        entity.ValidFrom = input.ValidFrom;
        entity.ValidUpto = input.ValidUpto;
        entity.CustomerId = input.CustomerId;
        entity.Description = input.Description;

        await _repository.InsertAsync(entity);
        return MapToDto(entity);
    }

    /// <summary>Validate and record coupon usage (called during SO/SI creation).</summary>
    public async Task<Guid> ValidateAndApplyAsync(string couponCode, Guid? customerId, DateTime transactionDate)
    {
        var query = await _repository.GetQueryableAsync();
        var coupon = query.FirstOrDefault(x => x.Code == couponCode);

        if (coupon == null)
            throw new BusinessException("MyERP:03019").WithData("couponCode", couponCode);

        if (!coupon.IsValid(transactionDate, customerId))
            throw new BusinessException("MyERP:03020").WithData("couponCode", couponCode);

        coupon.RecordUse();
        await _repository.UpdateAsync(coupon);

        return coupon.PricingRuleId;
    }

    /// <summary>Reverse coupon usage (called on document cancel).</summary>
    public async Task ReverseUsageAsync(string couponCode)
    {
        var query = await _repository.GetQueryableAsync();
        var coupon = query.FirstOrDefault(x => x.Code == couponCode);
        if (coupon != null)
        {
            coupon.ReverseUse();
            await _repository.UpdateAsync(coupon);
        }
    }

    [Authorize(MyERPPermissions.SalesInvoices.Edit)]
    public async Task ToggleAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        entity.IsEnabled = !entity.IsEnabled;
        await _repository.UpdateAsync(entity);
    }

    [Authorize(MyERPPermissions.SalesInvoices.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    private static CouponCodeDto MapToDto(CouponCode e) => new()
    {
        Id = e.Id,
        Code = e.Code,
        CouponName = e.CouponName,
        CouponType = e.CouponType,
        PricingRuleId = e.PricingRuleId,
        MaximumUse = e.MaximumUse,
        Used = e.Used,
        IsEnabled = e.IsEnabled,
        ValidFrom = e.ValidFrom,
        ValidUpto = e.ValidUpto,
        CustomerId = e.CustomerId,
        Description = e.Description
    };
}

public class CouponCodeDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public string CouponName { get; set; } = null!;
    public CouponType CouponType { get; set; }
    public Guid PricingRuleId { get; set; }
    public int MaximumUse { get; set; }
    public int Used { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidUpto { get; set; }
    public Guid? CustomerId { get; set; }
    public string? Description { get; set; }
}

public class CreateCouponCodeDto
{
    public string? Code { get; set; }
    public string CouponName { get; set; } = null!;
    public CouponType CouponType { get; set; }
    public Guid PricingRuleId { get; set; }
    public Guid? CompanyId { get; set; }
    public int MaximumUse { get; set; }
    public int MaximumUsePerCustomer { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidUpto { get; set; }
    public Guid? CustomerId { get; set; }
    public string? Description { get; set; }
}
