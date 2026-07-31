using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Permissions;
using MyERP.Shared;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

public class PeriodClosingVoucherDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public Guid FiscalYearId { get; set; }
    public string? VoucherNumber { get; set; }
    public DateTime PostingDate { get; set; }
    public DateTime TransactionDate { get; set; }
    public Guid ClosingAccountId { get; set; }
    public string? ClosingAccountName { get; set; }
    public decimal TotalClosingAmount { get; set; }
    public int Status { get; set; }
    public string? Remarks { get; set; }
    public int EntryCount { get; set; }
}

/// <summary>Per-account GL entry line created by PCV — for audit display.</summary>
public class PcvGlEntryDto
{
    public Guid AccountId { get; set; }
    public string? AccountName { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public Guid? CostCenterId { get; set; }
    public DateTime PostingDate { get; set; }
}

public class CreatePeriodClosingVoucherDto
{
    public Guid CompanyId { get; set; }
    public Guid FiscalYearId { get; set; }
    public DateTime PostingDate { get; set; }
    public DateTime TransactionDate { get; set; }
    public Guid ClosingAccountId { get; set; }
    public string? Remarks { get; set; }
}

[Authorize(MyERPPermissions.Accounts.Default)]
public class PeriodClosingVoucherAppService : ApplicationService
{
    private readonly IRepository<PeriodClosingVoucher, Guid> _repository;
    private readonly AccountClosingBalanceService _closingBalanceService;
    private readonly DocumentPostingOrchestrator _postingOrchestrator;

    public PeriodClosingVoucherAppService(
        IRepository<PeriodClosingVoucher, Guid> repository,
        AccountClosingBalanceService closingBalanceService,
        DocumentPostingOrchestrator postingOrchestrator)
    {
        _repository = repository;
        _closingBalanceService = closingBalanceService;
        _postingOrchestrator = postingOrchestrator;
    }

    public async Task<PagedResultDto<PeriodClosingVoucherDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = (await _repository.WithDetailsAsync()).AsQueryable();

        if (input.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter;
            query = query.Where(x => x.VoucherNumber != null && x.VoucherNumber.Contains(filter));
        }

        if (!string.IsNullOrWhiteSpace(input.Status) && Enum.TryParse<DocumentStatus>(input.Status, true, out var status))
            query = query.Where(x => x.Status == status);

        var totalCount = query.Count();
        var items = query.OrderByDescending(p => p.PostingDate)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<PeriodClosingVoucherDto>(totalCount, items.Select(ObjectMapper.Map<PeriodClosingVoucher, PeriodClosingVoucherDto>).ToList());
    }

    public async Task<PeriodClosingVoucherDto> GetAsync(Guid id)
    {
        var pcv = (await _repository.WithDetailsAsync()).First(p => p.Id == id);
        return ObjectMapper.Map<PeriodClosingVoucher, PeriodClosingVoucherDto>(pcv);
    }

    /// <summary>
    /// Gets the GL entries (Journal Entry lines) created by a Period Closing Voucher.
    /// Per ERPNext: PCV creates one JE with per-P&amp;L-account per-dimension reversing entries.
    /// This enables the audit trail: users can see exactly what GL postings the PCV made.
    /// </summary>
    public async Task<PcvGlEntryDto[]> GetGlEntriesAsync(Guid id)
    {
        var jeRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<JournalEntry, Guid>>();
        var accountRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Account, Guid>>();

        var linkedJes = (await jeRepo.WithDetailsAsync())
            .Where(je => je.ReferenceType == "PeriodClosingVoucher" && je.ReferenceId == id)
            .ToList();

        if (!linkedJes.Any())
            return [];

        // Resolve account names for display
        var accountIds = linkedJes.SelectMany(je => je.Lines).Select(l => l.AccountId).Distinct().ToList();
        var accountQuery = await accountRepo.GetQueryableAsync();
        var accountNames = accountQuery
            .Where(a => accountIds.Contains(a.Id))
            .Select(a => new { a.Id, a.AccountName, a.AccountCode })
            .ToDictionary(a => a.Id, a => $"{a.AccountCode} - {a.AccountName}");

        return linkedJes.SelectMany(je => je.Lines.Select(line => new PcvGlEntryDto
        {
            AccountId = line.AccountId,
            AccountName = accountNames.TryGetValue(line.AccountId, out var name) ? name : line.AccountId.ToString(),
            Debit = line.IsDebit ? line.Amount : 0,
            Credit = !line.IsDebit ? line.Amount : 0,
            CostCenterId = line.CostCenterId,
            PostingDate = je.PostingDate,
        })).OrderBy(e => e.AccountName).ToArray();
    }

    [Authorize(MyERPPermissions.Accounts.Create)]
    public async Task<PeriodClosingVoucherDto> CreateAsync(CreatePeriodClosingVoucherDto input)
    {
        var pcv = new PeriodClosingVoucher(GuidGenerator.Create(), input.CompanyId,
            input.FiscalYearId, input.PostingDate, input.TransactionDate,
            input.ClosingAccountId, CurrentTenant.Id)
        { Remarks = input.Remarks };
        await _repository.InsertAsync(pcv);
        return ObjectMapper.Map<PeriodClosingVoucher, PeriodClosingVoucherDto>(pcv);
    }

    [Authorize(MyERPPermissions.Accounts.Create)]
    public async Task<PeriodClosingVoucherDto> SubmitAsync(Guid id)
    {
        var pcv = (await _repository.WithDetailsAsync()).First(p => p.Id == id);

        // Validate posting period is not frozen/closed
        await _postingOrchestrator.ValidatePostingPeriodAsync(
            pcv.CompanyId, pcv.PostingDate, "PeriodClosingVoucher");

        // Block if a future PCV already exists for this company
        var allPcvs = await _repository.GetListAsync(p =>
            p.CompanyId == pcv.CompanyId && p.Status == DocumentStatus.Submitted
            && p.PostingDate > pcv.PostingDate);
        if (allPcvs.Any())
            throw new BusinessException("MyERP:02034")
                .WithData("existingDate", allPcvs.First().PostingDate.ToString("dd/MM/yyyy"));

        // Validate closing account via dedicated domain service (type + currency checks)
        var pcvPostingService = LazyServiceProvider.LazyGetRequiredService<PeriodClosingPostingService>();
        await pcvPostingService.ValidateForSubmitAsync(pcv);

        // Calculate P&L closing entries via domain service (per-account per-CC aggregation)
        var result = await pcvPostingService.CalculateClosingEntriesAsync(
            pcv.CompanyId, pcv.PostingDate, CurrentTenant.Id);

        if (!result.Balances.Any())
        {
            pcv.Submit();
            await _repository.UpdateAsync(pcv);
            return ObjectMapper.Map<PeriodClosingVoucher, PeriodClosingVoucherDto>(pcv);
        }

        // Create reversing JE via domain service (proper per-dimension entries)
        var je = await pcvPostingService.CreateClosingJournalEntryAsync(pcv, result);

        pcv.Submit();
        await _repository.UpdateAsync(pcv);

        // Build Account Closing Balances — enables O(1) Trial Balance queries
        var period = AccountClosingBalanceService.GetPeriodFromDate(pcv.PostingDate);
        await _closingBalanceService.RebuildAsync(
            pcv.CompanyId, pcv.PostingDate, period, CurrentTenant.Id);

        return ObjectMapper.Map<PeriodClosingVoucher, PeriodClosingVoucherDto>(pcv);
    }

    [Authorize(MyERPPermissions.Accounts.Delete)]
    public async Task<PeriodClosingVoucherDto> CancelAsync(Guid id)
    {
        var pcv = await _repository.GetAsync(id);

        // Per PR b3c2ba5381: validate account frozen date on cancel (uses today since reversals post at current date)
        await _postingOrchestrator.ValidatePostingPeriodAsync(
            pcv.CompanyId, DateTime.UtcNow.Date, "PeriodClosingVoucher");

        // Block cancel if a future PCV exists for this company
        var futurePcvs = await _repository.GetListAsync(p =>
            p.CompanyId == pcv.CompanyId && p.Status == DocumentStatus.Submitted
            && p.PostingDate > pcv.PostingDate);
        if (futurePcvs.Any())
            throw new BusinessException("MyERP:02036")
                .WithData("existingDate", futurePcvs.First().PostingDate.ToString("dd/MM/yyyy"));

        pcv.Cancel();
        await _repository.UpdateAsync(pcv);

        // Cancel the linked Journal Entry
        var jeRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<JournalEntry, Guid>>();
        var linkedJe = (await jeRepo.GetListAsync(je =>
            je.ReferenceType == "PeriodClosingVoucher" && je.ReferenceId == pcv.Id
            && je.Status == DocumentStatus.Posted)).FirstOrDefault();
        if (linkedJe != null)
        {
            linkedJe.Cancel();
            await jeRepo.UpdateAsync(linkedJe);
        }

        // Delete closing balances for the cancelled period
        var period = AccountClosingBalanceService.GetPeriodFromDate(pcv.PostingDate);
        await _closingBalanceService.DeleteForPeriodAsync(pcv.CompanyId, period);

        return ObjectMapper.Map<PeriodClosingVoucher, PeriodClosingVoucherDto>(pcv);
    }
}

