using System;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Sales;

/// <summary>
/// Regression coverage for SalesOrderAppService.CancelAsync/CloseAsync: StockReservationManager's
/// own doc comment on CancelReservationsForOrderAsync claims it is "used on SO cancel/close", but
/// neither method ever called it — a Stock Reservation Entry raised against a Sales Order stayed
/// Status=Submitted forever after the order was cancelled or closed, permanently counting against
/// PickListManager's double-pick-prevention availability check for other Pick Lists. Wired both
/// methods to actually call it; this test covers behavior that was previously unreachable.
/// </summary>
public abstract class SalesOrderCancelReservationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CancelAsync_CancelsActiveStockReservationEntriesForTheOrder()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var warehouseRepository = GetRequiredService<IRepository<Warehouse, Guid>>();
            var soRepository = GetRequiredService<IRepository<SalesOrder, Guid>>();
            var sreRepository = GetRequiredService<IRepository<StockReservationEntry, Guid>>();
            var salesOrderAppService = GetRequiredService<ISalesOrderAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "SO Reservation Test Co 1"), autoSave: true);
            var customer = await customerRepository.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "SO Reservation Cust 1"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "SORES-1", "SO Reservation Item 1", ItemType.Goods), autoSave: true);
            var warehouse = await warehouseRepository.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "SO Reservation WH 1"), autoSave: true);

            var so = new SalesOrder(Guid.NewGuid(), company.Id, customer.Id, "SO-RES-001", DateTime.Today);
            so.AddItem(item.Id, "SO Reservation Item 1", 10m, 100m, 0m);
            so.Submit();
            await soRepository.InsertAsync(so, autoSave: true);

            var sre = new StockReservationEntry(
                Guid.NewGuid(), company.Id, item.Id, warehouse.Id, "SalesOrder", so.Id, reservedQty: 10m);
            sre.Submit();
            await sreRepository.InsertAsync(sre, autoSave: true);

            await salesOrderAppService.CancelAsync(so.Id);

            var reloaded = await sreRepository.GetAsync(sre.Id);
            reloaded.Status.ShouldBe(DocumentStatus.Cancelled);
        });
    }

    [Fact]
    public async Task CloseAsync_CancelsActiveStockReservationEntriesForTheOrder()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var warehouseRepository = GetRequiredService<IRepository<Warehouse, Guid>>();
            var soRepository = GetRequiredService<IRepository<SalesOrder, Guid>>();
            var sreRepository = GetRequiredService<IRepository<StockReservationEntry, Guid>>();
            var salesOrderAppService = GetRequiredService<ISalesOrderAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "SO Reservation Test Co 2"), autoSave: true);
            var customer = await customerRepository.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "SO Reservation Cust 2"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "SORES-2", "SO Reservation Item 2", ItemType.Goods), autoSave: true);
            var warehouse = await warehouseRepository.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "SO Reservation WH 2"), autoSave: true);

            var so = new SalesOrder(Guid.NewGuid(), company.Id, customer.Id, "SO-RES-002", DateTime.Today);
            so.AddItem(item.Id, "SO Reservation Item 2", 10m, 100m, 0m);
            so.Submit();
            await soRepository.InsertAsync(so, autoSave: true);

            var sre = new StockReservationEntry(
                Guid.NewGuid(), company.Id, item.Id, warehouse.Id, "SalesOrder", so.Id, reservedQty: 10m);
            sre.Submit();
            await sreRepository.InsertAsync(sre, autoSave: true);

            await salesOrderAppService.CloseAsync(so.Id);

            var reloaded = await sreRepository.GetAsync(sre.Id);
            reloaded.Status.ShouldBe(DocumentStatus.Cancelled);
        });
    }
}
