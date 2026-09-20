using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.CRM;
using MyERP.CRM.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Projects.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Sales;

public abstract class SalesOrderAppServiceInvariantsTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateAsync_ProjectFromDifferentCustomer_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var projectRepo = GetRequiredService<IRepository<Project, Guid>>();
            var soAppService = GetRequiredService<ISalesOrderAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SO Invariant Co 1"), autoSave: true);
            var customer1 = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "Cust 1"), autoSave: true);
            var customer2 = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "Cust 2"), autoSave: true);

            var projectOfCust2 = await projectRepo.InsertAsync(new Project(Guid.NewGuid(), company.Id, "PROJ-001", "Proj Cust 2")
            {
                CustomerId = customer2.Id
            }, autoSave: true);

            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "SO-INV-ITEM-1", "SO Inv Item 1", ItemType.Goods), autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(() =>
                soAppService.CreateAsync(new CreateSalesOrderDto
                {
                    CompanyId = company.Id,
                    CustomerId = customer1.Id,
                    OrderDate = DateTime.UtcNow.Date,
                    ProjectId = projectOfCust2.Id,
                    Items = new List<CreateSalesOrderItemDto>
                    {
                        new() { ItemId = item.Id, Description = "SO Inv Item 1", Quantity = 1, UnitPrice = 100 }
                    }
                }));

            ex.Code.ShouldBe(MyERPDomainErrorCodes.ProjectCustomerMismatch);
        });
    }

    [Fact]
    public async Task CreateAsync_SalesTeam_CommissionRateExceeds100_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var salesPersonRepo = GetRequiredService<IRepository<SalesPerson, Guid>>();
            var soAppService = GetRequiredService<ISalesOrderAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SO Invariant Co 2"), autoSave: true);
            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "Cust for SalesTeam"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "SO-INV-ITEM-2", "SO Inv Item 2", ItemType.Goods), autoSave: true);

            var salesPerson = await salesPersonRepo.InsertAsync(new SalesPerson(Guid.NewGuid(), "Alice Agent")
            {
                IsGroup = false,
                IsEnabled = true
            }, autoSave: true);

            await Should.ThrowAsync<Volo.Abp.Validation.AbpValidationException>(() =>
                soAppService.CreateAsync(new CreateSalesOrderDto
                {
                    CompanyId = company.Id,
                    CustomerId = customer.Id,
                    OrderDate = DateTime.UtcNow.Date,
                    Items = new List<CreateSalesOrderItemDto>
                    {
                        new() { ItemId = item.Id, Description = "SO Inv Item 2", Quantity = 1, UnitPrice = 100 }
                    },
                    SalesTeam = new List<SalesTeamAllocationInputDto>
                    {
                        new()
                        {
                            SalesPersonId = salesPerson.Id,
                            AllocatedPercentage = 100m,
                            CommissionRate = 120m // > 100%
                        }
                    }
                }));
        });
    }

    [Fact]
    public async Task Customer_Delete_Reverts_Lead_Status_To_Interested()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var leadRepo = GetRequiredService<IRepository<Lead, Guid>>();
            var customerAppService = GetRequiredService<ICustomerAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SO Invariant Co 3"), autoSave: true);

            var lead = new Lead(Guid.NewGuid(), company.Id, "LEAD-INV-001", "Bob");
            var customerId = Guid.NewGuid();
            lead.ConvertToCustomer(customerId);
            await leadRepo.InsertAsync(lead, autoSave: true);

            var customer = new Customer(customerId, company.Id, "Bob's Company")
            {
                LeadId = lead.Id
            };
            await customerRepo.InsertAsync(customer, autoSave: true);

            // Delete customer via app service
            await customerAppService.DeleteAsync(customer.Id);

            // Lead should now be reverted to Interested
            var updatedLead = await leadRepo.GetAsync(lead.Id);
            updatedLead.Status.ShouldBe(LeadStatus.Interested);
            updatedLead.ConvertedCustomerId.ShouldBeNull();
        });
    }
}
