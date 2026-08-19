using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Sales.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

[Authorize(MyERPPermissions.SalesInvoices.Default)]
public class PricingRuleAppService : ApplicationService, IPricingRuleAppService
{
    private readonly IRepository<PricingRule, Guid> _repository;

    public PricingRuleAppService(IRepository<PricingRule, Guid> repository) => _repository = repository;

    public async Task<PagedResultDto<PricingRuleDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var totalCount = query.Count();
        var items = query.OrderByDescending(r => r.Priority).ThenBy(r => r.Title)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<PricingRuleDto>(totalCount, items.Select(ObjectMapper.Map<PricingRule, PricingRuleDto>).ToList());
    }

    public async Task<PricingRuleDto> GetAsync(Guid id)
    {
        var rule = await _repository.GetAsync(id);
        return ObjectMapper.Map<PricingRule, PricingRuleDto>(rule);
    }

    [Authorize(MyERPPermissions.SalesInvoices.Create)]
    public async Task<PricingRuleDto> CreateAsync(CreatePricingRuleDto input)
    {
        if (input.ValidFrom.HasValue && input.ValidUpto.HasValue && input.ValidUpto.Value < input.ValidFrom.Value)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidDateRange);
        }

        if (input.DiscountPercentage < 0 || input.DiscountPercentage > 100)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidDiscountPercentage);
        }

        if (input.DiscountAmount < 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", "DiscountAmount");
        }

        if (input.Rate < 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", "Rate");
        }

        var rule = new PricingRule(GuidGenerator.Create(), input.Title, input.ApplyOn, input.RuleType, CurrentTenant.Id)
        {
            CompanyId = input.CompanyId,
            ApplicableFor = input.ApplicableFor,
            ApplyOnId = input.ApplyOnId,
            ApplyOnName = input.ApplyOnName,
            DiscountPercentage = input.DiscountPercentage,
            DiscountAmount = input.DiscountAmount,
            Rate = input.Rate,
            MinQty = input.MinQty,
            MaxQty = input.MaxQty,
            MinAmount = input.MinAmount,
            MaxAmount = input.MaxAmount,
            Priority = input.Priority,
            ValidFrom = input.ValidFrom,
            ValidUpto = input.ValidUpto,
        };
        await _repository.InsertAsync(rule);
        return ObjectMapper.Map<PricingRule, PricingRuleDto>(rule);
    }

    /// <summary>
    /// Apply pricing rules to a transaction line and return matching rules.
    /// Priority-based: highest priority first. Same priority + multiple matches = error.
    /// </summary>
    public async Task<List<PricingRuleResultDto>> ApplyAsync(ApplyPricingRuleDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var rules = query.Where(r => !r.IsDisabled).ToList();

        var matching = rules
            .Where(r => r.Matches(input.ItemId, input.ItemGroupId, input.Qty, input.Amount, input.TransactionDate))
            .OrderByDescending(r => r.Priority)
            .ToList();

        if (!matching.Any()) return new List<PricingRuleResultDto>();

        // Check for ambiguity at highest priority
        var topPriority = matching[0].Priority;
        var topRules = matching.Where(r => r.Priority == topPriority).ToList();
        if (topRules.Count > 1)
            throw new BusinessException(MyERPDomainErrorCodes.AmbiguousPricingRule)
                .WithData("priority", topPriority);

        return topRules.Select(r => new PricingRuleResultDto
        {
            RuleId = r.Id, Title = r.Title,
            RuleType = (int)r.RuleType,
            DiscountPercentage = r.DiscountPercentage,
            DiscountAmount = r.DiscountAmount,
            Rate = r.Rate,
            FreeItemId = r.FreeItemId,
            FreeItemQty = r.FreeItemQty,
        }).ToList();
    }

    [Authorize(MyERPPermissions.SalesInvoices.Delete)]
    public async Task DeleteAsync(Guid id) => await _repository.DeleteAsync(id);
}
