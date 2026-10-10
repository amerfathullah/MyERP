using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.CRM;
using MyERP.CRM.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Sales;

public abstract class CustomerLeadConversionSyncTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateAsync_WithLead_SyncsProspectStatusAndRelinksQuotations()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var leadRepo = GetRequiredService<IRepository<Lead, Guid>>();
            var prospectRepo = GetRequiredService<IRepository<Prospect, Guid>>();
            var oppRepo = GetRequiredService<IRepository<Opportunity, Guid>>();
            var quotRepo = GetRequiredService<IRepository<Quotation, Guid>>();
            var customerAppService = GetRequiredService<ICustomerAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Cust Lead Sync Co"), autoSave: true);

            // Create lead in DoNotContact status (per PR #59907 / commit eb445464ca: also converts)
            var lead = new Lead(Guid.NewGuid(), company.Id, "LEAD-SYNC-1", "Dave");
            lead.MarkDoNotContact();
            await leadRepo.InsertAsync(lead, autoSave: true);

            // Create prospect with lead
            var prospect = new Prospect(Guid.NewGuid(), company.Id, "Dave Prospect");
            prospect.AddLead(Guid.NewGuid(), lead.Id, "Dave", "dave@example.com", LeadStatus.DoNotContact);
            await prospectRepo.InsertAsync(prospect, autoSave: true);

            // Opportunity for Lead
            var opp = new Opportunity(Guid.NewGuid(), company.Id, "OPP-SYNC-1", "Dave Deal")
            {
                LeadId = lead.Id
            };
            await oppRepo.InsertAsync(opp, autoSave: true);

            // Quotation for Opportunity (initially has temporary/dummy customer)
            var dummyCust = Guid.NewGuid();
            var quot = new Quotation(Guid.NewGuid(), company.Id, dummyCust, "QTN-SYNC-1", DateTime.UtcNow)
            {
                OpportunityId = opp.Id
            };
            quot.AddItem(Guid.NewGuid(), "Service", 1m, 200m, 200m, "Unit");
            await quotRepo.InsertAsync(quot, autoSave: true);

            // Create customer from lead
            var customerDto = await customerAppService.CreateAsync(new CreateUpdateCustomerDto
            {
                CompanyId = company.Id,
                Name = "Dave Corp",
                LeadId = lead.Id,
                IsActive = true
            });

            // 1. Lead must be Converted
            var updatedLead = await leadRepo.GetAsync(lead.Id);
            updatedLead.Status.ShouldBe(LeadStatus.Converted);

            // 2. Prospect lead row status must be Converted (PR #59907 / commit eb445464ca)
            var updatedProspect = (await prospectRepo.WithDetailsAsync(p => p.Leads)).First(p => p.Id == prospect.Id);
            updatedProspect.Leads[0].Status.ShouldBe(LeadStatus.Converted);

            // 3. Opportunity must point to new customer
            var updatedOpp = await oppRepo.GetAsync(opp.Id);
            updatedOpp.CustomerId.ShouldBe(customerDto.Id);

            // 4. Quotation must point to new customer (PR #60279 / commit 9f9cf26639)
            var updatedQuot = await quotRepo.GetAsync(quot.Id);
            updatedQuot.CustomerId.ShouldBe(customerDto.Id);
        });
    }
}
