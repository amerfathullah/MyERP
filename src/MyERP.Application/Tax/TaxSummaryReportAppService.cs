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
