using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

[Authorize(MyERPPermissions.ShippingRules.Default)]
public class ShippingRuleAppService : ApplicationService
{
    private readonly IRepository<ShippingRule, Guid> _repository;

    public ShippingRuleAppService(IRepository<ShippingRule, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<ShippingRuleDto> GetAsync(Guid id)
    {
        var rule = await _repository.GetAsync(id);
        return ObjectMapper.Map<ShippingRule, ShippingRuleDto>(rule);
    }

    public async Task<PagedResultDto<ShippingRuleDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var count = query.Count();
        var list = query.OrderBy(x => x.Label)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<ShippingRuleDto>(count, list.Select(ObjectMapper.Map<ShippingRule, ShippingRuleDto>).ToList());
    }

    [Authorize(MyERPPermissions.ShippingRules.Create)]
    public async Task<ShippingRuleDto> CreateAsync(CreateShippingRuleDto input)
    {
        var rule = new ShippingRule(
            GuidGenerator.Create(),
            input.Label,
            input.RuleType,
            input.CalculationMode,
            input.AccountId,
            input.CompanyId,
            CurrentTenant.Id);

        rule.FixedAmount = input.FixedAmount;
        rule.IsEnabled = input.IsEnabled;
        rule.CostCenterId = input.CostCenterId;
        rule.ProjectId = input.ProjectId;

        foreach (var cond in input.Conditions)
        {
            rule.AddCondition(cond.FromValue, cond.ToValue, cond.ShippingAmount);
        }

        foreach (var country in input.Countries)
        {
            rule.AddCountry(country);
        }

        rule.Validate();
        await _repository.InsertAsync(rule);
        return ObjectMapper.Map<ShippingRule, ShippingRuleDto>(rule);
    }

    [Authorize(MyERPPermissions.ShippingRules.Edit)]
    public async Task<ShippingRuleDto> ToggleAsync(Guid id, bool isEnabled)
    {
        var rule = await _repository.GetAsync(id);
        rule.IsEnabled = isEnabled;
        await _repository.UpdateAsync(rule);
        return ObjectMapper.Map<ShippingRule, ShippingRuleDto>(rule);
    }

    [Authorize(MyERPPermissions.ShippingRules.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    /// <summary>
    /// Calculate shipping charge for a given value and country (preview for UI).
    /// </summary>
    public async Task<decimal> CalculateAsync(Guid ruleId, decimal value, string? countryCode = null)
    {
        var rule = await _repository.GetAsync(ruleId);

        if (countryCode != null && !rule.AppliesToCountry(countryCode))
            return 0;

        return rule.Calculate(value);
    }
}

#region DTOs

public class ShippingRuleDto
{
    public Guid Id { get; set; }
    public string Label { get; set; } = null!;
    public Guid CompanyId { get; set; }
    public string CalculationMode { get; set; } = null!;
    public string RuleType { get; set; } = null!;
    public decimal ShippingAmount { get; set; }
    public bool IsEnabled { get; set; }
    public Guid? CostCenterId { get; set; }
    public Guid? ProjectId { get; set; }
    public List<ShippingConditionDto> Conditions { get; set; } = new();
    public List<string> Countries { get; set; } = new();
}

public class ShippingConditionDto
{
    public decimal FromValue { get; set; }
    public decimal ToValue { get; set; }
    public decimal ShippingAmount { get; set; }
}

public class CreateShippingRuleDto
{
    public string Label { get; set; } = null!;
    public Guid CompanyId { get; set; }
    public Guid AccountId { get; set; }
    public Guid? CostCenterId { get; set; }
    public Guid? ProjectId { get; set; }
    public ShippingCalculationMode CalculationMode { get; set; }
    public ShippingRuleType RuleType { get; set; }
    public decimal FixedAmount { get; set; }
    public bool IsEnabled { get; set; } = true;
    public List<ShippingConditionDto> Conditions { get; set; } = new();
    public List<string> Countries { get; set; } = new();
}

#endregion
