using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Account Category — groups accounts for financial report template formulas.
/// Per ERPNext v16: 28 standard categories (Cash and Cash Equivalents, COGS, Trade Payables, etc.)
/// with root_type scoping. Account entity gets account_category Link field.
/// On rename, updates ALL Financial Report Row formulas referencing old name.
/// Per gotcha #158.
/// </summary>
public class AccountCategory : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    /// <summary>Unique category name (e.g., "Cash and Cash Equivalents", "COGS").</summary>
    public string Name { get; private set; } = null!;

    /// <summary>Root type scoping: Asset, Liability, Equity, Income, or Expense.</summary>
    public string RootType { get; set; } = null!;

    /// <summary>Optional description for admin display.</summary>
    public string? Description { get; set; }

    protected AccountCategory() { }

    public AccountCategory(Guid id, string name, string rootType) : base(id)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(rootType))
            throw new ArgumentException("RootType is required.", nameof(rootType));

        Name = name;
        RootType = rootType;
    }

    public void Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("Name is required.", nameof(newName));
        Name = newName;
    }
}
