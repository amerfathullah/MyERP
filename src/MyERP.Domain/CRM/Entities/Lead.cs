using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.CRM.Entities;

public class Lead : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string LeadNumber { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string? LastName { get; set; }
    public string? CompanyName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? MobileNo { get; set; }
    public string? JobTitle { get; set; }
    public string? Website { get; set; }

    public LeadStatus Status { get; private set; }
    public LeadSource Source { get; set; }

    // Location
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }

    // Qualification
    public string? Industry { get; set; }
    public decimal? AnnualRevenue { get; set; }
    public Guid? AssignedUserId { get; set; }

    // Conversion
    public Guid? ConvertedCustomerId { get; set; }
    public Guid? ConvertedOpportunityId { get; set; }

    public Guid CompanyId { get; set; }
    public string? Notes { get; set; }

    protected Lead() { }

    public Lead(Guid id, Guid companyId, string leadNumber, string firstName, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        LeadNumber = leadNumber;
        FirstName = firstName;
        Status = LeadStatus.New;
        TenantId = tenantId;
    }

    public string GetFullName() =>
        string.IsNullOrWhiteSpace(LastName) ? FirstName : $"{FirstName} {LastName}";

    public void MarkOpen()
    {
        if (Status != LeadStatus.New)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = LeadStatus.Open;
    }

    public void MarkInterested()
    {
        if (Status is not (LeadStatus.New or LeadStatus.Open or LeadStatus.Replied))
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = LeadStatus.Interested;
    }

    public void Qualify()
    {
        if (Status is not (LeadStatus.Open or LeadStatus.Interested or LeadStatus.Replied))
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = LeadStatus.Qualified;
    }

    public void ConvertToOpportunity(Guid opportunityId)
    {
        if (Status is not (LeadStatus.Qualified or LeadStatus.Interested or LeadStatus.Open))
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = LeadStatus.Converted;
        ConvertedOpportunityId = opportunityId;
    }

    public void ConvertToCustomer(Guid customerId)
    {
        Status = LeadStatus.Converted;
        ConvertedCustomerId = customerId;
    }

    public void MarkLost()
    {
        if (Status == LeadStatus.Converted)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = LeadStatus.Lost;
    }

    public void MarkDoNotContact()
    {
        Status = LeadStatus.DoNotContact;
    }

    public void Reopen()
    {
        if (Status is not (LeadStatus.Lost or LeadStatus.DoNotContact))
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = LeadStatus.Open;
    }
}
