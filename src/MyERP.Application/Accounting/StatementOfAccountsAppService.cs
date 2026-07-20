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
public class StatementOfAccountsAppService : ApplicationService
{
    private readonly IRepository<SalesInvoice, Guid> _siRepository;
    private readonly IRepository<PurchaseInvoice, Guid> _piRepository;
    private readonly IRepository<PaymentEntry, Guid> _peRepository;

    public StatementOfAccountsAppService(
        IRepository<SalesInvoice, Guid> siRepository,
        IRepository<PurchaseInvoice, Guid> piRepository,
        IRepository<PaymentEntry, Guid> peRepository)
    {
        _siRepository = siRepository;
        _piRepository = piRepository;
        _peRepository = peRepository;
    }

    /// <summary>
    /// Generates a Statement of Accounts for a customer within a date range.
    /// Shows all invoices, payments, credit notes with running balance.
    /// </summary>
    public async Task<StatementOfAccountsDto> GetCustomerStatementAsync(
        Guid customerId, Guid companyId, DateTime fromDate, DateTime toDate)
    {
        var siQuery = await _siRepository.GetQueryableAsync();
        var peQuery = await _peRepository.GetQueryableAsync();

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
            .Select(pe => new { pe.Id, pe.PaymentNumber, pe.PostingDate, pe.PaidAmount })
            .ToList();

        // Calculate opening balance (outstanding before fromDate)
        var priorInvoices = siQuery
            .Where(si => si.CustomerId == customerId
                && si.CompanyId == companyId
                && si.Status == Core.DocumentStatus.Posted
                && si.IssueDate < fromDate)
            .ToList();
        decimal openingBalance = priorInvoices.Sum(si => si.GrandTotal - si.AmountPaid);

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

        // Add payments
        foreach (var pmt in payments)
        {
            runningBalance -= pmt.PaidAmount;
            entries.Add(new StatementEntryDto
            {
                Date = pmt.PostingDate,
                DocumentType = "Payment",
                DocumentNumber = pmt.PaymentNumber ?? "PE",
                DocumentId = pmt.Id,
                DebitAmount = 0,
                CreditAmount = pmt.PaidAmount,
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
    /// </summary>
    public async Task<SupplierStatementDto> GetSupplierStatementAsync(
        Guid supplierId, Guid companyId, DateTime fromDate, DateTime toDate)
    {
        var piQuery = await _piRepository.GetQueryableAsync();
        var peQuery = await _peRepository.GetQueryableAsync();

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
            .Select(pe => new { pe.Id, pe.PaymentNumber, pe.PostingDate, pe.PaidAmount })
            .ToList();

        // Opening balance
        var priorInvoices = piQuery
            .Where(pi => pi.SupplierId == supplierId
                && pi.CompanyId == companyId
                && pi.Status == Core.DocumentStatus.Posted
                && pi.IssueDate < fromDate)
            .ToList();
        decimal openingBalance = priorInvoices.Sum(pi => pi.GrandTotal - pi.AmountPaid);

        var entries = new List<StatementEntryDto>();
        decimal runningBalance = openingBalance;

        foreach (var inv in invoices)
        {
            decimal amount = inv.IsReturn ? -inv.GrandTotal : inv.GrandTotal;
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
            entries.Add(new StatementEntryDto
            {
                Date = pmt.PostingDate,
                DocumentType = "Payment",
                DocumentNumber = pmt.PaymentNumber ?? "PE",
                DocumentId = pmt.Id,
                DebitAmount = pmt.PaidAmount,
                CreditAmount = 0,
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
            Entries = entries
        };
    }
}

public class StatementOfAccountsDto
{
    public Guid CustomerId { get; set; }
    public Guid CompanyId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal ClosingBalance { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public List<StatementEntryDto> Entries { get; set; } = new();
}

public class StatementEntryDto
{
    public DateTime Date { get; set; }
    public string DocumentType { get; set; } = null!;
    public string DocumentNumber { get; set; } = null!;
    public Guid DocumentId { get; set; }
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public decimal RunningBalance { get; set; }
}

public class SupplierStatementDto
{
    public Guid SupplierId { get; set; }
    public Guid CompanyId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal ClosingBalance { get; set; }
    public decimal TotalInvoiced { get; set; }
    public decimal TotalPaid { get; set; }
    public List<StatementEntryDto> Entries { get; set; } = new();
}
