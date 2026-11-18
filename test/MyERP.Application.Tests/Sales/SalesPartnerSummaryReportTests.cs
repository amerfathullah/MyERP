using System;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Sales;

/// <summary>
/// EF Core integration tests for Sales Partner Commission Summary and Transaction Summary
/// per ERPNext selling/report/sales_partner_commission_summary and sales_partner_transaction_summary.
/// </summary>
public abstract class SalesPartnerSummaryReportTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task PartnerReports_ComputeCommissionSummary_AndItemTransactionSummaryRespectingGrantCommission()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var terrRepo = GetRequiredService<IRepository<Territory, Guid>>();
            var partnerRepo = GetRequiredService<IRepository<SalesPartner, Guid>>();
            var custRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var igRepo = GetRequiredService<IRepository<ItemGroup, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var siRepo = GetRequiredService<IRepository<SalesInvoice, Guid>>();
            var soRepo = GetRequiredService<IRepository<SalesOrder, Guid>>();
            var reportService = GetRequiredService<ISalesPartnerCommissionSummaryAppService>();

            var company = await companyRepo.InsertAsync(
                new Company(Guid.NewGuid(), "Partner Summary Co") { CurrencyCode = "MYR" }, autoSave: true);
            var terr = await terrRepo.InsertAsync(
                new Territory(Guid.NewGuid(), "Selangor"), autoSave: true);
            var partner = await partnerRepo.InsertAsync(
                new SalesPartner(Guid.NewGuid(), "Channel Prime", PartnerType.Reseller, 10m), autoSave: true);
            var customer = await custRepo.InsertAsync(
                new Customer(Guid.NewGuid(), company.Id, "Gamma Enterprise") { TerritoryId = terr.Id }, autoSave: true);

            var rootGroup = await igRepo.InsertAsync(
                new ItemGroup(Guid.NewGuid(), "Electronics", isGroup: true), autoSave: true);
            var childGroup = await igRepo.InsertAsync(
                new ItemGroup(Guid.NewGuid(), "Laptops", isGroup: false, parentId: rootGroup.Id), autoSave: true);

            var commissionItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "ITM-SP-01", "Pro Laptop", ItemType.Goods)
                {
                    ItemGroupId = childGroup.Id,
                    Brand = "ThinkBrand",
                    GrantCommission = true,
                }, autoSave: true);
            var nonCommissionItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "ITM-SP-02", "Warranty Fee", ItemType.Service)
                {
                    ItemGroupId = childGroup.Id,
                    Brand = "ThinkBrand",
                    GrantCommission = false,
                    MaintainStock = false,
                }, autoSave: true);

            var si = new SalesInvoice(Guid.NewGuid(), company.Id, customer.Id, "SINV-SP-001", new DateTime(2026, 11, 15))
            {
                SalesPartnerId = partner.Id,
                CommissionRate = 10m,
                TotalCommission = 100m,
            };
            si.AddItem(commissionItem.Id, "Pro Laptop", 1m, 1000m, 0m);
            si.AddItem(nonCommissionItem.Id, "Warranty Fee", 1m, 200m, 0m);
            si.Submit();
            si.Post();
            await siRepo.InsertAsync(si, autoSave: true);

            var posSi = new SalesInvoice(Guid.NewGuid(), company.Id, customer.Id, "POS-SP-001", new DateTime(2026, 11, 16))
            {
                IsPos = true,
                SalesPartnerId = partner.Id,
                CommissionRate = 10m,
                TotalCommission = 50m,
            };
            posSi.AddItem(commissionItem.Id, "Pro Laptop", 1m, 500m, 0m);
            posSi.Submit();
            posSi.Post();
            await siRepo.InsertAsync(posSi, autoSave: true);

            var so = new SalesOrder(Guid.NewGuid(), company.Id, customer.Id, "SO-SP-001", new DateTime(2026, 11, 12))
            {
                SalesPartnerId = partner.Id,
                CommissionRate = 10m,
                TotalCommission = 80m,
            };
            so.AddItem(commissionItem.Id, "Pro Laptop", 2m, 400m, 0m);
            so.Submit();
            await soRepo.InsertAsync(so, autoSave: true);

            // Unsupported docType check
            await Should.ThrowAsync<BusinessException>(() =>
                reportService.GetReportAsync(new GetSalesPartnerCommissionSummaryInput
                {
                    CompanyId = company.Id,
                    DocType = "Payment Entry",
                }));

            // Commission Summary for POS Invoice
            var posSummary = await reportService.GetReportAsync(new GetSalesPartnerCommissionSummaryInput
            {
                CompanyId = company.Id,
                DocType = "POS Invoice",
                FromDate = new DateTime(2026, 11, 1),
                ToDate = new DateTime(2026, 11, 30),
                TerritoryId = terr.Id,
            });
            posSummary.TransactionCount.ShouldBe(1);
            posSummary.Rows[0].TransactionNumber.ShouldBe("POS-SP-001");
            posSummary.TotalCommission.ShouldBe(50m);

            // Commission Summary for Sales Order
            var soSummary = await reportService.GetReportAsync(new GetSalesPartnerCommissionSummaryInput
            {
                CompanyId = company.Id,
                DocType = "Sales Order",
                FromDate = new DateTime(2026, 11, 1),
                ToDate = new DateTime(2026, 11, 30),
            });
            soSummary.TransactionCount.ShouldBe(1);
            soSummary.Rows[0].TransactionNumber.ShouldBe("SO-SP-001");
            soSummary.TotalAmount.ShouldBe(800m);

            // Transaction Summary filtering by root ItemGroup (recursive descendant match) and SINV-SP-001 item commission
            var txSummary = await reportService.GetTransactionSummaryAsync(new GetSalesPartnerCommissionSummaryInput
            {
                CompanyId = company.Id,
                DocType = "Sales Invoice",
                FromDate = new DateTime(2026, 11, 14),
                ToDate = new DateTime(2026, 11, 15),
                ItemGroupId = rootGroup.Id,
                Brand = "ThinkBrand",
            });
            txSummary.Rows.Count.ShouldBe(2);
            txSummary.TotalQty.ShouldBe(2m);
            txSummary.TotalAmount.ShouldBe(1200m);
            txSummary.TotalCommission.ShouldBe(100m); // 10% on 1000 only; GrantCommission=false 200 gives 0
        });
    }
}
