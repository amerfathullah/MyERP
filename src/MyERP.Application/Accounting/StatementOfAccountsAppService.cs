using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Accounting.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

/// <summary>
/// Statement of Accounts (SOA) — generates a ledger of all transactions
/// for a customer/supplier over a period, showing invoices, payments,
/// credit notes/debit notes, and running balance.
/// Essential for collections (customer) and payables management (supplier).
/// </summary>
[Authorize]
public class StatementOfAccountsAppService : ApplicationService, IStatementOfAccountsAppService
{
    private readonly IRepository<SalesInvoice, Guid> _siRepository;
    private readonly IRepository<PurchaseInvoice, Guid> _piRepository;
    private readonly IRepository<PaymentEntry, Guid> _peRepository;
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IRepository<Supplier, Guid> _supplierRepository;

    public StatementOfAccountsAppService(
        IRepository<SalesInvoice, Guid> siRepository,
        IRepository<PurchaseInvoice, Guid> piRepository,
        IRepository<PaymentEntry, Guid> peRepository,
        IRepository<Customer, Guid> customerRepository,
        IRepository<Supplier, Guid> supplierRepository)
    {
        _siRepository = siRepository;
        _piRepository = piRepository;
        _peRepository = peRepository;
        _customerRepository = customerRepository;
        _supplierRepository = supplierRepository;
    }

    private async Task<IQueryable<PaymentEntry>> GetPaymentQueryableWithTaxesAsync()
    {
        try
        {
            var withDetails = await _peRepository.WithDetailsAsync(p => p.Taxes);
            if (withDetails != null) return withDetails;
        }
        catch
        {
            // Fallback if WithDetailsAsync is not configured or unsupported in mock/provider
        }
        return await _peRepository.GetQueryableAsync();
    }

    /// <summary>
    /// Generates a Statement of Accounts for a customer within a date range.
    /// Shows all invoices, payments, credit notes with running balance.
    /// Deductions on payments (withholding, bank charges) are fully factored into settlement.
    /// Per ERPNext PR #58437 / commit dbe153a15e.
    /// </summary>
    public async Task<StatementOfAccountsDto> GetCustomerStatementAsync(
        Guid customerId, Guid companyId, DateTime fromDate, DateTime toDate)
    {
        var siQuery = await _siRepository.GetQueryableAsync();
        var peQuery = await GetPaymentQueryableWithTaxesAsync();

        // Get all posted invoices for this customer in the date range
        var invoices = siQuery
            .Where(si => si.CustomerId == customerId
                && si.CompanyId == companyId
                && si.Status == Core.DocumentStatus.Posted
                && si.IssueDate >= fromDate && si.IssueDate <= toDate)
            .OrderBy(si => si.IssueDate)
            .Select(si => new { si.Id, si.InvoiceNumber, si.IssueDate, si.GrandTotal, si.AmountPaid, si.IsReturn, si.CurrencyCode })
            .ToList();

        // Get all posted payments for this customer in the date range
        var payments = peQuery
            .Where(pe => pe.PartyType == "Customer"
                && pe.PartyId == customerId
                && pe.CompanyId == companyId
                && pe.Status == Core.DocumentStatus.Posted
                && pe.PostingDate >= fromDate && pe.PostingDate <= toDate)
            .OrderBy(pe => pe.PostingDate)
            .ToList();

        // Calculate opening balance (net ledger balance before fromDate: prior invoices debits/credits minus prior payments credits/debits)
        var priorInvoices = siQuery
            .Where(si => si.CustomerId == customerId
                && si.CompanyId == companyId
                && si.Status == Core.DocumentStatus.Posted
                && si.IssueDate < fromDate)
            .ToList();

        var priorPayments = peQuery
            .Where(pe => pe.PartyType == "Customer"
                && pe.PartyId == customerId
                && pe.CompanyId == companyId
                && pe.Status == Core.DocumentStatus.Posted
                && pe.PostingDate < fromDate)
            .ToList();

        decimal priorDebits = priorInvoices.Sum(si => si.IsReturn ? 0 : si.GrandTotal)
            + priorPayments.Where(pe => pe.PaymentType == PaymentType.Pay).Sum(pe => pe.TotalSettledBaseAmount > 0 ? pe.TotalSettledBaseAmount : pe.PaidAmount);
        decimal priorCredits = priorInvoices.Sum(si => si.IsReturn ? si.GrandTotal : 0)
            + priorPayments.Where(pe => pe.PaymentType != PaymentType.Pay).Sum(pe => pe.TotalSettledBaseAmount > 0 ? pe.TotalSettledBaseAmount : pe.PaidAmount);
        decimal openingBalance = priorDebits - priorCredits;

        // Build statement entries (chronological)
        var entries = new List<StatementEntryDto>();
        decimal runningBalance = openingBalance;

        // Add invoices
        foreach (var inv in invoices)
        {
            decimal amount = inv.IsReturn ? -inv.GrandTotal : inv.GrandTotal;
            runningBalance += amount;
            entries.Add(new StatementEntryDto
            {
                Date = inv.IssueDate,
                DocumentType = inv.IsReturn ? "Credit Note" : "Sales Invoice",
                DocumentNumber = inv.InvoiceNumber,
                DocumentId = inv.Id,
                DebitAmount = inv.IsReturn ? 0 : inv.GrandTotal,
                CreditAmount = inv.IsReturn ? Math.Abs(inv.GrandTotal) : 0,
                RunningBalance = runningBalance
            });
        }

        // Add payments (including deductions per ERPNext PR #58437 / commit dbe153a15e)
        foreach (var pmt in payments)
        {
            decimal settledAmount = pmt.TotalSettledBaseAmount > 0 ? pmt.TotalSettledBaseAmount : pmt.PaidAmount;
            bool isRefund = pmt.PaymentType == PaymentType.Pay;
            decimal debit = isRefund ? settledAmount : 0;
            decimal credit = isRefund ? 0 : settledAmount;
            runningBalance += (debit - credit);
            entries.Add(new StatementEntryDto
            {
                Date = pmt.PostingDate,
                DocumentType = isRefund ? "Refund" : "Payment",
                DocumentNumber = pmt.PaymentNumber ?? "PE",
                DocumentId = pmt.Id,
                DebitAmount = debit,
                CreditAmount = credit,
                RunningBalance = runningBalance
            });
        }

        // Sort all entries chronologically
        entries = entries.OrderBy(e => e.Date).ThenBy(e => e.DocumentType).ToList();

        // Recalculate running balance in order
        runningBalance = openingBalance;
        foreach (var entry in entries)
        {
            runningBalance += entry.DebitAmount - entry.CreditAmount;
            entry.RunningBalance = runningBalance;
        }

        return new StatementOfAccountsDto
        {
            CustomerId = customerId,
            CompanyId = companyId,
            FromDate = fromDate,
            ToDate = toDate,
            OpeningBalance = openingBalance,
            ClosingBalance = runningBalance,
            TotalDebit = entries.Sum(e => e.DebitAmount),
            TotalCredit = entries.Sum(e => e.CreditAmount),
            Entries = entries
        };
    }

    /// <summary>
    /// Generates a Statement of Accounts for a supplier (payables ledger).
    /// Shows purchase invoices, payments made, debit notes with running balance.
    /// Deductions on payments (withholding, bank charges) are fully factored into settlement.
    /// Per ERPNext PR #58437 / commit dbe153a15e.
    /// </summary>
    public async Task<SupplierStatementDto> GetSupplierStatementAsync(
        Guid supplierId, Guid companyId, DateTime fromDate, DateTime toDate)
    {
        var piQuery = await _piRepository.GetQueryableAsync();
        var peQuery = await GetPaymentQueryableWithTaxesAsync();

        // Invoices in period
        var invoices = piQuery
            .Where(pi => pi.SupplierId == supplierId
                && pi.CompanyId == companyId
                && pi.Status == Core.DocumentStatus.Posted
                && pi.IssueDate >= fromDate && pi.IssueDate <= toDate)
            .OrderBy(pi => pi.IssueDate)
            .Select(pi => new { pi.Id, pi.InvoiceNumber, pi.IssueDate, pi.GrandTotal, pi.AmountPaid, pi.IsReturn, pi.CurrencyCode })
            .ToList();

        // Payments in period
        var payments = peQuery
            .Where(pe => pe.PartyType == "Supplier"
                && pe.PartyId == supplierId
                && pe.CompanyId == companyId
                && pe.Status == Core.DocumentStatus.Posted
                && pe.PostingDate >= fromDate && pe.PostingDate <= toDate)
            .OrderBy(pe => pe.PostingDate)
            .ToList();

        // Opening balance for supplier (payables ledger: credits increase liability, debits decrease liability)
        var priorInvoices = piQuery
            .Where(pi => pi.SupplierId == supplierId
                && pi.CompanyId == companyId
                && pi.Status == Core.DocumentStatus.Posted
                && pi.IssueDate < fromDate)
            .ToList();

        var priorPayments = peQuery
            .Where(pe => pe.PartyType == "Supplier"
                && pe.PartyId == supplierId
                && pe.CompanyId == companyId
                && pe.Status == Core.DocumentStatus.Posted
                && pe.PostingDate < fromDate)
            .ToList();

        decimal priorCredits = priorInvoices.Sum(pi => pi.IsReturn ? 0 : pi.GrandTotal)
            + priorPayments.Where(pe => pe.PaymentType == PaymentType.Receive).Sum(pe => pe.TotalSettledBaseAmount > 0 ? pe.TotalSettledBaseAmount : pe.PaidAmount);
        decimal priorDebits = priorInvoices.Sum(pi => pi.IsReturn ? pi.GrandTotal : 0)
            + priorPayments.Where(pe => pe.PaymentType != PaymentType.Receive).Sum(pe => pe.TotalSettledBaseAmount > 0 ? pe.TotalSettledBaseAmount : pe.PaidAmount);
        decimal openingBalance = priorCredits - priorDebits;

        var entries = new List<StatementEntryDto>();
        decimal runningBalance = openingBalance;

        foreach (var inv in invoices)
        {
            entries.Add(new StatementEntryDto
            {
                Date = inv.IssueDate,
                DocumentType = inv.IsReturn ? "Debit Note" : "Purchase Invoice",
                DocumentNumber = inv.InvoiceNumber,
                DocumentId = inv.Id,
                DebitAmount = inv.IsReturn ? Math.Abs(inv.GrandTotal) : 0,
                CreditAmount = inv.IsReturn ? 0 : inv.GrandTotal,
                RunningBalance = 0
            });
        }

        foreach (var pmt in payments)
        {
            decimal settledAmount = pmt.TotalSettledBaseAmount > 0 ? pmt.TotalSettledBaseAmount : pmt.PaidAmount;
            bool isRefund = pmt.PaymentType == PaymentType.Receive;
            decimal debit = isRefund ? 0 : settledAmount;
            decimal credit = isRefund ? settledAmount : 0;
            entries.Add(new StatementEntryDto
            {
                Date = pmt.PostingDate,
                DocumentType = isRefund ? "Refund" : "Payment",
                DocumentNumber = pmt.PaymentNumber ?? "PE",
                DocumentId = pmt.Id,
                DebitAmount = debit,
                CreditAmount = credit,
                RunningBalance = 0
            });
        }

        // Sort chronologically and calculate running balance
        entries = entries.OrderBy(e => e.Date).ThenBy(e => e.DocumentType).ToList();
        runningBalance = openingBalance;
        foreach (var entry in entries)
        {
            // For payables: credit = invoice (increases liability), debit = payment (reduces liability)
            runningBalance += entry.CreditAmount - entry.DebitAmount;
            entry.RunningBalance = runningBalance;
        }

        return new SupplierStatementDto
        {
            SupplierId = supplierId,
            CompanyId = companyId,
            FromDate = fromDate,
            ToDate = toDate,
            OpeningBalance = openingBalance,
            ClosingBalance = runningBalance,
            TotalInvoiced = entries.Sum(e => e.CreditAmount),
            TotalPaid = entries.Sum(e => e.DebitAmount),
            TotalDebit = entries.Sum(e => e.DebitAmount),
            TotalCredit = entries.Sum(e => e.CreditAmount),
            Entries = entries
        };
    }

    /// <summary>
    /// Processes statements of accounts in batch for customers or suppliers with optional aging buckets (Gotcha #5998).
    /// </summary>
    public async Task<BatchStatementOfAccountsResultDto> ProcessBatchStatementAsync(BatchStatementOfAccountsInput input)
    {
        if (input.FromDate > input.ToDate)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.InvalidDateRange)
                .WithData("detail", "FromDate cannot be greater than ToDate.");
        }

        var result = new BatchStatementOfAccountsResultDto
        {
            CompanyId = input.CompanyId,
            FromDate = input.FromDate,
            ToDate = input.ToDate,
        };

        if (string.Equals(input.PartyType, "Supplier", StringComparison.OrdinalIgnoreCase))
        {
            var supQuery = await _supplierRepository.GetQueryableAsync();
            var suppliers = supQuery.Where(s => s.CompanyId == input.CompanyId);
            if (input.PartyIds != null && input.PartyIds.Count > 0)
            {
                suppliers = suppliers.Where(s => input.PartyIds.Contains(s.Id));
            }
            var supplierList = suppliers.ToList();

            var piQuery = await _piRepository.GetQueryableAsync();
            var peQuery = await GetPaymentQueryableWithTaxesAsync();

            var allInvoices = piQuery
                .Where(pi => pi.CompanyId == input.CompanyId && pi.Status == Core.DocumentStatus.Posted)
                .ToList();

            var allPayments = peQuery
                .Where(pe => pe.CompanyId == input.CompanyId && pe.PartyType == "Supplier" && pe.Status == Core.DocumentStatus.Posted)
                .ToList();

            foreach (var sup in supplierList)
            {
                var priorInvoices = allInvoices.Where(i => i.SupplierId == sup.Id && i.IssueDate < input.FromDate).ToList();
                var priorPayments = allPayments.Where(p => p.PartyId == sup.Id && p.PostingDate < input.FromDate).ToList();
                var openingBalance = priorInvoices.Sum(i => i.IsReturn ? -i.GrandTotal : i.GrandTotal)
                    - priorPayments.Sum(p => p.TotalSettledBaseAmount > 0 ? p.TotalSettledBaseAmount : p.PaidAmount);

                var periodInvoices = allInvoices.Where(i => i.SupplierId == sup.Id && i.IssueDate >= input.FromDate && i.IssueDate <= input.ToDate).ToList();
                var periodPayments = allPayments.Where(p => p.PartyId == sup.Id && p.PostingDate >= input.FromDate && p.PostingDate <= input.ToDate).ToList();

                var invoicedAmount = periodInvoices.Sum(i => i.IsReturn ? -i.GrandTotal : i.GrandTotal);
                var paidAmount = periodPayments.Sum(p => p.TotalSettledBaseAmount > 0 ? p.TotalSettledBaseAmount : p.PaidAmount);
                var closingBalance = openingBalance + invoicedAmount - paidAmount;

                if (!input.IncludeZeroBalance && openingBalance == 0 && invoicedAmount == 0 && paidAmount == 0 && closingBalance == 0)
                {
                    continue;
                }

                AgingBucketDto? aging = null;
                if (input.IncludeAging)
                {
                    aging = CalculateAging(allInvoices.Where(i => i.SupplierId == sup.Id && i.IssueDate <= input.ToDate).Select(i => (i.DueDate ?? i.IssueDate, i.GrandTotal - i.AmountPaid)).ToList(), input.ToDate);
                }

                result.Statements.Add(new PartyStatementSummaryDto
                {
                    PartyId = sup.Id,
                    PartyName = sup.Name,
                    PartyType = "Supplier",
                    OpeningBalance = openingBalance,
                    InvoicedAmount = invoicedAmount,
                    PaidAmount = paidAmount,
                    ClosingBalance = closingBalance,
                    Aging = aging
                });
            }
        }
        else
        {
            var custQuery = await _customerRepository.GetQueryableAsync();
            var customers = custQuery.Where(c => c.CompanyId == input.CompanyId);
            if (input.PartyIds != null && input.PartyIds.Count > 0)
            {
                customers = customers.Where(c => input.PartyIds.Contains(c.Id));
            }
            var customerList = customers.ToList();

            var siQuery = await _siRepository.GetQueryableAsync();
            var peQuery = await GetPaymentQueryableWithTaxesAsync();

            var allInvoices = siQuery
                .Where(si => si.CompanyId == input.CompanyId && si.Status == Core.DocumentStatus.Posted)
                .ToList();

            var allPayments = peQuery
                .Where(pe => pe.CompanyId == input.CompanyId && pe.PartyType == "Customer" && pe.Status == Core.DocumentStatus.Posted)
                .ToList();

            foreach (var cust in customerList)
            {
                var priorInvoices = allInvoices.Where(i => i.CustomerId == cust.Id && i.IssueDate < input.FromDate).ToList();
                var priorPayments = allPayments.Where(p => p.PartyId == cust.Id && p.PostingDate < input.FromDate).ToList();
                var openingBalance = priorInvoices.Sum(i => i.IsReturn ? -i.GrandTotal : i.GrandTotal)
                    - priorPayments.Sum(p => p.TotalSettledBaseAmount > 0 ? p.TotalSettledBaseAmount : p.PaidAmount);

                var periodInvoices = allInvoices.Where(i => i.CustomerId == cust.Id && i.IssueDate >= input.FromDate && i.IssueDate <= input.ToDate).ToList();
                var periodPayments = allPayments.Where(p => p.PartyId == cust.Id && p.PostingDate >= input.FromDate && p.PostingDate <= input.ToDate).ToList();

                var invoicedAmount = periodInvoices.Sum(i => i.IsReturn ? -i.GrandTotal : i.GrandTotal);
                var paidAmount = periodPayments.Sum(p => p.TotalSettledBaseAmount > 0 ? p.TotalSettledBaseAmount : p.PaidAmount);
                var closingBalance = openingBalance + invoicedAmount - paidAmount;

                if (!input.IncludeZeroBalance && openingBalance == 0 && invoicedAmount == 0 && paidAmount == 0 && closingBalance == 0)
                {
                    continue;
                }

                AgingBucketDto? aging = null;
                if (input.IncludeAging)
                {
                    aging = CalculateAging(allInvoices.Where(i => i.CustomerId == cust.Id && i.IssueDate <= input.ToDate).Select(i => (i.DueDate ?? i.IssueDate, i.GrandTotal - i.AmountPaid)).ToList(), input.ToDate);
                }

                result.Statements.Add(new PartyStatementSummaryDto
                {
                    PartyId = cust.Id,
                    PartyName = cust.Name,
                    PartyType = "Customer",
                    OpeningBalance = openingBalance,
                    InvoicedAmount = invoicedAmount,
                    PaidAmount = paidAmount,
                    ClosingBalance = closingBalance,
                    Aging = aging
                });
            }
        }

        result.TotalOpeningBalance = result.Statements.Sum(s => s.OpeningBalance);
        result.TotalInvoiced = result.Statements.Sum(s => s.InvoicedAmount);
        result.TotalPaid = result.Statements.Sum(s => s.PaidAmount);
        result.TotalClosingBalance = result.Statements.Sum(s => s.ClosingBalance);

        if (input.IncludeAging && result.Statements.Any(s => s.Aging != null))
        {
            result.GrandTotalAging = new AgingBucketDto
            {
                Current_0_30 = result.Statements.Where(s => s.Aging != null).Sum(s => s.Aging!.Current_0_30),
                Age_31_60 = result.Statements.Where(s => s.Aging != null).Sum(s => s.Aging!.Age_31_60),
                Age_61_90 = result.Statements.Where(s => s.Aging != null).Sum(s => s.Aging!.Age_61_90),
                Age_91_120 = result.Statements.Where(s => s.Aging != null).Sum(s => s.Aging!.Age_91_120),
                Age_120_Plus = result.Statements.Where(s => s.Aging != null).Sum(s => s.Aging!.Age_120_Plus),
                TotalOutstanding = result.Statements.Where(s => s.Aging != null).Sum(s => s.Aging!.TotalOutstanding)
            };
        }

        return result;
    }

    private static AgingBucketDto CalculateAging(List<(DateTime DueDate, decimal Outstanding)> invoices, DateTime asOfDate)
    {
        var aging = new AgingBucketDto();
        foreach (var (dueDate, outstanding) in invoices.Where(i => i.Outstanding > 0.01m))
        {
            var days = (asOfDate - dueDate).Days;
            if (days <= 30)
                aging.Current_0_30 += outstanding;
            else if (days <= 60)
                aging.Age_31_60 += outstanding;
            else if (days <= 90)
                aging.Age_61_90 += outstanding;
            else if (days <= 120)
                aging.Age_91_120 += outstanding;
            else
                aging.Age_120_Plus += outstanding;

            aging.TotalOutstanding += outstanding;
        }
        return aging;
    }
}

