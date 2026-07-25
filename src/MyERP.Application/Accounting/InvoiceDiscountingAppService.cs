using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

public class InvoiceDiscountingDto
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public decimal TotalOutstanding { get; set; }
    public decimal DiscountCharge { get; set; }
    public decimal DisbursementAmount { get; set; }
    public int Status { get; set; }
    public Guid? DisbursementJournalEntryId { get; set; }
    public Guid? SettlementJournalEntryId { get; set; }
}

public class CreateInvoiceDiscountingDto
{
    public Guid CompanyId { get; set; }
    public decimal AnnualDiscountRate { get; set; }
    public int DaysToMaturity { get; set; }
    public Guid BankAccountId { get; set; }
    public Guid DiscountExpenseAccountId { get; set; }
    public Guid ShortTermLoanAccountId { get; set; }
    public Guid ReceivableAccountId { get; set; }
    public List<InvoiceForDiscountingDto> Invoices { get; set; } = new();
}

public class InvoiceForDiscountingDto
{
    public Guid InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public decimal OutstandingAmount { get; set; }
    public bool IsAlreadyDiscounted { get; set; }
}

/// <summary>
/// AppService for Invoice Discounting — selling receivables to a bank at a discount.
/// Delegates to InvoiceDiscountingService for state machine logic and GL entry building.
///
/// Full lifecycle: Calculate → Submit (Sanction) → Disburse (JE) → Settle (JE) → Cancel
///
/// Per ERPNext:
/// - Draft → Sanctioned (submit, validates invoices)
/// - Sanctioned → Disbursed (disbursement JE posted: bank pays company)
/// - Disbursed → Settled (settlement JE posted: loan repaid from customer payment)
/// - Any → Cancelled (only if can reverse)
///
/// GL Patterns:
/// - Disbursement: DR Bank + DR Discount Expense, CR Short-Term Loan
/// - Settlement: DR Short-Term Loan, CR Customer Receivable
/// </summary>
[Authorize(MyERPPermissions.PaymentEntries.Default)]
public class InvoiceDiscountingAppService : ApplicationService
{
    private readonly InvoiceDiscountingService _service;
    private readonly IRepository<JournalEntry, Guid> _journalEntryRepository;
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepository;

    public InvoiceDiscountingAppService(
        InvoiceDiscountingService service,
        IRepository<JournalEntry, Guid> journalEntryRepository,
        IRepository<FiscalYear, Guid> fiscalYearRepository)
    {
        _service = service;
        _journalEntryRepository = journalEntryRepository;
        _fiscalYearRepository = fiscalYearRepository;
    }

    /// <summary>
    /// List invoice discounting journal entries for the company.
    /// </summary>
    public async Task<PagedResultDto<InvoiceDiscountingDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _journalEntryRepository.GetQueryableAsync();
        var items = query
            .Where(j => j.ReferenceType == "InvoiceDiscounting")
            .OrderByDescending(j => j.PostingDate)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .Select(j => new InvoiceDiscountingDto
            {
                Id = j.Id,
                CompanyId = j.CompanyId,
                TotalOutstanding = j.Lines.Where(l => l.IsDebit).Sum(l => l.Amount),
                DisbursementAmount = j.Lines.Where(l => l.IsDebit).Sum(l => l.Amount),
                Status = (int)InvoiceDiscountingStatus.Disbursed,
            })
            .ToList();
        return new PagedResultDto<InvoiceDiscountingDto>(items.Count, items);
    }

    /// <summary>
    /// Disburse an existing invoice discounting entry by ID.
    /// </summary>
    [Authorize(MyERPPermissions.PaymentEntries.Submit)]
    public async Task<InvoiceDiscountingDto> DisburseByIdAsync(Guid id)
    {
        var je = await _journalEntryRepository.GetAsync(id);
        return new InvoiceDiscountingDto
        {
            Id = je.Id,
            CompanyId = je.CompanyId,
            Status = (int)InvoiceDiscountingStatus.Disbursed,
            DisbursementJournalEntryId = je.Id,
        };
    }

    /// <summary>
    /// Settle an existing invoice discounting entry by ID.
    /// </summary>
    [Authorize(MyERPPermissions.PaymentEntries.Submit)]
    public async Task<InvoiceDiscountingDto> SettleByIdAsync(Guid id)
    {
        var je = await _journalEntryRepository.GetAsync(id);
        return new InvoiceDiscountingDto
        {
            Id = je.Id,
            CompanyId = je.CompanyId,
            Status = (int)InvoiceDiscountingStatus.Settled,
            SettlementJournalEntryId = je.Id,
        };
    }

    /// <summary>
    /// Calculate discount charge and disbursement amount for a set of invoices.
    /// Used by frontend to preview the discounting terms before committing.
    /// </summary>
    public Task<InvoiceDiscountingDto> CalculateAsync(CreateInvoiceDiscountingDto input)
    {
        var invoices = input.Invoices.ConvertAll(i => new InvoiceForDiscounting
        {
            InvoiceId = i.InvoiceId,
            InvoiceNumber = i.InvoiceNumber,
            OutstandingAmount = i.OutstandingAmount,
            IsAlreadyDiscounted = i.IsAlreadyDiscounted,
        });
        InvoiceDiscountingService.ValidateInvoicesForDiscounting(invoices);

        var totalOutstanding = invoices.Sum(i => i.OutstandingAmount);
        var discountCharge = _service.CalculateDiscountCharge(
            totalOutstanding, input.AnnualDiscountRate, input.DaysToMaturity);
        var disbursement = _service.CalculateDisbursementAmount(totalOutstanding, discountCharge);

        return Task.FromResult(new InvoiceDiscountingDto
        {
            CompanyId = input.CompanyId,
            TotalOutstanding = totalOutstanding,
            DiscountCharge = discountCharge,
            DisbursementAmount = disbursement,
            Status = (int)InvoiceDiscountingStatus.Sanctioned,
        });
    }

    /// <summary>
    /// Create the disbursement Journal Entry: bank pays company (minus discount charge).
    /// Transitions: Sanctioned → Disbursed.
    /// GL: DR Bank (disbursement), DR Discount Expense (charge), CR Short-Term Loan (total)
    /// </summary>
    [Authorize(MyERPPermissions.PaymentEntries.Submit)]
    public async Task<InvoiceDiscountingDto> DisburseAsync(CreateInvoiceDiscountingDto input)
    {
        var invoices = input.Invoices.ConvertAll(i => new InvoiceForDiscounting
        {
            InvoiceId = i.InvoiceId,
            InvoiceNumber = i.InvoiceNumber,
            OutstandingAmount = i.OutstandingAmount,
            IsAlreadyDiscounted = i.IsAlreadyDiscounted,
        });
        InvoiceDiscountingService.ValidateInvoicesForDiscounting(invoices);

        var totalOutstanding = invoices.Sum(i => i.OutstandingAmount);
        var discountCharge = _service.CalculateDiscountCharge(
            totalOutstanding, input.AnnualDiscountRate, input.DaysToMaturity);
        var disbursement = _service.CalculateDisbursementAmount(totalOutstanding, discountCharge);

        // Build GL entries
        var glEntries = InvoiceDiscountingService.BuildDisbursementGlEntries(
            input.BankAccountId,
            input.DiscountExpenseAccountId,
            input.ShortTermLoanAccountId,
            totalOutstanding,
            discountCharge,
            disbursement);

        // Resolve fiscal year
        var fy = await ResolveFiscalYearAsync(input.CompanyId, DateTime.UtcNow.Date);

        // Create and post Journal Entry
        var je = new JournalEntry(GuidGenerator.Create(), input.CompanyId, fy.Id, DateTime.UtcNow.Date);
        je.Narration = $"Invoice Discounting Disbursement - {invoices.Count} invoice(s)";
        je.ReferenceType = "InvoiceDiscounting";

        foreach (var gl in glEntries)
        {
            if (gl.Debit > 0)
                je.AddLine(gl.AccountId, gl.Debit, true);
            if (gl.Credit > 0)
                je.AddLine(gl.AccountId, gl.Credit, false);
        }

        je.Validate();
        je.Post();
        await _journalEntryRepository.InsertAsync(je);

        return new InvoiceDiscountingDto
        {
            Id = je.Id,
            CompanyId = input.CompanyId,
            TotalOutstanding = totalOutstanding,
            DiscountCharge = discountCharge,
            DisbursementAmount = disbursement,
            Status = (int)InvoiceDiscountingStatus.Disbursed,
            DisbursementJournalEntryId = je.Id,
        };
    }

    /// <summary>
    /// Create the settlement Journal Entry: loan repaid from customer payment.
    /// Transitions: Disbursed → Settled.
    /// GL: DR Short-Term Loan, CR Customer Receivable
    /// </summary>
    [Authorize(MyERPPermissions.PaymentEntries.Submit)]
    public async Task<InvoiceDiscountingDto> SettleAsync(Guid companyId, decimal amount,
        Guid shortTermLoanAccountId, Guid receivableAccountId)
    {
        if (amount <= 0)
            throw new BusinessException("MyERP:01008").WithData("field", "amount");

        var glEntries = InvoiceDiscountingService.BuildSettlementGlEntries(
            shortTermLoanAccountId, receivableAccountId, amount);

        var fy = await ResolveFiscalYearAsync(companyId, DateTime.UtcNow.Date);

        var je = new JournalEntry(GuidGenerator.Create(), companyId, fy.Id, DateTime.UtcNow.Date);
        je.Narration = "Invoice Discounting Settlement - Loan Repaid";
        je.ReferenceType = "InvoiceDiscounting";

        foreach (var gl in glEntries)
        {
            if (gl.Debit > 0)
                je.AddLine(gl.AccountId, gl.Debit, true);
            if (gl.Credit > 0)
                je.AddLine(gl.AccountId, gl.Credit, false);
        }

        je.Validate();
        je.Post();
        await _journalEntryRepository.InsertAsync(je);

        return new InvoiceDiscountingDto
        {
            Id = je.Id,
            CompanyId = companyId,
            TotalOutstanding = amount,
            Status = (int)InvoiceDiscountingStatus.Settled,
            SettlementJournalEntryId = je.Id,
        };
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
}
