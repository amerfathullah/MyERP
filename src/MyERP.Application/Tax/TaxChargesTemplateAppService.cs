using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Permissions;
using MyERP.Tax.Entities;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Tax;

/// <summary>
/// Manages Sales/Purchase Tax Charges Templates — reusable tax configurations for transactions.
/// Per ERPNext: auto-loaded into SO/PO/SI/PI when template is selected or default applies.
/// </summary>
[Authorize]
public class TaxChargesTemplateAppService : ApplicationService
{
    private readonly IRepository<TaxChargesTemplate, Guid> _repository;

    public TaxChargesTemplateAppService(IRepository<TaxChargesTemplate, Guid> repository)
    {
        _repository = repository;
    }

    /// <summary>Get all templates, optionally filtered by type and company.</summary>
    public async Task<PagedResultDto<TaxChargesTemplateDto>> GetListAsync(GetTaxTemplateListDto input)
    {
        var query = await _repository.GetQueryableAsync();

        if (input.CompanyId.HasValue)
            query = query.Where(t => t.CompanyId == input.CompanyId.Value);
        if (input.TemplateType.HasValue)
            query = query.Where(t => t.TemplateType == input.TemplateType.Value);
        if (!string.IsNullOrWhiteSpace(input.Filter))
            query = query.Where(t => t.Name.Contains(input.Filter));

        var totalCount = query.Count();
        var items = query
            .OrderBy(t => t.Name)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        return new PagedResultDto<TaxChargesTemplateDto>(totalCount, items.Select(MapToDto).ToList());
    }

    /// <summary>Get a single template with all rows.</summary>
    public async Task<TaxChargesTemplateDto> GetAsync(Guid id)
    {
        var template = await _repository.GetAsync(id, includeDetails: true);
        return MapToDto(template);
    }

    /// <summary>
    /// Get the default template for a transaction type in a company.
    /// Used to auto-populate tax rows when creating new invoices/orders.
    /// </summary>
    public async Task<TaxChargesTemplateDto?> GetDefaultAsync(Guid companyId, TaxTemplateType templateType, Guid? taxCategoryId = null)
    {
        var query = await _repository.GetQueryableAsync();
        var template = query
            .Where(t => t.CompanyId == companyId
                     && t.TemplateType == templateType
                     && t.IsDefault
                     && t.IsEnabled)
            .Where(t => taxCategoryId == null || t.TaxCategoryId == taxCategoryId)
            .FirstOrDefault();

        return template != null ? MapToDto(template) : null;
    }

    /// <summary>
    /// Get active templates for dropdown selection in transaction forms.
    /// Per DO-NOT: disabled templates cannot be used on new transactions.
    /// </summary>
    public async Task<List<TaxChargesTemplateDto>> GetActiveTemplatesAsync(Guid companyId, TaxTemplateType templateType)
    {
        var query = await _repository.GetQueryableAsync();
        return query
            .Where(t => t.CompanyId == companyId && t.TemplateType == templateType && t.IsEnabled)
            .OrderBy(t => t.Name)
            .ToList()
            .Select(MapToDto)
            .ToList();
    }

    /// <summary>Create a new tax template.</summary>
    [Authorize(MyERPPermissions.TaxTemplates.Create)]
    public async Task<TaxChargesTemplateDto> CreateAsync(CreateTaxChargesTemplateDto input)
    {
        var template = new TaxChargesTemplate(
            GuidGenerator.Create(), input.CompanyId, input.Name, input.TemplateType, CurrentTenant.Id)
        {
            TaxCategoryId = input.TaxCategoryId,
            IsDefault = input.IsDefault,
            IsEnabled = true,
        };

        foreach (var row in input.Rows)
        {
            template.AddRow(new TaxChargesTemplateRow(
                GuidGenerator.Create(), template.Id, row.ChargeType, row.Rate, row.AccountId, row.Description)
            {
                TaxCategory = row.TaxCategory ?? "Total",
                ReferenceRowIndex = row.ReferenceRowIndex,
                IncludedInPrintRate = row.IncludedInPrintRate,
                AccountName = row.AccountName,
                CostCenterId = row.CostCenterId,
            });
        }

        template.Validate();

        // Enforce unique default per (company, template_type, tax_category)
        if (template.IsDefault)
            await ClearOtherDefaultsAsync(template);

        await _repository.InsertAsync(template);
        return MapToDto(template);
    }

    /// <summary>Update an existing tax template.</summary>
    [Authorize(MyERPPermissions.TaxTemplates.Edit)]
    public async Task<TaxChargesTemplateDto> UpdateAsync(Guid id, CreateTaxChargesTemplateDto input)
    {
        var template = await _repository.GetAsync(id, includeDetails: true);

        template.Name = input.Name;
        template.TaxCategoryId = input.TaxCategoryId;
        template.IsDefault = input.IsDefault;

        template.ClearRows();
        foreach (var row in input.Rows)
        {
            template.AddRow(new TaxChargesTemplateRow(
                GuidGenerator.Create(), template.Id, row.ChargeType, row.Rate, row.AccountId, row.Description)
            {
                TaxCategory = row.TaxCategory ?? "Total",
                ReferenceRowIndex = row.ReferenceRowIndex,
                IncludedInPrintRate = row.IncludedInPrintRate,
                AccountName = row.AccountName,
                CostCenterId = row.CostCenterId,
            });
        }

        template.Validate();

        if (template.IsDefault)
            await ClearOtherDefaultsAsync(template);

        await _repository.UpdateAsync(template);
        return MapToDto(template);
    }

    /// <summary>Delete a tax template.</summary>
    [Authorize(MyERPPermissions.TaxTemplates.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    /// <summary>Toggle template enabled/disabled state.</summary>
    [Authorize(MyERPPermissions.TaxTemplates.Edit)]
    public async Task<TaxChargesTemplateDto> ToggleEnabledAsync(Guid id)
    {
        var template = await _repository.GetAsync(id);
        template.IsEnabled = !template.IsEnabled;
        if (!template.IsEnabled) template.IsDefault = false; // Disabled cannot be default
        await _repository.UpdateAsync(template);
        return MapToDto(template);
    }

    private async Task ClearOtherDefaultsAsync(TaxChargesTemplate template)
    {
        var query = await _repository.GetQueryableAsync();
        var otherDefaults = query
            .Where(t => t.CompanyId == template.CompanyId
                     && t.TemplateType == template.TemplateType
                     && t.IsDefault
                     && t.Id != template.Id
                     && (template.TaxCategoryId == null || t.TaxCategoryId == template.TaxCategoryId))
            .ToList();

        foreach (var other in otherDefaults)
        {
            other.IsDefault = false;
            await _repository.UpdateAsync(other);
        }
    }

    private static TaxChargesTemplateDto MapToDto(TaxChargesTemplate t) => new()
    {
        Id = t.Id,
        CompanyId = t.CompanyId,
        Name = t.Name,
        TemplateType = t.TemplateType,
        TaxCategoryId = t.TaxCategoryId,
        IsDefault = t.IsDefault,
        IsEnabled = t.IsEnabled,
        Rows = t.Rows.OrderBy(r => r.RowIndex).Select(r => new TaxChargesTemplateRowDto
        {
            Id = r.Id,
            RowIndex = r.RowIndex,
            ChargeType = r.ChargeType,
            Rate = r.Rate,
            AccountId = r.AccountId,
            AccountName = r.AccountName,
            TaxCategory = r.TaxCategory,
            ReferenceRowIndex = r.ReferenceRowIndex,
            IncludedInPrintRate = r.IncludedInPrintRate,
            Description = r.Description,
            CostCenterId = r.CostCenterId,
        }).ToList(),
    };
}

// --- DTOs ---

public class GetTaxTemplateListDto : PagedAndSortedResultRequestDto
{
    public Guid? CompanyId { get; set; }
    public TaxTemplateType? TemplateType { get; set; }
    public string? Filter { get; set; }
}

public class TaxChargesTemplateDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = "";
    public TaxTemplateType TemplateType { get; set; }
    public Guid? TaxCategoryId { get; set; }
    public bool IsDefault { get; set; }
    public bool IsEnabled { get; set; }
    public List<TaxChargesTemplateRowDto> Rows { get; set; } = new();
}

public class TaxChargesTemplateRowDto
{
    public Guid Id { get; set; }
    public int RowIndex { get; set; }
    public string ChargeType { get; set; } = "On Net Total";
    public decimal Rate { get; set; }
    public Guid? AccountId { get; set; }
    public string? AccountName { get; set; }
    public string TaxCategory { get; set; } = "Total";
    public int? ReferenceRowIndex { get; set; }
    public bool IncludedInPrintRate { get; set; }
    public string? Description { get; set; }
    public Guid? CostCenterId { get; set; }
}

public class CreateTaxChargesTemplateDto
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = "";
    public TaxTemplateType TemplateType { get; set; }
    public Guid? TaxCategoryId { get; set; }
    public bool IsDefault { get; set; }
    public List<CreateTaxChargesTemplateRowDto> Rows { get; set; } = new();
}

public class CreateTaxChargesTemplateRowDto
{
    public string ChargeType { get; set; } = "On Net Total";
    public decimal Rate { get; set; }
    public Guid? AccountId { get; set; }
    public string? AccountName { get; set; }
    public string? TaxCategory { get; set; }
    public int? ReferenceRowIndex { get; set; }
    public bool IncludedInPrintRate { get; set; }
    public string? Description { get; set; }
    public Guid? CostCenterId { get; set; }
}
