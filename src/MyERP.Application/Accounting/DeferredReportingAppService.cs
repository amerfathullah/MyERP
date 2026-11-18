using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

[Authorize(MyERPPermissions.Accounts.Default)]
public class DeferredReportingAppService : ApplicationService, IDeferredReportingAppService
{
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepository;
    private readonly IRepository<PurchaseInvoice, Guid> _purchaseInvoiceRepository;
    private readonly IRepository<JournalEntry, Guid> _journalEntryRepository;
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepository;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<AccountsSettings, Guid> _accountsSettingsRepository;
    private readonly IRepository<Item, Guid> _itemRepository;

    public DeferredReportingAppService(
        IRepository<SalesInvoice, Guid> salesInvoiceRepository,
        IRepository<PurchaseInvoice, Guid> purchaseInvoiceRepository,
        IRepository<JournalEntry, Guid> journalEntryRepository,
        IRepository<FiscalYear, Guid> fiscalYearRepository,
        IRepository<Company, Guid> companyRepository,
        IRepository<AccountsSettings, Guid> accountsSettingsRepository,
        IRepository<Item, Guid> itemRepository)
    {
        _salesInvoiceRepository = salesInvoiceRepository;
        _purchaseInvoiceRepository = purchaseInvoiceRepository;
        _journalEntryRepository = journalEntryRepository;
        _fiscalYearRepository = fiscalYearRepository;
        _companyRepository = companyRepository;
        _accountsSettingsRepository = accountsSettingsRepository;
        _itemRepository = itemRepository;
    }

    public async Task<DeferredRevenueAndExpenseReportDto> GetDeferredRevenueAndExpenseAsync(
        DeferredRevenueAndExpenseRequestDto input)
    {
        var isRevenue = !string.Equals(input.Type, "Expense", StringComparison.OrdinalIgnoreCase);
        var (startDate, endDate) = await ResolveDateRangeAsync(input);
        var periods = BuildPeriods(startDate, endDate, input.Periodicity);

        var company = await _companyRepository.FindAsync(input.CompanyId);
        var settings = await AsyncExecuter.FirstOrDefaultAsync(
            (await _accountsSettingsRepository.GetQueryableAsync()).OrderBy(s => s.CreationTime));
        var basedOn = string.IsNullOrWhiteSpace(settings?.BookDeferredEntriesBasedOn)
            ? "Days"
            : settings.BookDeferredEntriesBasedOn;

        var report = new DeferredRevenueAndExpenseReportDto
        {
            CompanyId = input.CompanyId,
            CurrencyCode = company?.CurrencyCode ?? "MYR",
            Type = isRevenue ? "Revenue" : "Expense",
            Periodicity = NormalizePeriodicity(input.Periodicity),
            PeriodStartDate = startDate,
            PeriodEndDate = endDate,
            WithUpcomingPostings = input.WithUpcomingPostings,
            Periods = periods,
        };

        if (periods.Count == 0)
            return report;

        var windowStart = periods[0].FromDate;
        var windowEnd = periods[^1].ToDate;

        var invoices = isRevenue
            ? await LoadSalesDeferredInvoicesAsync(input.CompanyId, windowStart, windowEnd)
            : await LoadPurchaseDeferredInvoicesAsync(input.CompanyId, windowStart, windowEnd);

        if (invoices.Count == 0)
        {
            report.Rows.Add(CreateTotalRow(isRevenue, periods.Count));
            return report;
        }

        var invoiceIds = invoices.Select(i => i.InvoiceId).ToList();
        var refType = isRevenue
            ? DeferredAccountingService.RevenueReferenceType
            : DeferredAccountingService.ExpenseReferenceType;
        var voucherType = isRevenue
            ? JournalEntryVoucherType.DeferredRevenue
            : JournalEntryVoucherType.DeferredExpense;

        var rawJournals = await AsyncExecuter.ToListAsync(
            (await _journalEntryRepository.WithDetailsAsync(j => j.Lines))
            .Where(j => j.CompanyId == input.CompanyId
                        && j.Status == DocumentStatus.Posted
                        && j.ReferenceId.HasValue
                        && invoiceIds.Contains(j.ReferenceId.Value)
                        && (j.ReferenceType == refType
                            || j.VoucherType == voucherType
                            || j.VoucherType == JournalEntryVoucherType.Reversal)));

        var reversedIds = rawJournals
            .Where(j => j.VoucherType == JournalEntryVoucherType.Reversal && j.ReversalOfId.HasValue)
            .Select(j => j.ReversalOfId!.Value)
            .ToHashSet();

        var activeByInvoice = rawJournals
            .Where(j => j.VoucherType != JournalEntryVoucherType.Reversal
                        && !reversedIds.Contains(j.Id)
                        && (j.ReferenceType == refType || j.VoucherType == voucherType))
            .ToLookup(j => j.ReferenceId!.Value);

        var itemIds = invoices.SelectMany(i => i.Rows.Select(r => r.ItemId)).Distinct().ToList();
        var items = await _itemRepository.GetListAsync(it => itemIds.Contains(it.Id));
        var itemMap = items.ToDictionary(it => it.Id, it => it.ItemName ?? it.ItemCode);

        var rows = new List<DeferredRevenueAndExpenseRowDto>();
        foreach (var inv in invoices)
        {
            var invPeriodExpected = new decimal[periods.Count];
            var invPeriodActual = new decimal[periods.Count];
            var itemRows = new List<DeferredRevenueAndExpenseRowDto>();
            decimal invBaseTotal = 0m;

            var invJournals = activeByInvoice[inv.InvoiceId].ToList();

            foreach (var row in inv.Rows)
            {
                invBaseTotal += row.BaseNetAmount;
                var rowExpected = new decimal[periods.Count];
                var rowActual = new decimal[periods.Count];

                decimal bookedBase = 0m;
                decimal bookedTx = 0m;
                DateTime? lastEntryDate = null;

                foreach (var je in invJournals)
                {
                    foreach (var line in je.Lines.Where(l =>
                                 l.ReferenceDetailId == row.RowId && l.AccountId == row.DeferredAccountId))
                    {
                        var postDate = je.PostingDate.Date;
                        if (!lastEntryDate.HasValue || postDate > lastEntryDate.Value)
                            lastEntryDate = postDate;

                        // Revenue DR deferred is positive recognized revenue; Expense CR deferred is negative recognized expense.
                        var signedAmount = line.IsDebit ? line.Amount : -line.Amount;
                        var bookedSign = line.IsDebit == isRevenue ? 1m : -1m;
                        bookedBase += bookedSign * line.Amount;
                        bookedTx += bookedSign * (line.AmountInAccountCurrency != 0m ? line.AmountInAccountCurrency : line.Amount);

                        BucketAmount(periods, postDate, signedAmount, rowActual, rowExpected);
                    }
                }

                if (input.WithUpcomingPostings)
                {
                    var simulated = DeferredAccountingService.SimulateUpcomingPeriods(
                        row.ServiceStartDate,
                        row.ServiceEndDate,
                        row.ServiceStopDate,
                        row.NetAmount,
                        row.BaseNetAmount,
                        basedOn,
                        lastEntryDate,
                        bookedTx,
                        bookedBase);

                    foreach (var p in simulated)
                    {
                        var forecastSigned = isRevenue ? p.BaseAmount : -p.BaseAmount;
                        BucketAmount(periods, p.End.Date, forecastSigned, actual: null, expected: rowExpected);
                    }
                }

                for (var idx = 0; idx < periods.Count; idx++)
                {
                    rowExpected[idx] = Math.Round(rowExpected[idx], 2);
                    rowActual[idx] = Math.Round(rowActual[idx], 2);
                    invPeriodExpected[idx] += rowExpected[idx];
                    invPeriodActual[idx] += rowActual[idx];
                }

                itemRows.Add(new DeferredRevenueAndExpenseRowDto
                {
                    InvoiceId = inv.InvoiceId,
                    Name = itemMap.GetValueOrDefault(row.ItemId, row.Description),
                    Indent = 1,
                    ServiceStartDate = row.ServiceStartDate,
                    ServiceEndDate = row.ServiceEndDate,
                    Amount = Math.Round(row.BaseNetAmount, 2),
                    PeriodValues = rowExpected,
                    ActualPeriodValues = rowActual,
                });
            }

            rows.Add(new DeferredRevenueAndExpenseRowDto
            {
                InvoiceId = inv.InvoiceId,
                Name = inv.InvoiceNumber,
                Indent = 0,
                Amount = Math.Round(invBaseTotal, 2),
                PeriodValues = invPeriodExpected,
                ActualPeriodValues = invPeriodActual,
            });
            rows.AddRange(itemRows);

            for (var idx = 0; idx < periods.Count; idx++)
            {
                periods[idx].ActualTotal = Math.Round(periods[idx].ActualTotal + invPeriodActual[idx], 2);
                periods[idx].ExpectedTotal = Math.Round(periods[idx].ExpectedTotal + invPeriodExpected[idx], 2);
            }
        }

        var totalRow = CreateTotalRow(isRevenue, periods.Count);
        totalRow.Amount = Math.Round(rows.Where(r => r.Indent == 0 && !r.IsTotalRow).Sum(r => r.Amount), 2);
        totalRow.PeriodValues = periods.Select(p => p.ExpectedTotal).ToArray();
        totalRow.ActualPeriodValues = periods.Select(p => p.ActualTotal).ToArray();
        rows.Add(totalRow);

        report.Rows = rows;
        return report;
    }

    private async Task<List<DeferredInvoiceSource>> LoadSalesDeferredInvoicesAsync(
        Guid companyId,
        DateTime windowStart,
        DateTime windowEnd)
    {
        var invoices = await AsyncExecuter.ToListAsync(
            (await _salesInvoiceRepository.WithDetailsAsync(i => i.Items))
            .Where(si => si.CompanyId == companyId && si.Status == DocumentStatus.Posted && !si.IsReturn)
            .OrderBy(si => si.IssueDate).ThenBy(si => si.InvoiceNumber));

        var result = new List<DeferredInvoiceSource>();
        foreach (var inv in invoices)
        {
            var netAmounts = DeferredAccountingService.GetRowNetAmounts(
                inv.Items.Select(i => (i.Id, i.LineTotal)), inv.NetTotal);

            var matchingRows = inv.Items
                .Where(i => i.EnableDeferredRevenue
                            && i.DeferredRevenueAccountId.HasValue
                            && i.ServiceStartDate.HasValue
                            && i.ServiceEndDate.HasValue
                            && OverlapsWindow(i.ServiceStartDate.Value, i.ServiceEndDate.Value, windowStart, windowEnd))
                .Select(i =>
                {
                    var net = netAmounts[i.Id];
                    var baseNet = inv.ExchangeRate == 1m ? net : Math.Round(net * inv.ExchangeRate, 4);
                    return new DeferredItemSource(
                        i.Id,
                        i.ItemId,
                        i.Description,
                        i.DeferredRevenueAccountId!.Value,
                        i.ServiceStartDate!.Value.Date,
                        i.ServiceEndDate!.Value.Date,
                        i.ServiceStopDate?.Date,
                        net,
                        baseNet);
                })
                .ToList();

            if (matchingRows.Count > 0)
            {
                result.Add(new DeferredInvoiceSource(inv.Id, inv.InvoiceNumber, inv.IssueDate, matchingRows));
            }
        }

        return result;
    }

    private async Task<List<DeferredInvoiceSource>> LoadPurchaseDeferredInvoicesAsync(
        Guid companyId,
        DateTime windowStart,
        DateTime windowEnd)
    {
        var invoices = await AsyncExecuter.ToListAsync(
            (await _purchaseInvoiceRepository.WithDetailsAsync(i => i.Items))
            .Where(pi => pi.CompanyId == companyId && pi.Status == DocumentStatus.Posted && !pi.IsReturn)
            .OrderBy(pi => pi.IssueDate).ThenBy(pi => pi.InvoiceNumber));

        var result = new List<DeferredInvoiceSource>();
        foreach (var inv in invoices)
        {
            var netAmounts = DeferredAccountingService.GetRowNetAmounts(
                inv.Items.Select(i => (i.Id, i.LineTotal)), inv.NetTotal);

            var matchingRows = inv.Items
                .Where(i => i.EnableDeferredExpense
                            && i.DeferredExpenseAccountId.HasValue
                            && i.ServiceStartDate.HasValue
                            && i.ServiceEndDate.HasValue
                            && OverlapsWindow(i.ServiceStartDate.Value, i.ServiceEndDate.Value, windowStart, windowEnd))
                .Select(i =>
                {
                    var net = netAmounts[i.Id];
                    var baseNet = inv.ExchangeRate == 1m ? net : Math.Round(net * inv.ExchangeRate, 4);
                    return new DeferredItemSource(
                        i.Id,
                        i.ItemId,
                        i.Description,
                        i.DeferredExpenseAccountId!.Value,
                        i.ServiceStartDate!.Value.Date,
                        i.ServiceEndDate!.Value.Date,
                        i.ServiceStopDate?.Date,
                        net,
                        baseNet);
                })
                .ToList();

            if (matchingRows.Count > 0)
            {
                result.Add(new DeferredInvoiceSource(inv.Id, inv.InvoiceNumber, inv.IssueDate, matchingRows));
            }
        }

        return result;
    }

    private async Task<(DateTime StartDate, DateTime EndDate)> ResolveDateRangeAsync(
        DeferredRevenueAndExpenseRequestDto input)
    {
        DateTime startDate;
        DateTime endDate;

        if (string.Equals(input.FilterBasedOn, "Fiscal Year", StringComparison.OrdinalIgnoreCase))
        {
            var fys = await _fiscalYearRepository.GetListAsync(f => f.CompanyId == input.CompanyId);
            var fromFy = input.FromFiscalYearId.HasValue
                ? fys.FirstOrDefault(f => f.Id == input.FromFiscalYearId.Value)
                : fys.OrderByDescending(f => f.StartDate).FirstOrDefault();
            var toFy = input.ToFiscalYearId.HasValue
                ? fys.FirstOrDefault(f => f.Id == input.ToFiscalYearId.Value)
                : fromFy;

            startDate = fromFy?.StartDate.Date ?? input.PeriodStartDate?.Date ?? new DateTime(DateTime.UtcNow.Year, 1, 1);
            endDate = toFy?.EndDate.Date ?? input.PeriodEndDate?.Date ?? new DateTime(DateTime.UtcNow.Year, 12, 31);
        }
        else
        {
            startDate = input.PeriodStartDate?.Date ?? new DateTime(DateTime.UtcNow.Year, 1, 1);
            endDate = input.PeriodEndDate?.Date ?? new DateTime(DateTime.UtcNow.Year, 12, 31);
        }

        if (startDate > endDate)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "From Date cannot be after To Date.");
        }

        return (startDate, endDate);
    }

    private static List<DeferredReportPeriodDto> BuildPeriods(
        DateTime startDate,
        DateTime endDate,
        string? periodicity)
    {
        var normalized = NormalizePeriodicity(periodicity);
        var stepMonths = normalized switch
        {
            "Quarterly" => 3,
            "Half-Yearly" => 6,
            "Yearly" => 12,
            _ => 1,
        };

        var periods = new List<DeferredReportPeriodDto>();
        var cur = new DateTime(startDate.Year, startDate.Month, 1);

        while (cur <= endDate)
        {
            var periodFrom = cur < startDate ? startDate : cur;
            var rawEnd = cur.AddMonths(stepMonths).AddDays(-1);
            var periodTo = rawEnd > endDate ? endDate : rawEnd;

            var (key, label) = FormatPeriodKeyAndLabel(cur, normalized);
            periods.Add(new DeferredReportPeriodDto
            {
                Key = key,
                Label = label,
                FromDate = periodFrom,
                ToDate = periodTo,
            });

            cur = cur.AddMonths(stepMonths);
        }

        return periods;
    }

    private static (string Key, string Label) FormatPeriodKeyAndLabel(DateTime start, string normalizedPeriodicity)
    {
        switch (normalizedPeriodicity)
        {
            case "Quarterly":
                var q = ((start.Month - 1) / 3) + 1;
                return ($"q{q}_{start.Year}", $"Q{q} {start.Year}");
            case "Half-Yearly":
                var h = start.Month <= 6 ? 1 : 2;
                return ($"h{h}_{start.Year}", $"H{h} {start.Year}");
            case "Yearly":
                return ($"y_{start.Year}", $"{start.Year}");
            default:
                return (
                    start.ToString("MMM_yyyy", CultureInfo.InvariantCulture).ToLowerInvariant(),
                    start.ToString("MMM yyyy", CultureInfo.InvariantCulture));
        }
    }

    private static string NormalizePeriodicity(string? periodicity)
    {
        if (string.Equals(periodicity, "Quarterly", StringComparison.OrdinalIgnoreCase)) return "Quarterly";
        if (string.Equals(periodicity, "Half-Yearly", StringComparison.OrdinalIgnoreCase)) return "Half-Yearly";
        if (string.Equals(periodicity, "Yearly", StringComparison.OrdinalIgnoreCase)) return "Yearly";
        return "Monthly";
    }

    private static bool OverlapsWindow(DateTime serviceStart, DateTime serviceEnd, DateTime windowStart, DateTime windowEnd)
    {
        var s = serviceStart.Date;
        var e = serviceEnd.Date;
        return (windowStart >= s && e >= windowStart) || (s >= windowStart && s <= windowEnd);
    }

    private static void BucketAmount(
        List<DeferredReportPeriodDto> periods,
        DateTime postDate,
        decimal signedAmount,
        decimal[]? actual,
        decimal[] expected)
    {
        for (var idx = 0; idx < periods.Count; idx++)
        {
            if (postDate >= periods[idx].FromDate && postDate <= periods[idx].ToDate)
            {
                expected[idx] += signedAmount;
                if (actual != null)
                    actual[idx] += signedAmount;
                break;
            }
        }
    }

    private static DeferredRevenueAndExpenseRowDto CreateTotalRow(bool isRevenue, int periodCount) => new()
    {
        IsTotalRow = true,
        Indent = 0,
        Name = isRevenue ? "Total Deferred Income" : "Total Deferred Expense",
        PeriodValues = new decimal[periodCount],
        ActualPeriodValues = new decimal[periodCount],
    };

    private sealed record DeferredItemSource(
        Guid RowId,
        Guid ItemId,
        string Description,
        Guid DeferredAccountId,
        DateTime ServiceStartDate,
        DateTime ServiceEndDate,
        DateTime? ServiceStopDate,
        decimal NetAmount,
        decimal BaseNetAmount);

    private sealed record DeferredInvoiceSource(
        Guid InvoiceId,
        string InvoiceNumber,
        DateTime IssueDate,
        List<DeferredItemSource> Rows);
}
