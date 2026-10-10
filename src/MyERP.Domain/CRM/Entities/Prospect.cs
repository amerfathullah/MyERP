using System;
using System.Collections.Generic;
using System.Linq;
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

    public void AddLead(Guid leadLinkId, Guid leadId, string? leadName = null, string? email = null, LeadStatus? status = null)
    {
        if (ConvertedCustomerId.HasValue)
            throw new BusinessException(MyERPDomainErrorCodes.ProspectAlreadyConverted)
                .WithData("prospectName", ProspectName);

        // Per ERPNext PR #59907 / commit 688c26c951: reject duplicate leads in the same prospect
        if (_leads.Any(l => l.LeadId == leadId))
            throw new BusinessException(MyERPDomainErrorCodes.LeadAlreadyInProspect)
                .WithData("leadId", leadId)
                .WithData("leadName", leadName ?? leadId.ToString())
                .WithData("prospectName", ProspectName);

        _leads.Add(new ProspectLead(leadLinkId, Id, leadId, leadName, email, status));
    }

    /// <summary>
    /// Synchronizes the status of a linked lead row.
    /// Per ERPNext commit eb445464ca: updates lead row status in prospect (e.g., Converted).
    /// </summary>
    public void UpdateLeadStatus(Guid leadId, LeadStatus status)
    {
        var leadRow = _leads.FirstOrDefault(l => l.LeadId == leadId);
        if (leadRow != null)
        {
            leadRow.Status = status;
        }
    }

    /// <summary>
    /// Removes a lead from the prospect.
    /// Per ERPNext commit 5e9c7cb58a: deleting a lead removes only its row, keeping the prospect intact.
    /// </summary>
    public void RemoveLead(Guid leadId)
    {
        _leads.RemoveAll(l => l.LeadId == leadId);
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
    public LeadStatus? Status { get; set; }

    protected ProspectLead() { }

    public ProspectLead(Guid id, Guid prospectId, Guid leadId, string? leadName, string? email, LeadStatus? status = null)
        : base(id)
    {
        ProspectId = prospectId;
        LeadId = leadId;
        LeadName = leadName;
        Email = email;
        Status = status;
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
