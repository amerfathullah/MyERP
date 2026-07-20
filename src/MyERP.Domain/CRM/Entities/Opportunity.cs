using System;
using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.CRM.Entities;

public class Opportunity : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string OpportunityNumber { get; set; } = null!;
    public string Title { get; set; } = null!;
    public OpportunityStatus Status { get; private set; }
    public OpportunityType OpportunityType { get; set; }

    // Source
    public Guid? LeadId { get; set; }
    public Guid? CustomerId { get; set; }
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }

    // Sales pipeline
    public string? SalesStage { get; set; }
    public int Probability { get; set; }
    public DateTime? ExpectedClosingDate { get; set; }

    // Financials
    public decimal OpportunityAmount { get; set; }
    public string CurrencyCode { get; set; } = "MYR";

    // Organization
    public Guid CompanyId { get; set; }
    public Guid? AssignedUserId { get; set; }
    public string? Territory { get; set; }

    // Loss tracking
    public string? LostReason { get; set; }

    public string? Notes { get; set; }

    // Child items
    public List<OpportunityItem> Items { get; private set; } = new();

    protected Opportunity() { }

    public Opportunity(Guid id, Guid companyId, string opportunityNumber, string title, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        OpportunityNumber = opportunityNumber;
        Title = title;
        Status = OpportunityStatus.Open;
        Probability = 20;
        SalesStage = "Prospecting";
        TenantId = tenantId;
    }

    public void MarkReplied()
    {
        if (Status != OpportunityStatus.Open)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = OpportunityStatus.Replied;
    }

    public void MarkQuotation()
    {
        if (Status is not (OpportunityStatus.Open or OpportunityStatus.Replied))
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = OpportunityStatus.Quotation;
    }

    public void Convert()
    {
        if (Status is not (OpportunityStatus.Open or OpportunityStatus.Quotation or OpportunityStatus.Replied))
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = OpportunityStatus.Converted;
    }

    public void DeclareLost(string? reason = null)
    {
        if (Status == OpportunityStatus.Converted)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = OpportunityStatus.Lost;
        LostReason = reason;
    }

    public void Close()
    {
        if (Status == OpportunityStatus.Converted)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = OpportunityStatus.Closed;
    }

    public void Reopen()
    {
        if (Status is not (OpportunityStatus.Lost or OpportunityStatus.Closed))
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = OpportunityStatus.Open;
        LostReason = null;
    }

    public void RecalculateAmount()
    {
        OpportunityAmount = 0;
        foreach (var item in Items)
        {
            OpportunityAmount += item.Amount;
        }
    }
}
