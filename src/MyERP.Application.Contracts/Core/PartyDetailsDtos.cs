using System;

namespace MyERP.Core;

public class GetPartyDetailsInput
{
    public Guid PartyId { get; set; }
    public Guid? CompanyId { get; set; }
}

public class PartyDetailsDto
{
    public Guid PartyId { get; set; }
    public string PartyName { get; set; } = null!;
    public string PartyType { get; set; } = null!;

    // Identity / registration (for LHDN e-Invoice)
    public string? Tin { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? SstRegistrationNumber { get; set; }
    public string? IdType { get; set; }
    public string? IdValue { get; set; }

    // Contact
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }

    // Billing address
    public Guid? BillingAddressId { get; set; }
    public string? BillingAddress { get; set; }
    public string? BillingCity { get; set; }
    public string? BillingState { get; set; }
    public string? BillingPostalCode { get; set; }
    public string? BillingCountry { get; set; }

    // Shipping address
    public Guid? ShippingAddressId { get; set; }
    public string? ShippingAddress { get; set; }

    // Defaults
    public Guid? DefaultPaymentTermsTemplateId { get; set; }
    public string? PaymentTermsTemplateName { get; set; }
    /// <summary>First term's credit days for due date auto-calculation.</summary>
    public int DefaultCreditDays { get; set; }
    public Guid? DefaultReceivableAccountId { get; set; }
    public Guid? DefaultPayableAccountId { get; set; }
    public Guid? CustomerGroupId { get; set; }
    public Guid? TerritoryId { get; set; }
    public string? CompanyCurrency { get; set; }

    // Credit (Customer only)
    public decimal CreditLimit { get; set; }
    public decimal Outstanding { get; set; }
}
