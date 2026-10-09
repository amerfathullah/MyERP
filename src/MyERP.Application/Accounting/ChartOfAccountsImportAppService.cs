using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Accounting.DomainServices;
using MyERP.Permissions;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

/// <summary>
/// Application service exposing Chart of Accounts import functionality.
/// Wraps ChartOfAccountsImportService domain service for API access.
/// </summary>
[Authorize(MyERPPermissions.Accounts.Create)]
public class ChartOfAccountsImportAppService : ApplicationService, IChartOfAccountsImportAppService
{
    private readonly ChartOfAccountsImportService _importService;

    public ChartOfAccountsImportAppService(ChartOfAccountsImportService importService)
    {
        _importService = importService;
    }

    /// <summary>
    /// Import a chart of accounts from CSV-style row data.
    /// Blocked if posted GL entries already exist for the company.
    /// </summary>
    public async Task<CoaImportResultDto> ImportAsync(ImportCoaDto input)
    {
        var rows = input.Rows.Select(r => new CoaTemplateRow(
            r.AccountCode,
            r.AccountName,
            r.AccountType,
            r.IsGroup,
            r.ParentCode,
            r.SubType
        )).ToList();

        var count = await _importService.ImportAsync(input.CompanyId, rows, CurrentTenant.Id);

        return new CoaImportResultDto { AccountsCreated = count, CompanyId = input.CompanyId };
    }

    /// <summary>
    /// Get the standard Malaysian chart of accounts template.
    /// Returns template rows that can be submitted to ImportAsync.
    /// </summary>
    public Task<List<CoaTemplateRowDto>> GetMalaysianTemplateAsync()
    {
        var template = ChartOfAccountsImportService.GetMalaysianTemplate();
        return Task.FromResult(MapRows(template));
    }

    /// <summary>
    /// Get the standard United States chart of accounts template.
    /// Loaded from verified us_chart_of_accounts.json (ERPNext PR #57279 / commit 6369f7fd5a).
    /// </summary>
    public Task<List<CoaTemplateRowDto>> GetUsTemplateAsync()
    {
        var template = ChartOfAccountsImportService.GetUsTemplate();
        return Task.FromResult(MapRows(template));
    }

    /// <summary>
    /// Get a chart of accounts template by ISO country code (e.g. "MY", "US").
    /// </summary>
    public Task<List<CoaTemplateRowDto>> GetTemplateByCountryAsync(string countryCode)
    {
        var template = ChartOfAccountsImportService.GetTemplateByCountry(countryCode);
        return Task.FromResult(MapRows(template));
    }

    private static List<CoaTemplateRowDto> MapRows(List<CoaTemplateRow> rows)
    {
        return rows.Select(r => new CoaTemplateRowDto
        {
            AccountCode = r.AccountCode,
            AccountName = r.AccountName,
            AccountType = r.AccountType,
            IsGroup = r.IsGroup,
            ParentCode = r.ParentCode,
            SubType = r.SubType,
        }).ToList();
    }
}
