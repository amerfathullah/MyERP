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
        if (input.ValidUpto.HasValue && input.ValidFrom.HasValue && input.ValidUpto.Value < input.ValidFrom.Value)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.InvalidDateRange);
        }

        if (input.MaximumUse < 0 || input.MaximumUsePerCustomer < 0)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", "MaximumUse/MaximumUsePerCustomer");
        }

        // Validate pricing rule exists and is not disabled (ERPNext commit 1db5df0d6d)
        var rule = await _pricingRuleRepository.FindAsync(input.PricingRuleId);
        if (rule == null)
            throw new BusinessException("MyERP:03018").WithData("pricingRuleId", input.PricingRuleId);

        if (rule.IsDisabled)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Pricing Rule '{rule.Title}' is disabled.");
        }

        if (input.CouponType == CouponType.GiftCard && !input.CustomerId.HasValue)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Please select the customer for Gift Card coupon.");
        }

        var couponName = input.CouponName.Trim();
        var existingQuery = await _repository.GetQueryableAsync();

        // Generate code if not provided, avoiding collisions (ERPNext commit 8fa3f79311)
        var code = input.Code?.Trim();
        if (!string.IsNullOrWhiteSpace(code))
        {
            if (existingQuery.Any(x => x.Code == code))
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Coupon Code '{code}' already exists.");
            }
        }
        else if (input.CouponType == CouponType.GiftCard)
        {
            do
            {
                code = CouponCode.GenerateGiftCardCode();
            } while (existingQuery.Any(x => x.Code == code));
        }
        else
        {
            var prefix = CouponCode.GeneratePromotionalCode(couponName);
            code = prefix;
            while (string.IsNullOrEmpty(code) || existingQuery.Any(x => x.Code == code))
            {
                code = prefix + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
            }
        }

        var entity = new CouponCode(
            GuidGenerator.Create(),
            code,
            couponName,
            input.CouponType,
            input.PricingRuleId,
            CurrentTenant.Id);

        entity.CompanyId = input.CompanyId;
        entity.MaximumUse = input.CouponType == CouponType.GiftCard ? 1 : input.MaximumUse;
        entity.MaximumUsePerCustomer = input.MaximumUsePerCustomer;
        entity.ValidFrom = input.ValidFrom;
        entity.ValidUpto = input.ValidUpto;
        entity.CustomerId = input.CustomerId;
        entity.Description = input.Description;

        await _repository.InsertAsync(entity);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "CouponCode", entity.Id,
            "Created", entity.CompanyId ?? Guid.Empty,
            entity.Code, "Draft", "Active", CurrentUser.Id,
            $"Coupon code '{entity.Code}' created", CurrentTenant.Id));

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

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "CouponCode", entity.Id,
            entity.IsEnabled ? "Enabled" : "Disabled", entity.CompanyId ?? Guid.Empty,
            entity.Code, entity.IsEnabled ? "Disabled" : "Enabled", entity.IsEnabled ? "Enabled" : "Disabled", CurrentUser.Id,
            $"Coupon code '{entity.Code}' {(entity.IsEnabled ? "enabled" : "disabled")}", CurrentTenant.Id));
    }

    [Authorize(MyERPPermissions.SalesInvoices.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}
