using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.CRM.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.CRM;

public class AddressAndContactsAppServiceTests
{
    private readonly IRepository<Address, Guid> _addressRepo = Substitute.For<IRepository<Address, Guid>>();
    private readonly IRepository<Contact, Guid> _contactRepo = Substitute.For<IRepository<Contact, Guid>>();
    private readonly IRepository<Customer, Guid> _customerRepo = Substitute.For<IRepository<Customer, Guid>>();
    private readonly IRepository<Supplier, Guid> _supplierRepo = Substitute.For<IRepository<Supplier, Guid>>();
    private readonly IRepository<SalesPartner, Guid> _salesPartnerRepo = Substitute.For<IRepository<SalesPartner, Guid>>();
    private readonly IRepository<Lead, Guid> _leadRepo = Substitute.For<IRepository<Lead, Guid>>();

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _supplierId = Guid.NewGuid();
    private readonly Guid _partnerId = Guid.NewGuid();

    public AddressAndContactsAppServiceTests()
    {
        var customer = new Customer(_customerId, _companyId, "Acme Customer")
        {
            CustomerCode = "CUST-001",
        };
        _customerRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Customer> { customer }.AsQueryable()));

        var supplier = new Supplier(_supplierId, _companyId, "Global Supplies")
        {
            SupplierCode = "SUP-001",
        };
        _supplierRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Supplier> { supplier }.AsQueryable()));

        var partner = new SalesPartner(_partnerId, "Nexus Channel", PartnerType.Reseller, 10m);
        _salesPartnerRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SalesPartner> { partner }.AsQueryable()));

        _leadRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Lead>().AsQueryable()));
    }

    [Fact]
    public async Task GetReportAsync_Customer_ReturnsAddressesAndContactsZipped()
    {
        var addr1 = new Address(Guid.NewGuid(), "Billing HQ", "Customer", _customerId, "123 Jalan Ampang", "Malaysia")
        {
            City = "Kuala Lumpur",
            State = "Wilayah Persekutuan",
            PostalCode = "50450",
            IsPrimaryAddress = true,
        };
        var addr2 = new Address(Guid.NewGuid(), "Warehouse Branch", "Customer", _customerId, "456 Jalan Ipoh", "Malaysia")
        {
            City = "Kuala Lumpur",
            State = "Wilayah Persekutuan",
            PostalCode = "51200",
            IsPrimaryAddress = false,
        };

        var contact = new Contact(Guid.NewGuid(), "John", "Customer", _customerId)
        {
            LastName = "Doe",
            Email = "john.doe@acme.com",
            Phone = "+60123456789",
            IsPrimaryContact = true,
            Designation = "Procurement Manager",
        };

        _addressRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Address> { addr1, addr2 }.AsQueryable()));
        _contactRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Contact> { contact }.AsQueryable()));

        var appService = new AddressAndContactsAppService(
            _addressRepo, _contactRepo, _customerRepo, _supplierRepo, _salesPartnerRepo, _leadRepo);

        var result = await appService.GetReportAsync(new GetAddressAndContactsInput
        {
            CompanyId = _companyId,
            PartyType = "Customer",
        });

        result.ShouldNotBeNull();
        result.TotalParties.ShouldBe(1);
        result.TotalAddresses.ShouldBe(2);
        result.TotalContacts.ShouldBe(1);
        result.Rows.Count.ShouldBe(2);

        var row1 = result.Rows[0];
        row1.PartyName.ShouldBe("Acme Customer");
        row1.PartyCode.ShouldBe("CUST-001");
        row1.AddressLine1.ShouldBe("123 Jalan Ampang");
        row1.IsPrimaryAddress.ShouldBeTrue();
        row1.ContactName.ShouldBe("John Doe");
        row1.Email.ShouldBe("john.doe@acme.com");
        row1.IsPrimaryContact.ShouldBeTrue();

        var row2 = result.Rows[1];
        row2.AddressLine1.ShouldBe("456 Jalan Ipoh");
        row2.IsPrimaryAddress.ShouldBeFalse();
        row2.ContactName.ShouldBeNull(); // Zipped with second address (no second contact)
    }

    [Fact]
    public async Task GetReportAsync_Supplier_NoAddressOrContact_ReturnsPartyRowWithNulls()
    {
        _addressRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Address>().AsQueryable()));
        _contactRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Contact>().AsQueryable()));

        var appService = new AddressAndContactsAppService(
            _addressRepo, _contactRepo, _customerRepo, _supplierRepo, _salesPartnerRepo, _leadRepo);

        var result = await appService.GetReportAsync(new GetAddressAndContactsInput
        {
            CompanyId = _companyId,
            PartyType = "Supplier",
        });

        result.ShouldNotBeNull();
        result.TotalParties.ShouldBe(1);
        result.TotalAddresses.ShouldBe(0);
        result.TotalContacts.ShouldBe(0);
        result.Rows.Count.ShouldBe(1);

        var row = result.Rows[0];
        row.PartyName.ShouldBe("Global Supplies");
        row.PartyCode.ShouldBe("SUP-001");
        row.AddressId.ShouldBeNull();
        row.ContactId.ShouldBeNull();
    }
}
