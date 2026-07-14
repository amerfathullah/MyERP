using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Accounting Period — controls which document types are allowed to post in a given date range.
/// Per ERPNext: each period has a list of closed document types.
/// A doctype NOT in the closed list is unaffected by the period closure.
/// Users with the exempted role can bypass the closure check entirely.
/// ERPNext protects 18 doctypes: Sales Invoice, Purchase Invoice, Journal Entry, Payment Entry,
/// Bank Transaction, Stock Entry, Purchase Receipt, Delivery Note, Sales Order, Purchase Order,
/// Quotation, Material Request, Payroll Entry, Asset, Period Closing Voucher, etc.
/// </summary>
public class AccountingPeriod : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string PeriodName { get; set; } = null!;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    /// <summary>If true, this period is closed for document types in ClosedDocumentTypes list.</summary>
    public bool IsClosed { get; set; }

    /// <summary>Role that can bypass the period closure check.</summary>
    public string? ExemptedRole { get; set; }

    /// <summary>
    /// Comma-separated list of document types that are closed in this period.
    /// E.g., "SalesInvoice,PurchaseInvoice,JournalEntry,PaymentEntry"
    /// If empty/null when IsClosed=true, ALL document types are blocked (legacy blanket closure).
    /// </summary>
    public string? ClosedDocumentTypes { get; set; }

    protected AccountingPeriod() { }

    public AccountingPeriod(Guid id, Guid companyId, string periodName, DateTime startDate, DateTime endDate, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        PeriodName = Check.NotNullOrWhiteSpace(periodName, nameof(periodName), 100);
        StartDate = startDate;
        EndDate = endDate;
        TenantId = tenantId;
    }

    public void Close()
    {
        IsClosed = true;
    }

    public void Reopen()
    {
        IsClosed = false;
    }

    /// <summary>Add a document type to the closed list.</summary>
    public void CloseDocumentType(string documentType)
    {
        var types = GetClosedDocumentTypesList();
        if (!types.Contains(documentType, StringComparer.OrdinalIgnoreCase))
        {
            types.Add(documentType);
            ClosedDocumentTypes = string.Join(",", types);
        }
        IsClosed = true;
    }

    /// <summary>Remove a document type from the closed list.</summary>
    public void ReopenDocumentType(string documentType)
    {
        var types = GetClosedDocumentTypesList();
        types.RemoveAll(t => t.Equals(documentType, StringComparison.OrdinalIgnoreCase));
        ClosedDocumentTypes = types.Count > 0 ? string.Join(",", types) : null;
        if (types.Count == 0) IsClosed = false;
    }

    /// <summary>Check if a specific document type is closed in this period.</summary>
    public bool IsClosedForDocumentType(string documentType)
    {
        if (!IsClosed) return false;

        // Blanket closure: ClosedDocumentTypes is null/empty → ALL types blocked
        if (string.IsNullOrWhiteSpace(ClosedDocumentTypes)) return true;

        var types = GetClosedDocumentTypesList();
        return types.Any(t => t.Equals(documentType, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Check if a posting date falls within this period.</summary>
    public bool ContainsDate(DateTime postingDate)
    {
        return postingDate >= StartDate && postingDate <= EndDate;
    }

    private List<string> GetClosedDocumentTypesList()
    {
        if (string.IsNullOrWhiteSpace(ClosedDocumentTypes))
            return new List<string>();
        return ClosedDocumentTypes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }
}
