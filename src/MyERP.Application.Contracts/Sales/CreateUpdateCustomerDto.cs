using System;
using System.ComponentModel.DataAnnotations;
using MyERP.Shared;

namespace MyERP.Sales;

public class CreateUpdateCustomerDto
{
    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    [StringLength(CustomerConsts.MaxNameLength)]
    public string Name { get; set; } = null!;

    [StringLength(CustomerConsts.MaxCodeLength)]
    public string? CustomerCode { get; set; }

    [StringLength(CustomerConsts.MaxTinLength)]
    public string? Tin { get; set; }

    [StringLength(CustomerConsts.MaxRegistrationNumberLength)]
    public string? RegistrationNumber { get; set; }

    [StringLength(CustomerConsts.MaxSstRegistrationLength)]
    public string? SstRegistrationNumber { get; set; }

    [StringLength(CustomerConsts.MaxIdTypeLength)]
    public string? IdType { get; set; }

    [StringLength(CustomerConsts.MaxIdValueLength)]
    public string? IdValue { get; set; }

    [StringLength(CustomerConsts.MaxContactPersonLength)]
    public string? ContactPerson { get; set; }

    [StringLength(CustomerConsts.MaxPhoneLength)]
    public string? Phone { get; set; }

    [StringLength(CustomerConsts.MaxEmailLength)]
    [OptionalEmailAddress]
    public string? Email { get; set; }

    [StringLength(CustomerConsts.MaxWebsiteLength)]
    public string? Website { get; set; }

    [StringLength(CustomerConsts.MaxAddressLength)]
    public string? Address { get; set; }

    [StringLength(CustomerConsts.MaxCityLength)]
    public string? City { get; set; }

    [StringLength(CustomerConsts.MaxStateLength)]
    public string? State { get; set; }

    [StringLength(CustomerConsts.MaxPostalCodeLength)]
    public string? PostalCode { get; set; }

    [StringLength(CustomerConsts.MaxCountryLength)]
    public string? Country { get; set; }

    public Guid? DefaultReceivableAccountId { get; set; }

    public bool IsActive { get; set; } = true;

    [Range(0, double.MaxValue)]
    public decimal CreditLimit { get; set; }

    public Guid? RepresentsCompanyId { get; set; }
    public Guid? CustomerGroupId { get; set; }
    public Guid? TerritoryId { get; set; }
    public Guid? LoyaltyProgramId { get; set; }
    public Guid? DefaultPaymentTermsTemplateId { get; set; }
    public Guid? DefaultPriceListId { get; set; }
    public bool RestrictToCompanies { get; set; }
    public bool SoRequired { get; set; }
    public bool DnRequired { get; set; }
    public Guid? LeadId { get; set; }
    public Guid? OpportunityId { get; set; }
    public Guid? ProspectId { get; set; }
}
