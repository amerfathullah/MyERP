using System;
using System.Threading.Tasks;
using MyERP.CRM;
using MyERP.CRM.DomainServices;
using MyERP.CRM.Entities;
using MyERP.Sales.Entities;
using NSubstitute;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Xunit;

namespace MyERP.Domain.Tests.CRM;

/// <summary>
/// LeadConversionManager.ConvertToCustomerAsync previously dropped the Lead's Email/Phone —
/// Customer has both fields but they were never copied from the source Lead.
/// </summary>
public class LeadConversionManagerTests
{
    private readonly IRepository<Lead, Guid> _leadRepository = Substitute.For<IRepository<Lead, Guid>>();
    private readonly IRepository<Opportunity, Guid> _opportunityRepository = Substitute.For<IRepository<Opportunity, Guid>>();
    private readonly IRepository<Customer, Guid> _customerRepository = Substitute.For<IRepository<Customer, Guid>>();
    private readonly IGuidGenerator _guidGenerator = Substitute.For<IGuidGenerator>();
    private readonly LeadConversionManager _manager;

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _leadId = Guid.NewGuid();

    public LeadConversionManagerTests()
    {
        _guidGenerator.Create().Returns(_ => Guid.NewGuid());
        _manager = new LeadConversionManager(_leadRepository, _opportunityRepository, _customerRepository, _guidGenerator);
    }

    [Fact]
    public async Task ConvertToCustomerAsync_CarriesEmailAndPhoneFromLead()
    {
        var lead = new Lead(_leadId, _companyId, "LEAD-0001", "Jane")
        {
            LastName = "Doe",
            Email = "jane.doe@example.com",
            Phone = "+60123456789",
            CompanyName = "Acme Sdn Bhd",
        };
        _leadRepository.GetAsync(_leadId).Returns(lead);

        Customer? createdCustomer = null;
        await _customerRepository.InsertAsync(Arg.Do<Customer>(c => createdCustomer = c));

        var result = await _manager.ConvertToCustomerAsync(_leadId, Guid.NewGuid().ToString());

        Assert.NotNull(createdCustomer);
        Assert.Equal("jane.doe@example.com", createdCustomer!.Email);
        Assert.Equal("+60123456789", createdCustomer.Phone);
        Assert.Equal("Acme Sdn Bhd", createdCustomer.Name);
        Assert.Same(result, createdCustomer);
    }

    [Fact]
    public async Task ConvertToCustomerAsync_MarksLeadAsConverted()
    {
        var lead = new Lead(_leadId, _companyId, "LEAD-0002", "John") { LastName = "Smith" };
        _leadRepository.GetAsync(_leadId).Returns(lead);

        await _manager.ConvertToCustomerAsync(_leadId, Guid.NewGuid().ToString());

        Assert.Equal(LeadStatus.Converted, lead.Status);
        Assert.NotNull(lead.ConvertedCustomerId);
    }
}
