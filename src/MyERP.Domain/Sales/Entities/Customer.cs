using System;
using MyERP.Sales;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Sales.Entities;

/// <summary>
/// Customer master data.
/// Maps to ERPNext selling/doctype/customer.
/// </summary>
public class Customer : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string Name { get; private set; } = null!;
    public string? CustomerCode { get; set; }

    /// <summary>Tax Identification Number — required for LHDN e-Invoice.</summary>
    public string? Tin { get; set; }

    /// <summary>Business registration number (BRN/SSM).</summary>
    public string? RegistrationNumber { get; set; }

    /// <summary>SST registration number.</summary>
    public string? SstRegistrationNumber { get; set; }

    /// <summary>ID type for LHDN (e.g., BRN, NRIC, PASSPORT, ARMY).</summary>
    public string? IdType { get; set; }

    /// <summary>ID value corresponding to IdType.</summary>
    public string? IdValue { get; set; }

    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }

    // Address
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }

    /// <summary>Default receivable account for this customer.</summary>
    public Guid? DefaultReceivableAccountId { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Credit limit in company currency. 0 = no limit.</summary>
    public decimal CreditLimit { get; set; }

    /// <summary>
    /// SO-specific credit limit bypass — unlike CustomerCreditLimit.BypassCreditLimitCheck
    /// (a blanket bypass checked everywhere), this only skips the check at Sales Order submit.
    /// Delivery Note and Sales Invoice submit still enforce the credit limit normally.
    /// </summary>
    public bool BypassCreditLimitCheckAtSalesOrder { get; set; }

    /// <summary>For inter-company: the Company this customer represents (bidirectional link).</summary>
    public Guid? RepresentsCompanyId { get; set; }

    /// <summary>Customer group for categorization, defaults, and reporting.</summary>
    public Guid? CustomerGroupId { get; set; }

    /// <summary>Sales territory for this customer.</summary>
    public Guid? TerritoryId { get; set; }

    /// <summary>Loyalty program for automatic point earning on invoices.</summary>
    public Guid? LoyaltyProgramId { get; set; }

    /// <summary>Default payment terms template (auto-applied to invoices for this customer).</summary>
    public Guid? DefaultPaymentTermsTemplateId { get; set; }

    /// <summary>Default Selling Price List — fetched onto new Sales Orders/Invoices for this customer.</summary>
    public Guid? DefaultPriceListId { get; set; }

    /// <summary>
    /// When true, this customer is restricted to specific companies (per PR #57258/#57352).
    /// Transactions in companies not in the AllowedCompanies list will be blocked.
    /// </summary>
    public bool RestrictToCompanies { get; set; }

    /// <summary>Allow Sales Invoice creation without a Sales Order for this customer (exempts the Selling Settings requirement).</summary>
    public bool SoRequired { get; set; }

    /// <summary>Frozen customers cannot be transacted with except by the company's frozen-entries role (ERPNext is_frozen).</summary>
    public bool IsFrozen { get; set; }

    /// <summary>Allow Sales Invoice creation without a Delivery Note for this customer (exempts the Selling Settings requirement).</summary>
    public bool DnRequired { get; set; }

    /// <summary>Source Lead ID if customer was converted from a Lead (PR #50665 / commit 310099f4cd).</summary>
    public Guid? LeadId { get; set; }

    /// <summary>Source Opportunity ID if customer was converted from an Opportunity (PR #50665 / commit 310099f4cd).</summary>
    public Guid? OpportunityId { get; set; }

    /// <summary>Source Prospect ID if customer was converted from a Prospect (PR #50665 / commit 310099f4cd).</summary>
    public Guid? ProspectId { get; set; }

    /// <summary>
    /// Blocks saving or submitting Sales Orders, Delivery Notes, Sales Invoices and POS Invoices for this customer.
    /// Maps to ERPNext selling/doctype/customer on_hold (PR #59303 / commit d75b957ce0).
    /// </summary>
    public bool OnHold { get; set; }

    /// <summary>
    /// Date until which the customer is on hold. Null = blocked indefinitely.
    /// Maps to ERPNext selling/doctype/customer release_date.
    /// </summary>
    public DateTime? ReleaseDate { get; set; }

    /// <summary>
    /// Whether customer is currently blocked (as of UTC today).
    /// </summary>
    public bool IsBlocked => OnHold && (!ReleaseDate.HasValue || DateTime.UtcNow.Date <= ReleaseDate.Value.Date);

    /// <summary>
    /// Whether customer is blocked as of the given date.
    /// </summary>
    public bool IsBlockedOn(DateTime asOfDate)
    {
        if (!OnHold) return false;
        return !ReleaseDate.HasValue || asOfDate.Date <= ReleaseDate.Value.Date;
    }

    protected Customer() { }

    public Customer(Guid id, Guid companyId, string name, Guid? tenantId = null) : base(id)
    {
        CompanyId = companyId;
        SetName(name);
        TenantId = tenantId;
    }

    public void SetName(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name?.Trim(), nameof(name), CustomerConsts.MaxNameLength);
    }
}
