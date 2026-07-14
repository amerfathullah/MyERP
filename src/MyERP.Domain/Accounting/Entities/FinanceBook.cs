using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Finance Book — enables multiple sets of accounting books for the same transactions.
/// ERPNext equivalent: accounts/doctype/finance_book/finance_book.py
/// 
/// Primary use case: different depreciation schedules for tax vs management reporting.
/// Each asset can have a depreciation schedule per finance book.
/// GL entries are tagged with the finance book they belong to.
/// 
/// Three GL filtering modes:
/// 1. No finance book filter → shows ALL entries (default book + all named books)
/// 2. Specific finance book → shows only that book's entries
/// 3. Default book (IsDefault=true) → entries without explicit book tag belong to this book
/// 
/// Only ONE finance book can be marked as default per company.
/// </summary>
public class FinanceBook : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    /// <summary>Unique name for this finance book (e.g., "Tax Depreciation", "IFRS Book").</summary>
    public string Name { get; private set; } = null!;

    /// <summary>
    /// If true, this is the default (primary) book. GL entries without an explicit 
    /// finance_book tag are considered part of this book.
    /// Only ONE finance book can be default per company.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>Company scope. Each company can have different sets of finance books.</summary>
    public Guid CompanyId { get; set; }

    /// <summary>Optional description/notes about this book's purpose.</summary>
    public string? Description { get; set; }

    protected FinanceBook() { }

    public FinanceBook(Guid id, Guid companyId, string name, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        TenantId = tenantId;
    }
}
