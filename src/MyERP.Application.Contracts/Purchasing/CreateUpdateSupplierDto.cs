using System;
using System.ComponentModel.DataAnnotations;

namespace MyERP.Purchasing;

public class CreateUpdateSupplierDto
{
    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    [StringLength(SupplierConsts.MaxNameLength)]
    public string Name { get; set; } = null!;

    [StringLength(SupplierConsts.MaxCodeLength)]
    public string? SupplierCode { get; set; }

    [StringLength(SupplierConsts.MaxTinLength)]
    public string? Tin { get; set; }

    [StringLength(SupplierConsts.MaxRegistrationNumberLength)]
    public string? RegistrationNumber { get; set; }

    [StringLength(SupplierConsts.MaxSstRegistrationLength)]
    public string? SstRegistrationNumber { get; set; }

    [StringLength(SupplierConsts.MaxIdTypeLength)]
    public string? IdType { get; set; }

    [StringLength(SupplierConsts.MaxIdValueLength)]
    public string? IdValue { get; set; }

    [StringLength(SupplierConsts.MaxContactPersonLength)]
    public string? ContactPerson { get; set; }

    [StringLength(SupplierConsts.MaxPhoneLength)]
    public string? Phone { get; set; }

    [StringLength(SupplierConsts.MaxEmailLength)]
    [EmailAddress]
    public string? Email { get; set; }

    [StringLength(SupplierConsts.MaxWebsiteLength)]
    public string? Website { get; set; }

    [StringLength(SupplierConsts.MaxAddressLength)]
    public string? Address { get; set; }

    [StringLength(SupplierConsts.MaxCityLength)]
    public string? City { get; set; }

    [StringLength(SupplierConsts.MaxStateLength)]
    public string? State { get; set; }

    [StringLength(SupplierConsts.MaxPostalCodeLength)]
    public string? PostalCode { get; set; }

    [StringLength(SupplierConsts.MaxCountryLength)]
    public string? Country { get; set; }

    public Guid? DefaultPayableAccountId { get; set; }

    public bool IsActive { get; set; } = true;
}
