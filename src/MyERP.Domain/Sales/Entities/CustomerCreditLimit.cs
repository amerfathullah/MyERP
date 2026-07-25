using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace MyERP.Sales.Entities;

/// <summary>
/// Per-company credit limit for a Customer.
/// Replaces the single CreditLimit field for multi-company scenarios.
/// Per DO-NOT: "Set credit limit below current outstanding amount (hard block)"
/// Per DO-NOT: "Same company cannot appear twice in credit_limit child table"
/// Maps to ERPNext selling/doctype/customer_credit_limit.
/// </summary>
public class CustomerCreditLimit : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    /// <summary>Parent customer.</summary>
    public Guid CustomerId { get; set; }

    /// <summary>Company this limit applies to.</summary>
    public Guid CompanyId { get; set; }

    /// <summary>Credit limit in company currency. 0 = no limit.</summary>
    public decimal CreditLimit { get; set; }

    /// <summary>Bypass credit limit check for this company.</summary>
    public bool BypassCreditLimitCheck { get; set; }

    /// <summary>Overdue billing threshold (blocks SI submit when overdue exceeds). 0 = no threshold.</summary>
    public decimal OverdueBillingThreshold { get; set; }

    protected CustomerCreditLimit() { }

    public CustomerCreditLimit(Guid id, Guid customerId, Guid companyId, decimal creditLimit, Guid? tenantId = null) : base(id)
    {
        CustomerId = customerId;
        CompanyId = companyId;
        CreditLimit = creditLimit;
        TenantId = tenantId;
    }
}
