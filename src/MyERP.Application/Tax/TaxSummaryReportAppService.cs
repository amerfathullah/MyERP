using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Sales.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Tax.Entities;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Tax;

/// <summary>
/// Tax Summary Report for SST filing (Malaysia).
/// Aggregates output tax (collected on sales) and input tax (paid on purchases)
/// for a filing period. Used to calculate net tax payable/refundable to Customs.
/// 
/// Per Malaysian SST:
/// - Sales Tax: 5% or 10% on manufactured/imported goods
/// - Service Tax: 6% on prescribed services
/// - Net payable = Output Tax - Input Tax (if positive → pay, if negative → refund/carry forward)
/// </summary>
[Authorize]
public class TaxSummaryReportAppService : ApplicationService, ITaxSummaryReportAppService
{
    private readonly IRepository<SalesInvoice, Guid> _siRepository;
    private readonly IRepository<PurchaseInvoice, Guid> _piRepository;
    private readonly IRepository<TransactionTaxRow, Guid> _taxRowRepository;
    private readonly IRepository<Inventory.Entities.Item, Guid> _itemRepository;
    private readonly IRepository<TaxCategory, Guid> _taxCategoryRepository;

    public TaxSummaryReportAppService(
        IRepository<SalesInvoice, Guid> siRepository,
        IRepository<PurchaseInvoice, Guid> piRepository,
        IRepository<TransactionTaxRow, Guid> taxRowRepository,
        IRepository<Inventory.Entities.Item, Guid> itemRepository,
        IRepository<TaxCategory, Guid> taxCategoryRepository)
    {
        _siRepository = siRepository;
        _piRepository = piRepository;
        _taxRowRepository = taxRowRepository;
        _itemRepository = itemRepository;
        _taxCategoryRepository = taxCategoryRepository;
    }

    /// <summary>
    /// Generates the SST filing summary for a company within a tax period.
    /// </summary>
    public async Task<TaxSummaryDto> GetTaxSummaryAsync(Guid companyId, DateTime fromDate, DateTime toDate)
    {
        var siQuery = await _siRepository.GetQueryableAsync();
        var piQuery = await _piRepository.GetQueryableAsync();

        // Output Tax: total tax collected on posted sales invoices (excluding returns/credit notes)
        var salesInvoices = siQuery
            .Where(si => si.CompanyId == companyId
                && si.Status == Core.DocumentStatus.Posted
                && si.IssueDate >= fromDate && si.IssueDate <= toDate)
            .ToList();

        decimal outputTax = salesInvoices
            .Where(si => !si.IsReturn)
            .Sum(si => si.TaxAmount);

        decimal creditNoteTax = salesInvoices
            .Where(si => si.IsReturn)
            .Sum(si => Math.Abs(si.TaxAmount));

        decimal netOutputTax = outputTax - creditNoteTax;

        decimal totalSalesNetAmount = salesInvoices
            .Where(si => !si.IsReturn)
            .Sum(si => si.NetTotal);

        // Input Tax: total tax paid on posted purchase invoices (excluding debit notes)
        var purchaseInvoices = piQuery
            .Where(pi => pi.CompanyId == companyId
                && pi.Status == Core.DocumentStatus.Posted
                && pi.IssueDate >= fromDate && pi.IssueDate <= toDate)
            .ToList();

        decimal inputTax = purchaseInvoices
            .Where(pi => !pi.IsReturn)
            .Sum(pi => pi.TaxAmount);

        decimal debitNoteTax = purchaseInvoices
            .Where(pi => pi.IsReturn)
            .Sum(pi => Math.Abs(pi.TaxAmount));

        decimal netInputTax = inputTax - debitNoteTax;

        decimal totalPurchaseNetAmount = purchaseInvoices
            .Where(pi => !pi.IsReturn)
            .Sum(pi => pi.NetTotal);

        // Net Tax Position
        decimal netTaxPayable = netOutputTax - netInputTax;

        // Break down by tax rate (group by TaxAmount/NetTotal ratio for each invoice)
        var outputBreakdown = BuildTaxBreakdown(salesInvoices.Where(si => !si.IsReturn));
        var inputBreakdown = BuildTaxBreakdown(purchaseInvoices.Where(pi => !pi.IsReturn));

        return new TaxSummaryDto
        {
            CompanyId = companyId,
            FromDate = fromDate,
            ToDate = toDate,
            // Output (Sales)
            TotalSalesAmount = totalSalesNetAmount,
            OutputTax = outputTax,
            CreditNoteTaxAdjustment = creditNoteTax,
            NetOutputTax = netOutputTax,
            SalesInvoiceCount = salesInvoices.Count(si => !si.IsReturn),
            CreditNoteCount = salesInvoices.Count(si => si.IsReturn),
            // Input (Purchases)
            TotalPurchaseAmount = totalPurchaseNetAmount,
            InputTax = inputTax,
            DebitNoteTaxAdjustment = debitNoteTax,
            NetInputTax = netInputTax,
            PurchaseInvoiceCount = purchaseInvoices.Count(pi => !pi.IsReturn),
            DebitNoteCount = purchaseInvoices.Count(pi => pi.IsReturn),
            // Net Position
            NetTaxPayable = netTaxPayable,
            IsRefundable = netTaxPayable < 0,
            // Breakdowns
            OutputTaxBreakdown = outputBreakdown,
            InputTaxBreakdown = inputBreakdown,
        };
    }

    /// <summary>Batch-resolves ItemId -> TaxType via Item.TaxCategoryId -> TaxCategory.TaxType.</summary>
    private async Task<Dictionary<Guid, TaxType>> ResolveItemTaxTypesAsync(IEnumerable<Guid> itemIds)
    {
        var ids = itemIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, TaxType>();

        var itemQuery = await _itemRepository.GetQueryableAsync();
        var itemCategoryMap = itemQuery
            .Where(i => ids.Contains(i.Id) && i.TaxCategoryId.HasValue)
            .Select(i => new { i.Id, TaxCategoryId = i.TaxCategoryId!.Value })
            .ToList();

        var categoryIds = itemCategoryMap.Select(x => x.TaxCategoryId).Distinct().ToList();
        var categoryQuery = await _taxCategoryRepository.GetQueryableAsync();
        var categoryTypeMap = categoryQuery
            .Where(c => categoryIds.Contains(c.Id))
            .ToDictionary(c => c.Id, c => c.TaxType);

        return itemCategoryMap
            .Where(x => categoryTypeMap.ContainsKey(x.TaxCategoryId))
            .ToDictionary(x => x.Id, x => categoryTypeMap[x.TaxCategoryId]);
    }

    private static List<TaxRateBreakdownDto> BuildTaxBreakdown(
        IEnumerable<dynamic> invoices)
    {
        // Group invoices by effective tax rate (approximate from TaxAmount/NetTotal)
        var groups = new Dictionary<string, (decimal taxable, decimal tax, int count)>();

        foreach (dynamic inv in invoices)
        {
            decimal net = (decimal)inv.NetTotal;
            decimal tax = (decimal)inv.TaxAmount;
            if (net <= 0) continue;

            decimal effectiveRate = Math.Round(tax / net * 100, 0);
            string rateKey = $"{effectiveRate}%";

            if (groups.ContainsKey(rateKey))
            {
                var existing = groups[rateKey];
                groups[rateKey] = (existing.taxable + net, existing.tax + tax, existing.count + 1);
            }
            else
            {
                groups[rateKey] = (net, tax, 1);
            }
        }

        return groups.Select(g => new TaxRateBreakdownDto
        {
            TaxRate = g.Key,
            TaxableAmount = g.Value.taxable,
            TaxAmount = g.Value.tax,
            InvoiceCount = g.Value.count
        }).OrderByDescending(b => b.TaxAmount).ToList();
    }

    private static List<TaxRateBreakdownDto> BuildTaxBreakdown(IEnumerable<SalesInvoice> invoices)
    {
        return BuildTaxBreakdown(invoices.Cast<dynamic>());
    }

    private static List<TaxRateBreakdownDto> BuildTaxBreakdown(IEnumerable<PurchaseInvoice> invoices)
    {
        return BuildTaxBreakdown(invoices.Cast<dynamic>());
    }

    /// <summary>
    /// Generates SST-02 filing data structured for the Malaysian Customs SST return form.
    /// Groups taxable sales by rate (5%, 6%, 10%) and identifies exempt/zero-rated supplies.
    /// Per Malaysian SST Act 2018: registered manufacturers and service providers must file
    /// SST-02 bimonthly within 28 days of the taxable period end.
    /// </summary>
    public async Task<Sst02FilingDataDto> GetSst02FilingDataAsync(Guid companyId, DateTime fromDate, DateTime toDate)
    {
        var siQuery = await _siRepository.GetQueryableAsync();
        var piQuery = await _piRepository.GetQueryableAsync();

        var salesInvoices = siQuery
            .Where(si => si.CompanyId == companyId
                && si.Status == Core.DocumentStatus.Posted
                && si.IssueDate >= fromDate && si.IssueDate <= toDate)
            .ToList();

        var purchaseInvoices = piQuery
            .Where(pi => pi.CompanyId == companyId
                && pi.Status == Core.DocumentStatus.Posted
                && pi.IssueDate >= fromDate && pi.IssueDate <= toDate)
            .ToList();

        // Categorize sales by effective tax rate
        decimal taxable6 = 0, tax6 = 0;
        decimal taxable10 = 0, tax10 = 0;
        decimal taxable5 = 0, tax5 = 0;
        decimal taxableOther = 0, taxOther = 0;
        decimal exempt = 0;
        decimal zeroRated = 0;

        var nonReturnSales = salesInvoices.Where(s => !s.IsReturn).ToList();
        var zeroTaxSales = nonReturnSales.Where(si => si.TaxAmount == 0 && si.NetTotal > 0).ToList();
        var itemTaxType = await ResolveItemTaxTypesAsync(zeroTaxSales.SelectMany(si => si.Items.Select(i => i.ItemId)));

        foreach (var si in nonReturnSales)
        {
            if (si.TaxAmount == 0 && si.NetTotal > 0)
            {
                // No tax → exempt or zero-rated per Malaysian SST (zero-rated = export, exempt =
                // specific gazetted categories). Distinguished by the invoice's own items' Tax
                // Category (Item.TaxCategoryId -> TaxCategory.TaxType): if any line item is
                // classified ZeroRated, the whole invoice reports as zero-rated; otherwise it
                // falls back to Exempt — the same default this always used, now only applied when
                // no item is actually classified zero-rated instead of unconditionally for every
                // zero-tax invoice.
                var isZeroRated = si.Items.Any(i => itemTaxType.GetValueOrDefault(i.ItemId) == TaxType.ZeroRated);
                if (isZeroRated)
                    zeroRated += si.NetTotal;
                else
                    exempt += si.NetTotal;
                continue;
            }

            var effectiveRate = si.NetTotal > 0 ? Math.Round(si.TaxAmount / si.NetTotal * 100, 0) : 0;

            switch (effectiveRate)
            {
                case 6:
                    taxable6 += si.NetTotal;
                    tax6 += si.TaxAmount;
                    break;
                case 10:
                    taxable10 += si.NetTotal;
                    tax10 += si.TaxAmount;
                    break;
                case 5:
                    taxable5 += si.NetTotal;
                    tax5 += si.TaxAmount;
                    break;
                default:
                    taxableOther += si.NetTotal;
                    taxOther += si.TaxAmount;
                    break;
            }
        }

        var totalOutputTax = tax6 + tax10 + tax5 + taxOther;

        // Input tax from purchases
        var inputTax = purchaseInvoices
            .Where(pi => !pi.IsReturn)
            .Sum(pi => pi.TaxAmount);

        // Adjustments
        var creditNoteAdj = salesInvoices
            .Where(si => si.IsReturn)
            .Sum(si => Math.Abs(si.TaxAmount));

        var debitNoteAdj = purchaseInvoices
            .Where(pi => pi.IsReturn)
            .Sum(pi => Math.Abs(pi.TaxAmount));

        var netAdjustment = -creditNoteAdj + debitNoteAdj;
        var netTaxPayable = totalOutputTax - inputTax + netAdjustment;

        return new Sst02FilingDataDto
        {
            CompanyId = companyId,
            TaxPeriod = $"{fromDate:MMM yyyy} - {toDate:MMM yyyy}",
            FromDate = fromDate,
            ToDate = toDate,
            // Section A
            TaxableSupplies6Percent = taxable6,
            TaxableSupplies10Percent = taxable10,
            TaxableSupplies5Percent = taxable5,
            TaxableSuppliesOtherRate = taxableOther,
            // Section B & C
            ExemptSupplies = exempt,
            ZeroRatedSupplies = zeroRated,
            // Section D
            OutputTax6Percent = tax6,
            OutputTax10Percent = tax10,
            OutputTax5Percent = tax5,
            OutputTaxOther = taxOther,
            TotalOutputTax = totalOutputTax,
            // Section E
            InputTaxCredit = inputTax,
            // Section F
            CreditNoteAdjustment = creditNoteAdj,
            DebitNoteAdjustment = debitNoteAdj,
            NetAdjustment = netAdjustment,
            // Section G
            NetTaxPayable = netTaxPayable,
            IsRefundable = netTaxPayable < 0,
            // Counts
            TotalSalesInvoices = salesInvoices.Count(s => !s.IsReturn),
            TotalPurchaseInvoices = purchaseInvoices.Count(p => !p.IsReturn),
            TotalCreditNotes = salesInvoices.Count(s => s.IsReturn),
            TotalDebitNotes = purchaseInvoices.Count(p => p.IsReturn),
        };
    }
}
