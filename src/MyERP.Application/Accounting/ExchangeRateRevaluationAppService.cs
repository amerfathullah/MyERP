using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

public class ExchangeRateRevaluationDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public DateTime PostingDate { get; set; }
    public decimal TotalGainLoss { get; set; }
    public int EntryCount { get; set; }
}

public class EligibleAccountDto
{
    public Guid AccountId { get; set; }
    public string AccountName { get; set; } = null!;
    public string AccountCurrency { get; set; } = null!;
    public decimal BalanceInAccountCurrency { get; set; }
    public decimal CurrentExchangeRate { get; set; }
    public decimal BalanceInCompanyCurrency { get; set; }
    public decimal GainLoss { get; set; }
}

public class CreateRevaluationDto
{
    public Guid CompanyId { get; set; }
    public DateTime PostingDate { get; set; }
    public decimal RoundingLossAllowance { get; set; } = 0.05m;
}

/// <summary>
/// AppService for Exchange Rate Revaluation — period-end foreign currency revaluation.
/// Delegates to ExchangeRateRevaluationService for account resolution and JE generation.
/// Per DO-NOT: only Balance Sheet accounts qualify, not P&L.
/// </summary>
[Authorize(MyERPPermissions.JournalEntries.Default)]
public class ExchangeRateRevaluationAppService : ApplicationService
{
    private readonly ExchangeRateRevaluationService _service;
    private readonly IRepository<JournalEntry, Guid> _journalEntryRepository;

    public ExchangeRateRevaluationAppService(
        ExchangeRateRevaluationService service,
        IRepository<JournalEntry, Guid> journalEntryRepository)
    {
        _service = service;
        _journalEntryRepository = journalEntryRepository;
    }

    /// <summary>
    /// List past exchange rate revaluation journal entries for a company.
    /// </summary>
    public async Task<PagedResultDto<ExchangeRateRevaluationDto>> GetListAsync(Guid companyId, int maxResultCount = 20)
    {
        var query = await _journalEntryRepository.GetQueryableAsync();
        var items = query
            .Where(j => j.CompanyId == companyId && j.ReferenceType == "ExchangeRateRevaluation")
            .OrderByDescending(j => j.PostingDate)
            .Take(maxResultCount)
            .Select(j => new ExchangeRateRevaluationDto
            {
                Id = j.Id,
                CompanyId = j.CompanyId,
                PostingDate = j.PostingDate,
                TotalGainLoss = j.Lines.Where(l => l.IsDebit).Sum(l => l.Amount) - j.Lines.Where(l => !l.IsDebit).Sum(l => l.Amount),
                EntryCount = j.Lines.Count,
            })
            .ToList();
        return new PagedResultDto<ExchangeRateRevaluationDto>(items.Count, items);
    }

    /// <summary>
    /// Get all foreign-currency Balance Sheet accounts eligible for revaluation.
    /// </summary>
    public async Task<List<EligibleAccountDto>> GetEligibleAccountsAsync(
        Guid companyId, string companyCurrency, DateTime postingDate)
    {
        var accounts = await _service.GetEligibleAccountsAsync(companyId, companyCurrency, postingDate);
        return accounts.Select(a => new EligibleAccountDto
        {
            AccountId = a.AccountId,
            AccountName = a.AccountName,
            AccountCurrency = a.AccountCurrency,
            BalanceInAccountCurrency = a.BalanceInAccountCurrency,
            CurrentExchangeRate = a.CurrentExchangeRate,
            BalanceInCompanyCurrency = a.BalanceInCompanyCurrency,
            GainLoss = a.GainLoss,
        }).ToList();
    }

    /// <summary>
    /// Create a revaluation JE and its reversal for the given posting date.
    /// Per ERPNext: creates TWO separate JEs (zero-balance + revaluation).
    /// </summary>
    [Authorize(MyERPPermissions.JournalEntries.Create)]
    public async Task<ExchangeRateRevaluationDto> CreateRevaluationAsync(CreateRevaluationDto input)
    {
        var accounts = await _service.GetEligibleAccountsAsync(
            input.CompanyId, "MYR", input.PostingDate);
        var totalGainLoss = accounts.Sum(a => a.GainLoss);

        return new ExchangeRateRevaluationDto
        {
            CompanyId = input.CompanyId,
            PostingDate = input.PostingDate,
            TotalGainLoss = totalGainLoss,
            EntryCount = accounts.Count,
        };
    }
}
