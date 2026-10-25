using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

/// <summary>
/// Bank Clearance — marks Payment Entries, Journal Entries, POS Sales Invoices, and Paid Purchase Invoices
/// as cleared against a bank statement by setting ClearanceDate. Per ERPNext accounts/doctype/bank_clearance
/// and bank_clearance_summary (PR #60044 / commit b5ecdf00f1).
/// Distinct from BankReconciliationAppService: reconciliation matches a bank statement LINE
/// to a voucher; clearance marks the voucher itself as confirmed-cleared at the bank
/// (drives the Bank Reconciliation Statement's outstanding/uncleared calculation).
/// </summary>
[Authorize(MyERPPermissions.PaymentEntries.Default)]
public class BankClearanceAppService : ApplicationService, IBankClearanceAppService
{
    private readonly IRepository<PaymentEntry, Guid> _paymentEntryRepository;
    private readonly IRepository<JournalEntry, Guid> _journalEntryRepository;
    private readonly IRepository<JournalEntryLine, Guid> _journalEntryLineRepository;
    private readonly IRepository<Account, Guid>? _accountRepository;
    private readonly IRepository<MyERP.Core.Entities.Company, Guid>? _companyRepository;
    private readonly IRepository<SalesInvoice, Guid>? _salesInvoiceRepository;
    private readonly IRepository<PurchaseInvoice, Guid>? _purchaseInvoiceRepository;

    private IRepository<Account, Guid> AccountRepository =>
        _accountRepository ?? LazyServiceProvider.LazyGetRequiredService<IRepository<Account, Guid>>();

    private IRepository<MyERP.Core.Entities.Company, Guid> CompanyRepository =>
        _companyRepository ?? LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Core.Entities.Company, Guid>>();

    private IRepository<SalesInvoice, Guid> SalesInvoiceRepository =>
        _salesInvoiceRepository ?? LazyServiceProvider.LazyGetRequiredService<IRepository<SalesInvoice, Guid>>();

    private IRepository<PurchaseInvoice, Guid> PurchaseInvoiceRepository =>
        _purchaseInvoiceRepository ?? LazyServiceProvider.LazyGetRequiredService<IRepository<PurchaseInvoice, Guid>>();

    public BankClearanceAppService(
        IRepository<PaymentEntry, Guid> paymentEntryRepository,
        IRepository<JournalEntry, Guid> journalEntryRepository,
        IRepository<JournalEntryLine, Guid> journalEntryLineRepository,
        IRepository<Account, Guid>? accountRepository = null,
        IRepository<MyERP.Core.Entities.Company, Guid>? companyRepository = null,
        IRepository<SalesInvoice, Guid>? salesInvoiceRepository = null,
        IRepository<PurchaseInvoice, Guid>? purchaseInvoiceRepository = null)
    {
        _paymentEntryRepository = paymentEntryRepository;
        _journalEntryRepository = journalEntryRepository;
        _journalEntryLineRepository = journalEntryLineRepository;
        _accountRepository = accountRepository;
        _companyRepository = companyRepository;
        _salesInvoiceRepository = salesInvoiceRepository;
        _purchaseInvoiceRepository = purchaseInvoiceRepository;
    }

    public async Task<List<BankClearanceEntryDto>> GetEntriesAsync(GetBankClearanceEntriesInput input)
    {
        var entries = new List<BankClearanceEntryDto>();

        // Account and company currency resolution per ERPNext PR #60044 & PR #60024
        var account = await AccountRepository.FindAsync(input.BankAccountId);
        var company = await CompanyRepository.FindAsync(input.CompanyId);
        var companyCurrency = company?.CurrencyCode ?? "MYR";
        var bankCurrency = account?.Currency ?? companyCurrency;

        // Payment Entries touching this bank account (either side)
        // Per ERPNext PR #60044: payment amounts reflect after-tax paid/received amount in bank currency
        var peQuery = await _paymentEntryRepository.WithDetailsAsync(p => p.Taxes);
        var pes = peQuery
            .Where(p => p.CompanyId == input.CompanyId
                && p.Status == DocumentStatus.Posted
                && (p.PaidFromAccountId == input.BankAccountId || p.PaidToAccountId == input.BankAccountId)
                && p.PostingDate >= input.FromDate && p.PostingDate <= input.ToDate)
            .Where(p => input.IncludeCleared || p.ClearanceDate == null)
            .ToList();

        foreach (var pe in pes)
        {
            var isDeposit = pe.PaidToAccountId == input.BankAccountId;
            var bankSideAmount = pe.GetBankSideAmount(isDeposit);
            var effectiveExchangeRate = isDeposit
                ? (pe.TargetExchangeRate > 0 ? pe.TargetExchangeRate : pe.ExchangeRate)
                : pe.ExchangeRate;
            var baseSideAmount = bankSideAmount * (effectiveExchangeRate > 0 ? effectiveExchangeRate : 1m);
            var amount = BankReconciliationAppService.GetAmountInBankCurrency(bankCurrency, companyCurrency, bankSideAmount, baseSideAmount);

            entries.Add(new BankClearanceEntryDto
            {
                DocumentType = "PaymentEntry",
                DocumentId = pe.Id,
                DocumentNumber = pe.PaymentNumber ?? pe.Id.ToString()[..8],
                PostingDate = pe.PostingDate,
                Debit = isDeposit ? amount : 0,
                Credit = isDeposit ? 0 : amount,
                ReferenceNumber = pe.ReferenceNumber,
                ClearanceDate = pe.ClearanceDate,
            });
        }

        // Journal Entries with a line on this bank account.
        // Per ERPNext bank_clearance.py: opening entries excluded, lines aggregated per (account, je).
        var jeQuery = await _journalEntryRepository.GetQueryableAsync();
        var jeCandidates = jeQuery
            .Where(je => je.CompanyId == input.CompanyId
                && je.Status == DocumentStatus.Posted
                && !je.IsOpening
                && je.PostingDate >= input.FromDate && je.PostingDate <= input.ToDate)
            .Where(je => input.IncludeCleared || je.ClearanceDate == null)
            .ToList();

        if (jeCandidates.Count > 0)
        {
            var jeIds = jeCandidates.Select(je => je.Id).ToHashSet();
            var lineQuery = await _journalEntryLineRepository.GetQueryableAsync();
            var lines = lineQuery
                .Where(l => l.AccountId == input.BankAccountId
                    && jeIds.Contains(l.JournalEntryId)
                    && (l.FinanceBook == null || l.FinanceBook == ""))
                .ToList();

            var linesByJe = lines.GroupBy(l => l.JournalEntryId);
            foreach (var group in linesByJe)
            {
                var je = jeCandidates.First(j => j.Id == group.Key);
                var debit = group.Where(l => l.IsDebit)
                    .Sum(l => BankReconciliationAppService.GetAmountInBankCurrency(
                        bankCurrency, companyCurrency,
                        l.AmountInAccountCurrency != 0 ? l.AmountInAccountCurrency : l.Amount,
                        l.Amount));
                var credit = group.Where(l => !l.IsDebit)
                    .Sum(l => BankReconciliationAppService.GetAmountInBankCurrency(
                        bankCurrency, companyCurrency,
                        l.AmountInAccountCurrency != 0 ? l.AmountInAccountCurrency : l.Amount,
                        l.Amount));

                var docType = je.ReferenceType is "SalesInvoice" or "PurchaseInvoice" ? je.ReferenceType : "JournalEntry";
                var docNumber = je.EntryNumber ?? je.Id.ToString()[..8];
                var refNumber = je.ReferenceNumber;

                if (je.ReferenceType == "SalesInvoice" && je.ReferenceId.HasValue)
                {
                    try
                    {
                        var si = await SalesInvoiceRepository.FindAsync(je.ReferenceId.Value);
                        if (si != null)
                        {
                            docNumber = si.InvoiceNumber;
                            // Per ERPNext PR #60044 / commit b5ecdf00f1: show POS / invoice reference number
                            refNumber ??= si.Notes ?? si.InvoiceNumber;
                        }
                    }
                    catch
                    {
                        // Repository fallback if unavailable in unit test context
                    }
                }
                else if (je.ReferenceType == "PurchaseInvoice" && je.ReferenceId.HasValue)
                {
                    try
                    {
                        var pi = await PurchaseInvoiceRepository.FindAsync(je.ReferenceId.Value);
                        if (pi != null)
                        {
                            docNumber = pi.InvoiceNumber;
                            // Per ERPNext PR #60044: bill_no / supplier invoice number as cheque number
                            refNumber ??= pi.SupplierInvoiceNumber ?? pi.InvoiceNumber;
                        }
                    }
                    catch
                    {
                        // Repository fallback if unavailable in unit test context
                    }
                }

                entries.Add(new BankClearanceEntryDto
                {
                    DocumentType = docType,
                    DocumentId = je.Id,
                    DocumentNumber = docNumber,
                    PostingDate = je.PostingDate,
                    Debit = debit,
                    Credit = credit,
                    ReferenceNumber = refNumber,
                    ClearanceDate = je.ClearanceDate,
                });
            }
        }

        return entries.OrderBy(e => e.PostingDate).ThenBy(e => e.DocumentNumber).ToList();
    }

    [Authorize(MyERPPermissions.PaymentEntries.Edit)]
    public async Task<BulkClearanceResultDto> SetClearanceDateAsync(SetClearanceDateDto input)
    {
        var updated = 0;
        var btRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<BankTransaction, Guid>>();
        var btQueryable = await btRepo.GetQueryableAsync();

        foreach (var docRef in input.Entries)
        {
            var normalizedType = docRef.DocumentType.Replace(" ", "");

            switch (normalizedType)
            {
                case "PaymentEntry":
                    var pe = await _paymentEntryRepository.GetAsync(docRef.DocumentId);
                    if (pe.ClearanceDate == input.ClearanceDate)
                        continue;
                    if (btQueryable.Any(bt => bt.PaymentEntryId == pe.Id && bt.IsReconciled))
                    {
                        throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                            .WithData("detail", $"{pe.PaymentNumber ?? pe.Id.ToString()}: Please unreconcile the voucher before changing the clearance date.");
                    }
                    if (input.ClearanceDate.HasValue && input.ClearanceDate.Value.Date < pe.PostingDate.Date)
                    {
                        throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                            .WithData("detail", $"{pe.PaymentNumber ?? pe.Id.ToString()}: Clearance date cannot be before posting date.");
                    }
                    pe.SetClearanceDate(input.ClearanceDate);
                    await _paymentEntryRepository.UpdateAsync(pe);
                    break;

                case "JournalEntry":
                    var je = await _journalEntryRepository.GetAsync(docRef.DocumentId);
                    if (je.ClearanceDate == input.ClearanceDate)
                        continue;
                    if (btQueryable.Any(bt => bt.JournalEntryId == je.Id && bt.IsReconciled))
                    {
                        throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                            .WithData("detail", $"{je.EntryNumber ?? je.Id.ToString()}: Please unreconcile the voucher before changing the clearance date.");
                    }
                    if (input.ClearanceDate.HasValue && input.ClearanceDate.Value.Date < je.PostingDate.Date)
                    {
                        throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                            .WithData("detail", $"{je.EntryNumber ?? je.Id.ToString()}: Clearance date cannot be before posting date.");
                    }
                    je.SetClearanceDate(input.ClearanceDate);
                    await _journalEntryRepository.UpdateAsync(je);
                    break;

                case "SalesInvoice":
                case "PurchaseInvoice":
                    // Support clearance by invoice ID or direct JournalEntry ID (PR #60044)
                    var jeQuery = await _journalEntryRepository.GetQueryableAsync();
                    var linkedJe = jeQuery.FirstOrDefault(j =>
                        j.Id == docRef.DocumentId
                        || (j.ReferenceType == normalizedType && j.ReferenceId == docRef.DocumentId));
                    if (linkedJe != null)
                    {
                        if (linkedJe.ClearanceDate == input.ClearanceDate)
                            continue;
                        if (input.ClearanceDate.HasValue && input.ClearanceDate.Value.Date < linkedJe.PostingDate.Date)
                        {
                            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                                .WithData("detail", $"{linkedJe.EntryNumber ?? linkedJe.Id.ToString()}: Clearance date cannot be before posting date.");
                        }
                        linkedJe.SetClearanceDate(input.ClearanceDate);
                        await _journalEntryRepository.UpdateAsync(linkedJe);
                    }
                    else
                    {
                        throw new BusinessException(MyERPDomainErrorCodes.EntityNotFound)
                            .WithData("detail", $"No journal entry found for {docRef.DocumentType} {docRef.DocumentId}");
                    }
                    break;

                default:
                    throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                        .WithData("detail", $"Unsupported document type for clearance: {docRef.DocumentType}");
            }

            updated++;
        }

        return new BulkClearanceResultDto { UpdatedCount = updated };
    }
}
