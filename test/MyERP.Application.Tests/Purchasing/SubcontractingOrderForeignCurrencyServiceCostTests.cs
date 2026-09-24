using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Purchasing;

public abstract class SubcontractingOrderForeignCurrencyServiceCostTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateOrderAsync_WithForeignCurrencyPurchaseOrder_ConvertsServiceCostToCompanyCurrency()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var poRepo = GetRequiredService<IRepository<PurchaseOrder, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var subcontractingAppService = GetRequiredService<ISubcontractingAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Subcontract FX Co"), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "SCO Series", "SCO", "SCO-.####"), autoSave: true);

            var supplier = await supplierRepo.InsertAsync(
                new Supplier(Guid.NewGuid(), company.Id, "FX Subcontractor Supplier"), autoSave: true);

            var fgItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "FG-SUB-01", "Subcontracted Finished Good", ItemType.Goods), autoSave: true);

            // Create PO in foreign currency (USD) with exchange rate 4.5
            var po = new PurchaseOrder(Guid.NewGuid(), company.Id, supplier.Id, "PO-FX-001", DateTime.UtcNow)
            {
                CurrencyCode = "USD",
                ExchangeRate = 4.5m,
                IsSubcontracted = true
            };
            po.AddItem(fgItem.Id, fgItem.ItemName, 10m, 100m, 0m, "Unit", null); // Rate in USD = 100
            await poRepo.InsertAsync(po, autoSave: true);

            // Create SubcontractingOrder against foreign currency PO
            var scoDto = await subcontractingAppService.CreateOrderAsync(new CreateSubcontractingOrderDto
            {
                CompanyId = company.Id,
                SupplierId = supplier.Id,
                OrderDate = DateTime.UtcNow,
                PurchaseOrderId = po.Id,
                Items = new List<CreateScoItemDto>
                {
                    new()
                    {
                        ItemId = fgItem.Id,
                        ItemName = fgItem.ItemName,
                        Qty = 10m,
                        Rate = 0m, // Leave 0 to auto-convert from PO service cost
                    }
                }
            });

            scoDto.CurrencyCode.ShouldBe("USD");
            scoDto.ExchangeRate.ShouldBe(4.5m);

            var itemDto = scoDto.Items.ShouldHaveSingleItem();
            // PR #59334 / commit ce3b63ae25: service cost in company currency = USD 100 * 4.5 = 450
            itemDto.ServiceCostPerQty.ShouldBe(450m);
            itemDto.Rate.ShouldBe(450m);
            scoDto.GrandTotal.ShouldBe(4500m); // 10 * 450
        });
    }

    [Fact]
    public async Task CreateOrderAsync_WithoutPo_UsesExplicitCurrencyAndServiceCost()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var subcontractingAppService = GetRequiredService<ISubcontractingAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Subcontract Standalone Co"), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "SCO Series", "SCO", "SCO-.####"), autoSave: true);

            var supplier = await supplierRepo.InsertAsync(
                new Supplier(Guid.NewGuid(), company.Id, "Standalone Subcontractor"), autoSave: true);

            var fgItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "FG-SUB-02", "Finished Good 2", ItemType.Goods), autoSave: true);

            var scoDto = await subcontractingAppService.CreateOrderAsync(new CreateSubcontractingOrderDto
            {
                CompanyId = company.Id,
                SupplierId = supplier.Id,
                OrderDate = DateTime.UtcNow,
                CurrencyCode = "EUR",
                ExchangeRate = 5.0m,
                Items = new List<CreateScoItemDto>
                {
                    new()
                    {
                        ItemId = fgItem.Id,
                        ItemName = fgItem.ItemName,
                        Qty = 5m,
                        Rate = 250m,
                        ServiceCostPerQty = 250m
                    }
                }
            });

            scoDto.CurrencyCode.ShouldBe("EUR");
            scoDto.ExchangeRate.ShouldBe(5.0m);
            var itemDto = scoDto.Items.ShouldHaveSingleItem();
            itemDto.ServiceCostPerQty.ShouldBe(250m);
            itemDto.Rate.ShouldBe(250m);
            scoDto.GrandTotal.ShouldBe(1250m);
        });
    }
}
