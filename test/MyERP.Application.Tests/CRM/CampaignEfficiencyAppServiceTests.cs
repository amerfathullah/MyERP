using System;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.CRM.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.CRM;

public abstract class CampaignEfficiencyAppServiceTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task GetReportAsync_GroupsByUtmCampaign_AndCalculatesFunnelMetrics()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var leadRepo = GetRequiredService<IRepository<Lead, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var oppRepo = GetRequiredService<IRepository<Opportunity, Guid>>();
            var quotRepo = GetRequiredService<IRepository<Quotation, Guid>>();
            var soRepo = GetRequiredService<IRepository<SalesOrder, Guid>>();
            var service = GetRequiredService<ICampaignEfficiencyAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Camp Eff Co"), autoSave: true);

            // Lead 1: Summer_Sale
            var lead1 = new Lead(Guid.NewGuid(), company.Id, "LEAD-CE-1", "Alice")
            {
                UtmCampaign = "Summer_Sale",
                Email = "alice@example.com"
            };
            await leadRepo.InsertAsync(lead1, autoSave: true);

            // Lead 2: Summer_Sale
            var lead2 = new Lead(Guid.NewGuid(), company.Id, "LEAD-CE-2", "Bob")
            {
                UtmCampaign = "Summer_Sale",
                Email = "bob@example.com"
            };
            await leadRepo.InsertAsync(lead2, autoSave: true);

            // Customer converted from Lead 1
            var customer1 = new Customer(Guid.NewGuid(), company.Id, "Alice Co")
            {
                LeadId = lead1.Id
            };
            await customerRepo.InsertAsync(customer1, autoSave: true);

            // Opportunity for Lead 1
            var opp1 = new Opportunity(Guid.NewGuid(), company.Id, "OPP-CE-1", "Summer Deal 1")
            {
                LeadId = lead1.Id,
                CustomerId = customer1.Id,
                OpportunityAmount = 1000m
            };
            await oppRepo.InsertAsync(opp1, autoSave: true);

            // Quotation linked to Opportunity
            var quot1 = new Quotation(Guid.NewGuid(), company.Id, customer1.Id, "QTN-CE-1", DateTime.UtcNow)
            {
                OpportunityId = opp1.Id,
                GrandTotal = 500m
            };
            quot1.AddItem(Guid.NewGuid(), "Widget", 5m, 100m, 0m, "Unit");
            quot1.Submit();
            await quotRepo.InsertAsync(quot1, autoSave: true);

            // Sales Order converted from Quotation (OrderStatus becomes Ordered)
            var so1 = new SalesOrder(Guid.NewGuid(), company.Id, customer1.Id, "SO-CE-1", DateTime.UtcNow)
            {
                QuotationId = quot1.Id,
                GrandTotal = 500m
            };
            so1.AddItem(Guid.NewGuid(), "Widget", 5m, 100m, 0m, "Unit", quotationItemId: quot1.Items[0].Id);
            so1.Submit();
            await soRepo.InsertAsync(so1, autoSave: true);

            // Update Quotation item ordered qty
            quot1.Items[0].OrderedQty = 5m;
            await quotRepo.UpdateAsync(quot1, autoSave: true);

            // Generate report
            var report = await service.GetReportAsync(new CampaignEfficiencyFilterDto
            {
                CompanyId = company.Id,
                BasedOn = "UtmCampaign"
            });

            report.Rows.Count.ShouldBe(1);
            var row = report.Rows[0];
            row.Campaign.ShouldBe("Summer_Sale");
            row.LeadCount.ShouldBe(2);
            row.OppCount.ShouldBe(1);
            row.QuotCount.ShouldBe(1);
            row.OrderCount.ShouldBe(1); // Per PR #59916: Ordered / Partially Ordered counted
            row.OrderValue.ShouldBe(500m);
            row.OppLeadRate.ShouldBe(50m);
            row.QuotLeadRate.ShouldBe(50m);
            row.OrderQuotRate.ShouldBe(100m);

            report.TotalLeads.ShouldBe(2);
            report.TotalOpps.ShouldBe(1);
            report.TotalQuots.ShouldBe(1);
            report.TotalOrders.ShouldBe(1);
            report.TotalOrderValue.ShouldBe(500m);
        });
    }
}
