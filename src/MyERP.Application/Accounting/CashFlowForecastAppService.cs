using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

/// <summary>
/// Cash Flow Forecast — forward-looking cash position projection.
/// Uses outstanding invoices + payment schedule to predict inflows/outflows.
/// Critical for Malaysian SME daily cash management.
/// 
/// ERPNext equivalent: accounts/report/cash_flow_prediction (conceptual, not exact match)
/// </summary>
[Authorize(MyERPPermissions.Accounts.Default)]
public class CashFlowForecastAppService : ApplicationService, ICashFlowForecastAppService
{
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepository;
    private readonly IRepository<PurchaseInvoice, Guid> _purchaseInvoiceRepository;
    private readonly IRepository<JournalEntry, Guid> _journalEntryRepository;
    private readonly IRepository<Account, Guid> _accountRepository;
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IRepository<Supplier, Guid> _supplierRepository;

    public CashFlowForecastAppService(
        IRepository<SalesInvoice, Guid> salesInvoiceRepository,
        IRepository<PurchaseInvoice, Guid> purchaseInvoiceRepository,
        IRepository<JournalEntry, Guid> journalEntryRepository,
        IRepository<Account, Guid> accountRepository,
        IRepository<Customer, Guid> customerRepository,
        IRepository<Supplier, Guid> supplierRepository)
    {
        _salesInvoiceRepository = salesInvoiceRepository;
        _purchaseInvoiceRepository = purchaseInvoiceRepository;
        _journalEntryRepository = journalEntryRepository;
        _accountRepository = accountRepository;
        _customerRepository = customerRepository;
        _supplierRepository = supplierRepository;
    }

    public async Task<CashFlowForecastDto> GetForecastAsync(CashFlowForecastRequestDto input)
    {
        var asOfDate = input.AsOfDate ?? DateTime.UtcNow.Date;
        var forecastEnd = asOfDate.AddDays(input.ForecastDays);

        // 1. Calculate current cash/bank balance from GL
        var currentCashBalance = await GetCurrentCashBalanceAsync(input.CompanyId);

        // 2. Get expected inflows (outstanding Sales Invoices)
        var inflows = await GetExpectedInflowsAsync(input.CompanyId, asOfDate, forecastEnd);

        // 3. Get expected outflows (outstanding Purchase Invoices)
        var outflows = await GetExpectedOutflowsAsync(input.CompanyId, asOfDate, forecastEnd);

        // 4. Build weekly periods for chart
        var periods = BuildForecastPeriods(asOfDate, input.ForecastDays, currentCashBalance, inflows, outflows);

        // 5. Calculate summary metrics
        var totalInflows = inflows.Sum(i => i.Amount);
        var totalOutflows = outflows.Sum(o => o.Amount);
        var netCashFlow = totalInflows - totalOutflows;

        var summary = BuildSummary(asOfDate, currentCashBalance, inflows, outflows, periods);

        return new CashFlowForecastDto
        {
            AsOfDate = asOfDate,
            ForecastDays = input.ForecastDays,
            CurrentCashBalance = currentCashBalance,
            TotalExpectedInflows = totalInflows,
            TotalExpectedOutflows = totalOutflows,
            NetCashFlow = netCashFlow,
            ProjectedClosingBalance = currentCashBalance + netCashFlow,
            Periods = periods,
            UpcomingInflows = inflows.OrderBy(i => i.DueDate).Take(20).ToList(),
            UpcomingOutflows = outflows.OrderBy(o => o.DueDate).Take(20).ToList(),
            Summary = summary
        };
    }

    private async Task<decimal> GetCurrentCashBalanceAsync(Guid companyId)
    {
        // Sum balances of all Cash + Bank accounts for the company
        var cashBankAccounts = await _accountRepository.GetListAsync(a =>
            a.CompanyId == companyId &&
            !a.IsGroup &&
            (a.AccountSubType == AccountSubType.CashAccount || a.AccountSubType == AccountSubType.BankAccount));

        if (!cashBankAccounts.Any()) return 0m;

        // Get GL balance: SUM(debit) - SUM(credit) for all cash/bank accounts
        var accountIds = cashBankAccounts.Select(a => a.Id).ToHashSet();
        var journalQuery = await _journalEntryRepository.GetQueryableAsync();

        var postedJournals = journalQuery
            .Where(je => je.CompanyId == companyId && je.Status == DocumentStatus.Posted)
            .SelectMany(je => je.Lines)
            .Where(line => accountIds.Contains(line.AccountId));

        var totalDebit = postedJournals.Where(l => l.IsDebit).Sum(l => l.Amount);
        var totalCredit = postedJournals.Where(l => !l.IsDebit).Sum(l => l.Amount);

        return totalDebit - totalCredit;
    }

    private async Task<List<CashFlowForecastEntryDto>> GetExpectedInflowsAsync(
        Guid companyId, DateTime asOfDate, DateTime forecastEnd)
    {
        // Outstanding Sales Invoices (Posted, OutstandingAmount > 0)
        var siQuery = await _salesInvoiceRepository.GetQueryableAsync();
        var outstandingInvoices = siQuery
            .Where(si => si.CompanyId == companyId
                         && si.Status == DocumentStatus.Posted
                         && !si.IsReturn
                         && (si.GrandTotal - si.AmountPaid - si.WriteOffAmount) > 0)
            .OrderBy(si => si.DueDate ?? si.IssueDate)
            .Take(100)
            .ToList();

        // Resolve customer names in batch
        var customerIds = outstandingInvoices.Select(si => si.CustomerId).Distinct().ToList();
        var customerQuery = await _customerRepository.GetQueryableAsync();
        var customerNames = customerQuery
            .Where(c => customerIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name })
            .ToDictionary(c => c.Id, c => c.Name);

        return outstandingInvoices.Select(si =>
        {
            var dueDate = si.DueDate ?? si.IssueDate.AddDays(30);
            var outstanding = si.GrandTotal - si.AmountPaid - si.WriteOffAmount;
            return new CashFlowForecastEntryDto
            {
                DocumentId = si.Id,
                DocumentNumber = si.InvoiceNumber ?? si.Id.ToString()[..8],
                DocumentType = "SalesInvoice",
                PartyName = customerNames.GetValueOrDefault(si.CustomerId, "—"),
                DueDate = dueDate,
                Amount = outstanding,
                DaysUntilDue = (int)(dueDate - asOfDate).TotalDays,
                IsOverdue = dueDate < asOfDate
            };
        }).Where(e => e.DueDate <= forecastEnd || e.IsOverdue).ToList();
    }

    private async Task<List<CashFlowForecastEntryDto>> GetExpectedOutflowsAsync(
        Guid companyId, DateTime asOfDate, DateTime forecastEnd)
    {
        // Outstanding Purchase Invoices (Posted, OutstandingAmount > 0)
        var piQuery = await _purchaseInvoiceRepository.GetQueryableAsync();
        var outstandingInvoices = piQuery
            .Where(pi => pi.CompanyId == companyId
                         && pi.Status == DocumentStatus.Posted
                         && !pi.IsReturn
                         && (pi.GrandTotal - pi.AmountPaid - pi.WriteOffAmount) > 0)
            .OrderBy(pi => pi.DueDate ?? pi.IssueDate)
            .Take(100)
            .ToList();

        // Resolve supplier names in batch
        var supplierIds = outstandingInvoices.Select(pi => pi.SupplierId).Distinct().ToList();
        var supplierQuery = await _supplierRepository.GetQueryableAsync();
        var supplierNames = supplierQuery
            .Where(s => supplierIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Name })
            .ToDictionary(s => s.Id, s => s.Name);

        return outstandingInvoices.Select(pi =>
        {
            var dueDate = pi.DueDate ?? pi.IssueDate.AddDays(30);
            var outstanding = pi.GrandTotal - pi.AmountPaid - pi.WriteOffAmount;
            return new CashFlowForecastEntryDto
            {
                DocumentId = pi.Id,
                DocumentNumber = pi.InvoiceNumber ?? pi.Id.ToString()[..8],
                DocumentType = "PurchaseInvoice",
                PartyName = supplierNames.GetValueOrDefault(pi.SupplierId, "—"),
                DueDate = dueDate,
                Amount = outstanding,
                DaysUntilDue = (int)(dueDate - asOfDate).TotalDays,
                IsOverdue = dueDate < asOfDate
            };
        }).Where(e => e.DueDate <= forecastEnd || e.IsOverdue).ToList();
    }

    private static List<CashFlowForecastPeriodDto> BuildForecastPeriods(
        DateTime asOfDate, int forecastDays, decimal openingBalance,
        List<CashFlowForecastEntryDto> inflows, List<CashFlowForecastEntryDto> outflows)
    {
        var periods = new List<CashFlowForecastPeriodDto>();
        var cumulativeBalance = openingBalance;

        // Build weekly periods
        var periodStart = asOfDate;
        while (periodStart < asOfDate.AddDays(forecastDays))
        {
            var periodEnd = periodStart.AddDays(7);
            if (periodEnd > asOfDate.AddDays(forecastDays))
                periodEnd = asOfDate.AddDays(forecastDays);

            var periodInflows = inflows
                .Where(i => i.DueDate >= periodStart && i.DueDate < periodEnd)
                .Sum(i => i.Amount);

            var periodOutflows = outflows
                .Where(o => o.DueDate >= periodStart && o.DueDate < periodEnd)
                .Sum(o => o.Amount);

            var netFlow = periodInflows - periodOutflows;
            cumulativeBalance += netFlow;

            periods.Add(new CashFlowForecastPeriodDto
            {
                Label = $"{periodStart:dd MMM} - {periodEnd.AddDays(-1):dd MMM}",
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                Inflows = periodInflows,
                Outflows = periodOutflows,
                NetFlow = netFlow,
                CumulativeBalance = cumulativeBalance
            });

            periodStart = periodEnd;
        }

        return periods;
    }

    private static CashFlowForecastSummaryDto BuildSummary(
        DateTime asOfDate, decimal currentBalance,
        List<CashFlowForecastEntryDto> inflows, List<CashFlowForecastEntryDto> outflows,
        List<CashFlowForecastPeriodDto> periods)
    {
        var overdueReceivables = inflows.Where(i => i.IsOverdue).ToList();
        var overduePayables = outflows.Where(o => o.IsOverdue).ToList();

        // Cash runway: how many days until balance goes negative (based on avg daily outflow)
        var totalOutflow = outflows.Sum(o => o.Amount);
        var avgDailyOutflow = totalOutflow > 0 && periods.Count > 0
            ? totalOutflow / (periods.Count * 7m)
            : 0m;

        var cashRunwayDays = avgDailyOutflow > 0
            ? currentBalance / avgDailyOutflow
            : 999m; // effectively infinite if no outflows

        // Find first period where cumulative balance goes negative
        DateTime? crunchDate = null;
        foreach (var period in periods)
        {
            if (period.CumulativeBalance < 0)
            {
                crunchDate = period.PeriodStart;
                break;
            }
        }

        return new CashFlowForecastSummaryDto
        {
            OverdueReceivablesCount = overdueReceivables.Count,
            OverdueReceivablesAmount = overdueReceivables.Sum(r => r.Amount),
            OverduePayablesCount = overduePayables.Count,
            OverduePayablesAmount = overduePayables.Sum(p => p.Amount),
            CashRunwayDays = Math.Min(cashRunwayDays, 999m),
            ProjectedCashCrunchDate = crunchDate
        };
    }
}
