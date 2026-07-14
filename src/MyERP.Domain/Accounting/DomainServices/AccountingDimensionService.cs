using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Accounting.DomainServices;

/// <summary>
/// Validates and manages accounting dimensions on GL entries.
/// ERPNext equivalent: accounts/doctype/accounting_dimension/accounting_dimension.py
/// 
/// Responsibilities:
/// 1. Validate mandatory dimensions are set on GL lines before posting
/// 2. Validate dimension values against allow/restrict filters per account
/// 3. Provide enabled dimensions list for transaction forms
/// </summary>
public class AccountingDimensionService : DomainService
{
    private readonly IRepository<AccountingDimension, Guid> _dimensionRepository;
    private readonly IRepository<AccountingDimensionFilter, Guid> _filterRepository;
    private readonly IRepository<GlDimensionValue, Guid> _glDimensionValueRepository;

    public AccountingDimensionService(
        IRepository<AccountingDimension, Guid> dimensionRepository,
        IRepository<AccountingDimensionFilter, Guid> filterRepository,
        IRepository<GlDimensionValue, Guid> glDimensionValueRepository)
    {
        _dimensionRepository = dimensionRepository;
        _filterRepository = filterRepository;
        _glDimensionValueRepository = glDimensionValueRepository;
    }

    /// <summary>
    /// Returns all enabled dimensions, optionally scoped by company.
    /// Global dimensions (CompanyId=null) are always included.
    /// </summary>
    public async Task<List<AccountingDimension>> GetEnabledDimensionsAsync(Guid? companyId = null)
    {
        var all = await _dimensionRepository.GetListAsync(d => d.IsEnabled);

        if (companyId == null)
            return all;

        // Global (no company) + company-specific dimensions
        return all.Where(d => d.CompanyId == null || d.CompanyId == companyId).ToList();
    }

    /// <summary>
    /// Returns only mandatory dimensions for a company.
    /// </summary>
    public async Task<List<AccountingDimension>> GetMandatoryDimensionsAsync(Guid? companyId = null)
    {
        var enabled = await GetEnabledDimensionsAsync(companyId);
        return enabled.Where(d => d.IsMandatory).ToList();
    }

    /// <summary>
    /// Validates that all mandatory dimensions are set on GL entry lines.
    /// Called before JournalEntry.Post() in the DocumentPostingOrchestrator.
    /// 
    /// Per ERPNext: mandatory dimensions must be filled on every GL line.
    /// Cost center is special: only mandatory on P&L accounts (not Balance Sheet).
    /// </summary>
    public async Task ValidateMandatoryDimensionsAsync(
        Guid companyId,
        IReadOnlyList<JournalEntryLine> lines,
        IReadOnlyList<GlDimensionValue>? dimensionValues = null)
    {
        var mandatoryDimensions = await GetMandatoryDimensionsAsync(companyId);
        if (!mandatoryDimensions.Any()) return;

        // Build lookup: lineId → set of filled dimension field names
        var filledDimensions = new Dictionary<Guid, HashSet<string>>();

        if (dimensionValues != null)
        {
            foreach (var dv in dimensionValues)
            {
                if (!filledDimensions.ContainsKey(dv.JournalEntryLineId))
                    filledDimensions[dv.JournalEntryLineId] = new HashSet<string>();
                filledDimensions[dv.JournalEntryLineId].Add(dv.DimensionFieldName);
            }
        }

        // Also check built-in dimension fields on JournalEntryLine itself
        foreach (var line in lines)
        {
            if (!filledDimensions.ContainsKey(line.Id))
                filledDimensions[line.Id] = new HashSet<string>();

            if (line.CostCenterId.HasValue)
                filledDimensions[line.Id].Add("cost_center_id");
            if (line.ProjectId.HasValue)
                filledDimensions[line.Id].Add("project_id");
        }

        // Validate each line has all mandatory dimensions
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var filled = filledDimensions.GetValueOrDefault(line.Id) ?? new HashSet<string>();

            foreach (var dim in mandatoryDimensions)
            {
                if (!filled.Contains(dim.FieldName))
                {
                    throw new BusinessException(MyERPDomainErrorCodes.MandatoryDimensionMissing)
                        .WithData("dimension", dim.Label)
                        .WithData("lineIndex", i + 1)
                        .WithData("accountId", line.AccountId);
                }
            }
        }
    }

    /// <summary>
    /// Validates dimension values against allow/restrict filters for specific accounts.
    /// Per ERPNext: certain accounts can only use specific dimension values.
    /// </summary>
    public async Task ValidateDimensionFiltersAsync(
        Guid companyId,
        IReadOnlyList<JournalEntryLine> lines,
        IReadOnlyList<GlDimensionValue>? dimensionValues = null)
    {
        if (dimensionValues == null || !dimensionValues.Any()) return;

        // Get all filters for this company
        var filters = await _filterRepository.GetListAsync(f => f.CompanyId == companyId);
        if (!filters.Any()) return;

        // Build lookup: (accountId, dimensionId) → filter
        var filterLookup = filters.ToDictionary(f => (f.AccountId, f.AccountingDimensionId));

        foreach (var dv in dimensionValues)
        {
            var line = lines.FirstOrDefault(l => l.Id == dv.JournalEntryLineId);
            if (line == null) continue;

            var key = (line.AccountId, dv.AccountingDimensionId);
            if (filterLookup.TryGetValue(key, out var filter))
            {
                if (!filter.IsValuePermitted(dv.DimensionValueId))
                {
                    throw new BusinessException(MyERPDomainErrorCodes.DimensionValueRestricted)
                        .WithData("dimensionField", dv.DimensionFieldName)
                        .WithData("accountId", line.AccountId)
                        .WithData("valueId", dv.DimensionValueId);
                }
            }
        }
    }

    /// <summary>
    /// Saves dimension values for a posted journal entry's lines.
    /// Called after successful posting in the orchestrator.
    /// </summary>
    public async Task SaveDimensionValuesAsync(IReadOnlyList<GlDimensionValue> values)
    {
        if (values == null || !values.Any()) return;

        foreach (var value in values)
        {
            await _glDimensionValueRepository.InsertAsync(value);
        }
    }
}
