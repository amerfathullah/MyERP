using System;
using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.CRM.Entities;

/// <summary>
/// Prospect — represents a potential customer in the CRM pipeline.
/// Per ERPNext: Prospects group multiple Leads from the same organization.
/// Pipeline: Lead → Prospect → Customer.
/// </summary>
public class Prospect : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
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

    /// <summary>Converted Customer (set when Prospect → Customer conversion happens).</summary>
    public Guid? ConvertedCustomerId { get; set; }

    public bool IsConverted => ConvertedCustomerId.HasValue;

    /// <summary>Linked Leads (multiple leads can belong to the same Prospect).</summary>
    private readonly List<ProspectLead> _leads = new();
    public IReadOnlyList<ProspectLead> Leads => _leads.AsReadOnly();

    /// <summary>Linked Opportunities.</summary>
    private readonly List<ProspectOpportunity> _opportunities = new();
    public IReadOnlyList<ProspectOpportunity> Opportunities => _opportunities.AsReadOnly();

    protected Prospect() { }

    public Prospect(Guid id, Guid companyId, string prospectName, Guid? tenantId = null)
        : base(id)
    {
        Check.NotNullOrWhiteSpace(prospectName, nameof(prospectName));
        CompanyId = companyId;
        ProspectName = prospectName;
        TenantId = tenantId;
    }

    public void AddLead(Guid leadLinkId, Guid leadId, string? leadName = null, string? email = null)
    {
        if (ConvertedCustomerId.HasValue)
            throw new BusinessException("MyERP:17001")
                .WithData("prospectName", ProspectName);
        _leads.Add(new ProspectLead(leadLinkId, Id, leadId, leadName, email));
    }

    public void AddOpportunity(Guid linkId, Guid opportunityId, string? opportunityName = null, decimal? amount = null)
    {
        _opportunities.Add(new ProspectOpportunity(linkId, Id, opportunityId, opportunityName, amount));
    }

    public void ConvertToCustomer(Guid customerId)
    {
        if (ConvertedCustomerId.HasValue)
            throw new BusinessException("MyERP:17001")
                .WithData("prospectName", ProspectName);
        ConvertedCustomerId = customerId;
    }
}

/// <summary>Links a Lead to a Prospect.</summary>
public class ProspectLead : FullAuditedEntity<Guid>
{
    public Guid ProspectId { get; set; }
    public Guid LeadId { get; set; }
    public string? LeadName { get; set; }
    public string? Email { get; set; }

    protected ProspectLead() { }

    public ProspectLead(Guid id, Guid prospectId, Guid leadId, string? leadName, string? email)
        : base(id)
    {
        ProspectId = prospectId;
        LeadId = leadId;
        LeadName = leadName;
        Email = email;
    }
}

/// <summary>Links an Opportunity to a Prospect.</summary>
public class ProspectOpportunity : FullAuditedEntity<Guid>
{
    public Guid ProspectId { get; set; }
    public Guid OpportunityId { get; set; }
    public string? OpportunityName { get; set; }
    public decimal? Amount { get; set; }

    protected ProspectOpportunity() { }

    public ProspectOpportunity(Guid id, Guid prospectId, Guid opportunityId,
        string? opportunityName, decimal? amount)
        : base(id)
    {
        ProspectId = prospectId;
        OpportunityId = opportunityId;
        OpportunityName = opportunityName;
        Amount = amount;
    }
}
