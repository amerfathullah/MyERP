using System;
using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.CRM.Entities;

/// <summary>
/// Contract Template — reusable contract terms text with placeholder substitution, referenced
/// by Contract.ContractTemplateId. Maps to ERPNext crm/doctype/contract_template.
/// </summary>
public class ContractTemplate : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string Title { get; set; } = null!;

    /// <summary>Terms text with {{placeholder}} tokens substituted at render time.</summary>
    public string? ContractTerms { get; set; }

    public bool RequiresFulfilment { get; set; }

    private readonly List<ContractTemplateFulfilmentTerm> _fulfilmentTerms = new();
    public IReadOnlyList<ContractTemplateFulfilmentTerm> FulfilmentTerms => _fulfilmentTerms.AsReadOnly();

    protected ContractTemplate() { }

    public ContractTemplate(Guid id, string title, Guid? tenantId = null) : base(id)
    {
        Title = Check.NotNullOrWhiteSpace(title, nameof(title), ContractTemplateConsts.MaxTitleLength);
        TenantId = tenantId;
    }

    public void AddFulfilmentTerm(ContractTemplateFulfilmentTerm term)
    {
        _fulfilmentTerms.Add(term);
    }

    /// <summary>
    /// Substitutes {{token}} placeholders in ContractTerms with values from <paramref name="values"/>.
    /// Simple string-replace substitution — no templating library dependency added for this.
    /// </summary>
    public string Render(IReadOnlyDictionary<string, string> values)
    {
        var text = ContractTerms ?? string.Empty;
        foreach (var kvp in values)
        {
            text = text.Replace("{{" + kvp.Key + "}}", kvp.Value);
        }
        return text;
    }
}

/// <summary>One fulfilment condition that must be satisfied for a Contract created from this template.</summary>
public class ContractTemplateFulfilmentTerm : Entity<Guid>
{
    public Guid ContractTemplateId { get; set; }
    public string TermText { get; set; } = null!;

    protected ContractTemplateFulfilmentTerm() { }

    public ContractTemplateFulfilmentTerm(Guid id, Guid contractTemplateId, string termText) : base(id)
    {
        ContractTemplateId = contractTemplateId;
        TermText = Check.NotNullOrWhiteSpace(termText, nameof(termText), ContractTemplateConsts.MaxFulfilmentTermLength);
    }
}
