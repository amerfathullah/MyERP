using System;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.CRM.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.CRM;

public abstract class LeadAppServiceTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateProspectAndContactAsync_CreatesBothContactAndProspect()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var leadRepo = GetRequiredService<IRepository<Lead, Guid>>();
            var contactRepo = GetRequiredService<IRepository<Contact, Guid>>();
            var prospectRepo = GetRequiredService<IRepository<Prospect, Guid>>();
            var leadAppService = GetRequiredService<ILeadAppService>();

            var company = await GetRequiredService<IRepository<Company, Guid>>()
                .InsertAsync(new Company(Guid.NewGuid(), "Lead Test Co 1"), autoSave: true);

            var lead = new Lead(Guid.NewGuid(), company.Id, "LEAD-2026-001", "Alice", null)
            {
                LastName = "Smith",
                CompanyName = "Acme Global",
                Email = "alice@acme.com",
                Phone = "12345678",
                JobTitle = "CTO",
                Industry = "Technology"
            };
            await leadRepo.InsertAsync(lead, autoSave: true);

            var input = new CreateProspectAndContactDto
            {
                CreateContact = true,
                CreateProspect = true,
                ProspectName = "Acme Global Ltd"
            };

            var result = await leadAppService.CreateProspectAndContactAsync(lead.Id, input);

            result.ContactId.ShouldNotBeNull();
            result.ProspectId.ShouldNotBeNull();

            var contact = await contactRepo.GetAsync(result.ContactId.Value);
            contact.FirstName.ShouldBe("Alice");
            contact.LastName.ShouldBe("Smith");
            contact.Email.ShouldBe("alice@acme.com");
            contact.PartyType.ShouldBe("Lead");
            contact.PartyId.ShouldBe(lead.Id);
            contact.Designation.ShouldBe("CTO");

            var prospect = await prospectRepo.GetAsync(result.ProspectId.Value);
            prospect.ProspectName.ShouldBe("Acme Global Ltd");
            prospect.CompanyName.ShouldBe("Acme Global");
            prospect.Industry.ShouldBe("Technology");
            prospect.Leads.ShouldContain(l => l.LeadId == lead.Id);
        });
    }

    [Fact]
    public async Task CreateProspectAndContactAsync_OnlyContact_WhenProspectFalse()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var leadRepo = GetRequiredService<IRepository<Lead, Guid>>();
            var contactRepo = GetRequiredService<IRepository<Contact, Guid>>();
            var leadAppService = GetRequiredService<ILeadAppService>();

            var company = await GetRequiredService<IRepository<Company, Guid>>()
                .InsertAsync(new Company(Guid.NewGuid(), "Lead Test Co 2"), autoSave: true);

            var lead = new Lead(Guid.NewGuid(), company.Id, "LEAD-2026-002", "Bob", null)
            {
                LastName = "Jones",
                Email = "bob@example.com"
            };
            await leadRepo.InsertAsync(lead, autoSave: true);

            var input = new CreateProspectAndContactDto
            {
                CreateContact = true,
                CreateProspect = false
            };

            var result = await leadAppService.CreateProspectAndContactAsync(lead.Id, input);

            result.ContactId.ShouldNotBeNull();
            result.ProspectId.ShouldBeNull();

            var contact = await contactRepo.GetAsync(result.ContactId.Value);
            contact.FirstName.ShouldBe("Bob");
            contact.PartyType.ShouldBe("Lead");
            contact.PartyId.ShouldBe(lead.Id);
        });
    }
}
