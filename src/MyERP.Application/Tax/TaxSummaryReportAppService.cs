using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
public class TaxSummaryReportAppService : ApplicationService
{
    private readonly IRepository<SalesInvoice, Guid> _siRepository;
    private readonly IRepository<PurchaseInvoice, Guid> _piRepository;
    private readonly IRepository<TransactionTaxRow, Guid> _taxRowRepository;

    public TaxSummaryReportAppService(
        IRepository<SalesInvoice, Guid> siRepository,
        IRepository<PurchaseInvoice, Guid> piRepository,
        IRepository<TransactionTaxRow, Guid> taxRowRepository)
    {
        _siRepository = siRepository;
        _piRepository = piRepository;
        _taxRowRepository = taxRowRepository;
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

        foreach (var si in salesInvoices.Where(s => !s.IsReturn))
        {
            if (si.TaxAmount == 0 && si.NetTotal > 0)
            {
                // No tax → could be exempt or zero-rated
                // Per MY SST: zero-rated = export, exempt = specific categories
                // Simplified: treat all zero-tax sales as exempt (in production, check item tax template)
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
                case 0:
                    zeroRated += si.NetTotal;
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

public class TaxSummaryDto
{
    public Guid CompanyId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }

    // Output Tax (Sales)
    public decimal TotalSalesAmount { get; set; }
    public decimal OutputTax { get; set; }
    public decimal CreditNoteTaxAdjustment { get; set; }
    public decimal NetOutputTax { get; set; }
    public int SalesInvoiceCount { get; set; }
    public int CreditNoteCount { get; set; }

    // Input Tax (Purchases)
    public decimal TotalPurchaseAmount { get; set; }
    public decimal InputTax { get; set; }
    public decimal DebitNoteTaxAdjustment { get; set; }
    public decimal NetInputTax { get; set; }
    public int PurchaseInvoiceCount { get; set; }
    public int DebitNoteCount { get; set; }

    // Net Position
    public decimal NetTaxPayable { get; set; }
    public bool IsRefundable { get; set; }

    // Rate Breakdowns
    public List<TaxRateBreakdownDto> OutputTaxBreakdown { get; set; } = new();
    public List<TaxRateBreakdownDto> InputTaxBreakdown { get; set; } = new();
}

public class TaxRateBreakdownDto
{
    public string TaxRate { get; set; } = null!;
    public decimal TaxableAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public int InvoiceCount { get; set; }
}

/// <summary>
/// SST-02 Filing Data — structured data matching the Malaysian SST-02 return form.
/// This is what accountants use to fill in the RMCD (Royal Malaysian Customs Department) form.
/// 
/// SST-02 Form Sections:
/// A. Total value of taxable supplies (sales at 5%, 6%, 10%)
/// B. Total value of exempt supplies (sales of exempt goods/services)
/// C. Zero-rated supplies (exports, etc.)
/// D. Total output tax payable (sum of tax on section A)
/// E. Less: Input tax credit (purchases with tax paid)
/// F. Tax adjustments (credit/debit notes, bad debt relief)
/// G. Net tax payable / refundable (D - E ± F)
/// </summary>
public class Sst02FilingDataDto
{
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = null!;
    public string? SstRegistrationNumber { get; set; }
    public string TaxPeriod { get; set; } = null!;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }

    // Section A: Taxable Supplies by Rate
    public decimal TaxableSupplies6Percent { get; set; }       // Service Tax 6%
    public decimal TaxableSupplies10Percent { get; set; }      // Sales Tax 10%
    public decimal TaxableSupplies5Percent { get; set; }       // Sales Tax 5%
    public decimal TaxableSuppliesOtherRate { get; set; }      // Other rates

    // Section B: Exempt Supplies (no tax charged)
    public decimal ExemptSupplies { get; set; }

    // Section C: Zero-Rated Supplies (0% tax, typically exports)
    public decimal ZeroRatedSupplies { get; set; }

    // Section D: Output Tax
    public decimal OutputTax6Percent { get; set; }
    public decimal OutputTax10Percent { get; set; }
    public decimal OutputTax5Percent { get; set; }
    public decimal OutputTaxOther { get; set; }
    public decimal TotalOutputTax { get; set; }

    // Section E: Input Tax Credit
    public decimal InputTaxCredit { get; set; }

    // Section F: Adjustments
    public decimal CreditNoteAdjustment { get; set; }  // Reduces output tax
    public decimal DebitNoteAdjustment { get; set; }   // Reduces input tax
    public decimal BadDebtRelief { get; set; }         // Recovery of tax on bad debts
    public decimal NetAdjustment { get; set; }

    // Section G: Net Tax Position
    public decimal NetTaxPayable { get; set; }
    public bool IsRefundable { get; set; }

    // Supporting counts
    public int TotalSalesInvoices { get; set; }
    public int TotalPurchaseInvoices { get; set; }
    public int TotalCreditNotes { get; set; }
    public int TotalDebitNotes { get; set; }
}
