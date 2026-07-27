using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Inventory.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Accounting.DomainServices;

/// <summary>
/// Manufacturing Variance GL Service — posts Purchase Price Variance (PPV) entries
/// for Standard Cost items on Manufacture Stock Entry and Purchase Receipt.
///
/// Per ERPNext:
/// - Standard Cost items: FG valued at standard_rate, RM consumed at actual cost
/// - Manufacturing Variance = actual_RM_cost + additional_costs - (standard_rate x FG_qty)
/// - PPV posted to manufacturing_variance_account (Company setting)
/// - Receipt PPV = actual_receipt_rate - standard_rate (per item)
///
/// Per DO-NOT:
/// - "Skip PPV GL entries for Standard Cost items on Material Receipt/Purchase Receipt"
/// </summary>
public class ManufacturingVarianceGlService : DomainService
{
    private readonly IRepository<JournalEntry, Guid> _journalEntryRepository;
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepository;

    public ManufacturingVarianceGlService(
        IRepository<JournalEntry, Guid> journalEntryRepository,
        IRepository<FiscalYear, Guid> fiscalYearRepository)
    {
        _journalEntryRepository = journalEntryRepository;
        _fiscalYearRepository = fiscalYearRepository;
    }

    /// <summary>
    /// Posts manufacturing variance for a Manufacture Stock Entry.
    /// Variance = actual cost of RM consumed + additional costs - standard value of FG produced.
    /// Positive variance: DR Variance (expense), CR WIP (over-spent)
    /// Negative variance: DR WIP, CR Variance (favorable/under-spent)
    /// </summary>
    public async Task<JournalEntry?> PostManufactureVarianceAsync(
        StockEntry stockEntry,
        decimal standardRate,
        decimal actualRmCost,
        decimal additionalCosts,
        decimal fgQty,
        Guid varianceAccountId,
        Guid wipAccountId,
        Guid companyId,
        Guid? tenantId = null)
    {
        var standardValue = standardRate * fgQty;
        var actualCost = actualRmCost + additionalCosts;
        var variance = actualCost - standardValue;

        if (Math.Abs(variance) < 0.01m) return null;

        var fiscalYearId = await ResolveFiscalYearAsync(companyId, stockEntry.PostingDate);

        var je = new JournalEntry(GuidGenerator.Create(), companyId, fiscalYearId,
            stockEntry.PostingDate, tenantId)
        {
            Narration = $"Manufacturing variance for SE {stockEntry.EntryNumber}. " +
                        $"Standard: {standardValue:N2}, Actual: {actualCost:N2}, Variance: {variance:N2}",
            ReferenceType = "StockEntry",
            ReferenceId = stockEntry.Id,
            ReferenceNumber = stockEntry.EntryNumber,
            VoucherType = JournalEntryVoucherType.JournalEntry,
        };

        if (variance > 0)
        {
            je.AddLine(varianceAccountId, variance, true, "Manufacturing variance (unfavorable)");
            je.AddLine(wipAccountId, variance, false, "WIP offset for manufacturing variance");
        }
        else
        {
            var absVariance = Math.Abs(variance);
            je.AddLine(wipAccountId, absVariance, true, "WIP offset for manufacturing variance");
            je.AddLine(varianceAccountId, absVariance, false, "Manufacturing variance (favorable)");
        }

        await _journalEntryRepository.InsertAsync(je);
        return je;
    }

    /// <summary>
    /// Posts Purchase Price Variance on Purchase Receipt for Standard Cost items.
    /// PPV = (actual_rate - standard_rate) x received_qty per item.
    /// Positive PPV: DR Variance, CR SRBNB (overpaid supplier)
    /// Negative PPV: DR SRBNB, CR Variance (favorable)
    /// </summary>
    public async Task<JournalEntry?> PostReceiptPpvAsync(
        string receiptNumber,
        Guid receiptId,
        DateTime postingDate,
        ReceiptPpvLine[] ppvLines,
        Guid varianceAccountId,
        Guid stockReceivedAccount,
        Guid companyId,
        Guid? tenantId = null)
    {
        var totalPpv = ppvLines.Sum(l => l.VarianceAmount);
        if (Math.Abs(totalPpv) < 0.01m) return null;

        var fiscalYearId = await ResolveFiscalYearAsync(companyId, postingDate);

        var je = new JournalEntry(GuidGenerator.Create(), companyId, fiscalYearId,
            postingDate, tenantId)
        {
            Narration = $"Purchase Price Variance for receipt {receiptNumber}. Total PPV: {totalPpv:N2}",
            ReferenceType = "PurchaseReceipt",
            ReferenceId = receiptId,
            ReferenceNumber = receiptNumber,
            VoucherType = JournalEntryVoucherType.JournalEntry,
        };

        if (totalPpv > 0)
        {
            je.AddLine(varianceAccountId, totalPpv, true, "PPV (unfavorable)");
            je.AddLine(stockReceivedAccount, totalPpv, false, "Stock Received offset");
        }
        else
        {
            var absTotal = Math.Abs(totalPpv);
            je.AddLine(stockReceivedAccount, absTotal, true, "Stock Received offset");
            je.AddLine(varianceAccountId, absTotal, false, "PPV (favorable)");
        }

        await _journalEntryRepository.InsertAsync(je);
        return je;
    }

    /// <summary>Calculates PPV for a single receipt line item.</summary>
    public static decimal CalculateReceiptPpv(decimal actualRate, decimal standardRate, decimal qty)
        => (actualRate - standardRate) * qty;

    private async Task<Guid> ResolveFiscalYearAsync(Guid companyId, DateTime postingDate)
    {
        var fyQuery = await _fiscalYearRepository.GetQueryableAsync();
        var fy = fyQuery.FirstOrDefault(f =>
            f.CompanyId == companyId && f.StartDate <= postingDate && f.EndDate >= postingDate);
        if (fy == null)
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.FiscalYearClosed)
                .WithData("date", postingDate);
        return fy.Id;
    }
}

public class ReceiptPpvLine
{
    public Guid ItemId { get; set; }
    public decimal ActualRate { get; set; }
    public decimal StandardRate { get; set; }
    public decimal Qty { get; set; }
    public decimal VarianceAmount => (ActualRate - StandardRate) * Qty;
}
