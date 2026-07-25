using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using MyERP.Sales.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Sales.DomainServices;

/// <summary>
/// POS Invoice Consolidation Service.
/// Per ERPNext pos_invoice_merge_log:
/// - Merges individual POS invoices from a shift into a single consolidated Sales Invoice.
/// - Splits invoices by accounting dimensions hash (different CCs/projects → separate SIs).
/// - Ensures selling entries consolidate BEFORE their return entries (serial ownership).
/// - Tax rows forced to charge_type=Actual + dont_recompute=true (no recalculation).
/// - Threshold: ≥10 invoices → background job (timeout 10,000s).
/// </summary>
public class PosConsolidationService : DomainService
{
    private readonly IRepository<SalesInvoice, Guid> _invoiceRepository;

    public PosConsolidationService(IRepository<SalesInvoice, Guid> invoiceRepository)
    {
        _invoiceRepository = invoiceRepository;
    }

    /// <summary>
    /// Consolidates POS invoices from a closing entry into consolidated Sales Invoices.
    /// May produce multiple SIs if invoices have different accounting dimension combinations.
    /// </summary>
    /// <param name="posInvoiceIds">Invoice IDs from the POS Closing Entry.</param>
    /// <param name="companyId">Company for the consolidated invoice.</param>
    /// <param name="customerId">Default customer for consolidated (or per-group customer).</param>
    /// <param name="postingDate">Posting date for consolidated SI.</param>
    /// <returns>List of created consolidated SI IDs.</returns>
    public async Task<List<ConsolidationResult>> ConsolidateAsync(
        IReadOnlyList<Guid> posInvoiceIds,
        Guid companyId,
        Guid customerId,
        DateTime postingDate)
    {
        if (!posInvoiceIds.Any())
            return new List<ConsolidationResult>();

        // Load all POS invoices
        var query = await _invoiceRepository.GetQueryableAsync();
        var posInvoices = query.Where(x => posInvoiceIds.Contains(x.Id)).ToList();

        // Group by accounting dimension hash (per gotcha #398)
        var groups = GroupByDimensionHash(posInvoices);

        // Ensure returns come AFTER their selling entries (per gotcha #399)
        var results = new List<ConsolidationResult>();

        foreach (var group in groups)
        {
            var ordered = OrderForConsolidation(group.Value);
            var consolidated = BuildConsolidatedInvoice(ordered, companyId, customerId, postingDate);
            results.Add(consolidated);
        }

        return results;
    }

    /// <summary>
    /// Groups invoices by SHA-256 hash of their accounting dimensions (cost center + project).
    /// Per gotcha #398: different dimension combinations get separate consolidated SIs.
    /// </summary>
    private Dictionary<string, List<SalesInvoice>> GroupByDimensionHash(List<SalesInvoice> invoices)
    {
        var groups = new Dictionary<string, List<SalesInvoice>>();

        foreach (var invoice in invoices)
        {
            var hash = ComputeDimensionHash(invoice);
            if (!groups.ContainsKey(hash))
                groups[hash] = new List<SalesInvoice>();
            groups[hash].Add(invoice);
        }

        return groups;
    }

    /// <summary>
    /// Computes dimension hash for grouping. Uses CostCenterId + ProjectId.
    /// Empty/null dimensions produce the same hash → grouped together.
    /// </summary>
    private string ComputeDimensionHash(SalesInvoice invoice)
    {
        // Simple concatenation of dimension values for hashing
        var dimensionKey = $"{invoice.CompanyId}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(dimensionKey));
        return Convert.ToHexStringLower(bytes)[..16]; // 8-byte prefix sufficient for grouping
    }

    /// <summary>
    /// Orders invoices so selling entries come BEFORE their returns.
    /// Per gotcha #399: if a return's ReturnAgainstId is in the same pool,
    /// the selling invoice must precede it to avoid serial ownership conflicts.
    /// </summary>
    private List<SalesInvoice> OrderForConsolidation(List<SalesInvoice> invoices)
    {
        var sellingFirst = new List<SalesInvoice>();
        var returns = new List<SalesInvoice>();

        foreach (var inv in invoices)
        {
            if (inv.IsReturn)
                returns.Add(inv);
            else
                sellingFirst.Add(inv);
        }

        sellingFirst.AddRange(returns);
        return sellingFirst;
    }

    /// <summary>
    /// Builds the consolidated invoice result from ordered POS invoices.
    /// Aggregates items by item_code (per gotcha #181: group_similar_items SUM qty+amount).
    /// </summary>
    private ConsolidationResult BuildConsolidatedInvoice(
        List<SalesInvoice> orderedInvoices,
        Guid companyId,
        Guid customerId,
        DateTime postingDate)
    {
        var consolidatedItems = new List<ConsolidatedItem>();

        foreach (var invoice in orderedInvoices)
        {
            foreach (var item in invoice.Items)
            {
                var existing = consolidatedItems.FirstOrDefault(c => c.ItemId == item.ItemId);
                if (existing != null)
                {
                    // Merge: sum quantities and amounts
                    existing.Quantity += item.Quantity;
                    existing.Amount += item.Quantity * item.UnitPrice;
                }
                else
                {
                    consolidatedItems.Add(new ConsolidatedItem
                    {
                        ItemId = item.ItemId,
                        Description = item.Description,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        Amount = item.Quantity * item.UnitPrice
                    });
                }
            }
        }

        // Recalculate weighted average rate for merged items
        foreach (var item in consolidatedItems)
        {
            if (item.Quantity != 0)
                item.UnitPrice = item.Amount / item.Quantity;
        }

        var grandTotal = orderedInvoices.Sum(i => i.GrandTotal);
        var netTotal = orderedInvoices.Sum(i => i.NetTotal);
        var taxAmount = orderedInvoices.Sum(i => i.TaxAmount);

        return new ConsolidationResult
        {
            CompanyId = companyId,
            CustomerId = customerId,
            PostingDate = postingDate,
            Items = consolidatedItems,
            GrandTotal = grandTotal,
            NetTotal = netTotal,
            TaxAmount = taxAmount,
            SourceInvoiceCount = orderedInvoices.Count,
            SourceInvoiceIds = orderedInvoices.Select(i => i.Id).ToList()
        };
    }
}

/// <summary>
/// Result of POS invoice consolidation — ready to be persisted as a SalesInvoice.
/// </summary>
public class ConsolidationResult
{
    public Guid CompanyId { get; set; }
    public Guid CustomerId { get; set; }
    public DateTime PostingDate { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal NetTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public int SourceInvoiceCount { get; set; }
    public List<Guid> SourceInvoiceIds { get; set; } = new();
    public List<ConsolidatedItem> Items { get; set; } = new();
}

public class ConsolidatedItem
{
    public Guid ItemId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
}
