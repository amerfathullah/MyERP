using System;
using System.Threading.Tasks;
using MyERP.CRM.Entities;
using MyERP.Sales.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;

namespace MyERP.CRM.DomainServices;

/// <summary>
/// Domain service for Lead conversion logic.
/// Converts a qualified Lead into an Opportunity or a Customer.
/// </summary>
public class LeadConversionManager : DomainService
{
    private readonly IRepository<Lead, Guid> _leadRepository;
    private readonly IRepository<Opportunity, Guid> _opportunityRepository;
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IGuidGenerator _guidGenerator;

    public LeadConversionManager(
        IRepository<Lead, Guid> leadRepository,
        IRepository<Opportunity, Guid> opportunityRepository,
        IRepository<Customer, Guid> customerRepository,
        IGuidGenerator guidGenerator)
    {
        _leadRepository = leadRepository;
        _opportunityRepository = opportunityRepository;
        _customerRepository = customerRepository;
        _guidGenerator = guidGenerator;
    }

    /// <summary>
    /// Converts a Lead to an Opportunity.
    /// </summary>
    public async Task<Opportunity> ConvertToOpportunityAsync(Guid leadId, string opportunityTitle)
    {
        var lead = await _leadRepository.GetAsync(leadId);
        
        // Use GuidGenerator for proper sequential GUID generation
        var oppId = _guidGenerator.Create();
        var oppNum = "OPP-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");

        var opportunity = new Opportunity(
            id: oppId,
            companyId: lead.CompanyId,
            opportunityNumber: oppNum,
            title: opportunityTitle,
            tenantId: lead.TenantId
        )
        {
            LeadId = lead.Id,
            ContactName = lead.GetFullName(),
            ContactEmail = lead.Email,
            ContactPhone = lead.Phone,
            AssignedUserId = lead.AssignedUserId
        };

        lead.ConvertToOpportunity(oppId);

        await _opportunityRepository.InsertAsync(opportunity);
        await _leadRepository.UpdateAsync(lead);

        return opportunity;
    }

    /// <summary>
    /// Converts a Lead to a Customer.
    /// </summary>
    public async Task<Customer> ConvertToCustomerAsync(Guid leadId, string customerGroupId)
    {
        var lead = await _leadRepository.GetAsync(leadId);

        var custId = _guidGenerator.Create();
        var custName = lead.CompanyName ?? lead.GetFullName();
        var custNum = "CUST-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");

        var customer = new Customer(
            id: custId,
            companyId: lead.CompanyId,
            name: custName,
            tenantId: lead.TenantId
        );
        customer.CustomerCode = custNum;
        customer.CustomerGroupId = Guid.TryParse(customerGroupId, out var cGroupId) ? cGroupId : null;

        // Mark lead as converted to customer
        lead.ConvertToCustomer(custId);

        await _customerRepository.InsertAsync(customer);
        await _leadRepository.UpdateAsync(lead);

        return customer;
    }
}
