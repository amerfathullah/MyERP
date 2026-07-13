using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Payment Terms Template — defines how invoice payments are scheduled.
/// E.g., "Net 30", "50% Advance + 50% on Delivery", "30/60/90 days".
/// The sum of all term InvoicePortions must equal exactly 100%.
/// </summary>
public class PaymentTermsTemplate : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string Name { get; set; } = null!;

    public bool IsActive { get; set; } = true;

    private readonly List<PaymentTerm> _terms = new();
    public IReadOnlyList<PaymentTerm> Terms => _terms.AsReadOnly();

    protected PaymentTermsTemplate() { }

    public PaymentTermsTemplate(Guid id, string name, Guid? tenantId = null)
        : base(id)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), 200);
        TenantId = tenantId;
    }

    public void AddTerm(PaymentTerm term)
    {
        _terms.Add(term);
    }

    /// <summary>Validate that all term portions sum to exactly 100%.</summary>
    public void ValidatePortions()
    {
        var total = _terms.Sum(t => t.InvoicePortion);
        if (Math.Abs(total - 100m) > 0.01m)
            throw new BusinessException(MyERPDomainErrorCodes.PaymentTermsPortionMustBe100);
    }

    /// <summary>
    /// Generate a payment schedule from this template.
    /// Each term produces a due date and amount based on the document's posting date and grand total.
    /// </summary>
    public List<PaymentScheduleLine> GenerateSchedule(DateTime postingDate, decimal grandTotal)
    {
        var schedule = new List<PaymentScheduleLine>();
        foreach (var term in _terms.OrderBy(t => t.CreditDays))
        {
            var dueDate = postingDate.AddDays(term.CreditDays);
            // Due date can never be before posting date
            if (dueDate < postingDate) dueDate = postingDate;

            var amount = Math.Round(grandTotal * term.InvoicePortion / 100m, 2);
            schedule.Add(new PaymentScheduleLine
            {
                DueDate = dueDate,
                InvoicePortion = term.InvoicePortion,
                PaymentAmount = amount,
                Description = term.Description,
            });
        }
        return schedule;
    }
}

/// <summary>
/// Single payment term within a template (e.g., "50% due in 30 days").
/// </summary>
public class PaymentTerm : Entity<Guid>
{
    public Guid PaymentTermsTemplateId { get; set; }

    /// <summary>Percentage of invoice total due in this term (sum across all terms = 100%).</summary>
    public decimal InvoicePortion { get; set; }

    /// <summary>Number of days after posting date when payment is due.</summary>
    public int CreditDays { get; set; }

    /// <summary>Description (e.g., "Advance", "On Delivery", "Net 30").</summary>
    public string? Description { get; set; }

    /// <summary>Mode of payment for this specific term (optional).</summary>
    public Guid? ModeOfPaymentId { get; set; }

    protected PaymentTerm() { }

    public PaymentTerm(Guid id, Guid templateId, decimal invoicePortion, int creditDays, string? description = null)
        : base(id)
    {
        PaymentTermsTemplateId = templateId;
        InvoicePortion = invoicePortion;
        CreditDays = creditDays;
        Description = description;
    }
}

/// <summary>Generated payment schedule line (not persisted — computed on demand).</summary>
public class PaymentScheduleLine
{
    public DateTime DueDate { get; set; }
    public decimal InvoicePortion { get; set; }
    public decimal PaymentAmount { get; set; }
    public string? Description { get; set; }
}
