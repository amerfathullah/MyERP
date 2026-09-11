using System;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.CRM.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.CRM;

public abstract class LeadConversionGuardTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task ConvertToCustomerAsync_FromNewLead_CreatesCustomerAndMarksConverted()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var leadRepo = GetRequiredService<IRepository<Lead, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var leadAppService = GetRequiredService<ILeadAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Lead Conv Co 1"), autoSave: true);
            var lead = new Lead(Guid.NewGuid(), company.Id, "LEAD-CONV-01", "Charlie")
            {
                LastName = "Brown",
                CompanyName = "Peanuts Corp",
                Email = "charlie@peanuts.com"
            };
            await leadRepo.InsertAsync(lead, autoSave: true);

            var customerId = await leadAppService.ConvertToCustomerAsync(new ConvertLeadToCustomerDto
            {
                LeadId = lead.Id,
            });

            var customer = await customerRepo.GetAsync(customerId);
            customer.Name.ShouldBe("Peanuts Corp");
            customer.CompanyId.ShouldBe(company.Id);

            var updatedLead = await leadRepo.GetAsync(lead.Id);
            updatedLead.Status.ShouldBe(LeadStatus.Converted);
            updatedLead.ConvertedCustomerId.ShouldBe(customerId);
        });
    }

    [Fact]
    public async Task ConvertToCustomerAsync_AlreadyConvertedLead_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var leadRepo = GetRequiredService<IRepository<Lead, Guid>>();
            var leadAppService = GetRequiredService<ILeadAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Lead Conv Co 2"), autoSave: true);
            var lead = new Lead(Guid.NewGuid(), company.Id, "LEAD-CONV-02", "David");
            lead.ConvertToCustomer(Guid.NewGuid());
            await leadRepo.InsertAsync(lead, autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(() =>
                leadAppService.ConvertToCustomerAsync(new ConvertLeadToCustomerDto
                {
                    LeadId = lead.Id
                }));

            ex.Code.ShouldBe(MyERPDomainErrorCodes.InvalidStatusTransition);
        });
    }

    [Fact]
    public async Task ConvertToCustomerAsync_LostLead_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var leadRepo = GetRequiredService<IRepository<Lead, Guid>>();
            var leadAppService = GetRequiredService<ILeadAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Lead Conv Co 3"), autoSave: true);
            var lead = new Lead(Guid.NewGuid(), company.Id, "LEAD-CONV-03", "Eve");
            lead.MarkOpen();
            lead.MarkLost();
            await leadRepo.InsertAsync(lead, autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(() =>
                leadAppService.ConvertToCustomerAsync(new ConvertLeadToCustomerDto
                {
                    LeadId = lead.Id
                }));

            ex.Code.ShouldBe(MyERPDomainErrorCodes.InvalidStatusTransition);
        });
    }

    [Fact]
    public async Task ConvertToCustomerAsync_GroupCustomerGroup_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var leadRepo = GetRequiredService<IRepository<Lead, Guid>>();
            var groupRepo = GetRequiredService<IRepository<CustomerGroup, Guid>>();
            var leadAppService = GetRequiredService<ILeadAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Lead Conv Co 4"), autoSave: true);
            var lead = new Lead(Guid.NewGuid(), company.Id, "LEAD-CONV-04", "Frank");
            await leadRepo.InsertAsync(lead, autoSave: true);

            var groupNode = new CustomerGroup(Guid.NewGuid(), "All Groups Node", null, isGroup: true);
            await groupRepo.InsertAsync(groupNode, autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(() =>
                leadAppService.ConvertToCustomerAsync(new ConvertLeadToCustomerDto
                {
                    LeadId = lead.Id,
                    CustomerGroupId = groupNode.Id
                }));

            ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        });
    }

    [Fact]
    public async Task ConvertToOpportunityAsync_NewLead_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var leadRepo = GetRequiredService<IRepository<Lead, Guid>>();
            var leadAppService = GetRequiredService<ILeadAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Lead Conv Co 5"), autoSave: true);
            var lead = new Lead(Guid.NewGuid(), company.Id, "LEAD-CONV-05", "Grace");
            // Status is New (not Open, Interested, Qualified, Replied)
            await leadRepo.InsertAsync(lead, autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(() =>
                leadAppService.ConvertToOpportunityAsync(new ConvertLeadToOpportunityDto
                {
                    LeadId = lead.Id,
                    Title = "Grace Opportunity"
                }));

            ex.Code.ShouldBe(MyERPDomainErrorCodes.InvalidStatusTransition);
        });
    }

    [Fact]
    public async Task ConvertToOpportunityAsync_OpenLead_CreatesOpportunityAndMarksConverted()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var leadRepo = GetRequiredService<IRepository<Lead, Guid>>();
            var oppRepo = GetRequiredService<IRepository<Opportunity, Guid>>();
            var leadAppService = GetRequiredService<ILeadAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Lead Conv Co 6"), autoSave: true);
            var lead = new Lead(Guid.NewGuid(), company.Id, "LEAD-CONV-06", "Hank");
            lead.MarkOpen();
            await leadRepo.InsertAsync(lead, autoSave: true);

            var oppDto = await leadAppService.ConvertToOpportunityAsync(new ConvertLeadToOpportunityDto
            {
                LeadId = lead.Id,
                Title = "Hank Opportunity Deal",
                OpportunityAmount = 2500m,
            });

            oppDto.CompanyId.ShouldBe(company.Id);
            oppDto.LeadId.ShouldBe(lead.Id);
            oppDto.OpportunityAmount.ShouldBe(2500m);

            var opp = await oppRepo.GetAsync(oppDto.Id);
            opp.Title.ShouldBe("Hank Opportunity Deal");

            var updatedLead = await leadRepo.GetAsync(lead.Id);
            updatedLead.Status.ShouldBe(LeadStatus.Converted);
            updatedLead.ConvertedOpportunityId.ShouldBe(opp.Id);
        });
    }

    [Fact]
    public async Task UpdateLead_AlreadyConverted_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var leadRepo = GetRequiredService<IRepository<Lead, Guid>>();
            var leadAppService = GetRequiredService<ILeadAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Lead Conv Co 7"), autoSave: true);
            var lead = new Lead(Guid.NewGuid(), company.Id, "LEAD-CONV-07", "Ivy");
            lead.ConvertToCustomer(Guid.NewGuid());
            await leadRepo.InsertAsync(lead, autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(() =>
                leadAppService.UpdateAsync(lead.Id, new UpdateLeadDto
                {
                    FirstName = "Ivy Renamed"
                }));

            ex.Code.ShouldBe(MyERPDomainErrorCodes.InvalidStatusTransition);
        });
    }
}
