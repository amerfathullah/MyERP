using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Core;

public class AddressDto : EntityDto<Guid>
{
    public string Title { get; set; } = null!;
    public string AddressType { get; set; } = null!;
    public string AddressLine1 { get; set; } = null!;
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string Country { get; set; } = null!;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string PartyType { get; set; } = null!;
    public Guid PartyId { get; set; }
    public bool IsPrimaryAddress { get; set; }
    public bool IsShippingAddress { get; set; }
}

public class CreateUpdateAddressDto
{
    [Required][StringLength(200)] public string Title { get; set; } = null!;
    [StringLength(50)] public string? AddressType { get; set; }
    [Required][StringLength(300)] public string AddressLine1 { get; set; } = null!;
    [StringLength(300)] public string? AddressLine2 { get; set; }
    [StringLength(100)] public string? City { get; set; }
    [StringLength(100)] public string? State { get; set; }
    [StringLength(20)] public string? PostalCode { get; set; }
    [Required][StringLength(100)] public string Country { get; set; } = "Malaysia";
    [StringLength(50)] public string? Phone { get; set; }
    [StringLength(200)] public string? Email { get; set; }
    [Required] public string PartyType { get; set; } = null!;
    [Required] public Guid PartyId { get; set; }
    public bool IsPrimaryAddress { get; set; }
    public bool IsShippingAddress { get; set; }
}
