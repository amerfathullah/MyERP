using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.CRM;

// === Lead DTOs ===

public class LeadDto : AuditedEntityDto<Guid>
{
    public string LeadNumber { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string? LastName { get; set; }
    public string? CompanyName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? MobileNo { get; set; }
    public string? JobTitle { get; set; }
    public string? Website { get; set; }
    public LeadStatus Status { get; set; }
    public LeadSource Source { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public string? Industry { get; set; }
    public decimal? AnnualRevenue { get; set; }
    public Guid? AssignedUserId { get; set; }
    public Guid? ConvertedCustomerId { get; set; }
    public Guid? ConvertedOpportunityId { get; set; }
    public Guid CompanyId { get; set; }
    public string? Notes { get; set; }
    public string? FullName { get; set; }
}

public class CreateLeadDto
{
    [Required]
    [StringLength(LeadConsts.MaxFirstNameLength)]
    public string FirstName { get; set; } = null!;

    [StringLength(LeadConsts.MaxLastNameLength)]
    public string? LastName { get; set; }

    [StringLength(LeadConsts.MaxCompanyNameLength)]
    public string? CompanyName { get; set; }

    [StringLength(LeadConsts.MaxEmailLength)]
    [EmailAddress]
    public string? Email { get; set; }

    [StringLength(LeadConsts.MaxPhoneLength)]
    public string? Phone { get; set; }

    [StringLength(LeadConsts.MaxPhoneLength)]
    public string? MobileNo { get; set; }

    [StringLength(LeadConsts.MaxJobTitleLength)]
    public string? JobTitle { get; set; }

    [StringLength(LeadConsts.MaxWebsiteLength)]
    public string? Website { get; set; }

    public LeadSource Source { get; set; }

    [StringLength(LeadConsts.MaxCityLength)]
    public string? City { get; set; }

    [StringLength(LeadConsts.MaxStateLength)]
    public string? State { get; set; }

    [StringLength(LeadConsts.MaxCountryLength)]
    public string? Country { get; set; }

    [StringLength(LeadConsts.MaxIndustryLength)]
    public string? Industry { get; set; }

    public decimal? AnnualRevenue { get; set; }
    public Guid? AssignedUserId { get; set; }

    [Required]
    public Guid CompanyId { get; set; }

    [StringLength(LeadConsts.MaxNoteLength)]
    public string? Notes { get; set; }
}

public class UpdateLeadDto
{
    [Required]
    [StringLength(LeadConsts.MaxFirstNameLength)]
    public string FirstName { get; set; } = null!;

    [StringLength(LeadConsts.MaxLastNameLength)]
    public string? LastName { get; set; }

    [StringLength(LeadConsts.MaxCompanyNameLength)]
    public string? CompanyName { get; set; }

    [StringLength(LeadConsts.MaxEmailLength)]
    [EmailAddress]
    public string? Email { get; set; }

    [StringLength(LeadConsts.MaxPhoneLength)]
    public string? Phone { get; set; }

    [StringLength(LeadConsts.MaxPhoneLength)]
    public string? MobileNo { get; set; }

    [StringLength(LeadConsts.MaxJobTitleLength)]
    public string? JobTitle { get; set; }

    [StringLength(LeadConsts.MaxWebsiteLength)]
    public string? Website { get; set; }

    public LeadSource Source { get; set; }

    [StringLength(LeadConsts.MaxCityLength)]
    public string? City { get; set; }

    [StringLength(LeadConsts.MaxStateLength)]
    public string? State { get; set; }

    [StringLength(LeadConsts.MaxCountryLength)]
    public string? Country { get; set; }

    [StringLength(LeadConsts.MaxIndustryLength)]
    public string? Industry { get; set; }

    public decimal? AnnualRevenue { get; set; }
    public Guid? AssignedUserId { get; set; }

    [StringLength(LeadConsts.MaxNoteLength)]
    public string? Notes { get; set; }
}

public class GetLeadListDto : PagedAndSortedResultRequestDto
{
    public LeadStatus? Status { get; set; }
    public LeadSource? Source { get; set; }
    public string? Filter { get; set; }
    public Guid? CompanyId { get; set; }
}

// === Opportunity DTOs ===

public class OpportunityDto : AuditedEntityDto<Guid>
{
    public string OpportunityNumber { get; set; } = null!;
    public string Title { get; set; } = null!;
    public OpportunityStatus Status { get; set; }
    public OpportunityType OpportunityType { get; set; }
    public Guid? LeadId { get; set; }
    public Guid? CustomerId { get; set; }
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? SalesStage { get; set; }
    public int Probability { get; set; }
    public DateTime? ExpectedClosingDate { get; set; }
    public decimal OpportunityAmount { get; set; }
    public string CurrencyCode { get; set; } = "MYR";
    public Guid CompanyId { get; set; }
    public Guid? AssignedUserId { get; set; }
    public string? Territory { get; set; }
    public string? LostReason { get; set; }
    public string? Notes { get; set; }
    public List<OpportunityItemDto> Items { get; set; } = new();
}

public class OpportunityItemDto
{
    public Guid Id { get; set; }
    public Guid? ItemId { get; set; }
    public string Description { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
    public string? Uom { get; set; }
}

public class CreateOpportunityDto
{
    [Required]
    [StringLength(OpportunityConsts.MaxTitleLength)]
    public string Title { get; set; } = null!;

    public OpportunityType OpportunityType { get; set; }

    public Guid? LeadId { get; set; }
    public Guid? CustomerId { get; set; }
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }

    [StringLength(OpportunityConsts.MaxSalesStageLength)]
    public string? SalesStage { get; set; }

    [Range(0, 100)]
    public int Probability { get; set; } = 20;

    public DateTime? ExpectedClosingDate { get; set; }
    public decimal OpportunityAmount { get; set; }

    [StringLength(OpportunityConsts.MaxCurrencyCodeLength)]
    public string CurrencyCode { get; set; } = "MYR";

    [Required]
    public Guid CompanyId { get; set; }

    public Guid? AssignedUserId { get; set; }
    public string? Territory { get; set; }

    [StringLength(OpportunityConsts.MaxNoteLength)]
    public string? Notes { get; set; }

    public List<CreateOpportunityItemDto> Items { get; set; } = new();
}

public class CreateOpportunityItemDto
{
    public Guid? ItemId { get; set; }

    [Required]
    public string Description { get; set; } = null!;

    [Range(0.01, double.MaxValue)]
    public decimal Quantity { get; set; }

    [Range(0, double.MaxValue)]
    public decimal UnitPrice { get; set; }

    public string? Uom { get; set; }
}

public class UpdateOpportunityDto
{
    [Required]
    [StringLength(OpportunityConsts.MaxTitleLength)]
    public string Title { get; set; } = null!;

    public OpportunityType OpportunityType { get; set; }
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }

    [StringLength(OpportunityConsts.MaxSalesStageLength)]
    public string? SalesStage { get; set; }

    [Range(0, 100)]
    public int Probability { get; set; }

    public DateTime? ExpectedClosingDate { get; set; }
    public decimal OpportunityAmount { get; set; }

    [StringLength(OpportunityConsts.MaxCurrencyCodeLength)]
    public string CurrencyCode { get; set; } = "MYR";

    public Guid? AssignedUserId { get; set; }
    public string? Territory { get; set; }

    [StringLength(OpportunityConsts.MaxNoteLength)]
    public string? Notes { get; set; }

    public List<CreateOpportunityItemDto> Items { get; set; } = new();
}

public class GetOpportunityListDto : PagedAndSortedResultRequestDto
{
    public OpportunityStatus? Status { get; set; }
    public OpportunityType? OpportunityType { get; set; }
    public string? Filter { get; set; }
    public Guid? CompanyId { get; set; }
    public Guid? LeadId { get; set; }
}

public class ConvertLeadToOpportunityDto
{
    [Required]
    public Guid LeadId { get; set; }

    [Required]
    [StringLength(OpportunityConsts.MaxTitleLength)]
    public string Title { get; set; } = null!;

    public OpportunityType OpportunityType { get; set; } = OpportunityType.Sales;
    public decimal OpportunityAmount { get; set; }
    public string? SalesStage { get; set; }
    public DateTime? ExpectedClosingDate { get; set; }
}
