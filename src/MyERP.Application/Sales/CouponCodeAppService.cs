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
public class CouponCodeAppService : ApplicationService, ICouponCodeAppService
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
            items.Select(ObjectMapper.Map<CouponCode, CouponCodeDto>).ToList());
    }

    public async Task<CouponCodeDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return ObjectMapper.Map<CouponCode, CouponCodeDto>(entity);
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
        return ObjectMapper.Map<CouponCode, CouponCodeDto>(entity);
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
}
