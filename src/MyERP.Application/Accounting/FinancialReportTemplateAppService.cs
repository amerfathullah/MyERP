using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

// ─── AppService ─────────────────────────────────────────────────────────────

[Authorize(MyERPPermissions.Accounts.Default)]
public class FinancialReportTemplateAppService : ApplicationService, IFinancialReportTemplateAppService
{
    private readonly IRepository<FinancialReportTemplate, Guid> _templateRepo;
    private readonly FinancialReportFormulaEngine _formulaEngine;
    private readonly IRepository<Account, Guid>? _accountRepo;
    private readonly IRepository<AccountCategory>? _categoryRepo;

    public FinancialReportTemplateAppService(
        IRepository<FinancialReportTemplate, Guid> templateRepo,
        FinancialReportFormulaEngine formulaEngine,
        IRepository<Account, Guid>? accountRepo = null,
        IRepository<AccountCategory>? categoryRepo = null)
    {
        _templateRepo = templateRepo;
        _formulaEngine = formulaEngine;
        _accountRepo = accountRepo;
        _categoryRepo = categoryRepo;
    }

    private IRepository<Account, Guid> AccountRepository =>
        _accountRepo ?? LazyServiceProvider.LazyGetRequiredService<IRepository<Account, Guid>>();

    private IRepository<AccountCategory> CategoryRepository =>
        _categoryRepo ?? LazyServiceProvider.LazyGetRequiredService<IRepository<AccountCategory>>();

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

    /// <summary>
    /// Previews accounts matching report template row filters, enforcing field whitelist.
    /// Per ERPNext PR #58790 / commit ed368a3e2a.
    /// </summary>
    public async Task<List<FilteredAccountDto>> GetFilteredAccountsAsync(GetFilteredAccountsDto input)
    {
        // Per ERPNext PR #58790: company is required
        if (input.CompanyId == Guid.Empty)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Company is required");
        }

        // Validate all filter rows against whitelist before querying
        var parsedFilters = new List<(string Field, string Operator, string Value)>();
        if (input.AccountRows != null)
        {
            foreach (var row in input.AccountRows)
            {
                parsedFilters.Add(ParseFilterRow(row));
            }
        }

        var accountsQ = await AccountRepository.GetQueryableAsync();
        var companyAccounts = accountsQ
            .Where(a => a.CompanyId == input.CompanyId && a.IsActive && !a.IsGroup)
            .ToList();

        var categoriesQ = await CategoryRepository.GetQueryableAsync();
        var categoryMap = categoriesQ.ToDictionary(c => c.Id, c => c.Name);

        var filtered = companyAccounts.AsEnumerable();
        foreach (var (field, op, val) in parsedFilters)
        {
            filtered = filtered.Where(a => MatchesFilter(a, field, op, val, categoryMap));
        }

        return filtered
            .OrderBy(a => a.AccountCode)
            .ThenBy(a => a.AccountName)
            .Select(a => new FilteredAccountDto
            {
                Id = a.Id,
                AccountCode = a.AccountCode,
                AccountName = a.AccountName,
                AccountType = a.AccountSubType?.ToString() ?? a.AccountType.ToString(),
                RootType = a.AccountType == AccountType.Revenue ? "Income" : a.AccountType.ToString(),
                AccountCategory = a.AccountCategoryId.HasValue && categoryMap.TryGetValue(a.AccountCategoryId.Value, out var cName) ? cName : null,
                Currency = a.Currency ?? "MYR",
                IsGroup = a.IsGroup
            })
            .ToList();
    }

    private static readonly HashSet<string> WhitelistedFilterFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "account_type",
        "accounttype",
        "root_type",
        "roottype",
        "account_category",
        "accountcategory",
        "currency",
        "account_code",
        "accountcode",
        "account_name",
        "accountname",
        "is_group",
        "isgroup"
    };

    private static readonly HashSet<string> AllowedOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        "=", "==", "!=", "<>", "in", "not in", "like", "contains"
    };

    private static (string Field, string Operator, string Value) ParseFilterRow(AccountFilterRowDto row)
    {
        string? field = row.Field;
        string? op = row.Operator;
        string? val = row.Value;

        if (!string.IsNullOrWhiteSpace(row.CalculationFormula))
        {
            var trimmed = row.CalculationFormula.Trim();
            if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
            {
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(trimmed);
                    var root = doc.RootElement;
                    if (root.ValueKind == System.Text.Json.JsonValueKind.Array && root.GetArrayLength() >= 3)
                    {
                        field = root[0].GetString();
                        op = root[1].GetString();
                        val = root[2].ValueKind == System.Text.Json.JsonValueKind.String
                            ? root[2].GetString()
                            : root[2].GetRawText();
                    }
                }
                catch (System.Text.Json.JsonException ex)
                {
                    throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                        .WithData("detail", $"[Account Filter] Invalid JSON format: {ex.Message}");
                }
            }
            else if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
            {
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(trimmed);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("field", out var f)) field = f.GetString();
                    if (root.TryGetProperty("operator", out var o)) op = o.GetString();
                    if (root.TryGetProperty("value", out var v))
                    {
                        val = v.ValueKind == System.Text.Json.JsonValueKind.String
                            ? v.GetString()
                            : v.GetRawText();
                    }
                }
                catch (System.Text.Json.JsonException ex)
                {
                    throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                        .WithData("detail", $"[Account Filter] Invalid JSON format: {ex.Message}");
                }
            }
        }

        if (string.IsNullOrWhiteSpace(field))
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "[Account Filter] Field and operator must be strings");
        }

        if (!WhitelistedFilterFields.Contains(field))
        {
            var escaped = System.Net.WebUtility.HtmlEncode(field);
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"[Account Filter] Field '{escaped}' is not a valid Account field");
        }

        op = (op ?? "=").Trim();
        if (!AllowedOperators.Contains(op))
        {
            var escapedOp = System.Net.WebUtility.HtmlEncode(op);
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"[Account Filter] Invalid operator '{escapedOp}'");
        }

        return (field, op, val ?? string.Empty);
    }

    private static bool MatchesFilter(
        Account account,
        string field,
        string op,
        string value,
        Dictionary<Guid, string> categoryNames)
    {
        string normField = field.Replace("_", "").ToLowerInvariant();
        string normOp = op.ToLowerInvariant();

        string actualVal = normField switch
        {
            "accounttype" => account.AccountSubType?.ToString() ?? account.AccountType.ToString(),
            "roottype" => account.AccountType == AccountType.Revenue ? "Income" : account.AccountType.ToString(),
            "accountcategory" => account.AccountCategoryId.HasValue && categoryNames.TryGetValue(account.AccountCategoryId.Value, out var cName) ? cName : "",
            "currency" => account.Currency ?? "MYR",
            "accountcode" => account.AccountCode ?? "",
            "accountname" => account.AccountName ?? "",
            "isgroup" => account.IsGroup ? "1" : "0",
            _ => ""
        };

        if (normField == "roottype" && (value.Equals("Revenue", StringComparison.OrdinalIgnoreCase) || value.Equals("Income", StringComparison.OrdinalIgnoreCase)))
        {
            bool isIncome = actualVal.Equals("Income", StringComparison.OrdinalIgnoreCase) || actualVal.Equals("Revenue", StringComparison.OrdinalIgnoreCase);
            if (normOp is "=" or "==") return isIncome;
            if (normOp is "!=" or "<>") return !isIncome;
        }

        switch (normOp)
        {
            case "=":
            case "==":
                return string.Equals(actualVal, value, StringComparison.OrdinalIgnoreCase);
            case "!=":
            case "<>":
                return !string.Equals(actualVal, value, StringComparison.OrdinalIgnoreCase);
            case "in":
            {
                var candidates = value.Trim('[', ']', '"', ' ')
                    .Split(new[] { ',', '"' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                return candidates.Contains(actualVal);
            }
            case "not in":
            {
                var candidates = value.Trim('[', ']', '"', ' ')
                    .Split(new[] { ',', '"' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                return !candidates.Contains(actualVal);
            }
            case "like":
            case "contains":
                return actualVal.Contains(value.Trim('%', ' '), StringComparison.OrdinalIgnoreCase);
            default:
                return false;
        }
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

