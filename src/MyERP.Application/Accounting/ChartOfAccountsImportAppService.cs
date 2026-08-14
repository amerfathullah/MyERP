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
        var dtos = template.Select(r => new CoaTemplateRowDto
        {
            AccountCode = r.AccountCode,
            AccountName = r.AccountName,
            AccountType = r.AccountType,
            IsGroup = r.IsGroup,
            ParentCode = r.ParentCode,
            SubType = r.SubType,
        }).ToList();

        return Task.FromResult(dtos);
    }
}
