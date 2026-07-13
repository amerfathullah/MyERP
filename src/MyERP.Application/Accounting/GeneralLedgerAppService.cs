using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

public class GeneralLedgerLineDto
{
    public Guid Id { get; set; }
    public DateTime PostingDate { get; set; }
    public string? AccountCode { get; set; }
    public string? AccountName { get; set; }
    public string? VoucherType { get; set; }
    public Guid? VoucherId { get; set; }
    public string? VoucherNumber { get; set; }
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public string? Description { get; set; }
}

public class GeneralLedgerReportDto
{
    public List<GeneralLedgerLineDto> Entries { get; set; } = new();
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public decimal Balance { get; set; }
    public int Count { get; set; }
}

public class GeneralLedgerFilterDto
{
    public Guid CompanyId { get; set; }
    public Guid? AccountId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

[Authorize(MyERPPermissions.Accounts.Default)]
public class GeneralLedgerAppService : ApplicationService
{
    private readonly IRepository<JournalEntry, Guid> _journalRepository;
    private readonly IRepository<Account, Guid> _accountRepository;

    public GeneralLedgerAppService(
        IRepository<JournalEntry, Guid> journalRepository,
        IRepository<Account, Guid> accountRepository)
    {
        _journalRepository = journalRepository;
        _accountRepository = accountRepository;
    }

    public async Task<GeneralLedgerReportDto> GetReportAsync(GeneralLedgerFilterDto input)
    {
        var from = input.FromDate ?? DateTime.UtcNow.AddMonths(-1).Date;
        var to = input.ToDate ?? DateTime.UtcNow.Date;

        var jeQuery = await _journalRepository.GetQueryableAsync();
        var journals = jeQuery
            .Where(je => je.CompanyId == input.CompanyId
                      && je.PostingDate >= from
                      && je.PostingDate <= to
                      && je.Status == Core.DocumentStatus.Posted)
            .OrderBy(je => je.PostingDate)
            .ToList();

        // Build account lookup
        var accountQuery = await _accountRepository.GetQueryableAsync();
        var accounts = accountQuery
            .Where(a => a.CompanyId == input.CompanyId)
            .ToDictionary(a => a.Id, a => new { a.AccountCode, a.AccountName });

        var entries = new List<GeneralLedgerLineDto>();
        foreach (var je in journals)
        {
            foreach (var line in je.Lines)
            {
                if (input.AccountId.HasValue && line.AccountId != input.AccountId.Value)
                    continue;

                accounts.TryGetValue(line.AccountId, out var acct);

                entries.Add(new GeneralLedgerLineDto
                {
                    Id = line.Id,
                    PostingDate = je.PostingDate,
                    AccountCode = acct?.AccountCode,
                    AccountName = acct?.AccountName,
                    VoucherType = "JournalEntry",
                    VoucherId = je.Id,
                    VoucherNumber = je.EntryNumber,
                    DebitAmount = line.IsDebit ? line.Amount : 0,
                    CreditAmount = !line.IsDebit ? line.Amount : 0,
                    Description = line.Description,
                });
            }
        }

        return new GeneralLedgerReportDto
        {
            Entries = entries,
            TotalDebit = entries.Sum(e => e.DebitAmount),
            TotalCredit = entries.Sum(e => e.CreditAmount),
            Balance = entries.Sum(e => e.DebitAmount) - entries.Sum(e => e.CreditAmount),
            Count = entries.Count,
        };
    }
}
