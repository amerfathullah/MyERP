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
    public decimal Balance { get; set; }
    public string? PartyType { get; set; }
    public string? PartyName { get; set; }
    public string? CostCenterName { get; set; }
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
    public string? PartyType { get; set; }
    public Guid? PartyId { get; set; }
    public string? VoucherNumber { get; set; }
    public Guid? CostCenterId { get; set; }
}

/// <summary>
/// Lightweight DTO for voucher-level GL view (per ERPNext "Accounting Ledger" button on document detail pages).
/// </summary>
public class VoucherLedgerEntryDto
{
    public DateTime PostingDate { get; set; }
    public string? AccountCode { get; set; }
    public string? AccountName { get; set; }
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public string? CostCenterName { get; set; }
    public string? Description { get; set; }
    public string? FinanceBook { get; set; }
}

public class VoucherLedgerDto
{
    public string VoucherType { get; set; } = null!;
    public Guid VoucherId { get; set; }
    public string? VoucherNumber { get; set; }
    public List<VoucherLedgerEntryDto> Entries { get; set; } = new();
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public bool IsBalanced => Math.Abs(TotalDebit - TotalCredit) < 0.01m;
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
        var journalsQuery = jeQuery
            .Where(je => je.CompanyId == input.CompanyId
                      && je.PostingDate >= from
                      && je.PostingDate <= to
                      && je.Status == Core.DocumentStatus.Posted);

        // Filter by voucher number if specified
        if (!string.IsNullOrWhiteSpace(input.VoucherNumber))
            journalsQuery = journalsQuery.Where(je => je.EntryNumber != null && je.EntryNumber.Contains(input.VoucherNumber));

        var journals = journalsQuery
            .OrderBy(je => je.PostingDate)
            .ThenBy(je => je.CreationTime)
            .ToList();

        // Build account lookup
        var accountQuery = await _accountRepository.GetQueryableAsync();
        var accounts = accountQuery
            .Where(a => a.CompanyId == input.CompanyId)
            .ToDictionary(a => a.Id, a => new { a.AccountCode, a.AccountName });

        // Build cost center lookup
        var costCenters = new Dictionary<Guid, string>();
        if (journals.Any(je => je.Lines.Any(l => l.CostCenterId.HasValue)))
        {
            var ccRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<CostCenter, Guid>>();
            var ccQuery = await ccRepo.GetQueryableAsync();
            costCenters = ccQuery
                .Where(cc => cc.CompanyId == input.CompanyId)
                .ToDictionary(cc => cc.Id, cc => cc.Name);
        }

        var entries = new List<GeneralLedgerLineDto>();
        decimal runningBalance = 0;

        foreach (var je in journals)
        {
            foreach (var line in je.Lines)
            {
                // Apply account filter
                if (input.AccountId.HasValue && line.AccountId != input.AccountId.Value)
                    continue;

                // Apply cost center filter
                if (input.CostCenterId.HasValue && line.CostCenterId != input.CostCenterId.Value)
                    continue;

                accounts.TryGetValue(line.AccountId, out var acct);
                string? costCenterName = null;
                if (line.CostCenterId.HasValue)
                    costCenters.TryGetValue(line.CostCenterId.Value, out costCenterName);

                var debit = line.IsDebit ? line.Amount : 0;
                var credit = !line.IsDebit ? line.Amount : 0;
                runningBalance += debit - credit;

                entries.Add(new GeneralLedgerLineDto
                {
                    Id = line.Id,
                    PostingDate = je.PostingDate,
                    AccountCode = acct?.AccountCode,
                    AccountName = acct?.AccountName,
                    VoucherType = je.ReferenceType ?? "JournalEntry",
                    VoucherId = je.Id,
                    VoucherNumber = je.EntryNumber,
                    DebitAmount = debit,
                    CreditAmount = credit,
                    Balance = runningBalance,
                    PartyType = line.PartyType,
                    PartyName = null, // Party name resolution requires separate lookup
                    CostCenterName = costCenterName,
                    Description = line.Description,
                });
            }
        }

        return new GeneralLedgerReportDto
        {
            Entries = entries,
            TotalDebit = entries.Sum(e => e.DebitAmount),
            TotalCredit = entries.Sum(e => e.CreditAmount),
            Balance = entries.LastOrDefault()?.Balance ?? 0,
            Count = entries.Count,
        };
    }

    /// <summary>
    /// Returns all GL entries posted by a specific source document (per ERPNext "Accounting Ledger" button).
    /// Used on SI/PI/PE/DN/PR/SE/JE detail pages to show what GL entries were created.
    /// </summary>
    public async Task<VoucherLedgerDto> GetForVoucherAsync(string voucherType, Guid voucherId)
    {
        var jeQuery = await _journalRepository.GetQueryableAsync();

        // JEs reference their source document via ReferenceType + ReferenceId (for auto-posted JEs)
        // OR via the JE's own Id when the voucher IS a Journal Entry
        var journals = jeQuery
            .Where(je => (je.ReferenceType == voucherType && je.ReferenceId == voucherId)
                      || (voucherType == "JournalEntry" && je.Id == voucherId))
            .Where(je => je.Status == Core.DocumentStatus.Posted)
            .OrderBy(je => je.PostingDate)
            .ThenBy(je => je.CreationTime)
            .ToList();

        if (!journals.Any())
            return new VoucherLedgerDto
            {
                VoucherType = voucherType,
                VoucherId = voucherId,
            };

        // Resolve account names
        var accountIds = journals.SelectMany(je => je.Lines).Select(l => l.AccountId).Distinct().ToList();
        var accountQuery = await _accountRepository.GetQueryableAsync();
        var accounts = accountQuery
            .Where(a => accountIds.Contains(a.Id))
            .ToDictionary(a => a.Id, a => new { a.AccountCode, a.AccountName });

        // Resolve cost center names
        var costCenters = new Dictionary<Guid, string>();
        var ccIds = journals.SelectMany(je => je.Lines)
            .Where(l => l.CostCenterId.HasValue)
            .Select(l => l.CostCenterId!.Value)
            .Distinct()
            .ToList();
        if (ccIds.Any())
        {
            var ccRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<CostCenter, Guid>>();
            var ccQuery = await ccRepo.GetQueryableAsync();
            costCenters = ccQuery
                .Where(cc => ccIds.Contains(cc.Id))
                .ToDictionary(cc => cc.Id, cc => cc.Name);
        }

        var entries = new List<VoucherLedgerEntryDto>();
        foreach (var je in journals)
        {
            foreach (var line in je.Lines)
            {
                accounts.TryGetValue(line.AccountId, out var acct);
                string? ccName = null;
                if (line.CostCenterId.HasValue)
                    costCenters.TryGetValue(line.CostCenterId.Value, out ccName);

                entries.Add(new VoucherLedgerEntryDto
                {
                    PostingDate = je.PostingDate,
                    AccountCode = acct?.AccountCode,
                    AccountName = acct?.AccountName,
                    DebitAmount = line.IsDebit ? line.Amount : 0,
                    CreditAmount = !line.IsDebit ? line.Amount : 0,
                    CostCenterName = ccName,
                    Description = line.Description,
                    FinanceBook = line.FinanceBook,
                });
            }
        }

        return new VoucherLedgerDto
        {
            VoucherType = voucherType,
            VoucherId = voucherId,
            VoucherNumber = journals.First().EntryNumber,
            Entries = entries,
            TotalDebit = entries.Sum(e => e.DebitAmount),
            TotalCredit = entries.Sum(e => e.CreditAmount),
        };
    }
}
