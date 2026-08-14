using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace MyERP.CRM;

public class ProspectDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string ProspectName { get; set; } = null!;
    public string? CompanyName { get; set; }
    public string? Industry { get; set; }
    public string? Website { get; set; }
    public string? Territory { get; set; }
    public decimal? AnnualRevenue { get; set; }
    public int? NumberOfEmployees { get; set; }
    public bool IsConverted { get; set; }
    public Guid? ConvertedCustomerId { get; set; }
    public int LeadCount { get; set; }
    public int OpportunityCount { get; set; }
    public string? Notes { get; set; }
}

public class CreateProspectDto
{
    public Guid CompanyId { get; set; }
    public string ProspectName { get; set; } = null!;
    public string? CompanyName { get; set; }
    public string? Industry { get; set; }
    public string? Website { get; set; }
    public string? Territory { get; set; }
    public string? CustomerGroup { get; set; }
    public decimal? AnnualRevenue { get; set; }
    public int? NumberOfEmployees { get; set; }
    public string? Notes { get; set; }
}

public class ContractDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string ContractNumber { get; set; } = null!;
    public string? ContractName { get; set; }
    public string PartyType { get; set; } = null!;
    public Guid PartyId { get; set; }
    public string? PartyName { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? SigningDate { get; set; }
    public ContractStatus Status { get; set; }
    public decimal? ContractValue { get; set; }
    public string? CurrencyCode { get; set; }
    public bool RequiresFulfilment { get; set; }
    public bool IsAutoRenewal { get; set; }
    public string? Notes { get; set; }
}

public class CreateContractDto
{
    public Guid CompanyId { get; set; }
    public string? ContractName { get; set; }
    public string PartyType { get; set; } = null!;
    public Guid PartyId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? ContractTerms { get; set; }
    public decimal? ContractValue { get; set; }
    public string? CurrencyCode { get; set; }
    public bool RequiresFulfilment { get; set; }
    public bool IsAutoRenewal { get; set; }
    public int? RenewalReminderDays { get; set; }
    public string? Notes { get; set; }
}

public class ShipmentDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string ShipmentNumber { get; set; } = null!;
    public string? PickupFromName { get; set; }
    public string? DeliveryToName { get; set; }
    public DateTime? PickupDate { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public string? Carrier { get; set; }
    public string? TrackingNumber { get; set; }
    public string? TrackingUrl { get; set; }
    public ShipmentStatus Status { get; set; }
    public int DeliveryNoteCount { get; set; }
    public decimal? TotalNetWeight { get; set; }
    public decimal? ValueOfGoods { get; set; }
    public string? CurrencyCode { get; set; }
    public string? Notes { get; set; }
}

public class CreateShipmentDto
{
    public Guid CompanyId { get; set; }
    public string? PickupFromType { get; set; }
    public Guid? PickupFromId { get; set; }
    public Guid? PickupAddressId { get; set; }
    public string? DeliveryToType { get; set; }
    public Guid? DeliveryToId { get; set; }
    public Guid? DeliveryAddressId { get; set; }
    public DateTime? PickupDate { get; set; }
    public string? Carrier { get; set; }
    public string? CarrierService { get; set; }
    public decimal? TotalNetWeight { get; set; }
    public decimal? TotalGrossWeight { get; set; }
    public string? WeightUom { get; set; }
    public decimal? ValueOfGoods { get; set; }
    public string? CurrencyCode { get; set; }
    public string? Notes { get; set; }
    public List<Guid>? DeliveryNoteIds { get; set; }
}
