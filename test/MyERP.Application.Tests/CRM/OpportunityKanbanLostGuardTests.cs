using System;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.CRM;

/// <summary>
/// Regression coverage for a real gap found via ERPNext validate() parity: ERPNext's
/// declare_enquiry_lost()/has_active_quotation() blocks marking an Opportunity Lost while a live,
/// submitted Quotation still exists for it. MyERP's DeclareLostAsync had this guard (per ERPNext PR
/// #57489), but UpdateStageAsync — the Kanban drag-and-drop path, which also calls
/// Opportunity.DeclareLost() when a card is dropped on the "Lost" column — bypassed it entirely,
/// letting a card-drag silently mark an Opportunity Lost while its Quotation could still convert to
/// a Sales Order.
/// </summary>
public abstract class OpportunityKanbanLostGuardTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task UpdateStageAsync_ToLostWithActiveQuotation_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
            var opportunityRepository = GetRequiredService<IRepository<MyERP.CRM.Entities.Opportunity, Guid>>();
            var quotationRepository = GetRequiredService<IRepository<Quotation, Guid>>();
            var opportunityAppService = GetRequiredService<IOpportunityAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Opp Kanban Guard Co"), autoSave: true);
            var customer = await customerRepository.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "Opp Kanban Guard Customer"), autoSave: true);

            var opp = await opportunityRepository.InsertAsync(
                new MyERP.CRM.Entities.Opportunity(Guid.NewGuid(), company.Id, "OPP-KAN-001", "Kanban Guard Deal"), autoSave: true);

            var quotation = new Quotation(Guid.NewGuid(), company.Id, customer.Id, "QTN-KAN-001", DateTime.Today)
            {
                OpportunityId = opp.Id,
            };
            quotation.AddItem(Guid.NewGuid(), "Widget", 1m, 100m, 0m, "Unit");
            quotation.Submit();
            await quotationRepository.InsertAsync(quotation, autoSave: true);

            await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                opportunityAppService.UpdateStageAsync(opp.Id, new UpdateOpportunityStageDto { SalesStage = "Lost" }));
        });
    }

    [Fact]
    public async Task UpdateStageAsync_ToLostWithNoActiveQuotation_Succeeds()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var opportunityRepository = GetRequiredService<IRepository<MyERP.CRM.Entities.Opportunity, Guid>>();
            var opportunityAppService = GetRequiredService<IOpportunityAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Opp Kanban Happy Co"), autoSave: true);
            var opp = await opportunityRepository.InsertAsync(
                new MyERP.CRM.Entities.Opportunity(Guid.NewGuid(), company.Id, "OPP-KAN-002", "Kanban Happy Deal"), autoSave: true);

            var dto = await opportunityAppService.UpdateStageAsync(opp.Id, new UpdateOpportunityStageDto { SalesStage = "Lost" });

            dto.Id.ShouldBe(opp.Id);
        });
    }
}
