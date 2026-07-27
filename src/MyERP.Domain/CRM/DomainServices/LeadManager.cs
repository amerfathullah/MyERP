using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.CRM.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.CRM.DomainServices;

/// <summary>
/// Lead Manager — handles auto-status transitions, email uniqueness, and contact auto-creation.
/// 
/// Per ERPNext lead.py:
/// - Status auto-detection: first email → Open (if New)
/// - Email uniqueness: configurable via CRM Settings (duplicate leads blocked)
/// - Contact auto-creation: on qualify/convert, creates Contact entity from Lead fields
/// - Lead→Customer inference: company_name set → Company type, else Individual
/// 
/// Per gotcha #2656: Lead Management - status auto-detection, email uniqueness, contact auto-creation
/// </summary>
public class LeadManager : DomainService
{
    private readonly IRepository<Lead, Guid> _leadRepository;

    public LeadManager(IRepository<Lead, Guid> leadRepository)
    {
        _leadRepository = leadRepository;
    }

    /// <summary>
    /// Validates email uniqueness for the lead.
    /// Per ERPNext: prevents duplicate leads with the same email address.
    /// </summary>
    public async Task ValidateEmailUniquenessAsync(string? email, Guid? excludeLeadId = null)
    {
        if (string.IsNullOrWhiteSpace(email)) return;

        var query = await _leadRepository.GetQueryableAsync();
        var duplicate = query.Any(l =>
            l.Email == email
            && l.Status != LeadStatus.Converted
            && l.Status != LeadStatus.DoNotContact
            && (!excludeLeadId.HasValue || l.Id != excludeLeadId.Value));

        if (duplicate)
        {
            throw new BusinessException("MyERP:03022")
                .WithData("email", email);
        }
    }

    /// <summary>
    /// Auto-advances status from New to Open on first interaction (email/reply).
    /// Per ERPNext: set_status checks for any Communication → auto-transitions to Open.
    /// Only fires when current status is New.
    /// </summary>
    public static void AutoAdvanceOnInteraction(Lead lead)
    {
        if (lead.Status == LeadStatus.New)
        {
            lead.MarkOpen();
        }
    }

    /// <summary>
    /// Determines the customer type based on Lead fields.
    /// Per ERPNext: company_name → "Company" type, else "Individual".
    /// </summary>
    public static string InferCustomerType(Lead lead) =>
        !string.IsNullOrWhiteSpace(lead.CompanyName) ? "Company" : "Individual";

    /// <summary>
    /// Builds a Contact creation data payload from Lead fields.
    /// Per ERPNext: on qualify/convert, creates Contact with first/last name, email, phone.
    /// Returns null if Lead has no contact information.
    /// </summary>
    public static ContactFromLead? BuildContactFromLead(Lead lead)
    {
        if (string.IsNullOrWhiteSpace(lead.Email) && string.IsNullOrWhiteSpace(lead.Phone)
            && string.IsNullOrWhiteSpace(lead.MobileNo))
            return null;

        return new ContactFromLead
        {
            FirstName = lead.FirstName,
            LastName = lead.LastName,
            Email = lead.Email,
            Phone = lead.Phone,
            MobileNo = lead.MobileNo,
            CompanyName = lead.CompanyName,
        };
    }
}

/// <summary>
/// Contact creation data derived from a Lead.
/// </summary>
public record ContactFromLead
{
    public string FirstName { get; init; } = null!;
    public string? LastName { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? MobileNo { get; init; }
    public string? CompanyName { get; init; }
}
