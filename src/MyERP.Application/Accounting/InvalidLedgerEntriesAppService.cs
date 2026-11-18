using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

[Authorize(MyERPPermissions.Accounts.Default)]
public class InvalidLedgerEntriesAppService : ApplicationService, IInvalidLedgerEntriesAppService
{
    private readonly IRepository<JournalEntry, Guid> _journalEntryRepository;
    private readonly IRepository<PaymentLedgerEntry, Guid> _pleRepository;
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepository;
    private readonly IRepository<PurchaseInvoice, Guid> _purchaseInvoiceRepository;
    private readonly IRepository<PaymentEntry, Guid> _paymentEntryRepository;
    private readonly IRepository<DeliveryNote, Guid> _deliveryNoteRepository;
    private readonly IRepository<PurchaseReceipt, Guid> _purchaseReceiptRepository;
    private readonly IRepository<StockEntry, Guid> _stockEntryRepository;
    private readonly IRepository<PeriodClosingVoucher, Guid> _periodClosingRepository;

    public InvalidLedgerEntriesAppService(
        IRepository<JournalEntry, Guid> journalEntryRepository,
        IRepository<PaymentLedgerEntry, Guid> pleRepository,
        IRepository<SalesInvoice, Guid> salesInvoiceRepository,
        IRepository<PurchaseInvoice, Guid> purchaseInvoiceRepository,
        IRepository<PaymentEntry, Guid> paymentEntryRepository,
        IRepository<DeliveryNote, Guid> deliveryNoteRepository,
        IRepository<PurchaseReceipt, Guid> purchaseReceiptRepository,
        IRepository<StockEntry, Guid> stockEntryRepository,
        IRepository<PeriodClosingVoucher, Guid> periodClosingRepository)
    {
        _journalEntryRepository = journalEntryRepository;
        _pleRepository = pleRepository;
        _salesInvoiceRepository = salesInvoiceRepository;
        _purchaseInvoiceRepository = purchaseInvoiceRepository;
        _paymentEntryRepository = paymentEntryRepository;
        _deliveryNoteRepository = deliveryNoteRepository;
        _purchaseReceiptRepository = purchaseReceiptRepository;
        _stockEntryRepository = stockEntryRepository;
        _periodClosingRepository = periodClosingRepository;
    }

    public async Task<InvalidLedgerEntriesReportDto> GetReportAsync(InvalidLedgerEntriesRequestDto input)
    {
        ValidateInput(input);

        var fromStart = input.FromDate.Date;
        var toExclusive = input.ToDate.Date.AddDays(1);
        var accountFilter = (input.AccountIds ?? new List<Guid>()).Where(id => id != Guid.Empty).ToHashSet();
        var voucherNoFilter = string.IsNullOrWhiteSpace(input.VoucherNo) ? null : input.VoucherNo.Trim();

        var candidates = new List<VoucherCandidate>();

        // 1. Scan posted Journal Entries (GL) in range
        var activeJEs = await AsyncExecuter.ToListAsync(
            (await _journalEntryRepository.WithDetailsAsync(j => j.Lines))
            .Where(j => j.CompanyId == input.CompanyId
                        && j.Status == DocumentStatus.Posted
                        && j.PostingDate >= fromStart
                        && j.PostingDate < toExclusive));

        foreach (var je in activeJEs)
        {
            var matchingLines = accountFilter.Count > 0
                ? je.Lines.Where(l => accountFilter.Contains(l.AccountId)).ToList()
                : je.Lines.ToList();

            if (accountFilter.Count > 0 && matchingLines.Count == 0)
                continue;

            if (je.ReferenceId.HasValue && TryNormalizeVoucherType(je.ReferenceType, out var refType))
            {
                candidates.Add(new VoucherCandidate(refType, je.ReferenceId.Value, je.ReferenceNumber, "GL Entry"));
            }

            foreach (var line in matchingLines)
            {
                if (line.AgainstVoucherId.HasValue && TryNormalizeVoucherType(line.AgainstVoucherType, out var againstType))
                {
                    candidates.Add(new VoucherCandidate(againstType, line.AgainstVoucherId.Value, null, "GL Entry"));
                }
            }
        }

        // 2. Scan active (non-delinked) Payment Ledger Entries in range
        var pleQuery = (await _pleRepository.GetQueryableAsync())
            .Where(p => !p.Delinked
                        && p.CompanyId == input.CompanyId
                        && p.PostingDate >= fromStart
                        && p.PostingDate < toExclusive);

        if (accountFilter.Count > 0)
        {
            pleQuery = pleQuery.Where(p => accountFilter.Contains(p.AccountId));
        }

        var activePles = await AsyncExecuter.ToListAsync(pleQuery);
        foreach (var ple in activePles)
        {
            if (ple.VoucherId != Guid.Empty && TryNormalizeVoucherType(ple.VoucherType, out var pleVoucherType))
            {
                candidates.Add(new VoucherCandidate(pleVoucherType, ple.VoucherId, null, "Payment Ledger Entry"));
            }

            if (ple.AgainstVoucherId != Guid.Empty && TryNormalizeVoucherType(ple.AgainstVoucherType, out var pleAgainstType))
            {
                candidates.Add(new VoucherCandidate(pleAgainstType, ple.AgainstVoucherId, null, "Payment Ledger Entry"));
            }
        }

        if (candidates.Count == 0)
        {
            return new InvalidLedgerEntriesReportDto
            {
                CompanyId = input.CompanyId,
                FromDate = fromStart,
                ToDate = input.ToDate.Date,
            };
        }

        // Batch-resolve status & document number for each distinct voucher type
        var idsByType = candidates
            .GroupBy(c => c.CanonicalType)
            .ToDictionary(g => g.Key, g => g.Select(x => x.VoucherId).Distinct().ToList());

        var statusLookup = await ResolveStatusesAsync(idsByType);

        var rowsByKey = new Dictionary<(string VoucherType, Guid VoucherId), InvalidLedgerEntryRowDto>();
        foreach (var candidate in candidates)
        {
            var key = (candidate.CanonicalType, candidate.VoucherId);
            var hasDoc = statusLookup.TryGetValue(key, out var docInfo);

            var isSubmitted = hasDoc && IsSubmittedStatus(docInfo.Status);
            if (isSubmitted)
                continue;

            var resolvedNo = hasDoc && !string.IsNullOrWhiteSpace(docInfo.VoucherNo)
                ? docInfo.VoucherNo!
                : !string.IsNullOrWhiteSpace(candidate.FallbackNo)
                    ? candidate.FallbackNo!
                    : candidate.VoucherId.ToString()[..8];

            if (voucherNoFilter != null
                && !string.Equals(resolvedNo, voucherNoFilter, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(candidate.VoucherId.ToString(), voucherNoFilter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var reason = hasDoc ? docInfo.Status.ToString() : "NotFound";
            if (!rowsByKey.TryGetValue(key, out var existing))
            {
                rowsByKey[key] = new InvalidLedgerEntryRowDto
                {
                    VoucherType = candidate.CanonicalType,
                    VoucherId = candidate.VoucherId,
                    VoucherNo = resolvedNo,
                    LedgerSource = candidate.LedgerSource,
                    Reason = reason,
                };
            }
            else if (!existing.LedgerSource.Contains(candidate.LedgerSource, StringComparison.Ordinal))
            {
                existing.LedgerSource = $"{existing.LedgerSource}, {candidate.LedgerSource}";
            }
        }

        return new InvalidLedgerEntriesReportDto
        {
            CompanyId = input.CompanyId,
            FromDate = fromStart,
            ToDate = input.ToDate.Date,
            Rows = rowsByKey.Values
                .OrderBy(r => r.VoucherType)
                .ThenBy(r => r.VoucherNo)
                .ToList(),
        };
    }

    private static void ValidateInput(InvalidLedgerEntriesRequestDto input)
    {
        if (input.CompanyId == Guid.Empty)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Company is mandatory.");
        }

        if (input.FromDate == default || input.ToDate == default)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "From Date and To Date are mandatory.");
        }

        if (input.FromDate.Date > input.ToDate.Date)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "From Date cannot be after To Date.");
        }
    }

    private async Task<Dictionary<(string VoucherType, Guid VoucherId), (DocumentStatus Status, string? VoucherNo)>>
        ResolveStatusesAsync(Dictionary<string, List<Guid>> idsByType)
    {
        var result = new Dictionary<(string VoucherType, Guid VoucherId), (DocumentStatus Status, string? VoucherNo)>();

        foreach (var (canonicalType, ids) in idsByType)
        {
            switch (canonicalType)
            {
                case "SalesInvoice":
                    foreach (var d in await _salesInvoiceRepository.GetListAsync(x => ids.Contains(x.Id)))
                        result[(canonicalType, d.Id)] = (d.Status, d.InvoiceNumber);
                    break;

                case "PurchaseInvoice":
                    foreach (var d in await _purchaseInvoiceRepository.GetListAsync(x => ids.Contains(x.Id)))
                        result[(canonicalType, d.Id)] = (d.Status, d.InvoiceNumber);
                    break;

                case "PaymentEntry":
                    foreach (var d in await _paymentEntryRepository.GetListAsync(x => ids.Contains(x.Id)))
                        result[(canonicalType, d.Id)] = (d.Status, d.PaymentNumber);
                    break;

                case "JournalEntry":
                    foreach (var d in await _journalEntryRepository.GetListAsync(x => ids.Contains(x.Id)))
                        result[(canonicalType, d.Id)] = (d.Status, d.EntryNumber ?? d.ReferenceNumber);
                    break;

                case "DeliveryNote":
                    foreach (var d in await _deliveryNoteRepository.GetListAsync(x => ids.Contains(x.Id)))
                        result[(canonicalType, d.Id)] = (d.Status, d.DeliveryNumber);
                    break;

                case "PurchaseReceipt":
                    foreach (var d in await _purchaseReceiptRepository.GetListAsync(x => ids.Contains(x.Id)))
                        result[(canonicalType, d.Id)] = (d.Status, d.ReceiptNumber);
                    break;

                case "StockEntry":
                    foreach (var d in await _stockEntryRepository.GetListAsync(x => ids.Contains(x.Id)))
                        result[(canonicalType, d.Id)] = (d.Status, d.EntryNumber);
                    break;

                case "PeriodClosingVoucher":
                    foreach (var d in await _periodClosingRepository.GetListAsync(x => ids.Contains(x.Id)))
                        result[(canonicalType, d.Id)] = (d.Status, null);
                    break;
            }
        }

        return result;
    }

    private static bool IsSubmittedStatus(DocumentStatus status)
        => status == DocumentStatus.Posted || status == DocumentStatus.Submitted;

    private static bool TryNormalizeVoucherType(string? rawType, out string canonicalType)
    {
        canonicalType = string.Empty;
        if (string.IsNullOrWhiteSpace(rawType))
            return false;

        switch (rawType.Trim().Replace(" ", string.Empty).ToUpperInvariant())
        {
            case "SALESINVOICE":
            case "POSINVOICE":
                canonicalType = "SalesInvoice";
                return true;
            case "PURCHASEINVOICE":
                canonicalType = "PurchaseInvoice";
                return true;
            case "PAYMENTENTRY":
                canonicalType = "PaymentEntry";
                return true;
            case "JOURNALENTRY":
                canonicalType = "JournalEntry";
                return true;
            case "DELIVERYNOTE":
                canonicalType = "DeliveryNote";
                return true;
            case "PURCHASERECEIPT":
                canonicalType = "PurchaseReceipt";
                return true;
            case "STOCKENTRY":
                canonicalType = "StockEntry";
                return true;
            case "PERIODCLOSINGVOUCHER":
                canonicalType = "PeriodClosingVoucher";
                return true;
            default:
                return false;
        }
    }

    private sealed record VoucherCandidate(
        string CanonicalType,
        Guid VoucherId,
        string? FallbackNo,
        string LedgerSource);
}
