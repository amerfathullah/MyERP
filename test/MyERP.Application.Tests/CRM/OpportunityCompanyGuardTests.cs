using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.CRM.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.CRM;

public abstract class OpportunityCompanyGuardTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateOpportunity_CustomerFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var oppAppService = GetRequiredService<IOpportunityAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Opp Owner Co"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Opp Other Co"), autoSave: true);

            var otherCustomer = await customerRepo.InsertAsync(
                new Customer(Guid.NewGuid(), otherCompany.Id, "Cross Co Customer"), autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(() =>
                oppAppService.CreateAsync(new CreateOpportunityDto
                {
                    CompanyId = ownerCompany.Id,
                    Title = "Cross-company deal",
                    CustomerId = otherCustomer.Id,
                }));

            ex.Code.ShouldBe(MyERPDomainErrorCodes.CompanyMismatch);
        });
    }

    [Fact]
    public async Task CreateOpportunity_LeadFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var leadRepo = GetRequiredService<IRepository<Lead, Guid>>();
            var oppAppService = GetRequiredService<IOpportunityAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Opp Owner Co 2"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Opp Other Co 2"), autoSave: true);

            var otherLead = await leadRepo.InsertAsync(
                new Lead(Guid.NewGuid(), otherCompany.Id, "LEAD-CROSS-01", "Bob"), autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(() =>
                oppAppService.CreateAsync(new CreateOpportunityDto
                {
                    CompanyId = ownerCompany.Id,
                    Title = "Cross-company deal from lead",
                    LeadId = otherLead.Id,
                }));

            ex.Code.ShouldBe(MyERPDomainErrorCodes.CompanyMismatch);
        });
    }

    [Fact]
    public async Task CreateOpportunity_DisabledCustomer_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var oppAppService = GetRequiredService<IOpportunityAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Opp Disabled Cust Co"), autoSave: true);
            var disabledCust = new Customer(Guid.NewGuid(), company.Id, "Disabled Customer")
            {
                IsActive = false
            };
            await customerRepo.InsertAsync(disabledCust, autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(() =>
                oppAppService.CreateAsync(new CreateOpportunityDto
                {
                    CompanyId = company.Id,
                    Title = "Disabled customer deal",
                    CustomerId = disabledCust.Id,
                }));

            ex.Code.ShouldBe(MyERPDomainErrorCodes.PartyDisabled);
        });
    }

    [Fact]
    public async Task UpdateOpportunity_ConvertedStatus_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var oppRepo = GetRequiredService<IRepository<Opportunity, Guid>>();
            var oppAppService = GetRequiredService<IOpportunityAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Opp Immutability Co"), autoSave: true);
            var opp = new Opportunity(Guid.NewGuid(), company.Id, "OPP-IMM-001", "Immutable Deal");
            opp.Convert();
            await oppRepo.InsertAsync(opp, autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(() =>
                oppAppService.UpdateAsync(opp.Id, new UpdateOpportunityDto
                {
                    Title = "Attempted rename",
                }));

            ex.Code.ShouldBe(MyERPDomainErrorCodes.InvalidStatusTransition);
        });
    }

    [Fact]
    public async Task UpdateOpportunity_Success_UpdatesFieldsAndItems()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var oppRepo = GetRequiredService<IRepository<Opportunity, Guid>>();
            var oppAppService = GetRequiredService<IOpportunityAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Opp Update Co"), autoSave: true);
            var opp = new Opportunity(Guid.NewGuid(), company.Id, "OPP-UPD-001", "Initial Deal");
            await oppRepo.InsertAsync(opp, autoSave: true);

            var updated = await oppAppService.UpdateAsync(opp.Id, new UpdateOpportunityDto
            {
                Title = "Updated Deal Title",
                SalesStage = "Proposal",
                Probability = 60,
                Items = new List<CreateOpportunityItemDto>
                {
                    new() { Description = "Consulting Service", Quantity = 10, UnitPrice = 150m }
                }
            });

            updated.Title.ShouldBe("Updated Deal Title");
            updated.SalesStage.ShouldBe("Proposal");
            updated.Probability.ShouldBe(60);
            updated.OpportunityAmount.ShouldBe(1500m);
            updated.Items.Count.ShouldBe(1);
            updated.Items[0].Description.ShouldBe("Consulting Service");
        });
    }

    [Fact]
    public async Task ConvertOpportunityToQuotation_DisabledCustomer_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var oppRepo = GetRequiredService<IRepository<Opportunity, Guid>>();
            var conversionAppService = GetRequiredService<IDocumentConversionAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Opp Conv Owner Co"), autoSave: true);

            var customer = await customerRepo.InsertAsync(
                new Customer(Guid.NewGuid(), ownerCompany.Id, "Customer Conv"), autoSave: true);

            var opp = new Opportunity(Guid.NewGuid(), ownerCompany.Id, "OPP-CONV-001", "Convert Deal")
            {
                CustomerId = customer.Id
            };
            await oppRepo.InsertAsync(opp, autoSave: true);

            customer.IsActive = false;
            await customerRepo.UpdateAsync(customer, autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(() =>
                conversionAppService.ConvertOpportunityToQuotationAsync(opp.Id));

            ex.Code.ShouldBe(MyERPDomainErrorCodes.PartyDisabled);
        });
    }

    [Fact]
    public async Task CreateQuotation_OpportunityFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var oppRepo = GetRequiredService<IRepository<Opportunity, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var quotationAppService = GetRequiredService<IQuotationAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Opp Qtn Owner Co"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Opp Qtn Other Co"), autoSave: true);

            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), ownerCompany.Id, "Quotation Series", "Quotation", "QTN-"), autoSave: true);

            var ownerCustomer = await customerRepo.InsertAsync(
                new Customer(Guid.NewGuid(), ownerCompany.Id, "Owner Customer"), autoSave: true);

            var ownerItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), ownerCompany.Id, "QTN-ITEM-1", "Owner Item", ItemType.Goods), autoSave: true);

            var otherOpp = new Opportunity(Guid.NewGuid(), otherCompany.Id, "OPP-OTHER-001", "Other Co Deal");
            await oppRepo.InsertAsync(otherOpp, autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(() =>
                quotationAppService.CreateAsync(new CreateQuotationDto
                {
                    CompanyId = ownerCompany.Id,
                    CustomerId = ownerCustomer.Id,
                    OpportunityId = otherOpp.Id,
                    IssueDate = DateTime.Today,
                    Items = new List<CreateQuotationItemDto>
                    {
                        new() { ItemId = ownerItem.Id, Description = "Owner Item", Quantity = 1, UnitPrice = 100 }
                    }
                }));

            ex.Code.ShouldBe(MyERPDomainErrorCodes.CompanyMismatch);
        });
    }
}
