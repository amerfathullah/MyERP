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

namespace MyERP.Core;

/// <summary>
/// Global Document Search — searches across all document types by number, party, or amount.
/// Returns unified results with document type, number, party, amount, status, and date.
/// 
/// Enables "find anything" from a single search box in the UI.
/// </summary>
[Authorize]
public class GlobalSearchAppService : ApplicationService
{
    private readonly IRepository<SalesInvoice, Guid> _siRepo;
    private readonly IRepository<PurchaseInvoice, Guid> _piRepo;
    private readonly IRepository<SalesOrder, Guid> _soRepo;
    private readonly IRepository<PurchaseOrder, Guid> _poRepo;
    private readonly IRepository<PaymentEntry, Guid> _peRepo;
    private readonly IRepository<JournalEntry, Guid> _jeRepo;
    private readonly IRepository<Customer, Guid> _customerRepo;
    private readonly IRepository<Supplier, Guid> _supplierRepo;

    public GlobalSearchAppService(
        IRepository<SalesInvoice, Guid> siRepo,
        IRepository<PurchaseInvoice, Guid> piRepo,
        IRepository<SalesOrder, Guid> soRepo,
        IRepository<PurchaseOrder, Guid> poRepo,
        IRepository<PaymentEntry, Guid> peRepo,
        IRepository<JournalEntry, Guid> jeRepo,
        IRepository<Customer, Guid> customerRepo,
        IRepository<Supplier, Guid> supplierRepo)
    {
        _siRepo = siRepo;
        _piRepo = piRepo;
        _soRepo = soRepo;
        _poRepo = poRepo;
        _peRepo = peRepo;
        _jeRepo = jeRepo;
        _customerRepo = customerRepo;
        _supplierRepo = supplierRepo;
    }

    /// <summary>
    /// Searches across all document types. Returns max 20 results sorted by relevance (date desc).
    /// </summary>
    public async Task<List<SearchResultDto>> SearchAsync(GlobalSearchInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Query) || input.Query.Length < 2)
            return new List<SearchResultDto>();

        var query = input.Query.ToLower().Trim();
        var results = new List<SearchResultDto>();
        int limit = input.MaxResults > 0 ? Math.Min(input.MaxResults, 50) : 20;

        // Search Sales Invoices
        var siQuery = await _siRepo.GetQueryableAsync();
        var salesInvoices = siQuery
            .Where(si => si.CompanyId == input.CompanyId &&
                (si.InvoiceNumber.ToLower().Contains(query) ||
                 (si.Notes != null && si.Notes.ToLower().Contains(query))))
            .OrderByDescending(si => si.IssueDate)
            .Take(5).ToList();

        results.AddRange(salesInvoices.Select(si => new SearchResultDto
        {
            Id = si.Id,
            DocumentType = "SalesInvoice",
            DocumentNumber = si.InvoiceNumber,
            Date = si.IssueDate,
            Amount = si.GrandTotal,
            Status = si.Status.ToString(),
            Route = $"/sales/invoices/{si.Id}"
        }));

        // Search Purchase Invoices
        var piQuery = await _piRepo.GetQueryableAsync();
        var purchaseInvoices = piQuery
            .Where(pi => pi.CompanyId == input.CompanyId &&
                (pi.InvoiceNumber.ToLower().Contains(query) ||
                 (pi.SupplierInvoiceNumber != null && pi.SupplierInvoiceNumber.ToLower().Contains(query))))
            .OrderByDescending(pi => pi.IssueDate)
            .Take(5).ToList();

        results.AddRange(purchaseInvoices.Select(pi => new SearchResultDto
        {
            Id = pi.Id,
            DocumentType = "PurchaseInvoice",
            DocumentNumber = pi.InvoiceNumber,
            Date = pi.IssueDate,
            Amount = pi.GrandTotal,
            Status = pi.Status.ToString(),
            Route = $"/purchasing/invoices/{pi.Id}"
        }));

        // Search Sales Orders
        var soQuery = await _soRepo.GetQueryableAsync();
        var salesOrders = soQuery
            .Where(so => so.CompanyId == input.CompanyId &&
                so.OrderNumber.ToLower().Contains(query))
            .OrderByDescending(so => so.OrderDate)
            .Take(5).ToList();

        results.AddRange(salesOrders.Select(so => new SearchResultDto
        {
            Id = so.Id,
            DocumentType = "SalesOrder",
            DocumentNumber = so.OrderNumber,
            Date = so.OrderDate,
            Amount = so.GrandTotal,
            Status = so.Status.ToString(),
            Route = $"/sales/orders/{so.Id}"
        }));

        // Search Purchase Orders
        var poQuery = await _poRepo.GetQueryableAsync();
        var purchaseOrders = poQuery
            .Where(po => po.CompanyId == input.CompanyId &&
                po.OrderNumber.ToLower().Contains(query))
            .OrderByDescending(po => po.OrderDate)
            .Take(5).ToList();

        results.AddRange(purchaseOrders.Select(po => new SearchResultDto
        {
            Id = po.Id,
            DocumentType = "PurchaseOrder",
            DocumentNumber = po.OrderNumber,
            Date = po.OrderDate,
            Amount = po.GrandTotal,
            Status = po.Status.ToString(),
            Route = $"/purchasing/orders/{po.Id}"
        }));

        // Search Payment Entries
        var peQuery = await _peRepo.GetQueryableAsync();
        var payments = peQuery
            .Where(pe => pe.CompanyId == input.CompanyId &&
                (pe.PaymentNumber != null && pe.PaymentNumber.ToLower().Contains(query)))
            .OrderByDescending(pe => pe.PostingDate)
            .Take(5).ToList();

        results.AddRange(payments.Select(pe => new SearchResultDto
        {
            Id = pe.Id,
            DocumentType = "PaymentEntry",
            DocumentNumber = pe.PaymentNumber ?? "",
            Date = pe.PostingDate,
            Amount = pe.PaidAmount,
            Status = pe.Status.ToString(),
            Route = $"/accounting/payments/{pe.Id}"
        }));

        // Search Journal Entries
        var jeQuery = await _jeRepo.GetQueryableAsync();
        var journals = jeQuery
            .Where(je => je.CompanyId == input.CompanyId &&
                (je.EntryNumber != null && je.EntryNumber.ToLower().Contains(query)))
            .OrderByDescending(je => je.PostingDate)
            .Take(5).ToList();

        results.AddRange(journals.Select(je => new SearchResultDto
        {
            Id = je.Id,
            DocumentType = "JournalEntry",
            DocumentNumber = je.EntryNumber ?? "",
            Date = je.PostingDate,
            Amount = je.TotalDebit,
            Status = je.Status.ToString(),
            Route = $"/accounting/journal-entries/{je.Id}"
        }));

        // Search Customers
        var custQuery = await _customerRepo.GetQueryableAsync();
        var customers = custQuery
            .Where(c => c.Name.ToLower().Contains(query) ||
                       (c.Tin != null && c.Tin.ToLower().Contains(query)))
            .Take(3).ToList();

        results.AddRange(customers.Select(c => new SearchResultDto
        {
            Id = c.Id,
            DocumentType = "Customer",
            DocumentNumber = c.Name,
            Date = c.CreationTime,
            Amount = 0,
            Status = c.IsActive ? "Active" : "Inactive",
            Route = $"/customers/{c.Id}/edit"
        }));

        // Search Suppliers
        var suppQuery = await _supplierRepo.GetQueryableAsync();
        var suppliers = suppQuery
            .Where(s => s.Name.ToLower().Contains(query) ||
                       (s.Tin != null && s.Tin.ToLower().Contains(query)))
            .Take(3).ToList();

        results.AddRange(suppliers.Select(s => new SearchResultDto
        {
            Id = s.Id,
            DocumentType = "Supplier",
            DocumentNumber = s.Name,
            Date = s.CreationTime,
            Amount = 0,
            Status = "Active",
            Route = $"/suppliers/{s.Id}/edit"
        }));

        // Sort by date descending and limit
        return results.OrderByDescending(r => r.Date).Take(limit).ToList();
    }
}

#region DTOs

public class GlobalSearchInput
{
    public string Query { get; set; } = null!;
    public Guid CompanyId { get; set; }
    public int MaxResults { get; set; } = 20;
}

public class SearchResultDto
{
    public Guid Id { get; set; }
    public string DocumentType { get; set; } = null!;
    public string DocumentNumber { get; set; } = null!;
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = null!;
    public string Route { get; set; } = null!;
}

#endregion

