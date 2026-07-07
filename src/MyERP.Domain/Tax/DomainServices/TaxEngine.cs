using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Tax.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Tax.DomainServices;

/// <summary>
/// Calculates tax based on configurable rules. NEVER hardcode rates.
/// </summary>
public class TaxEngine : DomainService
{
    private readonly IRepository<TaxRule, Guid> _taxRuleRepository;
    private readonly IRepository<TaxCategory, Guid> _taxCategoryRepository;

    public TaxEngine(
        IRepository<TaxRule, Guid> taxRuleRepository,
        IRepository<TaxCategory, Guid> taxCategoryRepository)
    {
        _taxRuleRepository = taxRuleRepository;
        _taxCategoryRepository = taxCategoryRepository;
    }

    /// <summary>
    /// Calculate tax for a given amount based on tax category and transaction date.
    /// </summary>
    public async Task<TaxCalculationResult> CalculateAsync(TaxCalculationContext context)
    {
        var rules = await GetApplicableRulesAsync(
            context.TaxCategoryId,
            context.TransactionDate,
            context.ItemGroup,
            context.Region);

        if (!rules.Any())
        {
            throw new BusinessException(MyERPDomainErrorCodes.NoApplicableTaxRule)
                .WithData("taxCategoryId", context.TaxCategoryId)
                .WithData("date", context.TransactionDate);
        }

        // Use highest priority matching rule
        var rule = rules.OrderByDescending(r => r.Priority).First();

        var taxAmount = Math.Round(context.TaxableAmount * rule.Rate / 100m, 2);

        return new TaxCalculationResult
        {
            TaxableAmount = context.TaxableAmount,
            TaxRate = rule.Rate,
            TaxAmount = taxAmount,
            TotalAmount = context.TaxableAmount + taxAmount,
            AppliedTaxRuleId = rule.Id,
            TaxCategoryId = context.TaxCategoryId
        };
    }

    /// <summary>
    /// Get applicable tax rules based on filters and date range.
    /// </summary>
    private async Task<List<TaxRule>> GetApplicableRulesAsync(
        Guid taxCategoryId,
        DateTime transactionDate,
        string? itemGroup,
        string? region)
    {
        var allRules = await _taxRuleRepository.GetListAsync(r =>
            r.TaxCategoryId == taxCategoryId &&
            r.IsActive &&
            r.EffectiveFrom <= transactionDate &&
            (r.EffectiveTo == null || r.EffectiveTo >= transactionDate));

        // Apply optional filters
        return allRules.Where(r =>
            (string.IsNullOrEmpty(r.ItemGroupFilter) || r.ItemGroupFilter == itemGroup) &&
            (string.IsNullOrEmpty(r.RegionFilter) || r.RegionFilter == region))
            .ToList();
    }
}

/// <summary>Input context for tax calculation.</summary>
public class TaxCalculationContext
{
    public Guid TaxCategoryId { get; set; }
    public decimal TaxableAmount { get; set; }
    public DateTime TransactionDate { get; set; }
    public string? ItemGroup { get; set; }
    public string? Region { get; set; }
}

/// <summary>Result of tax calculation.</summary>
public class TaxCalculationResult
{
    public decimal TaxableAmount { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public Guid AppliedTaxRuleId { get; set; }
    public Guid TaxCategoryId { get; set; }
}
