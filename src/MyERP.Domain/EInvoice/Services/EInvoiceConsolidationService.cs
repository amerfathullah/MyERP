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
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IGuidGenerator _guidGenerator;

    public EInvoiceConsolidationService(
        IRepository<SalesInvoice, Guid> salesInvoiceRepository,
        IRepository<EInvoiceConsolidation, Guid> consolidationRepository,
        IRepository<Customer, Guid> customerRepository,
        IGuidGenerator guidGenerator)
    {
        _salesInvoiceRepository = salesInvoiceRepository;
        _consolidationRepository = consolidationRepository;
        _customerRepository = customerRepository;
        _guidGenerator = guidGenerator;
    }

    /// <summary>
    /// Consolidates a list of SalesInvoices. Returns the ID of the new consolidated invoice.
    /// Excluded items (>10,000) are placed into new separate invoices (returned in the list).
    /// </summary>
    public async Task<List<Guid>> ConsolidateInvoicesAsync(List<Guid> invoiceIds, Guid companyId, Guid? tenantId = null)
    {
        if (invoiceIds == null || invoiceIds.Count < 2)
            throw new BusinessException(MyERPDomainErrorCodes.EInvoiceConsolidationAtLeastTwoInvoices, "Please select at least two Sales Invoices to merge.");

        var invoices = (await _salesInvoiceRepository.WithDetailsAsync(i => i.Items))
            .Where(i => invoiceIds.Contains(i.Id) && i.CompanyId == companyId)
            .ToList();

        if (invoices.Count == 0)
            throw new BusinessException(MyERPDomainErrorCodes.EInvoiceConsolidationNoValidInvoices, "No valid Sales Invoices found.");

        // B2C consolidation only — reject any invoice whose buyer has a real registered TIN.
        // LHDN requires B2B invoices to carry the actual buyer TIN on their own individual
        // e-Invoice; merging one into an anonymous consolidated document would lose that TIN
        // entirely. Checked here too (not just at the candidate-list level) since this method
        // is the one that actually performs the merge and shouldn't trust its caller.
        var customerIds = invoices.Select(i => i.CustomerId).Distinct().ToList();
        var customerTins = (await _customerRepository.GetQueryableAsync())
            .Where(c => customerIds.Contains(c.Id))
            .ToDictionary(c => c.Id, c => c.Tin);
        var b2bInvoices = invoices
            .Where(i => !string.IsNullOrWhiteSpace(i.BuyerTin)
                     || (customerTins.TryGetValue(i.CustomerId, out var tin) && !string.IsNullOrWhiteSpace(tin)))
            .ToList();
        if (b2bInvoices.Count > 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.EInvoiceConsolidationB2BNotAllowed, "Cannot consolidate B2B invoices — invoices with a registered buyer TIN must be submitted individually.")
                .WithData("invoiceNumbers", string.Join(", ", b2bInvoices.Select(i => i.InvoiceNumber)));
        }

        // Check if any already consolidated
        var existingConsolidations = await _consolidationRepository.GetListAsync(c => invoiceIds.Contains(c.OriginalInvoiceId));
        if (existingConsolidations.Any())
        {
            var alreadyMergedIds = existingConsolidations.Select(c => c.OriginalInvoiceId).ToList();
            throw new BusinessException(MyERPDomainErrorCodes.EInvoiceConsolidationAlreadyConsolidated, $"Some invoices are already consolidated: {string.Join(", ", alreadyMergedIds)}");
        }

        var createdInvoiceIds = new List<Guid>();
        var baseInvoice = invoices.First();
        var earliestIssueDate = invoices.Min(i => i.IssueDate);
        var latestDueDate = invoices.Max(i => i.DueDate ?? i.IssueDate);

        // Create the consolidated invoice
        var consolidatedInvoice = new SalesInvoice(
            _guidGenerator.Create(),
            baseInvoice.CompanyId,
            baseInvoice.CustomerId, // Should be General Public customer ideally
            _guidGenerator.Create().ToString("N"),
            earliestIssueDate,
            tenantId
        )
        {
            DueDate = latestDueDate,
            CurrencyCode = baseInvoice.CurrencyCode,
            Notes = $"Merged from {invoices.Count} invoices",
            BuyerTin = EInvoiceConsts.GenericBuyerTin,
            IsConsolidated = true,
            EInvoiceDocType = EInvoiceDocumentType.Invoice,
            EInvoiceStatus = EInvoiceStatus.NotSubmitted
        };

        var consolidatedItems = new Dictionary<string, SalesInvoiceItem>();
        var singleInvoicesToCreate = new List<(SalesInvoice singleInvoice, Guid originalInvoiceId)>();

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
                        DueDate = inv.DueDate,
                        CurrencyCode = inv.CurrencyCode,
                        Notes = "Auto-created from item exceeding 10,000 in original invoice",
                        BuyerTin = !string.IsNullOrWhiteSpace(inv.BuyerTin) ? inv.BuyerTin : EInvoiceConsts.GenericBuyerTin,
                        IsConsolidated = false,
                        EInvoiceDocType = EInvoiceDocumentType.Invoice,
                        EInvoiceStatus = EInvoiceStatus.NotSubmitted
                    };
                    
                    singleInvoice.AddItem(
                        item.ItemId,
                        item.Description,
                        item.Quantity,
                        item.UnitPrice,
                        item.TaxAmount,
                        item.Uom
                    );
                    
                    singleInvoicesToCreate.Add((singleInvoice, inv.Id));
                    continue;
                }

                // Group by ItemId + UnitPrice
                var key = $"{item.ItemId}_{item.UnitPrice}";
                if (consolidatedItems.TryGetValue(key, out var existingItem))
                {
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
                    consolidatedItems[key] = item;
                }
            }
        }

        // Rule: if all items were excluded (>10,000), abort without saving (consolidate_invoice.py line 841)
        if (!consolidatedInvoice.Items.Any())
        {
            throw new BusinessException(
                MyERPDomainErrorCodes.EInvoiceAllItemsExceedConsolidationThreshold,
                "All items were excluded because their amount exceeded 10,000. Consolidated invoice not created.");
        }

        // Calculate totals for consolidated invoice (done automatically in AddItem)
        consolidatedInvoice.ApplyRounding();

        await _salesInvoiceRepository.InsertAsync(consolidatedInvoice);
        createdInvoiceIds.Add(consolidatedInvoice.Id);

        foreach (var inv in invoices)
        {
            inv.ConsolidatedSalesInvoiceId = consolidatedInvoice.Id;
            await _salesInvoiceRepository.UpdateAsync(inv);

            await _consolidationRepository.InsertAsync(new EInvoiceConsolidation(
                _guidGenerator.Create(),
                companyId,
                consolidatedInvoice.Id,
                inv.Id,
                tenantId
            ));
        }

        // Persist excluded single invoices (>10,000)
        foreach (var (singleInvoice, originalInvId) in singleInvoicesToCreate)
        {
            singleInvoice.ApplyRounding();
            await _salesInvoiceRepository.InsertAsync(singleInvoice);
            createdInvoiceIds.Add(singleInvoice.Id);

            await _consolidationRepository.InsertAsync(new EInvoiceConsolidation(
                _guidGenerator.Create(),
                companyId,
                singleInvoice.Id,
                originalInvId,
                tenantId
            ));
        }

        return createdInvoiceIds;
    }
}
