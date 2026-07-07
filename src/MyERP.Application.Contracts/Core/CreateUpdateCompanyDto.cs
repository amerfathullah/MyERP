using System.ComponentModel.DataAnnotations;

namespace MyERP.Core;

public class CreateUpdateCompanyDto
{
    [Required]
    [StringLength(CompanyConsts.MaxNameLength)]
    public string Name { get; set; } = null!;

    [StringLength(CompanyConsts.MaxShortNameLength)]
    public string? ShortName { get; set; }

    [StringLength(CompanyConsts.MaxTaxIdLength)]
    public string? TaxId { get; set; }

    [StringLength(CompanyConsts.MaxRegistrationNumberLength)]
    public string? RegistrationNumber { get; set; }

    [StringLength(CompanyConsts.MaxSstRegistrationLength)]
    public string? SstRegistrationNumber { get; set; }

    [StringLength(CompanyConsts.MaxMsicCodeLength)]
    public string? MsicCode { get; set; }

    [StringLength(CompanyConsts.MaxPhoneLength)]
    public string? Phone { get; set; }

    [StringLength(CompanyConsts.MaxEmailLength)]
    [EmailAddress]
    public string? Email { get; set; }

    [StringLength(CompanyConsts.MaxWebsiteLength)]
    public string? Website { get; set; }

    [StringLength(CompanyConsts.MaxAddressLength)]
    public string? Address { get; set; }

    [StringLength(CompanyConsts.MaxCityLength)]
    public string? City { get; set; }

    [StringLength(CompanyConsts.MaxStateLength)]
    public string? State { get; set; }

    [StringLength(CompanyConsts.MaxPostalCodeLength)]
    public string? PostalCode { get; set; }

    [StringLength(CompanyConsts.MaxCountryLength)]
    public string? Country { get; set; }

    [Required]
    [StringLength(CompanyConsts.MaxCurrencyCodeLength)]
    public string CurrencyCode { get; set; } = "MYR";

    [Range(1, 12)]
    public int FiscalYearStartMonth { get; set; } = 1;

    public bool IsActive { get; set; } = true;
}
