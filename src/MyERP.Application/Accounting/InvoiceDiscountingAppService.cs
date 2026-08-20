using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

[Authorize(MyERPPermissions.PaymentEntries.Default)]
public class InvoiceDiscountingAppService : ApplicationService, IInvoiceDiscountingAppService
{
    private readonly InvoiceDiscountingService _service;
    private readonly IRepository<InvoiceDiscounting, Guid> _repository;
    private readonly IRepository<JournalEntry, Guid> _journalEntryRepository;
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepository;
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepository;
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly DocumentPostingOrchestrator _postingOrchestrator;

    public InvoiceDiscountingAppService(
        InvoiceDiscountingService service,
        IRepository<InvoiceDiscounting, Guid> repository,
        IRepository<JournalEntry, Guid> journalEntryRepository,
        IRepository<FiscalYear, Guid> fiscalYearRepository,
        IRepository<SalesInvoice, Guid> salesInvoiceRepository,
        IRepository<Customer, Guid> customerRepository,
        DocumentPostingOrchestrator postingOrchestrator)
    {
        _service = service;
        _repository = repository;
        _journalEntryRepository = journalEntryRepository;
        _fiscalYearRepository = fiscalYearRepository;
        _salesInvoiceRepository = salesInvoiceRepository;
        _customerRepository = customerRepository;
        _postingOrchestrator = postingOrchestrator;
    }

    public async Task<PagedResultDto<InvoiceDiscountingDto>> GetListAsync(PagedAndSortedResultRequestDto input, Guid? companyId = null)
    {
        var query = await _repository.WithDetailsAsync(x => x.Invoices);
        if (companyId.HasValue)
            query = query.Where(x => x.CompanyId == companyId.Value);

        var totalCount = query.Count();
        var items = query
            .OrderByDescending(x => x.PostingDate)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        return new PagedResultDto<InvoiceDiscountingDto>(totalCount, items.Select(ToDto).ToList());
    }

    public async Task<InvoiceDiscountingDto> GetAsync(Guid id)
    {
        var query = await _repository.WithDetailsAsync(x => x.Invoices);
        var entity = query.First(x => x.Id == id);
        return await ToDetailedDtoAsync(entity);
    }

    public async Task<List<InvoiceForDiscountingDto>> GetEligibleInvoicesAsync(Guid companyId, Guid? customerId = null)
    {
        var pledgedInvoiceIds = await GetPledgedInvoiceIdsAsync();

        var siQuery = await _salesInvoiceRepository.GetQueryableAsync();
        var candidates = siQuery
            .Where(si => si.CompanyId == companyId
                && si.Status == DocumentStatus.Posted
                && !si.IsReturn
                && (si.GrandTotal - si.AmountPaid - si.WriteOffAmount - si.TotalAdvance) > 0.01m);

        if (customerId.HasValue)
            candidates = candidates.Where(si => si.CustomerId == customerId.Value);

        var invoices = candidates
            .Select(si => new
            {
                si.Id, si.InvoiceNumber, si.CustomerId, si.IssueDate,
                Outstanding = si.GrandTotal - si.AmountPaid - si.WriteOffAmount - si.TotalAdvance,
            })
            .ToList()
            .Where(si => !pledgedInvoiceIds.Contains(si.Id))
            .ToList();

        if (invoices.Count == 0) return new List<InvoiceForDiscountingDto>();

        var customerIds = invoices.Select(i => i.CustomerId).Distinct().ToList();
        var customerQuery = await _customerRepository.GetQueryableAsync();
        var customerNames = customerQuery.Where(c => customerIds.Contains(c.Id)).ToDictionary(c => c.Id, c => c.Name);

        return invoices.Select(si => new InvoiceForDiscountingDto
        {
            InvoiceId = si.Id,
            InvoiceNumber = si.InvoiceNumber,
            CustomerId = si.CustomerId,
            CustomerName = customerNames.GetValueOrDefault(si.CustomerId, ""),
            IssueDate = si.IssueDate,
            OutstandingAmount = si.Outstanding,
        }).ToList();
    }

    public Task<DiscountingCalculationResultDto> CalculateAsync(CalculateDiscountingDto input)
    {
        var discountCharge = _service.CalculateDiscountCharge(input.TotalOutstanding, input.AnnualDiscountRate, input.DaysToMaturity);
        var disbursement = _service.CalculateDisbursementAmount(input.TotalOutstanding, discountCharge);

        return Task.FromResult(new DiscountingCalculationResultDto
        {
            DiscountCharge = discountCharge,
            DisbursementAmount = disbursement,
            EffectiveRate = input.AnnualDiscountRate,
        });
    }

    public async Task<InvoiceDiscountingDto> CreateAsync(CreateInvoiceDiscountingDto input)
    {
        if (input.Invoices.Count == 0)
            throw new BusinessException(MyERPDomainErrorCodes.InvoiceDiscountingNoInvoices);

        await ValidateInvoicesEligibleAsync(input.Invoices, excludeInvoiceDiscountingId: null);

        var doc = new InvoiceDiscounting(
            GuidGenerator.Create(), input.CompanyId, input.PostingDate,
            input.ShortTermLoanAccountId, input.BankAccountId, input.BankChargesAccountId,
            input.AccountsReceivableCreditAccountId, input.AccountsReceivableDiscountedAccountId,
            input.AccountsReceivableUnpaidAccountId);

        var salesInvoices = await LoadSalesInvoicesAsync(input.Invoices.Select(i => i.SalesInvoiceId));
        var lines = input.Invoices.Select(i => new InvoiceDiscountingInvoice(
            GuidGenerator.Create(), doc.Id, i.SalesInvoiceId, salesInvoices[i.SalesInvoiceId].CustomerId, i.OutstandingAmount));
        doc.SetInvoices(lines);

        await _repository.InsertAsync(doc);
        return await ToDetailedDtoAsync(doc);
    }

    public async Task<InvoiceDiscountingDto> SubmitAsync(Guid id, SubmitInvoiceDiscountingDto input)
    {
        var doc = await GetWithInvoicesAsync(id);

        // Re-validate eligibility at submit time too — the invoice's outstanding amount or
        // discounted-elsewhere status may have changed since this document was drafted.
        await ValidateInvoicesEligibleAsync(
            doc.Invoices.Select(i => new CreateInvoiceDiscountingInvoiceDto { SalesInvoiceId = i.SalesInvoiceId, OutstandingAmount = i.OutstandingAmount }).ToList(),
            excludeInvoiceDiscountingId: doc.Id);

        doc.LoanStartDate = input.LoanStartDate;
        doc.LoanPeriodDays = input.LoanPeriodDays;
        doc.Submit();

        var salesInvoices = await LoadSalesInvoicesAsync(doc.Invoices.Select(i => i.SalesInvoiceId));
        var fy = await ResolveFiscalYearAsync(doc.CompanyId, doc.PostingDate);
        var je = new JournalEntry(GuidGenerator.Create(), doc.CompanyId, fy.Id, doc.PostingDate)
        {
            Narration = $"Invoice Discounting Sanction - {doc.Invoices.Count} invoice(s) pledged",
            ReferenceType = "InvoiceDiscounting",
            ReferenceId = doc.Id,
            VoucherType = JournalEntryVoucherType.JournalEntry,
        };

        foreach (var line in doc.Invoices)
        {
            var si = salesInvoices[line.SalesInvoiceId];
            je.AddLineWithParty(doc.AccountsReceivableCreditAccountId, line.OutstandingAmount, true,
                line.CustomerId, "Customer", null, $"Pledged: {si.InvoiceNumber}");
            je.AddLineWithParty(si.DebitToAccountId, line.OutstandingAmount, false,
                line.CustomerId, "Customer", null, $"Pledged: {si.InvoiceNumber}");
        }

        je.Post();
        await _journalEntryRepository.InsertAsync(je);
        doc.MarkSanctionPosted(je.Id);

        await _repository.UpdateAsync(doc);
        return await ToDetailedDtoAsync(doc);
    }

    public async Task<InvoiceDiscountingDto> DisburseAsync(Guid id, DisburseInvoiceDiscountingDto input)
    {
        var doc = await GetWithInvoicesAsync(id);
        var fy = await ResolveFiscalYearAsync(doc.CompanyId, doc.PostingDate);

        var je = new JournalEntry(GuidGenerator.Create(), doc.CompanyId, fy.Id, doc.PostingDate)
        {
            Narration = "Invoice Discounting Disbursement - Loan Disbursed",
            ReferenceType = "InvoiceDiscounting",
            ReferenceId = doc.Id,
            VoucherType = JournalEntryVoucherType.JournalEntry,
        };

        var netToBank = doc.TotalAmount - input.BankCharges;
        je.AddLine(doc.BankAccountId, netToBank, true);
        if (input.BankCharges > 0)
            je.AddLine(doc.BankChargesAccountId, input.BankCharges, true);
        je.AddLine(doc.ShortTermLoanAccountId, doc.TotalAmount, false);

        je.Post();
        await _journalEntryRepository.InsertAsync(je);

        doc.MarkDisbursed(je.Id, input.BankCharges);
        await _repository.UpdateAsync(doc);
        return await ToDetailedDtoAsync(doc);
    }

    public async Task<InvoiceDiscountingDto> SettleAsync(Guid id)
    {
        var doc = await GetWithInvoicesAsync(id);
        var fy = await ResolveFiscalYearAsync(doc.CompanyId, doc.PostingDate);

        var je = new JournalEntry(GuidGenerator.Create(), doc.CompanyId, fy.Id, DateTime.UtcNow.Date)
        {
            Narration = "Invoice Discounting Settlement - Loan Repaid",
            ReferenceType = "InvoiceDiscounting",
            ReferenceId = doc.Id,
            VoucherType = JournalEntryVoucherType.JournalEntry,
        };

        je.AddLine(doc.ShortTermLoanAccountId, doc.TotalAmount, true);
        je.AddLine(doc.BankAccountId, doc.TotalAmount, false);

        je.Post();
        await _journalEntryRepository.InsertAsync(je);

        doc.MarkSettled(je.Id);
        await _repository.UpdateAsync(doc);
        return await ToDetailedDtoAsync(doc);
    }

    public async Task<InvoiceDiscountingDto> CancelAsync(Guid id)
    {
        var doc = await GetWithInvoicesAsync(id);

        var disbursementJeId = doc.DisbursementJournalEntryId;
        var sanctionJeId = doc.SanctionJournalEntryId;

        doc.Cancel();

        if (disbursementJeId.HasValue)
            await _postingOrchestrator.ReverseGlForJournalEntryAsync(disbursementJeId.Value);
        if (sanctionJeId.HasValue)
            await _postingOrchestrator.ReverseGlForJournalEntryAsync(sanctionJeId.Value);

        await _repository.UpdateAsync(doc);
        return await ToDetailedDtoAsync(doc);
    }

    private async Task<InvoiceDiscounting> GetWithInvoicesAsync(Guid id)
    {
        var query = await _repository.WithDetailsAsync(x => x.Invoices);
        return query.First(x => x.Id == id);
    }

    private async Task<HashSet<Guid>> GetPledgedInvoiceIdsAsync(Guid? excludeInvoiceDiscountingId = null)
    {
        var query = await _repository.WithDetailsAsync(x => x.Invoices);
        var active = query.Where(x =>
            x.Status != InvoiceDiscountingStatus.Cancelled && x.Status != InvoiceDiscountingStatus.Settled);
        if (excludeInvoiceDiscountingId.HasValue)
            active = active.Where(x => x.Id != excludeInvoiceDiscountingId.Value);

        return active.SelectMany(x => x.Invoices).Select(i => i.SalesInvoiceId).ToHashSet();
    }

    private async Task ValidateInvoicesEligibleAsync(List<CreateInvoiceDiscountingInvoiceDto> requested, Guid? excludeInvoiceDiscountingId)
    {
        var pledgedInvoiceIds = await GetPledgedInvoiceIdsAsync(excludeInvoiceDiscountingId);
        var salesInvoices = await LoadSalesInvoicesAsync(requested.Select(i => i.SalesInvoiceId));

        var checks = requested.Select(i =>
        {
            var si = salesInvoices[i.SalesInvoiceId];
            return new InvoiceForDiscounting
            {
                InvoiceId = i.SalesInvoiceId,
                InvoiceNumber = si.InvoiceNumber,
                OutstandingAmount = i.OutstandingAmount,
                ActualOutstandingAmount = si.OutstandingAmount,
                IsAlreadyDiscounted = pledgedInvoiceIds.Contains(i.SalesInvoiceId),
            };
        }).ToList();

        InvoiceDiscountingService.ValidateInvoicesForDiscounting(checks);
    }

    private async Task<Dictionary<Guid, SalesInvoice>> LoadSalesInvoicesAsync(IEnumerable<Guid> ids)
    {
        var idList = ids.Distinct().ToList();
        var query = await _salesInvoiceRepository.GetQueryableAsync();
        return query.Where(si => idList.Contains(si.Id)).ToList().ToDictionary(si => si.Id);
    }

    private async Task<FiscalYear> ResolveFiscalYearAsync(Guid companyId, DateTime date)
    {
        var fyQuery = await _fiscalYearRepository.GetQueryableAsync();
        var fy = fyQuery.FirstOrDefault(f =>
            f.CompanyId == companyId && f.StartDate <= date && f.EndDate >= date);

        if (fy == null)
            throw new BusinessException(MyERPDomainErrorCodes.FiscalYearClosed)
                .WithData("postingDate", date.ToString("yyyy-MM-dd"));

        return fy;
    }

    private static InvoiceDiscountingDto ToDto(InvoiceDiscounting entity) => new()
    {
        Id = entity.Id,
        CompanyId = entity.CompanyId,
        PostingDate = entity.PostingDate,
        LoanStartDate = entity.LoanStartDate,
        LoanPeriodDays = entity.LoanPeriodDays,
        LoanEndDate = entity.LoanEndDate,
        Status = (int)entity.Status,
        TotalAmount = entity.TotalAmount,
        BankCharges = entity.BankCharges,
        ShortTermLoanAccountId = entity.ShortTermLoanAccountId,
        BankAccountId = entity.BankAccountId,
        BankChargesAccountId = entity.BankChargesAccountId,
        AccountsReceivableCreditAccountId = entity.AccountsReceivableCreditAccountId,
        AccountsReceivableDiscountedAccountId = entity.AccountsReceivableDiscountedAccountId,
        AccountsReceivableUnpaidAccountId = entity.AccountsReceivableUnpaidAccountId,
        SanctionJournalEntryId = entity.SanctionJournalEntryId,
        DisbursementJournalEntryId = entity.DisbursementJournalEntryId,
        SettlementJournalEntryId = entity.SettlementJournalEntryId,
    };

    private async Task<InvoiceDiscountingDto> ToDetailedDtoAsync(InvoiceDiscounting entity)
    {
        var dto = ToDto(entity);
        if (entity.Invoices.Count == 0) return dto;

        var salesInvoices = await LoadSalesInvoicesAsync(entity.Invoices.Select(i => i.SalesInvoiceId));
        var customerIds = entity.Invoices.Select(i => i.CustomerId).Distinct().ToList();
        var customerQuery = await _customerRepository.GetQueryableAsync();
        var customerNames = customerQuery.Where(c => customerIds.Contains(c.Id)).ToDictionary(c => c.Id, c => c.Name);

        dto.Invoices = entity.Invoices.Select(i => new InvoiceDiscountingInvoiceDto
        {
            SalesInvoiceId = i.SalesInvoiceId,
            InvoiceNumber = salesInvoices.TryGetValue(i.SalesInvoiceId, out var si) ? si.InvoiceNumber : null,
            CustomerId = i.CustomerId,
            CustomerName = customerNames.GetValueOrDefault(i.CustomerId, ""),
            OutstandingAmount = i.OutstandingAmount,
        }).ToList();

        return dto;
    }
}
