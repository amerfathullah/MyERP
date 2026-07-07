using System;
using System.ComponentModel.DataAnnotations;

namespace MyERP.Core;

public class CreateUpdateBranchDto
{
    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    [StringLength(BranchConsts.MaxNameLength)]
    public string Name { get; set; } = null!;

    [StringLength(BranchConsts.MaxCodeLength)]
    public string? Code { get; set; }

    [StringLength(BranchConsts.MaxPhoneLength)]
    public string? Phone { get; set; }

    [StringLength(BranchConsts.MaxEmailLength)]
    [EmailAddress]
    public string? Email { get; set; }

    [StringLength(BranchConsts.MaxAddressLength)]
    public string? Address { get; set; }

    [StringLength(BranchConsts.MaxCityLength)]
    public string? City { get; set; }

    [StringLength(BranchConsts.MaxStateLength)]
    public string? State { get; set; }

    [StringLength(BranchConsts.MaxPostalCodeLength)]
    public string? PostalCode { get; set; }

    [StringLength(BranchConsts.MaxCountryLength)]
    public string? Country { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsHeadquarters { get; set; }
}
