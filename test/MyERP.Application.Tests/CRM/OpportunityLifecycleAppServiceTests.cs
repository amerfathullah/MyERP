using System;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.CRM.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.CRM;

/// <summary>
/// OpportunityAppService.ConvertAsync/DeclareLostAsync/CloseAsync/ReopenAsync had zero Angular
/// callers and zero App-service-level test coverage (only the underlying domain entity methods
/// were unit tested — see OpportunityTests). Added Mark Won/Mark Lost/Close/Reopen actions to
/// opportunity-detail.component.ts; this covers the App-service layer they now reach, including
/// DeclareLostAsync's active-quotation guard, which lives only at this layer, not in the entity.
/// </summary>
public abstract class OpportunityLifecycleAppServiceTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task ConvertAsync_FromOpen_MarksConverted()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var opportunityRepository = GetRequiredService<IRepository<Opportunity, Guid>>();
            var opportunityAppService = GetRequiredService<IOpportunityAppService>();

            var company = await GetRequiredService<IRepository<Company, Guid>>()
                .InsertAsync(new Company(Guid.NewGuid(), "Opp Lifecycle Test Co 1"), autoSave: true);
            var opportunity = await opportunityRepository.InsertAsync(
                new Opportunity(Guid.NewGuid(), company.Id, "OPP-LC-1", "Convert Test Deal"), autoSave: true);

            var result = await opportunityAppService.ConvertAsync(opportunity.Id);

            result.Status.ShouldBe(OpportunityStatus.Converted);
            (await opportunityRepository.GetAsync(opportunity.Id)).Status.ShouldBe(OpportunityStatus.Converted);
        });
    }

    [Fact]
    public async Task DeclareLostAsync_NoActiveQuotation_MarksLostWithReason()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var opportunityRepository = GetRequiredService<IRepository<Opportunity, Guid>>();
            var opportunityAppService = GetRequiredService<IOpportunityAppService>();

            var company = await GetRequiredService<IRepository<Company, Guid>>()
                .InsertAsync(new Company(Guid.NewGuid(), "Opp Lifecycle Test Co 2"), autoSave: true);
            var opportunity = await opportunityRepository.InsertAsync(
                new Opportunity(Guid.NewGuid(), company.Id, "OPP-LC-2", "Lost Test Deal"), autoSave: true);

            var result = await opportunityAppService.DeclareLostAsync(opportunity.Id, "Budget constraints");

            result.Status.ShouldBe(OpportunityStatus.Lost);
            result.LostReason.ShouldBe("Budget constraints");
        });
    }

    [Fact]
    public async Task DeclareLostAsync_WithActiveQuotation_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var opportunityRepository = GetRequiredService<IRepository<Opportunity, Guid>>();
            var quotationRepository = GetRequiredService<IRepository<Quotation, Guid>>();
            var opportunityAppService = GetRequiredService<IOpportunityAppService>();

            var company = await GetRequiredService<IRepository<Company, Guid>>()
                .InsertAsync(new Company(Guid.NewGuid(), "Opp Lifecycle Test Co 3"), autoSave: true);
            var customer = await GetRequiredService<IRepository<Customer, Guid>>()
                .InsertAsync(new Customer(Guid.NewGuid(), company.Id, "Opp Lifecycle Customer 3"), autoSave: true);
            var opportunity = await opportunityRepository.InsertAsync(
                new Opportunity(Guid.NewGuid(), company.Id, "OPP-LC-3", "Blocked Lost Test Deal")
                {
                    CustomerId = customer.Id,
                }, autoSave: true);

            var quotation = new Quotation(Guid.NewGuid(), company.Id, customer.Id, "QTN-LC-3", DateTime.UtcNow)
            {
                OpportunityId = opportunity.Id,
            };
            quotation.AddItem(Guid.NewGuid(), "Widget", 1m, 100m, 0m);
            quotation.Submit();
            await quotationRepository.InsertAsync(quotation, autoSave: true);

            await Should.ThrowAsync<BusinessException>(
                () => opportunityAppService.DeclareLostAsync(opportunity.Id, "Should be blocked"));

            (await opportunityRepository.GetAsync(opportunity.Id)).Status.ShouldBe(OpportunityStatus.Open);
        });
    }

    [Fact]
    public async Task CloseAsync_ThenReopenAsync_RoundTrips()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var opportunityRepository = GetRequiredService<IRepository<Opportunity, Guid>>();
            var opportunityAppService = GetRequiredService<IOpportunityAppService>();

            var company = await GetRequiredService<IRepository<Company, Guid>>()
                .InsertAsync(new Company(Guid.NewGuid(), "Opp Lifecycle Test Co 4"), autoSave: true);
            var opportunity = await opportunityRepository.InsertAsync(
                new Opportunity(Guid.NewGuid(), company.Id, "OPP-LC-4", "Close Reopen Test Deal"), autoSave: true);

            var closed = await opportunityAppService.CloseAsync(opportunity.Id);
            closed.Status.ShouldBe(OpportunityStatus.Closed);

            var reopened = await opportunityAppService.ReopenAsync(opportunity.Id);
            reopened.Status.ShouldBe(OpportunityStatus.Open);
        });
    }
}
