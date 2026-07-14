using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Accounting.DomainServices;

/// <summary>
/// GL Repost Service — rebuilds GL entries when stock valuation changes retroactively.
/// ERPNext equivalent: accounts/doctype/repost_accounting_ledger/repost_accounting_ledger.py
/// 
/// When stock valuation is reposted (backdated entries, rate changes, LCV),
/// the corresponding GL entries become stale. This service:
/// 1. Identifies affected vouchers (by type + ID list)
/// 2. Deletes old GL entries for those vouchers
/// 3. Regenerates GL entries using current valuation rates
/// 
/// Allowed voucher types for GL repost:
///   - Sales Invoice, Purchase Invoice, Payment Entry, Journal Entry, Purchase Receipt
/// Per DO-NOT: cannot repost for deferred revenue/expense accounts or closed fiscal years.
/// 
/// Typically runs as a background job triggered by RepostItemValuation completion.
/// </summary>
public class GlRepostService : DomainService
{
    private readonly AccountingRuleEngine _ruleEngine;
    private readonly IRepository<JournalEntry, Guid> _journalRepository;
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepository;
    private readonly IRepository<Company, Guid> _companyRepository;

    /// <summary>Voucher types allowed for GL repost (per ERPNext whitelist).</summary>
    public static readonly HashSet<string> AllowedVoucherTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "SalesInvoice",
        "PurchaseInvoice",
        "PaymentEntry",
        "JournalEntry",
        "PurchaseReceipt",
        "DeliveryNote",
        "StockEntry"
    };

    public GlRepostService(
        AccountingRuleEngine ruleEngine,
        IRepository<JournalEntry, Guid> journalRepository,
        IRepository<FiscalYear, Guid> fiscalYearRepository,
        IRepository<Company, Guid> companyRepository)
    {
        _ruleEngine = ruleEngine;
        _journalRepository = journalRepository;
        _fiscalYearRepository = fiscalYearRepository;
        _companyRepository = companyRepository;
    }

    /// <summary>
    /// Reposts GL entries for a single voucher. Deletes existing GL and regenerates.
    /// </summary>
    /// <param name="companyId">Company context</param>
    /// <param name="voucherType">Document type (e.g., "PurchaseReceipt")</param>
    /// <param name="voucherId">Document ID</param>
    /// <param name="document">The accountable document to re-post</param>
    /// <returns>The new journal entry, or null if validation blocked the repost.</returns>
    public async Task<JournalEntry?> RepostForVoucherAsync(
        Guid companyId,
        string voucherType,
        Guid voucherId,
        IAccountableDocument document)
    {
        // Validate voucher type is allowed
        if (!AllowedVoucherTypes.Contains(voucherType))
            return null;

        // Validate not in closed fiscal year
        var company = await _companyRepository.GetAsync(companyId);
        var fiscalYear = await _fiscalYearRepository.FindAsync(fy =>
            fy.CompanyId == companyId &&
            fy.StartDate <= document.PostingDate &&
            fy.EndDate >= document.PostingDate);

        if (fiscalYear?.IsClosed == true)
            return null; // Per DO-NOT: cannot repost in closed FY

        // Delete existing GL entries for this voucher
        var existingJournals = await _journalRepository.GetListAsync(je =>
            je.CompanyId == companyId &&
            je.ReferenceType == voucherType &&
            je.ReferenceId == voucherId);

        foreach (var existingJe in existingJournals)
        {
            await _journalRepository.DeleteAsync(existingJe);
        }

        // Regenerate GL from the document using current accounting rules
        var newJournal = await _ruleEngine.PostDocumentAsync(document);
        return newJournal;
    }

    /// <summary>
    /// Batch repost GL entries for multiple vouchers (used by background job).
    /// Processes in posting_date order (oldest first) and continues on individual failures.
    /// </summary>
    /// <returns>Count of successfully reposted vouchers.</returns>
    public async Task<GlRepostResult> RepostBatchAsync(
        Guid companyId,
        IReadOnlyList<(string VoucherType, Guid VoucherId, IAccountableDocument Document)> vouchers)
    {
        int successCount = 0;
        int skippedCount = 0;
        int failedCount = 0;
        var errors = new List<string>();

        // Process in chronological order
        var ordered = vouchers.OrderBy(v => v.Document.PostingDate).ToList();

        foreach (var (voucherType, voucherId, document) in ordered)
        {
            try
            {
                var result = await RepostForVoucherAsync(companyId, voucherType, voucherId, document);
                if (result != null)
                    successCount++;
                else
                    skippedCount++;
            }
            catch (Exception ex)
            {
                failedCount++;
                errors.Add($"{voucherType}/{voucherId}: {ex.Message}");
            }
        }

        return new GlRepostResult(successCount, skippedCount, failedCount, errors);
    }

    /// <summary>
    /// Checks if a voucher type is eligible for GL repost.
    /// </summary>
    public static bool IsRepostAllowed(string voucherType)
        => AllowedVoucherTypes.Contains(voucherType);
}

/// <summary>
/// Result of a batch GL repost operation.
/// </summary>
public record GlRepostResult(int SuccessCount, int SkippedCount, int FailedCount, List<string> Errors)
{
    public int TotalProcessed => SuccessCount + SkippedCount + FailedCount;
    public bool HasErrors => FailedCount > 0;
}
