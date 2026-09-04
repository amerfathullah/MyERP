using System;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Sales;

public abstract class InternalPartyValidationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly ICustomerAppService _customerAppService;
    private readonly ISupplierAppService _supplierAppService;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IRepository<Supplier, Guid> _supplierRepository;

    protected InternalPartyValidationTests()
    {
        _customerAppService = GetRequiredService<ICustomerAppService>();
        _supplierAppService = GetRequiredService<ISupplierAppService>();
        _companyRepository = GetRequiredService<IRepository<Company, Guid>>();
        _customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
        _supplierRepository = GetRequiredService<IRepository<Supplier, Guid>>();
    }

    [Fact]
    public async Task Customer_RepresentsOwnCompany_ThrowsPartyCannotRepresentOwnCompany()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var company = await _companyRepository.InsertAsync(
                new Company(Guid.NewGuid(), "Company A - Cust Own"), autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(async () =>
            {
                await _customerAppService.CreateAsync(new CreateUpdateCustomerDto
                {
                    CompanyId = company.Id,
                    Name = "Internal Customer Own Co",
                    RepresentsCompanyId = company.Id,
                    IsActive = true,
                });
            });

            ex.Code.ShouldBe(MyERPDomainErrorCodes.PartyCannotRepresentOwnCompany);
        });
    }

    [Fact]
    public async Task Customer_DuplicateActiveInternalCustomer_ThrowsInternalPartyAlreadyExists()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyA = await _companyRepository.InsertAsync(
                new Company(Guid.NewGuid(), "Company A - Cust Dup"), autoSave: true);
            var companyB = await _companyRepository.InsertAsync(
                new Company(Guid.NewGuid(), "Company B - Cust Dup"), autoSave: true);

            // First internal customer representing Company B
            await _customerRepository.InsertAsync(
                new Customer(Guid.NewGuid(), companyA.Id, "Active Internal Cust 1")
                {
                    RepresentsCompanyId = companyB.Id,
                    IsActive = true,
                }, autoSave: true);

            // Attempt to create second active internal customer representing Company B
            var ex = await Should.ThrowAsync<BusinessException>(async () =>
            {
                await _customerAppService.CreateAsync(new CreateUpdateCustomerDto
                {
                    CompanyId = companyA.Id,
                    Name = "Active Internal Cust 2",
                    RepresentsCompanyId = companyB.Id,
                    IsActive = true,
                });
            });

            ex.Code.ShouldBe(MyERPDomainErrorCodes.InternalPartyAlreadyExists);
            ex.Data["partyType"].ShouldBe("Customer");
            ex.Data["partyName"].ShouldBe("Active Internal Cust 1");
            ex.Data["companyName"].ShouldBe(companyB.Name);
        });
    }

    [Fact]
    public async Task Customer_WhenExistingInternalCustomerIsDisabled_Succeeds()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyA = await _companyRepository.InsertAsync(
                new Company(Guid.NewGuid(), "Company A - Inactive Cust"), autoSave: true);
            var companyB = await _companyRepository.InsertAsync(
                new Company(Guid.NewGuid(), "Company B - Inactive Cust"), autoSave: true);

            // Disabled internal customer
            await _customerRepository.InsertAsync(
                new Customer(Guid.NewGuid(), companyA.Id, "Disabled Internal Cust")
                {
                    RepresentsCompanyId = companyB.Id,
                    IsActive = false,
                }, autoSave: true);

            // New active internal customer should succeed
            var created = await _customerAppService.CreateAsync(new CreateUpdateCustomerDto
            {
                CompanyId = companyA.Id,
                Name = "New Active Internal Cust",
                RepresentsCompanyId = companyB.Id,
                IsActive = true,
            });

            created.ShouldNotBeNull();
            created.RepresentsCompanyId.ShouldBe(companyB.Id);
        });
    }

    [Fact]
    public async Task Customer_UpdateSelf_PreservingRepresentsCompanyId_Succeeds()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyA = await _companyRepository.InsertAsync(
                new Company(Guid.NewGuid(), "Company A - Cust Self"), autoSave: true);
            var companyB = await _companyRepository.InsertAsync(
                new Company(Guid.NewGuid(), "Company B - Cust Self"), autoSave: true);

            var cust = await _customerRepository.InsertAsync(
                new Customer(Guid.NewGuid(), companyA.Id, "Self Internal Cust")
                {
                    RepresentsCompanyId = companyB.Id,
                    IsActive = true,
                }, autoSave: true);

            var updated = await _customerAppService.UpdateAsync(cust.Id, new CreateUpdateCustomerDto
            {
                CompanyId = companyA.Id,
                Name = "Self Internal Cust Renamed",
                RepresentsCompanyId = companyB.Id,
                IsActive = true,
            });

            updated.Name.ShouldBe("Self Internal Cust Renamed");
            updated.RepresentsCompanyId.ShouldBe(companyB.Id);
        });
    }

    [Fact]
    public async Task Supplier_RepresentsOwnCompany_ThrowsPartyCannotRepresentOwnCompany()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var company = await _companyRepository.InsertAsync(
                new Company(Guid.NewGuid(), "Company A - Supp Own"), autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(async () =>
            {
                await _supplierAppService.CreateAsync(new CreateUpdateSupplierDto
                {
                    CompanyId = company.Id,
                    Name = "Internal Supplier Own Co",
                    RepresentsCompanyId = company.Id,
                    IsActive = true,
                });
            });

            ex.Code.ShouldBe(MyERPDomainErrorCodes.PartyCannotRepresentOwnCompany);
        });
    }

    [Fact]
    public async Task Supplier_DuplicateActiveInternalSupplier_ThrowsInternalPartyAlreadyExists()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyA = await _companyRepository.InsertAsync(
                new Company(Guid.NewGuid(), "Company A - Supp Dup"), autoSave: true);
            var companyB = await _companyRepository.InsertAsync(
                new Company(Guid.NewGuid(), "Company B - Supp Dup"), autoSave: true);

            // First internal supplier representing Company B
            await _supplierRepository.InsertAsync(
                new Supplier(Guid.NewGuid(), companyA.Id, "Active Internal Supp 1")
                {
                    RepresentsCompanyId = companyB.Id,
                    IsActive = true,
                }, autoSave: true);

            // Attempt to create second active internal supplier representing Company B
            var ex = await Should.ThrowAsync<BusinessException>(async () =>
            {
                await _supplierAppService.CreateAsync(new CreateUpdateSupplierDto
                {
                    CompanyId = companyA.Id,
                    Name = "Active Internal Supp 2",
                    RepresentsCompanyId = companyB.Id,
                    IsActive = true,
                });
            });

            ex.Code.ShouldBe(MyERPDomainErrorCodes.InternalPartyAlreadyExists);
            ex.Data["partyType"].ShouldBe("Supplier");
            ex.Data["partyName"].ShouldBe("Active Internal Supp 1");
            ex.Data["companyName"].ShouldBe(companyB.Name);
        });
    }

    [Fact]
    public async Task Supplier_WhenExistingInternalSupplierIsDisabled_Succeeds()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyA = await _companyRepository.InsertAsync(
                new Company(Guid.NewGuid(), "Company A - Inactive Supp"), autoSave: true);
            var companyB = await _companyRepository.InsertAsync(
                new Company(Guid.NewGuid(), "Company B - Inactive Supp"), autoSave: true);

            // Disabled internal supplier
            await _supplierRepository.InsertAsync(
                new Supplier(Guid.NewGuid(), companyA.Id, "Disabled Internal Supp")
                {
                    RepresentsCompanyId = companyB.Id,
                    IsActive = false,
                }, autoSave: true);

            // New active internal supplier should succeed
            var created = await _supplierAppService.CreateAsync(new CreateUpdateSupplierDto
            {
                CompanyId = companyA.Id,
                Name = "New Active Internal Supp",
                RepresentsCompanyId = companyB.Id,
                IsActive = true,
            });

            created.ShouldNotBeNull();
            created.RepresentsCompanyId.ShouldBe(companyB.Id);
        });
    }

    [Fact]
    public async Task Supplier_UpdateSelf_PreservingRepresentsCompanyId_Succeeds()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyA = await _companyRepository.InsertAsync(
                new Company(Guid.NewGuid(), "Company A - Supp Self"), autoSave: true);
            var companyB = await _companyRepository.InsertAsync(
                new Company(Guid.NewGuid(), "Company B - Supp Self"), autoSave: true);

            var supp = await _supplierRepository.InsertAsync(
                new Supplier(Guid.NewGuid(), companyA.Id, "Self Internal Supp")
                {
                    RepresentsCompanyId = companyB.Id,
                    IsActive = true,
                }, autoSave: true);

            var updated = await _supplierAppService.UpdateAsync(supp.Id, new CreateUpdateSupplierDto
            {
                CompanyId = companyA.Id,
                Name = "Self Internal Supp Renamed",
                RepresentsCompanyId = companyB.Id,
                IsActive = true,
            });

            updated.Name.ShouldBe("Self Internal Supp Renamed");
            updated.RepresentsCompanyId.ShouldBe(companyB.Id);
        });
    }
}
