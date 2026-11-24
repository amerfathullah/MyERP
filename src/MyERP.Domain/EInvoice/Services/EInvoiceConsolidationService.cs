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

        // Per LHDN MyInvois consolidation rules (consolidate_invoice.py):
        // reject Cancelled, already-consolidated parent/child invoices, Return/Credit/Debit Notes,
        // already-submitted LHDN invoices, and mixed currencies.
        var cancelled = invoices.Where(i => i.Status == Core.DocumentStatus.Cancelled).ToList();
        if (cancelled.Count > 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Cannot consolidate cancelled invoices: {string.Join(", ", cancelled.Select(i => i.InvoiceNumber))}");
        }

        var alreadyConsolidatedInvoices = invoices.Where(i => i.IsConsolidated || i.ConsolidatedSalesInvoiceId.HasValue).ToList();
        if (alreadyConsolidatedInvoices.Count > 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.EInvoiceConsolidationAlreadyConsolidated,
                $"Some invoices are already consolidated: {string.Join(", ", alreadyConsolidatedInvoices.Select(i => i.InvoiceNumber))}");
        }

        var alreadySubmitted = invoices
            .Where(i => i.EInvoiceStatus is EInvoiceStatus.Pending or EInvoiceStatus.Valid)
            .ToList();
        if (alreadySubmitted.Count > 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Cannot consolidate invoices already submitted to LHDN: {string.Join(", ", alreadySubmitted.Select(i => i.InvoiceNumber))}");
        }

        var returnsOrNotes = invoices.Where(i => i.IsReturn || i.IsDebitNote).ToList();
        if (returnsOrNotes.Count > 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Cannot consolidate Credit/Debit Note invoices into a standard consolidated e-Invoice: {string.Join(", ", returnsOrNotes.Select(i => i.InvoiceNumber))}");
        }

        var distinctCurrencies = invoices
            .Select(i => string.IsNullOrWhiteSpace(i.CurrencyCode) ? "MYR" : i.CurrencyCode.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();
        if (distinctCurrencies.Count > 1)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Cannot consolidate invoices with mixed currencies ({string.Join(", ", distinctCurrencies)}).");
        }

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
            ExchangeRate = baseInvoice.ExchangeRate > 0 ? baseInvoice.ExchangeRate : 1m,
            Notes = $"Merged from {invoices.Count} invoices",
            BuyerTin = EInvoiceConsts.GenericBuyerTin,
            IsConsolidated = true,
            IsSubmitToLhdn = true,
            EInvoiceDocType = EInvoiceDocumentType.Invoice,
            EInvoiceStatus = EInvoiceStatus.NotSubmitted
        };

        var singleInvoicesToCreate = new List<(SalesInvoice singleInvoice, Guid originalInvoiceId)>();

        foreach (var inv in invoices)
        {
            var invRate = inv.ExchangeRate > 0 ? inv.ExchangeRate : 1m;
            foreach (var item in inv.Items)
            {
                var itemBaseTotal = item.Quantity * item.UnitPrice * invRate;

                // LHDN rule (consolidate_invoice.py): line items exceeding RM 10,000 cannot be consolidated
                if (itemBaseTotal > 10000m)
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
                        ExchangeRate = invRate,
                        Notes = "Auto-created from item exceeding 10,000 in original invoice",
                        BuyerTin = !string.IsNullOrWhiteSpace(inv.BuyerTin) ? inv.BuyerTin : EInvoiceConsts.GenericBuyerTin,
                        IsConsolidated = false,
                        IsSubmitToLhdn = true,
                        EInvoiceDocType = EInvoiceDocumentType.Invoice,
                        EInvoiceStatus = EInvoiceStatus.NotSubmitted
                    };

                    AddCopiedItem(singleInvoice, item, item.ClassificationCode);
                    singleInvoicesToCreate.Add((singleInvoice, inv.Id));
                    continue;
                }

                // Consolidated B2C lines always carry LHDN classification code "004" (consolidate_invoice.py line 837)
                AddCopiedItem(consolidatedInvoice, item, EInvoiceConsts.ConsolidatedClassificationCode);
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

    private static void AddCopiedItem(SalesInvoice target, SalesInvoiceItem source, string? classificationCode)
    {
        target.AddItem(
            source.ItemId,
            source.Description,
            source.Quantity,
            source.UnitPrice,
            source.TaxAmount,
            source.Uom);
        var added = target.Items.Last();
        added.TaxCategoryId = source.TaxCategoryId;
        if (!string.IsNullOrWhiteSpace(classificationCode))
        {
            added.ClassificationCode = classificationCode;
        }
    }

    /// <summary>
    /// Determines whether GL and AR ledger impact should be skipped for a consolidated LHDN e-Invoice
    /// per myinvois <c>consolidate_invoice.py</c> and <c>original.py: validate_before()</c>.
    /// Consolidated invoices aggregate already-booked B2C invoices solely for LHDN submission and must
    /// never post duplicate GL entries or hold AR outstanding balance.
    /// </summary>
    public static bool ShouldSkipGlPostingForConsolidatedInvoice(SalesInvoice invoice)
    {
        return invoice != null && invoice.IsConsolidated;
    }

    /// <summary>
    /// Merges tax rows from multiple source Sales Invoices being consolidated into a single LHDN
    /// consolidated e-Invoice by grouping on <c>(AccountHead, ChargeType)</c> and summing
    /// <c>TaxAmount</c> per myinvois <c>consolidate_invoice.py: merge_sales_invoices()</c>.
    /// </summary>
    public static List<(string AccountHead, string ChargeType, decimal TaxAmount)> MergeConsolidatedTaxes(
        IEnumerable<(string AccountHead, string ChargeType, decimal TaxAmount)>? sourceTaxes)
    {
        var merged = new Dictionary<(string AccountHead, string ChargeType), decimal>();
        var order = new List<(string AccountHead, string ChargeType)>();

        if (sourceTaxes == null)
            return new List<(string AccountHead, string ChargeType, decimal TaxAmount)>();

        foreach (var tax in sourceTaxes)
        {
            var key = (tax.AccountHead ?? string.Empty, tax.ChargeType ?? string.Empty);
            if (!merged.ContainsKey(key))
            {
                merged[key] = tax.TaxAmount;
                order.Add(key);
            }
            else
            {
                merged[key] += tax.TaxAmount;
            }
        }

        return order.Select(k => (k.AccountHead, k.ChargeType, merged[k])).ToList();
    }
}
