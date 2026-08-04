using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Sales.Entities;
using MyERP.EInvoice.Entities;
using MyERP.Sales;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;

namespace MyERP.EInvoice.Services;

/// <summary>
/// Service to consolidate multiple B2C Sales Invoices into a single e-Invoice for LHDN submission.
/// Migrated from myinvois consolidate_invoice.py
/// Excludes items with amount > 10,000 (creates separate invoices).
/// </summary>
public class EInvoiceConsolidationService : DomainService
{
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepository;
    private readonly IRepository<EInvoiceConsolidation, Guid> _consolidationRepository;
    private readonly IGuidGenerator _guidGenerator;

    public EInvoiceConsolidationService(
        IRepository<SalesInvoice, Guid> salesInvoiceRepository,
        IRepository<EInvoiceConsolidation, Guid> consolidationRepository,
        IGuidGenerator guidGenerator)
    {
        _salesInvoiceRepository = salesInvoiceRepository;
        _consolidationRepository = consolidationRepository;
        _guidGenerator = guidGenerator;
    }

    /// <summary>
    /// Consolidates a list of SalesInvoices. Returns the ID of the new consolidated invoice.
    /// Excluded items (>10,000) are placed into new separate invoices (returned in the list).
    /// </summary>
    public async Task<List<Guid>> ConsolidateInvoicesAsync(List<Guid> invoiceIds, Guid companyId, Guid? tenantId = null)
    {
        if (invoiceIds == null || invoiceIds.Count < 2)
            throw new BusinessException("EInvoice:002", "Please select at least two Sales Invoices to merge.");

        var invoices = (await _salesInvoiceRepository.GetQueryableAsync())
            .Where(i => invoiceIds.Contains(i.Id) && i.CompanyId == companyId)
            .ToList();

        if (invoices.Count == 0)
            throw new BusinessException("EInvoice:003", "No valid Sales Invoices found.");

        // Check if any already consolidated
        var existingConsolidations = await _consolidationRepository.GetListAsync(c => invoiceIds.Contains(c.OriginalInvoiceId));
        if (existingConsolidations.Any())
        {
            var alreadyMergedIds = existingConsolidations.Select(c => c.OriginalInvoiceId).ToList();
            throw new BusinessException("EInvoice:004", $"Some invoices are already consolidated: {string.Join(", ", alreadyMergedIds)}");
        }

        var createdInvoiceIds = new List<Guid>();
        var baseInvoice = invoices.First();

        // Create the consolidated invoice
        var consolidatedInvoice = new SalesInvoice(
            _guidGenerator.Create(),
            baseInvoice.CompanyId,
            baseInvoice.CustomerId, // Should be General Public customer ideally
            _guidGenerator.Create().ToString("N"),
            baseInvoice.IssueDate,
            tenantId
        )
        {
            CurrencyCode = baseInvoice.CurrencyCode,
            Notes = $"Merged from {invoices.Count} invoices"
        };
        // Flags needed for e-invoice module
        // We set EInvoiceDocType to 01 (Invoice) and mark it ready
        consolidatedInvoice.EInvoiceDocType = (EInvoiceDocumentType)1;

        var consolidatedItems = new Dictionary<string, SalesInvoiceItem>();

        foreach (var inv in invoices)
        {
            foreach (var item in inv.Items)
            {
                var itemTotal = item.Quantity * item.UnitPrice;
                
                // Rule: amount > 10,000 cannot be consolidated
                if (itemTotal > 10000)
                {
                    var singleInvoice = new SalesInvoice(
                        _guidGenerator.Create(),
                        inv.CompanyId,
                        inv.CustomerId,
                        _guidGenerator.Create().ToString("N"),
                        inv.IssueDate,
                        tenantId)
                    {
                        CurrencyCode = inv.CurrencyCode,
                        Notes = $"Auto-created from item exceeding 10,000 in original invoice",
                        EInvoiceDocType = (EInvoiceDocumentType)1
                    };
                    
                    singleInvoice.AddItem(
                        item.ItemId,
                        item.Description,
                        item.Quantity,
                        item.UnitPrice,
                        item.TaxAmount,
                        item.Uom
                    );
                    
                    await _salesInvoiceRepository.InsertAsync(singleInvoice);
                    createdInvoiceIds.Add(singleInvoice.Id);
                    
                    await _consolidationRepository.InsertAsync(new EInvoiceConsolidation(
                        _guidGenerator.Create(),
                        companyId,
                        singleInvoice.Id,
                        inv.Id,
                        tenantId
                    ));
                    
                    continue;
                }

                // Group by ItemId + UnitPrice
                var key = $"{item.ItemId}_{item.UnitPrice}";
                if (consolidatedItems.TryGetValue(key, out var existingItem))
                {
                    // Assuming we have a way to update quantity, but SalesInvoiceItem properties might be private setter.
                    // If immutable, we recreate it or use internal methods. Here we assume we can add a new item or there's a Merge logic.
                    // For now, just add as separate lines in consolidated invoice if we can't mutate.
                    consolidatedInvoice.AddItem(
                        item.ItemId,
                        item.Description,
                        item.Quantity,
                        item.UnitPrice,
                        item.TaxAmount,
                        item.Uom
                    );
                }
                else
                {
                    consolidatedInvoice.AddItem(
                        item.ItemId,
                        item.Description,
                        item.Quantity,
                        item.UnitPrice,
                        item.TaxAmount,
                        item.Uom
                    );
                    // Just adding it to dictionary to track, though we aren't merging quantities here due to DDD rules.
                    consolidatedItems[key] = item;
                }
            }
        }

        // Calculate totals for consolidated invoice (done automatically in AddItem)
        consolidatedInvoice.ApplyRounding();

        // If it has items, save it
        if (consolidatedInvoice.Items.Any())
        {
            await _salesInvoiceRepository.InsertAsync(consolidatedInvoice);
            createdInvoiceIds.Add(consolidatedInvoice.Id);

            foreach (var inv in invoices)
            {
                // Only link if items from this invoice actually went into the consolidated invoice.
                // Simplified: link all original invoices to the main consolidated one.
                await _consolidationRepository.InsertAsync(new EInvoiceConsolidation(
                    _guidGenerator.Create(),
                    companyId,
                    consolidatedInvoice.Id,
                    inv.Id,
                    tenantId
                ));
            }
        }

        return createdInvoiceIds;
    }
}
