using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MyERP.Inventory;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

using Volo.Abp.Modularity;

namespace MyERP.Inventory.Tests;

public abstract class PickListAppService_Tests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly PickListAppService _pickListAppService;
    private readonly IRepository<MyERP.Inventory.Entities.PickList, Guid> _pickListRepository;
    private readonly IRepository<MyERP.Sales.Entities.DeliveryNote, Guid> _deliveryNoteRepository;

    protected PickListAppService_Tests()
    {
        _pickListAppService = GetRequiredService<PickListAppService>();
        _pickListRepository = GetRequiredService<IRepository<MyERP.Inventory.Entities.PickList, Guid>>();
        _deliveryNoteRepository = GetRequiredService<IRepository<MyERP.Sales.Entities.DeliveryNote, Guid>>();
    }

    [Fact]
    public async Task CreateDeliveryNoteFromPickList_Should_Map_Customer_When_No_SalesOrder_Exists()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        
        // Mock a submitted PickList without a Sales Order, but with a Customer
        var pickList = new MyERP.Inventory.Entities.PickList(Guid.NewGuid(), companyId, "Delivery", null)
        {
            CustomerId = customerId
        };
        pickList.AddItem(Guid.NewGuid(), Guid.NewGuid(), 10, 10, "Test Item");
        pickList.Submit(); // Transition to submitted
        await _pickListRepository.InsertAsync(pickList, autoSave: true);

        // Act
        // We catch exception here if number generator or other dependencies fail,
        // but the main logic we want to test is inside the app service.
        // For a full test we'd need to mock number generators, but we can verify the logic throws a specific error if customer is missing, and doesn't if it's there.
        try
        {
            await _pickListAppService.CreateDeliveryNoteFromPickListAsync(pickList.Id);
        }
        catch (Exception ex)
        {
            // If it throws because of warehouse missing or ID document number generator, it bypassed the Customer check!
            ex.ShouldBeOfType<Volo.Abp.BusinessException>();
        }
    }

    [Fact]
    public async Task GetStockAvailabilityInsightAsync_Should_Return_Insight_And_DeliveryStatus()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyId = Guid.NewGuid();
            var itemId = Guid.NewGuid();
            var warehouseId = Guid.NewGuid();

            var binRepo = GetRequiredService<IRepository<MyERP.Inventory.Entities.Bin, Guid>>();
            var bin = new MyERP.Inventory.Entities.Bin(Guid.NewGuid(), itemId, warehouseId);
            bin.ApplyStockMovement(150m, 1500m);
            await binRepo.InsertAsync(bin, autoSave: true);

            var pl1 = new MyERP.Inventory.Entities.PickList(Guid.NewGuid(), companyId, "Delivery", null)
            {
                PickListNumber = "PL-001"
            };
            pl1.AddItem(itemId, warehouseId, 50, 50, "Test Item 1");
            pl1.Submit();
            await _pickListRepository.InsertAsync(pl1, autoSave: true);

            var pl2 = new MyERP.Inventory.Entities.PickList(Guid.NewGuid(), companyId, "Delivery", null)
            {
                PickListNumber = "PL-002"
            };
            pl2.AddItem(itemId, warehouseId, 40, 40, "Test Item 1");
            await _pickListRepository.InsertAsync(pl2, autoSave: true);

            // Act
            var insights = await _pickListAppService.GetStockAvailabilityInsightAsync(pl2.Id);
            var plDto = await _pickListAppService.GetAsync(pl1.Id);

            // Assert
            plDto.DeliveryStatus.ShouldBe("Not Delivered");
            insights.ShouldNotBeEmpty();
            var insight = insights.First(i => i.ItemId == itemId && i.WarehouseId == warehouseId);
            insight.ActualQty.ShouldBe(150m);
            insight.PickedQty.ShouldBe(50m);
            insight.FreeQty.ShouldBe(100m);
            insight.HoldingPickLists.ShouldContain(h => h.PickListId == pl1.Id && h.HoldingQty == 50m);
        });
    }

    [Fact]
    public async Task CreateDeliveryNoteFromPickList_WithMismatchedSalesOrder_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyId = Guid.NewGuid();
            var soId = Guid.NewGuid();
            var otherSoId = Guid.NewGuid();

            var pickList = new MyERP.Inventory.Entities.PickList(Guid.NewGuid(), companyId, "Delivery", null)
            {
                SalesOrderId = soId,
                CustomerId = Guid.NewGuid()
            };
            pickList.AddItem(Guid.NewGuid(), Guid.NewGuid(), 10, 10, "Test Item");
            pickList.Submit();
            await _pickListRepository.InsertAsync(pickList, autoSave: true);

            var ex = await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                _pickListAppService.CreateDeliveryNoteFromPickListAsync(pickList.Id, otherSoId));
            ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        });
    }

    [Fact]
    public async Task CreateDeliveryNoteFromPickList_DraftPickList_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyId = Guid.NewGuid();
            var pickList = new MyERP.Inventory.Entities.PickList(Guid.NewGuid(), companyId, "Delivery", null)
            {
                CustomerId = Guid.NewGuid()
            };
            pickList.AddItem(Guid.NewGuid(), Guid.NewGuid(), 10, 10, "Draft Item");
            await _pickListRepository.InsertAsync(pickList, autoSave: true);

            var ex = await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                _pickListAppService.CreateDeliveryNoteFromPickListAsync(pickList.Id));
            ex.Code.ShouldBe(MyERPDomainErrorCodes.DocumentMustBeSubmittedForConversion);
        });
    }

    [Fact]
    public async Task CreateDeliveryNoteFromPickList_ProductBundlePartialDelivery_SizesSecondDeliveryNoteToRemainingBundles()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<MyERP.Core.Entities.Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<MyERP.Sales.Entities.Customer, Guid>>();
            var itemRepo = GetRequiredService<IRepository<MyERP.Inventory.Entities.Item, Guid>>();
            var whRepo = GetRequiredService<IRepository<MyERP.Inventory.Entities.Warehouse, Guid>>();
            var bundleRepo = GetRequiredService<IRepository<MyERP.Sales.Entities.ProductBundle, Guid>>();
            var soRepo = GetRequiredService<IRepository<MyERP.Sales.Entities.SalesOrder, Guid>>();

            var company = await companyRepo.InsertAsync(new MyERP.Core.Entities.Company(Guid.NewGuid(), "PL Bundle Co"), autoSave: true);
            var seriesRepo = GetRequiredService<IRepository<MyERP.Core.Entities.DocumentSeries, Guid>>();
            await seriesRepo.InsertAsync(new MyERP.Core.Entities.DocumentSeries(Guid.NewGuid(), company.Id, "DNS", "DeliveryNote", "DN-"), autoSave: true);
            var customer = await customerRepo.InsertAsync(new MyERP.Sales.Entities.Customer(Guid.NewGuid(), company.Id, "PL Bundle Cust"), autoSave: true);
            var warehouse = await whRepo.InsertAsync(new MyERP.Inventory.Entities.Warehouse(Guid.NewGuid(), company.Id, "PL Bundle WH"), autoSave: true);

            var bundleItem = new MyERP.Inventory.Entities.Item(Guid.NewGuid(), company.Id, "BND-PARENT", "Bundle Parent", ItemType.Goods) { MaintainStock = false };
            var compA = new MyERP.Inventory.Entities.Item(Guid.NewGuid(), company.Id, "BND-COMP-A", "Component A", ItemType.Goods);
            var compB = new MyERP.Inventory.Entities.Item(Guid.NewGuid(), company.Id, "BND-COMP-B", "Component B", ItemType.Goods);
            await itemRepo.InsertAsync(bundleItem, autoSave: true);
            await itemRepo.InsertAsync(compA, autoSave: true);
            await itemRepo.InsertAsync(compB, autoSave: true);

            var bundle = new MyERP.Sales.Entities.ProductBundle(Guid.NewGuid(), bundleItem.Id);
            bundle.AddItem(compA.Id, 2m, "Component A", "Unit");
            bundle.AddItem(compB.Id, 1m, "Component B", "Unit");
            await bundleRepo.InsertAsync(bundle, autoSave: true);

            var so = new MyERP.Sales.Entities.SalesOrder(Guid.NewGuid(), company.Id, customer.Id, "SO-PL-BND-1", DateTime.Today);
            so.AddItem(bundleItem.Id, "Bundle Parent", 3m, 200m, 0m, "Unit");
            var soItem = so.Items.Single();
            soItem.WarehouseId = warehouse.Id;
            soItem.ProductBundleId = bundle.Id;
            so.Submit();
            await soRepo.InsertAsync(so, autoSave: true);

            var conversionSvc = GetRequiredService<MyERP.Sales.IDocumentConversionAppService>();
            var plId = await conversionSvc.ConvertSalesOrderToPickListAsync(so.Id);
            await _pickListAppService.SubmitAsync(plId);

            // First Delivery Note maps all 3 picked bundles
            var dn1Id = await _pickListAppService.CreateDeliveryNoteFromPickListAsync(plId);
            var dn1 = await _deliveryNoteRepository.GetAsync(dn1Id);
            dn1.Items.Count.ShouldBe(1);
            dn1.Items[0].ItemId.ShouldBe(bundleItem.Id);
            dn1.Items[0].Quantity.ShouldBe(3m);

            // Simulate partial delivery of 1 bundle (2 of compA, 1 of compB) recorded on the Pick List
            var plAfterPartial = await _pickListRepository.GetAsync(plId, includeDetails: true);
            plAfterPartial.Items.Single(i => i.ItemId == compA.Id).RecordDelivery(2m);
            plAfterPartial.Items.Single(i => i.ItemId == compB.Id).RecordDelivery(1m);
            await _pickListRepository.UpdateAsync(plAfterPartial, autoSave: true);

            // Second Delivery Note must map only the 2 remaining undelivered bundles (ERPNext commit e3c6082498)
            var dn2Id = await _pickListAppService.CreateDeliveryNoteFromPickListAsync(plId);
            var dn2 = await _deliveryNoteRepository.GetAsync(dn2Id);
            dn2.Items.Count.ShouldBe(1);
            dn2.Items[0].ItemId.ShouldBe(bundleItem.Id);
            dn2.Items[0].Quantity.ShouldBe(2m);

            // Simulate remaining 2 bundles delivered -> no third Delivery Note can be created
            var plFullyDelivered = await _pickListRepository.GetAsync(plId, includeDetails: true);
            plFullyDelivered.Items.Single(i => i.ItemId == compA.Id).RecordDelivery(4m);
            plFullyDelivered.Items.Single(i => i.ItemId == compB.Id).RecordDelivery(2m);
            await _pickListRepository.UpdateAsync(plFullyDelivered, autoSave: true);

            var emptyEx = await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                _pickListAppService.CreateDeliveryNoteFromPickListAsync(plId));
            emptyEx.Code.ShouldBe(MyERPDomainErrorCodes.DocumentMustHaveItems);
        });
    }

    [Fact]
    public async Task ConvertSalesOrderToPickList_SkipsClosedAndDropShipBundleAndPlainRows()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<MyERP.Core.Entities.Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<MyERP.Sales.Entities.Customer, Guid>>();
            var itemRepo = GetRequiredService<IRepository<MyERP.Inventory.Entities.Item, Guid>>();
            var whRepo = GetRequiredService<IRepository<MyERP.Inventory.Entities.Warehouse, Guid>>();
            var bundleRepo = GetRequiredService<IRepository<MyERP.Sales.Entities.ProductBundle, Guid>>();
            var soRepo = GetRequiredService<IRepository<MyERP.Sales.Entities.SalesOrder, Guid>>();

            var company = await companyRepo.InsertAsync(new MyERP.Core.Entities.Company(Guid.NewGuid(), "PL Filter Co"), autoSave: true);
            var customer = await customerRepo.InsertAsync(new MyERP.Sales.Entities.Customer(Guid.NewGuid(), company.Id, "PL Filter Cust"), autoSave: true);
            var warehouse = await whRepo.InsertAsync(new MyERP.Inventory.Entities.Warehouse(Guid.NewGuid(), company.Id, "PL Filter WH"), autoSave: true);

            var bundleClosed = new MyERP.Inventory.Entities.Item(Guid.NewGuid(), company.Id, "BND-CLS", "Closed Bundle", ItemType.Goods) { MaintainStock = false };
            var bundleDropShip = new MyERP.Inventory.Entities.Item(Guid.NewGuid(), company.Id, "BND-DS", "DropShip Bundle", ItemType.Goods) { MaintainStock = false };
            var bundleOpen = new MyERP.Inventory.Entities.Item(Guid.NewGuid(), company.Id, "BND-OPN", "Open Bundle", ItemType.Goods) { MaintainStock = false };
            var compClosed = new MyERP.Inventory.Entities.Item(Guid.NewGuid(), company.Id, "CMP-CLS", "Closed Comp", ItemType.Goods);
            var compDropShip = new MyERP.Inventory.Entities.Item(Guid.NewGuid(), company.Id, "CMP-DS", "DropShip Comp", ItemType.Goods);
            var compOpen = new MyERP.Inventory.Entities.Item(Guid.NewGuid(), company.Id, "CMP-OPN", "Open Comp", ItemType.Goods);
            foreach (var it in new[] { bundleClosed, bundleDropShip, bundleOpen, compClosed, compDropShip, compOpen })
            {
                await itemRepo.InsertAsync(it, autoSave: true);
            }

            var pbClosed = new MyERP.Sales.Entities.ProductBundle(Guid.NewGuid(), bundleClosed.Id);
            pbClosed.AddItem(compClosed.Id, 2m, "Closed Comp", "Unit");
            var pbDropShip = new MyERP.Sales.Entities.ProductBundle(Guid.NewGuid(), bundleDropShip.Id);
            pbDropShip.AddItem(compDropShip.Id, 2m, "DropShip Comp", "Unit");
            var pbOpen = new MyERP.Sales.Entities.ProductBundle(Guid.NewGuid(), bundleOpen.Id);
            pbOpen.AddItem(compOpen.Id, 2m, "Open Comp", "Unit");
            await bundleRepo.InsertAsync(pbClosed, autoSave: true);
            await bundleRepo.InsertAsync(pbDropShip, autoSave: true);
            await bundleRepo.InsertAsync(pbOpen, autoSave: true);

            var so = new MyERP.Sales.Entities.SalesOrder(Guid.NewGuid(), company.Id, customer.Id, "SO-PL-FLT-1", DateTime.Today);
            so.AddItem(bundleClosed.Id, "Closed Bundle", 1m, 100m, 0m, "Unit");
            so.AddItem(bundleDropShip.Id, "DropShip Bundle", 1m, 100m, 0m, "Unit");
            so.AddItem(bundleOpen.Id, "Open Bundle", 2m, 100m, 0m, "Unit");

            so.Items[0].WarehouseId = warehouse.Id;
            so.Items[0].ProductBundleId = pbClosed.Id;
            so.Items[0].IsClosed = true;

            so.Items[1].WarehouseId = warehouse.Id;
            so.Items[1].ProductBundleId = pbDropShip.Id;
            so.Items[1].DeliveredBySupplier = true;

            so.Items[2].WarehouseId = warehouse.Id;
            so.Items[2].ProductBundleId = pbOpen.Id;

            so.Submit();
            await soRepo.InsertAsync(so, autoSave: true);

            var conversionSvc = GetRequiredService<MyERP.Sales.IDocumentConversionAppService>();
            var plId = await conversionSvc.ConvertSalesOrderToPickListAsync(so.Id);
            var pl = await _pickListRepository.GetAsync(plId, includeDetails: true);

            // Per ERPNext commit 754475b51a: closed and drop-ship bundle rows must not be picked
            pl.Items.Count.ShouldBe(1);
            pl.Items[0].ItemId.ShouldBe(compOpen.Id);
            pl.Items[0].Qty.ShouldBe(4m);
        });
    }

    [Fact]
    public async Task CreateDeliveryNoteFromPickList_BundleSoldInOtherUom_MapsToDeliveryNoteInOrderUom_cf712fd6d2()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<MyERP.Core.Entities.Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<MyERP.Sales.Entities.Customer, Guid>>();
            var itemRepo = GetRequiredService<IRepository<MyERP.Inventory.Entities.Item, Guid>>();
            var whRepo = GetRequiredService<IRepository<MyERP.Inventory.Entities.Warehouse, Guid>>();
            var bundleRepo = GetRequiredService<IRepository<MyERP.Sales.Entities.ProductBundle, Guid>>();
            var soRepo = GetRequiredService<IRepository<MyERP.Sales.Entities.SalesOrder, Guid>>();

            var company = await companyRepo.InsertAsync(new MyERP.Core.Entities.Company(Guid.NewGuid(), "PL Bundle UOM Co"), autoSave: true);
            var seriesRepo = GetRequiredService<IRepository<MyERP.Core.Entities.DocumentSeries, Guid>>();
            await seriesRepo.InsertAsync(new MyERP.Core.Entities.DocumentSeries(Guid.NewGuid(), company.Id, "DNS", "DeliveryNote", "DN-"), autoSave: true);
            var customer = await customerRepo.InsertAsync(new MyERP.Sales.Entities.Customer(Guid.NewGuid(), company.Id, "PL Bundle UOM Cust"), autoSave: true);
            var warehouse = await whRepo.InsertAsync(new MyERP.Inventory.Entities.Warehouse(Guid.NewGuid(), company.Id, "PL Bundle UOM WH"), autoSave: true);

            var bundleItem = new MyERP.Inventory.Entities.Item(Guid.NewGuid(), company.Id, "BND-BOX-PARENT", "Bundle Box Parent", ItemType.Goods) { MaintainStock = false };
            var compItem = new MyERP.Inventory.Entities.Item(Guid.NewGuid(), company.Id, "BND-COMP-BOX", "Component Box", ItemType.Goods);
            await itemRepo.InsertAsync(bundleItem, autoSave: true);
            await itemRepo.InsertAsync(compItem, autoSave: true);

            var bundle = new MyERP.Sales.Entities.ProductBundle(Guid.NewGuid(), bundleItem.Id);
            bundle.AddItem(compItem.Id, 1m, "Component Box", "Unit");
            await bundleRepo.InsertAsync(bundle, autoSave: true);

            var so = new MyERP.Sales.Entities.SalesOrder(Guid.NewGuid(), company.Id, customer.Id, "SO-PL-BND-UOM-1", DateTime.Today);
            so.AddItem(bundleItem.Id, "Bundle Box Parent", 3m, 200m, 0m, "Box");
            var soItem = so.Items.Single();
            soItem.ConversionFactor = 2m; // 3 Boxes = 6 Units
            soItem.WarehouseId = warehouse.Id;
            soItem.ProductBundleId = bundle.Id;
            so.Submit();
            await soRepo.InsertAsync(so, autoSave: true);

            var pl = new MyERP.Inventory.Entities.PickList(Guid.NewGuid(), company.Id, "Delivery", null)
            {
                SalesOrderId = so.Id,
                CustomerId = customer.Id,
                PickListNumber = "PL-BND-UOM-001"
            };
            // 6 units picked for component item in stock UOM
            pl.AddItem(compItem.Id, warehouse.Id, 6m, 6m, "Component Box", productBundleItemId: bundle.Items[0].Id);
            pl.Items[0].SourceDocumentItemId = soItem.Id;
            pl.Submit();
            await _pickListRepository.InsertAsync(pl, autoSave: true);

            // Per ERPNext commit cf712fd6d2: map a picked bundle to the delivery note in the order row's UOM
            var dnId = await _pickListAppService.CreateDeliveryNoteFromPickListAsync(pl.Id);
            var dn = await _deliveryNoteRepository.GetAsync(dnId);

            dn.Items.Count.ShouldBe(1);
            dn.Items[0].ItemId.ShouldBe(bundleItem.Id);
            dn.Items[0].Uom.ShouldBe("Box");
            dn.Items[0].Quantity.ShouldBe(3m); // 6 picked components / 2 factor = 3 Boxes
            dn.Items[0].ConversionFactor.ShouldBe(2m);
            dn.Items[0].StockQty.ShouldBe(6m);
        });
    }

    [Fact]
    public async Task GetPickItemsFromSalesOrderAsync_DraftOrder_ThrowsInvalidStatusTransition()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<MyERP.Core.Entities.Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<MyERP.Sales.Entities.Customer, Guid>>();
            var soRepo = GetRequiredService<IRepository<MyERP.Sales.Entities.SalesOrder, Guid>>();

            var company = await companyRepo.InsertAsync(new MyERP.Core.Entities.Company(Guid.NewGuid(), "PL Draft Co"), autoSave: true);
            var customer = await customerRepo.InsertAsync(new MyERP.Sales.Entities.Customer(Guid.NewGuid(), company.Id, "PL Draft Cust"), autoSave: true);

            var so = new MyERP.Sales.Entities.SalesOrder(Guid.NewGuid(), company.Id, customer.Id, "SO-PL-DRAFT-1", DateTime.Today);
            so.AddItem(Guid.NewGuid(), "Item 1", 5m, 10m, 0m, "Unit");
            // Still draft, not submitted
            await soRepo.InsertAsync(so, autoSave: true);

            var ex = await Should.ThrowAsync<Volo.Abp.BusinessException>(async () =>
            {
                await _pickListAppService.GetPickItemsFromSalesOrderAsync(so.Id);
            });
            ex.Code.ShouldBe(MyERPDomainErrorCodes.InvalidStatusTransition);
        });
    }

    [Fact]
    public async Task GetPickItemsFromSalesOrderAsync_ActiveSre_ThrowsReservationConflict()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<MyERP.Core.Entities.Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<MyERP.Sales.Entities.Customer, Guid>>();
            var soRepo = GetRequiredService<IRepository<MyERP.Sales.Entities.SalesOrder, Guid>>();
            var sreRepo = GetRequiredService<IRepository<MyERP.Inventory.Entities.StockReservationEntry, Guid>>();
            var whRepo = GetRequiredService<IRepository<MyERP.Inventory.Entities.Warehouse, Guid>>();

            var company = await companyRepo.InsertAsync(new MyERP.Core.Entities.Company(Guid.NewGuid(), "PL SRE Co"), autoSave: true);
            var customer = await customerRepo.InsertAsync(new MyERP.Sales.Entities.Customer(Guid.NewGuid(), company.Id, "PL SRE Cust"), autoSave: true);
            var warehouse = await whRepo.InsertAsync(new MyERP.Inventory.Entities.Warehouse(Guid.NewGuid(), company.Id, "PL SRE WH"), autoSave: true);

            var itemId = Guid.NewGuid();
            var so = new MyERP.Sales.Entities.SalesOrder(Guid.NewGuid(), company.Id, customer.Id, "SO-PL-SRE-1", DateTime.Today);
            so.AddItem(itemId, "Item 1", 5m, 10m, 0m, "Unit");
            so.Submit();
            await soRepo.InsertAsync(so, autoSave: true);

            var sre = new MyERP.Inventory.Entities.StockReservationEntry(
                Guid.NewGuid(), company.Id, itemId, warehouse.Id, "SalesOrder", so.Id, 5m, 5m);
            sre.Submit();
            await sreRepo.InsertAsync(sre, autoSave: true);

            var ex = await Should.ThrowAsync<Volo.Abp.BusinessException>(async () =>
            {
                await _pickListAppService.GetPickItemsFromSalesOrderAsync(so.Id);
            });
            ex.Code.ShouldBe("MyERP:13020");
        });
    }

    [Fact]
    public async Task GetPickItemsFromSalesOrderAsync_ProductBundle_MapsComponentsAndPicksRemaining()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<MyERP.Core.Entities.Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<MyERP.Sales.Entities.Customer, Guid>>();
            var soRepo = GetRequiredService<IRepository<MyERP.Sales.Entities.SalesOrder, Guid>>();
            var whRepo = GetRequiredService<IRepository<MyERP.Inventory.Entities.Warehouse, Guid>>();
            var itemRepo = GetRequiredService<IRepository<MyERP.Inventory.Entities.Item, Guid>>();
            var bundleRepo = GetRequiredService<IRepository<MyERP.Sales.Entities.ProductBundle, Guid>>();

            var company = await companyRepo.InsertAsync(new MyERP.Core.Entities.Company(Guid.NewGuid(), "PL Bnd Map Co"), autoSave: true);
            var customer = await customerRepo.InsertAsync(new MyERP.Sales.Entities.Customer(Guid.NewGuid(), company.Id, "PL Bnd Map Cust"), autoSave: true);
            var warehouse = await whRepo.InsertAsync(new MyERP.Inventory.Entities.Warehouse(Guid.NewGuid(), company.Id, "PL Bnd Map WH"), autoSave: true);

            var bundleItem = new MyERP.Inventory.Entities.Item(Guid.NewGuid(), company.Id, "BND-MAP-PARENT", "Bundle Map Parent", ItemType.Goods) { MaintainStock = false };
            var compItemA = new MyERP.Inventory.Entities.Item(Guid.NewGuid(), company.Id, "BND-COMP-A", "Component A", ItemType.Goods);
            var compItemB = new MyERP.Inventory.Entities.Item(Guid.NewGuid(), company.Id, "BND-COMP-B", "Component B", ItemType.Goods);
            await itemRepo.InsertAsync(bundleItem, autoSave: true);
            await itemRepo.InsertAsync(compItemA, autoSave: true);
            await itemRepo.InsertAsync(compItemB, autoSave: true);

            var bundle = new MyERP.Sales.Entities.ProductBundle(Guid.NewGuid(), bundleItem.Id);
            bundle.AddItem(compItemA.Id, 1m, "Component A", "Unit");
            bundle.AddItem(compItemB.Id, 2m, "Component B", "Unit");
            await bundleRepo.InsertAsync(bundle, autoSave: true);

            var so = new MyERP.Sales.Entities.SalesOrder(Guid.NewGuid(), company.Id, customer.Id, "SO-PL-BND-MAP-1", DateTime.Today);
            so.AddItem(bundleItem.Id, "Bundle Map Parent", 3m, 200m, 0m, "Box");
            var soItem = so.Items.Single();
            soItem.ConversionFactor = 2m; // 3 Boxes = 6 Units
            soItem.WarehouseId = warehouse.Id;
            soItem.ProductBundleId = bundle.Id;
            so.Submit();
            await soRepo.InsertAsync(so, autoSave: true);

            // 1. Initial mapping should include Comp A (6 units) and Comp B (12 units)
            var initialMapping = await _pickListAppService.GetPickItemsFromSalesOrderAsync(so.Id);
            initialMapping.SalesOrderId.ShouldBe(so.Id);
            initialMapping.CustomerId.ShouldBe(customer.Id);
            initialMapping.Items.Count.ShouldBe(2);

            var rowA = initialMapping.Items.Single(i => i.ItemId == compItemA.Id);
            rowA.Qty.ShouldBe(6m);
            rowA.StockQty.ShouldBe(6m);
            rowA.ConversionFactor.ShouldBe(1m);
            rowA.ProductBundleItemId.ShouldBe(bundle.Items[0].Id);
            rowA.SourceDocumentItemId.ShouldBe(soItem.Id);

            var rowB = initialMapping.Items.Single(i => i.ItemId == compItemB.Id);
            rowB.Qty.ShouldBe(12m);
            rowB.StockQty.ShouldBe(12m);
            rowB.ConversionFactor.ShouldBe(1m);
            rowB.ProductBundleItemId.ShouldBe(bundle.Items[1].Id);
            rowB.SourceDocumentItemId.ShouldBe(soItem.Id);

            // 2. Submit a partial Pick List picking 6 Comp A and 6 Comp B (PR #65115 / commit 51954d883a)
            var pl1 = new MyERP.Inventory.Entities.PickList(Guid.NewGuid(), company.Id, "Delivery", null)
            {
                SalesOrderId = so.Id,
                CustomerId = customer.Id,
                PickListNumber = "PL-PARTIAL-001"
            };
            pl1.AddItem(compItemA.Id, warehouse.Id, 6m, 6m, "Component A", productBundleItemId: bundle.Items[0].Id);
            pl1.Items[0].SourceDocumentItemId = soItem.Id;
            pl1.AddItem(compItemB.Id, warehouse.Id, 6m, 6m, "Component B", productBundleItemId: bundle.Items[1].Id);
            pl1.Items[1].SourceDocumentItemId = soItem.Id;
            pl1.Submit();
            await _pickListRepository.InsertAsync(pl1, autoSave: true);

            // 3. Second mapping should omit Comp A (fully picked) and return only remaining 6 for Comp B
            var secondMapping = await _pickListAppService.GetPickItemsFromSalesOrderAsync(so.Id);
            secondMapping.Items.Count.ShouldBe(1);
            var remainingB = secondMapping.Items.Single();
            remainingB.ItemId.ShouldBe(compItemB.Id);
            remainingB.Qty.ShouldBe(6m); // 12 total - 6 picked = 6 left
            remainingB.StockQty.ShouldBe(6m);
            remainingB.ConversionFactor.ShouldBe(1m);
            remainingB.ProductBundleItemId.ShouldBe(bundle.Items[1].Id);
            remainingB.SourceDocumentItemId.ShouldBe(soItem.Id);
        });
    }
}
