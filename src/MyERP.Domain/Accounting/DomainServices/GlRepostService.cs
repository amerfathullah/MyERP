using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Accounting.DomainServices;

/// <summary>
/// GL Repost Service — rebuilds GL (and, for party-facing documents, PLE) entries when a document's
/// underlying data changes retroactively (stock valuation repost, backdated entry, manual account
/// correction). ERPNext equivalent: accounts/doctype/repost_accounting_ledger/repost_accounting_ledger.py
///
/// Every repost reverses the existing entries via contra-entry (this codebase's established
/// never-delete convention — see <see cref="DocumentPostingOrchestrator.ReverseGlForDocumentAsync"/>)
/// then rebuilds fresh from the document's current field values, and always persists the rebuilt
/// Journal Entry. An earlier version of this file deleted the old <c>JournalEntry</c> rows outright
/// and never inserted the replacement — every repost silently left the voucher with zero GL rows.
/// That was found and fixed in this pass; if you're reading this while investigating a "GL missing
/// after repost" report predating this comment, that bug is the likely cause.
///
/// Allowed voucher types for GL repost: Sales Invoice, Purchase Invoice, Purchase Receipt,
/// Delivery Note, Stock Entry. Payment Entry was removed from the allowed set in the same pass —
/// see the field's doc comment for why. Journal Entry stays listed for API/UI backward compatibility
/// but is a structural no-op: a JE's lines already ARE its ledger, so callers that resolve the
/// document (<c>GlRepostAppService.ResolveDocumentAsync</c>) get null for it and this service is
/// never actually invoked for that type.
/// </summary>
public class GlRepostService : DomainService
{
    private readonly AccountingRuleEngine _ruleEngine;
    private readonly DocumentPostingOrchestrator _postingOrchestrator;
    private readonly CommonPartyAccountingService _commonPartyService;
    private readonly IRepository<JournalEntry, Guid> _journalRepository;
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepository;
    private readonly IRepository<Company, Guid> _companyRepository;

    /// <summary>
    /// Voucher types allowed for GL repost. PaymentEntry is deliberately NOT included: its GL repost
    /// would need to also reverse+rebuild PLE across potentially multiple allocated references and
    /// re-derive its exchange-gain/loss JE (see PaymentEntryAppService.PostAsync) — a materially
    /// bigger, riskier piece of work than the SalesInvoice/PurchaseInvoice PLE handling added in this
    /// pass, deliberately scoped out rather than shipped half-correct. Silently reposting a PE's main
    /// GL while leaving its PLE stale would be worse than not supporting repost for it at all.
    /// </summary>
    public static readonly HashSet<string> AllowedVoucherTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "SalesInvoice",
        "PurchaseInvoice",
        "JournalEntry",
        "PurchaseReceipt",
        "DeliveryNote",
        "StockEntry"
    };

    public GlRepostService(
        AccountingRuleEngine ruleEngine,
        DocumentPostingOrchestrator postingOrchestrator,
        CommonPartyAccountingService commonPartyService,
        IRepository<JournalEntry, Guid> journalRepository,
        IRepository<FiscalYear, Guid> fiscalYearRepository,
        IRepository<Company, Guid> companyRepository)
    {
        _ruleEngine = ruleEngine;
        _postingOrchestrator = postingOrchestrator;
        _commonPartyService = commonPartyService;
        _journalRepository = journalRepository;
        _fiscalYearRepository = fiscalYearRepository;
        _companyRepository = companyRepository;
    }

    /// <summary>
    /// Reposts GL (and PLE, for SalesInvoice/PurchaseInvoice) for a single voucher: reverses the
    /// existing entries via contra-entry, then rebuilds fresh from the document's current field
    /// values. Returns the new Journal Entry, or null if the voucher type isn't allowed or its
    /// posting date falls in a closed fiscal year.
    /// </summary>
    public async Task<JournalEntry?> RepostForVoucherAsync(
        Guid companyId,
        string voucherType,
        Guid voucherId,
        IAccountableDocument document)
    {
        if (!AllowedVoucherTypes.Contains(voucherType))
            return null;

        var fiscalYear = await _fiscalYearRepository.FindAsync(fy =>
            fy.CompanyId == companyId &&
            fy.StartDate <= document.PostingDate &&
            fy.EndDate >= document.PostingDate);

        if (fiscalYear?.IsClosed == true)
            return null; // Per DO-NOT: cannot repost in closed FY

        switch (voucherType)
        {
            case "SalesInvoice":
                await _postingOrchestrator.ReversePleForDocumentAsync(voucherType, voucherId);
                await _postingOrchestrator.ReverseGlForDocumentAsync(voucherType, voucherId);
                return await RebuildSalesInvoiceGlAsync((SalesInvoice)document);

            case "PurchaseInvoice":
                await _postingOrchestrator.ReversePleForDocumentAsync(voucherType, voucherId);
                await _postingOrchestrator.ReverseGlForDocumentAsync(voucherType, voucherId);
                return await RebuildPurchaseInvoiceGlAsync((PurchaseInvoice)document);

            case "StockEntry":
                await _postingOrchestrator.ReverseGlForDocumentAsync(voucherType, voucherId);
                return await _postingOrchestrator.PostStockEntryAsync((StockEntry)document);

            default: // PurchaseReceipt, DeliveryNote — company-default-account GL rebuild only,
                     // no PLE (neither posts PLE). See class doc comment re: JournalEntry.
                await _postingOrchestrator.ReverseGlForDocumentAsync(voucherType, voucherId);
                return await RebuildGenericGlAsync(document);
        }
    }

    /// <summary>Pure GL/PLE build from current field values, no reversal — the shape a first-time
    /// Post() needs (nothing to reverse yet). SalesInvoiceAppService.PostAsync calls this.</summary>
    public async Task<JournalEntry> RebuildSalesInvoiceGlAsync(SalesInvoice invoice)
    {
        var company = await _companyRepository.GetAsync(invoice.CompanyId);
        var receivableAccountId = invoice.DebitToAccountId != Guid.Empty
            ? invoice.DebitToAccountId
            : company.DefaultReceivableAccountId ?? Guid.Empty;

        if (receivableAccountId == Guid.Empty)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.DefaultAccountNotConfigured)
                .WithData("reason", "No receivable account configured. Set Default Receivable Account in Company settings.");
        }

        if (company.DefaultExpenseAccountId.HasValue)
        {
            var expenseItems = invoice.Items
                .Select(i => new BudgetCheckItem(company.DefaultExpenseAccountId.Value, i.Quantity * i.UnitPrice))
                .ToList();
            await _postingOrchestrator.ValidateBudgetOnPostingAsync(
                invoice.CompanyId, invoice.IssueDate, expenseItems, invoice.TenantId);
        }

        var journal = await _postingOrchestrator.PostSalesInvoiceAsync(
            invoice,
            receivableAccountId: receivableAccountId,
            dueDate: invoice.DueDate,
            accountCurrency: invoice.CurrencyCode,
            exchangeRate: invoice.ExchangeRate);

        var reconciliation = await _commonPartyService.ReconcileAsync(new CommonPartyReconciliationContext
        {
            CompanyId = invoice.CompanyId,
            TenantId = invoice.TenantId,
            PostingDate = invoice.IssueDate,
            DocumentType = "SalesInvoice",
            DocumentId = invoice.Id,
            DocumentNumber = invoice.InvoiceNumber,
            PartyType = "Customer",
            PartyId = invoice.CustomerId,
            PartyAccountId = receivableAccountId,
            OutstandingAmount = invoice.OutstandingAmount,
            CurrencyCode = invoice.CurrencyCode,
            ExchangeRate = invoice.ExchangeRate,
            CostCenterId = invoice.CostCenterId,
            ProjectId = invoice.ProjectId,
            IsReturn = invoice.IsReturn,
        });
        if (reconciliation != null)
        {
            invoice.AmountPaid += reconciliation.ReconciledAmount;
        }

        return journal;
    }

    public async Task<JournalEntry> RebuildPurchaseInvoiceGlAsync(PurchaseInvoice invoice)
    {
        var company = await _companyRepository.GetAsync(invoice.CompanyId);
        var payableAccountId = invoice.CreditToAccountId != Guid.Empty
            ? invoice.CreditToAccountId
            : company.DefaultPayableAccountId ?? Guid.Empty;

        if (payableAccountId == Guid.Empty)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.DefaultAccountNotConfigured)
                .WithData("reason", "No payable account configured. Set Default Payable Account in Company settings.");
        }

        var updateStockSplit = await ResolveUpdateStockSplitAsync(invoice);

        if (company.DefaultExpenseAccountId.HasValue)
        {
            // Lines that go to inventory are not expense-budget spend — they buy an asset. Checking
            // them against the expense budget would reject an update_stock invoice for cost that
            // never reaches the expense account at all.
            var expenseItems = invoice.Items
                .Where(i => updateStockSplit == null || !updateStockSplit.StockItemIds.Contains(i.ItemId))
                .Select(i => new BudgetCheckItem(company.DefaultExpenseAccountId.Value, i.Quantity * i.UnitPrice))
                .ToList();
            if (expenseItems.Count > 0)
            {
                await _postingOrchestrator.ValidateBudgetOnPostingAsync(
                    invoice.CompanyId, invoice.IssueDate, expenseItems, invoice.TenantId);
            }
        }

        var journal = await _postingOrchestrator.PostPurchaseInvoiceAsync(
            invoice,
            payableAccountId: payableAccountId,
            dueDate: invoice.DueDate,
            accountCurrency: invoice.CurrencyCode,
            exchangeRate: invoice.ExchangeRate,
            warehouseStockSplit: updateStockSplit?.Lines);

        var reconciliation = await _commonPartyService.ReconcileAsync(new CommonPartyReconciliationContext
        {
            CompanyId = invoice.CompanyId,
            TenantId = invoice.TenantId,
            PostingDate = invoice.IssueDate,
            DocumentType = "PurchaseInvoice",
            DocumentId = invoice.Id,
            DocumentNumber = invoice.InvoiceNumber,
            PartyType = "Supplier",
            PartyId = invoice.SupplierId,
            PartyAccountId = payableAccountId,
            OutstandingAmount = invoice.OutstandingAmount,
            CurrencyCode = invoice.CurrencyCode,
            ExchangeRate = invoice.ExchangeRate,
            CostCenterId = invoice.CostCenterId,
            ProjectId = invoice.ProjectId,
            IsReturn = invoice.IsReturn,
        });
        if (reconciliation != null)
        {
            invoice.AmountPaid += reconciliation.ReconciledAmount;
        }

        return journal;
    }


    /// <summary>
    /// For a Purchase Invoice that moves stock itself (update_stock, i.e. a direct purchase with no
    /// Purchase Receipt), works out how much of the debit leg is inventory rather than expense, per
    /// stock account.
    /// </summary>
    /// <remarks>
    /// The seeded PI rules debit ItemExpense for the whole net total. That is correct for a service
    /// invoice, but for update_stock the goods are sitting in a warehouse, not consumed — ERPNext's
    /// PI gl_composer debits the warehouse's inventory account for stock items and leaves only
    /// non-stock lines on expense. Without this the invoice created real StockLedgerEntry and Bin
    /// rows while GL expensed the cost immediately, so inventory value never appeared on the books
    /// and the same cost was recognised twice — once here, again as COGS when the stock was sold.
    ///
    /// Returns null for anything that is not a stock-moving invoice, leaving GL composition exactly
    /// as it was.
    /// </remarks>
    private async Task<UpdateStockSplit?> ResolveUpdateStockSplitAsync(PurchaseInvoice invoice)
    {
        if (!invoice.UpdateStock || !invoice.WarehouseId.HasValue || invoice.IsReturn) return null;

        var itemRepository = LazyServiceProvider.LazyGetRequiredService<IRepository<Item, Guid>>();
        var warehouseAccountService = LazyServiceProvider
            .LazyGetRequiredService<Inventory.DomainServices.WarehouseAccountService>();

        var valueByWarehouse = new Dictionary<Guid, decimal>();
        var stockItemIds = new HashSet<Guid>();
        foreach (var item in invoice.Items)
        {
            // Service items keep their expense treatment — they never reached the stock ledger.
            var itemEntity = await itemRepository.FindAsync(item.ItemId);
            if (itemEntity != null && !itemEntity.MaintainStock) continue;
            stockItemIds.Add(item.ItemId);

            // Same value the stock ledger was written with: stock qty at the per-stock-unit rate,
            // so GL and the ledger cannot drift apart.
            var ratePerStockUnit = item.ConversionFactor != 0
                ? item.UnitPrice / item.ConversionFactor
                : item.UnitPrice;
            var warehouseId = item.WarehouseId ?? invoice.WarehouseId.Value;

            valueByWarehouse.TryGetValue(warehouseId, out var prior);
            valueByWarehouse[warehouseId] = prior + (item.StockQty * ratePerStockUnit);
        }

        if (valueByWarehouse.Count == 0) return null;

        var split = new List<WarehouseStockSplitLine>();
        foreach (var (warehouseId, value) in valueByWarehouse)
        {
            var accountId = await warehouseAccountService.ResolveStockAccountAsync(warehouseId, invoice.CompanyId);
            split.Add(new WarehouseStockSplitLine(accountId, value));
        }
        return new UpdateStockSplit(split, stockItemIds);
    }

    /// <summary>The inventory share of an update_stock invoice: where it posts, and which items it came from.</summary>
    private sealed record UpdateStockSplit(List<WarehouseStockSplitLine> Lines, HashSet<Guid> StockItemIds);

    /// <summary>Company-default-account GL build for voucher types with no dedicated account
    /// resolution or PLE handling (PurchaseReceipt, DeliveryNote) — same rule-engine call
    /// DocumentPostingOrchestrator's own bare (non-warehouse-specific) overloads use.</summary>
    private async Task<JournalEntry> RebuildGenericGlAsync(IAccountableDocument document)
    {
        var journal = await _ruleEngine.PostDocumentAsync(document);
        await _journalRepository.InsertAsync(journal);
        return journal;
    }

    /// <summary>
    /// Batch repost GL entries for multiple vouchers (used by background jobs and
    /// GlRepostAppService.RepostBatchAsync). Processes in posting-date order (oldest first) and
    /// continues on individual failures.
    /// </summary>
    public async Task<GlRepostResult> RepostBatchAsync(
        Guid companyId,
        IReadOnlyList<(string VoucherType, Guid VoucherId, IAccountableDocument Document)> vouchers)
    {
        int successCount = 0;
        int skippedCount = 0;
        int failedCount = 0;
        var errors = new List<string>();

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

    /// <summary>Checks if a voucher type is eligible for GL repost.</summary>
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
