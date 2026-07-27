using System;
using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Sales.Entities;

/// <summary>
/// POS Profile — defines configuration for a POS terminal/register.
/// Each POS Profile specifies: warehouse, price list, accepted payment modes,
/// default customer, and various operational settings.
/// Per ERPNext selling/doctype/pos_profile:
/// - Cannot disable while open POS Opening Entries exist
/// - One-per-profile open session enforced
/// Per DO-NOT: "Allow POS Profile disable while open POS Opening Entries exist"
/// </summary>
public class PosProfile : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    /// <summary>Profile name (e.g., "Counter 1", "Mall Outlet").</summary>
    public string ProfileName { get; set; } = null!;

    /// <summary>Default warehouse for stock deduction on POS sales.</summary>
    public Guid WarehouseId { get; set; }

    /// <summary>Price list for item pricing.</summary>
    public Guid? PriceListId { get; set; }

    /// <summary>Default customer for walk-in sales.</summary>
    public Guid? DefaultCustomerId { get; set; }

    /// <summary>Currency for POS transactions.</summary>
    public string CurrencyCode { get; set; } = "MYR";

    /// <summary>Whether to validate available stock before allowing sale.</summary>
    public bool ValidateStock { get; set; } = true;

    /// <summary>Whether to auto-create Sales Invoice (vs POS Invoice type).</summary>
    public string InvoiceType { get; set; } = "POS Invoice";

    /// <summary>Whether this profile is disabled.</summary>
    public bool IsDisabled { get; set; }

    /// <summary>Tax template to apply on POS invoices (SST for Malaysia).</summary>
    public Guid? TaxTemplateId { get; set; }

    /// <summary>Write-off account for small rounding differences.</summary>
    public Guid? WriteOffAccountId { get; set; }

    /// <summary>Write-off cost center.</summary>
    public Guid? WriteOffCostCenterId { get; set; }

    /// <summary>Maximum write-off amount per invoice (prevents large accidental write-offs).</summary>
    public decimal WriteOffLimit { get; set; }

    /// <summary>Whether to post change (cash overpayment) to GL.</summary>
    public bool PostChangeGlEntries { get; set; }

    /// <summary>Income account override for this POS profile.</summary>
    public Guid? IncomeAccountId { get; set; }

    /// <summary>Expense account override for this POS profile.</summary>
    public Guid? ExpenseAccountId { get; set; }

    /// <summary>Accepted payment modes for this POS profile.</summary>
    private readonly List<PosProfilePaymentMethod> _paymentMethods = new();
    public IReadOnlyList<PosProfilePaymentMethod> PaymentMethods => _paymentMethods.AsReadOnly();

    protected PosProfile() { }

    public PosProfile(Guid id, Guid companyId, string profileName, Guid warehouseId, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        ProfileName = Check.NotNullOrWhiteSpace(profileName, nameof(profileName), maxLength: 140);
        WarehouseId = warehouseId;
        TenantId = tenantId;
    }

    public void AddPaymentMethod(PosProfilePaymentMethod method)
    {
        _paymentMethods.Add(method);
    }

    public void ClearPaymentMethods()
    {
        _paymentMethods.Clear();
    }

    public void Disable()
    {
        IsDisabled = true;
    }

    public void Enable()
    {
        IsDisabled = false;
    }
}

/// <summary>
/// Payment method configuration for a POS Profile.
/// Each row links a Mode of Payment to a GL account for that POS register.
/// </summary>
public class PosProfilePaymentMethod : Entity<Guid>
{
    public Guid PosProfileId { get; set; }

    /// <summary>Mode of Payment ID (Cash, Credit Card, E-Wallet, etc.).</summary>
    public Guid ModeOfPaymentId { get; set; }

    /// <summary>GL Account to post payments to for this mode.</summary>
    public Guid AccountId { get; set; }

    /// <summary>Whether this is the default payment mode for this profile.</summary>
    public bool IsDefault { get; set; }

    protected PosProfilePaymentMethod() { }

    public PosProfilePaymentMethod(Guid id, Guid posProfileId, Guid modeOfPaymentId, Guid accountId)
        : base(id)
    {
        PosProfileId = posProfileId;
        ModeOfPaymentId = modeOfPaymentId;
        AccountId = accountId;
    }
}
