using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

/// <summary>
/// Bank Clearance — marks Payment Entries and Journal Entries as cleared against a bank
/// statement by setting ClearanceDate. Per ERPNext accounts/doctype/bank_clearance.
/// Distinct from BankReconciliationAppService: reconciliation matches a bank statement LINE
/// to a voucher; clearance marks the voucher itself as confirmed-cleared at the bank
/// (drives the Bank Reconciliation Statement's outstanding/uncleared calculation).
/// </summary>
[Authorize(MyERPPermissions.PaymentEntries.Default)]
public class BankClearanceAppService : ApplicationService, IBankClearanceAppService
{
    /// <summary>JE voucher types that can touch a bank/cash GL account and thus be cleared.</summary>
    private static readonly JournalEntryVoucherType[] BankTouchingVoucherTypes =
    {
        JournalEntryVoucherType.BankEntry,
        JournalEntryVoucherType.ContraEntry,
        JournalEntryVoucherType.CreditCardEntry,
    };

    private readonly IRepository<PaymentEntry, Guid> _paymentEntryRepository;
    private readonly IRepository<JournalEntry, Guid> _journalEntryRepository;
    private readonly IRepository<JournalEntryLine, Guid> _journalEntryLineRepository;

    public BankClearanceAppService(
        IRepository<PaymentEntry, Guid> paymentEntryRepository,
        IRepository<JournalEntry, Guid> journalEntryRepository,
        IRepository<JournalEntryLine, Guid> journalEntryLineRepository)
    {
        _paymentEntryRepository = paymentEntryRepository;
        _journalEntryRepository = journalEntryRepository;
        _journalEntryLineRepository = journalEntryLineRepository;
    }

    public async Task<List<BankClearanceEntryDto>> GetEntriesAsync(GetBankClearanceEntriesInput input)
    {
        var entries = new List<BankClearanceEntryDto>();

        // Payment Entries touching this bank account (either side)
        var peQuery = await _paymentEntryRepository.GetQueryableAsync();
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
            entries.Add(new BankClearanceEntryDto
            {
                DocumentType = "PaymentEntry",
                DocumentId = pe.Id,
                DocumentNumber = pe.PaymentNumber ?? pe.Id.ToString()[..8],
                PostingDate = pe.PostingDate,
                Debit = isDeposit ? pe.PaidAmount : 0,
                Credit = isDeposit ? 0 : pe.PaidAmount,
                ReferenceNumber = pe.ReferenceNumber,
                ClearanceDate = pe.ClearanceDate,
            });
        }

        // Journal Entries (Bank/Contra/Credit Card type) with a line on this bank account.
        // Per ERPNext bank_clearance.py: opening entries excluded, lines aggregated per (account, je).
        var jeQuery = await _journalEntryRepository.GetQueryableAsync();
        var jeCandidates = jeQuery
            .Where(je => je.CompanyId == input.CompanyId
                && je.Status == DocumentStatus.Posted
                && !je.IsOpening
                && BankTouchingVoucherTypes.Contains(je.VoucherType)
                && je.PostingDate >= input.FromDate && je.PostingDate <= input.ToDate)
            .Where(je => input.IncludeCleared || je.ClearanceDate == null)
            .ToList();

        if (jeCandidates.Count > 0)
        {
            var jeIds = jeCandidates.Select(je => je.Id).ToHashSet();
            var lineQuery = await _journalEntryLineRepository.GetQueryableAsync();
            var lines = lineQuery
                .Where(l => l.AccountId == input.BankAccountId && jeIds.Contains(l.JournalEntryId))
                .ToList();

            var linesByJe = lines.GroupBy(l => l.JournalEntryId);
            foreach (var group in linesByJe)
            {
                var je = jeCandidates.First(j => j.Id == group.Key);
                entries.Add(new BankClearanceEntryDto
                {
                    DocumentType = "JournalEntry",
                    DocumentId = je.Id,
                    DocumentNumber = je.EntryNumber ?? je.Id.ToString()[..8],
                    PostingDate = je.PostingDate,
                    Debit = group.Where(l => l.IsDebit).Sum(l => l.Amount),
                    Credit = group.Where(l => !l.IsDebit).Sum(l => l.Amount),
                    ReferenceNumber = je.ReferenceNumber,
                    ClearanceDate = je.ClearanceDate,
                });
            }
        }

        return entries.OrderBy(e => e.PostingDate).ToList();
    }

    [Authorize(MyERPPermissions.PaymentEntries.Edit)]
    public async Task<BulkClearanceResultDto> SetClearanceDateAsync(SetClearanceDateDto input)
    {
        var updated = 0;

        foreach (var docRef in input.Entries)
        {
            switch (docRef.DocumentType)
            {
                case "PaymentEntry":
                    var pe = await _paymentEntryRepository.GetAsync(docRef.DocumentId);
                    pe.SetClearanceDate(input.ClearanceDate);
                    await _paymentEntryRepository.UpdateAsync(pe);
                    break;

                case "JournalEntry":
                    var je = await _journalEntryRepository.GetAsync(docRef.DocumentId);
                    je.SetClearanceDate(input.ClearanceDate);
                    await _journalEntryRepository.UpdateAsync(je);
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
