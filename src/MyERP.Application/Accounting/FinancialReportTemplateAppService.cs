using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

// ─── DTOs ───────────────────────────────────────────────────────────────────

public class FinancialReportTemplateDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public FinancialReportType ReportType { get; set; }
    public Guid? CompanyId { get; set; }
    public bool IsStandard { get; set; }
    public bool IsEnabled { get; set; }
    public string? Description { get; set; }
    public List<FinancialReportRowDto> Rows { get; set; } = new();
}

public class FinancialReportRowDto
{
    public Guid Id { get; set; }
    public string Label { get; set; } = null!;
    public FinancialReportDataSource DataSource { get; set; }
    public int SortOrder { get; set; }
    public string? ReferenceCode { get; set; }
    public string? CalculationFormula { get; set; }
    public string? AccountCategoryFilter { get; set; }
    public string? CustomApiPath { get; set; }
    public bool HideWhenEmpty { get; set; }
    public bool IsBold { get; set; }
    public int IndentLevel { get; set; }
    public int SignMultiplier { get; set; } = 1;
}

public class CreateFinancialReportTemplateDto
{
    public string Name { get; set; } = null!;
    public FinancialReportType ReportType { get; set; }
    public Guid? CompanyId { get; set; }
    public string? Description { get; set; }
    public List<CreateFinancialReportRowDto> Rows { get; set; } = new();
}

public class CreateFinancialReportRowDto
{
    public string Label { get; set; } = null!;
    public FinancialReportDataSource DataSource { get; set; }
    public int SortOrder { get; set; }
    public string? ReferenceCode { get; set; }
    public string? CalculationFormula { get; set; }
    public string? AccountCategoryFilter { get; set; }
    public string? CustomApiPath { get; set; }
    public bool HideWhenEmpty { get; set; }
    public bool IsBold { get; set; }
    public int IndentLevel { get; set; }
    public int SignMultiplier { get; set; } = 1;
}

public class ExecuteReportDto
{
    public Guid TemplateId { get; set; }
    public Guid CompanyId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string? FinanceBook { get; set; }
}

public class FinancialReportResultDto
{
    public string TemplateName { get; set; } = null!;
    public string ReportType { get; set; } = null!;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public decimal GrandTotal { get; set; }
    public List<FinancialReportResultRowDto> Rows { get; set; } = new();
}

public class FinancialReportResultRowDto
{
    public string Label { get; set; } = null!;
    public decimal Value { get; set; }
    public int IndentLevel { get; set; }
    public bool IsBold { get; set; }
    public string? ReferenceCode { get; set; }
    public string DataSource { get; set; } = null!;
}

// ─── AppService ─────────────────────────────────────────────────────────────

[Authorize(MyERPPermissions.Accounts.Default)]
public class FinancialReportTemplateAppService : ApplicationService
{
    private readonly IRepository<FinancialReportTemplate, Guid> _templateRepo;
    private readonly FinancialReportFormulaEngine _formulaEngine;

    public FinancialReportTemplateAppService(
        IRepository<FinancialReportTemplate, Guid> templateRepo,
        FinancialReportFormulaEngine formulaEngine)
    {
        _templateRepo = templateRepo;
        _formulaEngine = formulaEngine;
    }

    public async Task<PagedResultDto<FinancialReportTemplateDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _templateRepo.GetQueryableAsync();
        var totalCount = query.Count();
        var items = query.OrderBy(t => t.Name)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        return new PagedResultDto<FinancialReportTemplateDto>(totalCount,
            items.Select(MapToDto).ToList());
    }

    public async Task<FinancialReportTemplateDto> GetAsync(Guid id)
    {
        var template = await _templateRepo.GetAsync(id);
        return MapToDto(template);
    }

    [Authorize(MyERPPermissions.Accounts.Create)]
    public async Task<FinancialReportTemplateDto> CreateAsync(CreateFinancialReportTemplateDto input)
    {
        var template = new FinancialReportTemplate(Guid.NewGuid(), input.Name, input.ReportType);
        template.CompanyId = input.CompanyId;
        template.Description = input.Description;

        foreach (var rowDto in input.Rows.OrderBy(r => r.SortOrder))
        {
            template.AddRow(
                rowDto.Label,
                rowDto.DataSource,
                rowDto.SortOrder,
                rowDto.ReferenceCode,
                rowDto.CalculationFormula,
                rowDto.AccountCategoryFilter,
                rowDto.CustomApiPath,
                rowDto.HideWhenEmpty,
                rowDto.IsBold
            );
            // Set additional fields
            var lastRow = template.Rows.Last();
            lastRow.IndentLevel = rowDto.IndentLevel;
            lastRow.SignMultiplier = rowDto.SignMultiplier;
        }

        // Validate formulas for circular dependencies
        var errors = template.ValidateFormulas();
        if (errors.Any())
        {
            throw new Volo.Abp.BusinessException("MyERP:02045")
                .WithData("errors", string.Join("; ", errors));
        }

        await _templateRepo.InsertAsync(template);
        return MapToDto(template);
    }

    [Authorize(MyERPPermissions.Accounts.Edit)]
    public async Task ToggleAsync(Guid id)
    {
        var template = await _templateRepo.GetAsync(id);
        if (template.IsEnabled) template.Disable();
        else template.Enable();
        await _templateRepo.UpdateAsync(template);
    }

    [Authorize(MyERPPermissions.Accounts.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        var template = await _templateRepo.GetAsync(id);
        if (template.IsStandard)
            throw new Volo.Abp.BusinessException("MyERP:02046")
                .WithData("name", template.Name);
        await _templateRepo.DeleteAsync(id);
    }

    /// <summary>Execute a report template and return computed results.</summary>
    public async Task<FinancialReportResultDto> ExecuteAsync(ExecuteReportDto input)
    {
        var template = await _templateRepo.GetAsync(input.TemplateId);
        var result = await _formulaEngine.ExecuteAsync(template, input.CompanyId, input.FromDate, input.ToDate, input.FinanceBook);

        return new FinancialReportResultDto
        {
            TemplateName = result.TemplateName,
            ReportType = result.ReportType.ToString(),
            FromDate = result.FromDate,
            ToDate = result.ToDate,
            GrandTotal = result.GrandTotal,
            Rows = result.Rows.Select(r => new FinancialReportResultRowDto
            {
                Label = r.Label,
                Value = r.Value,
                IndentLevel = r.IndentLevel,
                IsBold = r.IsBold,
                ReferenceCode = r.ReferenceCode,
                DataSource = r.DataSource.ToString()
            }).ToList()
        };
    }

    /// <summary>Validate template formulas without executing (dry-run).</summary>
    public async Task<IReadOnlyList<string>> ValidateAsync(Guid id)
    {
        var template = await _templateRepo.GetAsync(id);
        return template.ValidateFormulas();
    }

    private static FinancialReportTemplateDto MapToDto(FinancialReportTemplate t) => new()
    {
        Id = t.Id,
        Name = t.Name,
        ReportType = t.ReportType,
        CompanyId = t.CompanyId,
        IsStandard = t.IsStandard,
        IsEnabled = t.IsEnabled,
        Description = t.Description,
        Rows = t.Rows.OrderBy(r => r.SortOrder).Select(r => new FinancialReportRowDto
        {
            Id = r.Id,
            Label = r.Label,
            DataSource = r.DataSource,
            SortOrder = r.SortOrder,
            ReferenceCode = r.ReferenceCode,
            CalculationFormula = r.CalculationFormula,
            AccountCategoryFilter = r.AccountCategoryFilter,
            CustomApiPath = r.CustomApiPath,
            HideWhenEmpty = r.HideWhenEmpty,
            IsBold = r.IsBold,
            IndentLevel = r.IndentLevel,
            SignMultiplier = r.SignMultiplier
        }).ToList()
    };
}
