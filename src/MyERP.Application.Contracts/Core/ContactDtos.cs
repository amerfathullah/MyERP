using System;
using Volo.Abp.Application.Dtos;

namespace MyERP.Core;

public class ContactDto : EntityDto<Guid>
{
    public string PartyType { get; set; } = null!;
    public Guid PartyId { get; set; }
    public string FirstName { get; set; } = null!;
    public string? LastName { get; set; }
    public string? Salutation { get; set; }
    public string FullName { get; set; } = null!;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? MobileNo { get; set; }
    public string? Designation { get; set; }
    public string? Department { get; set; }
    public bool IsPrimaryContact { get; set; }
    public bool IsBillingContact { get; set; }
}

public class CreateContactDto
{
    public string PartyType { get; set; } = null!;
    public Guid PartyId { get; set; }
    public string? Salutation { get; set; }
    public string FirstName { get; set; } = null!;
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? MobileNo { get; set; }
    public string? Designation { get; set; }
    public string? Department { get; set; }
    public bool IsPrimaryContact { get; set; }
    public bool IsBillingContact { get; set; }
}
