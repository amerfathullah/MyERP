using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Sales.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

public class PricingRuleDto : EntityDto<Guid>
{
    public string Title { get; set; } = null!;
    public string ApplicableFor { get; set; } = null!;
    public int ApplyOn { get; set; }
    public Guid? ApplyOnId { get; set; }
    public string? ApplyOnName { get; set; }
    public int RuleType { get; set; }
    public decimal DiscountPercentage { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal Rate { get; set; }
    public decimal MinQty { get; set; }
    public decimal MaxQty { get; set; }
    public decimal MinAmount { get; set; }
    public decimal MaxAmount { get; set; }
    public int Priority { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidUpto { get; set; }
    public bool IsDisabled { get; set; }
}

public class CreatePricingRuleDto
{
    public string Title { get; set; } = null!;
    public string ApplicableFor { get; set; } = "Selling";
    public PricingRuleApplyOn ApplyOn { get; set; }
    public Guid? ApplyOnId { get; set; }
    public string? ApplyOnName { get; set; }
    public PricingRuleType RuleType { get; set; }
    public decimal DiscountPercentage { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal Rate { get; set; }
    public decimal MinQty { get; set; }
    public decimal MaxQty { get; set; }
    public decimal MinAmount { get; set; }
    public decimal MaxAmount { get; set; }
    public int Priority { get; set; } = 1;
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidUpto { get; set; }
    public Guid? CompanyId { get; set; }
}

/// <summary>
/// Applies pricing rules to a transaction context and returns matching discounts.
/// </summary>
public class ApplyPricingRuleDto
{
    public Guid? ItemId { get; set; }
    public Guid? ItemGroupId { get; set; }
    public decimal Qty { get; set; }
    public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; }
}

public class PricingRuleResultDto
{
    public Guid RuleId { get; set; }
    public string Title { get; set; } = null!;
    public int RuleType { get; set; }
    public decimal DiscountPercentage { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal Rate { get; set; }
    public Guid? FreeItemId { get; set; }
    public decimal FreeItemQty { get; set; }
}

[Authorize(MyERPPermissions.SalesInvoices.Default)]
public class PricingRuleAppService : ApplicationService
{
    private readonly IRepository<PricingRule, Guid> _repository;

    public PricingRuleAppService(IRepository<PricingRule, Guid> repository) => _repository = repository;

    public async Task<PagedResultDto<PricingRuleDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var totalCount = query.Count();
        var items = query.OrderByDescending(r => r.Priority).ThenBy(r => r.Title)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<PricingRuleDto>(totalCount, items.Select(MapToDto).ToList());
    }

    [Authorize(MyERPPermissions.SalesInvoices.Create)]
    public async Task<PricingRuleDto> CreateAsync(CreatePricingRuleDto input)
    {
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
        return MapToDto(rule);
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
            throw new Volo.Abp.BusinessException("MyERP:11001")
                .WithData("detail", $"Multiple pricing rules at priority {topPriority} match. Resolve ambiguity.");

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

    private static PricingRuleDto MapToDto(PricingRule r) => new()
    {
        Id = r.Id, Title = r.Title, ApplicableFor = r.ApplicableFor,
        ApplyOn = (int)r.ApplyOn, ApplyOnId = r.ApplyOnId, ApplyOnName = r.ApplyOnName,
        RuleType = (int)r.RuleType, DiscountPercentage = r.DiscountPercentage,
        DiscountAmount = r.DiscountAmount, Rate = r.Rate,
        MinQty = r.MinQty, MaxQty = r.MaxQty,
        MinAmount = r.MinAmount, MaxAmount = r.MaxAmount,
        Priority = r.Priority, ValidFrom = r.ValidFrom, ValidUpto = r.ValidUpto,
        IsDisabled = r.IsDisabled,
    };
}
