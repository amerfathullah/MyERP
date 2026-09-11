using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing;
using MyERP.Manufacturing.DomainServices;
using MyERP.Manufacturing.Entities;
using MyERP.Purchasing.DomainServices;
using MyERP.Purchasing.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.Manufacturing;

public class UpstreamBatch58924To58949Tests
{
    // --- PR #58924 / commit 28bd1332de: Partial returns on non-stock purchase invoice ---

    [Fact]
    public async Task PurchaseInvoice_NonStock_PartialReturns_IgnoreReceivedQty()
    {
        var supplierRepo = Substitute.For<IRepository<Supplier, Guid>>();
        var piRepo = Substitute.For<IRepository<PurchaseInvoice, Guid>>();
        var poRepo = Substitute.For<IRepository<PurchaseOrder, Guid>>();

        var manager = new PurchaseInvoiceManager(supplierRepo, piRepo, poRepo);

        var origId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();

        var originalInvoice = new PurchaseInvoice(origId, companyId, supplierId, "PINV-ORIG-01", DateTime.UtcNow)
        {
            UpdateStock = false,
            ExchangeRate = 1m
        };
        originalInvoice.AddItem(itemId, "Service Item", 10m, 50m, 0m);
        originalInvoice.Submit();

        piRepo.GetAsync(origId).Returns(originalInvoice);

        // First partial debit note: returns 4 of 10
        var return1Id = Guid.NewGuid();
        var return1 = new PurchaseInvoice(return1Id, companyId, supplierId, "DN-01", DateTime.UtcNow)
        {
            IsReturn = true,
            ReturnAgainstId = origId,
            UpdateStock = false,
            ExchangeRate = 1m
        };
        return1.AddItem(itemId, "Service Item", -4m, 50m, 0m);
        return1.Submit();

        // Second partial debit note: returns 6 of remaining (total 10 of 10)
        var return2Id = Guid.NewGuid();
        var return2 = new PurchaseInvoice(return2Id, companyId, supplierId, "DN-02", DateTime.UtcNow)
        {
            IsReturn = true,
            ReturnAgainstId = origId,
            UpdateStock = false,
            ExchangeRate = 1m
        };
        return2.AddItem(itemId, "Service Item", -6m, 50m, 0m);

        piRepo.GetQueryableAsync().Returns(new List<PurchaseInvoice> { originalInvoice, return1, return2 }.AsQueryable());

        // Both returns must succeed without being blocked by stale received qty
        await Should.NotThrowAsync(() => manager.ValidateReturnAsync(return2));
    }

    [Fact]
    public async Task PurchaseInvoice_NonStock_ReturnExceedingOriginalQty_Throws()
    {
        var supplierRepo = Substitute.For<IRepository<Supplier, Guid>>();
        var piRepo = Substitute.For<IRepository<PurchaseInvoice, Guid>>();
        var poRepo = Substitute.For<IRepository<PurchaseOrder, Guid>>();

        var manager = new PurchaseInvoiceManager(supplierRepo, piRepo, poRepo);

        var origId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();

        var originalInvoice = new PurchaseInvoice(origId, companyId, supplierId, "PINV-ORIG-02", DateTime.UtcNow)
        {
            UpdateStock = false,
            ExchangeRate = 1m
        };
        originalInvoice.AddItem(itemId, "Service Item", 10m, 50m, 0m);
        originalInvoice.Submit();

        piRepo.GetAsync(origId).Returns(originalInvoice);

        // Prior return for 6
        var return1 = new PurchaseInvoice(Guid.NewGuid(), companyId, supplierId, "DN-01", DateTime.UtcNow)
        {
            IsReturn = true,
            ReturnAgainstId = origId,
            UpdateStock = false,
            ExchangeRate = 1m
        };
        return1.AddItem(itemId, "Service Item", -6m, 50m, 0m);
        return1.Submit();

        // Second return for 5 (total 11 > 10) -> must throw
        var return2 = new PurchaseInvoice(Guid.NewGuid(), companyId, supplierId, "DN-02", DateTime.UtcNow)
        {
            IsReturn = true,
            ReturnAgainstId = origId,
            UpdateStock = false,
            ExchangeRate = 1m
        };
        return2.AddItem(itemId, "Service Item", -5m, 50m, 0m);

        piRepo.GetQueryableAsync().Returns(new List<PurchaseInvoice> { originalInvoice, return1, return2 }.AsQueryable());

        var ex = await Should.ThrowAsync<BusinessException>(() => manager.ValidateReturnAsync(return2));
        ex.Code.ShouldBe("MyERP:08004");
    }

    // --- PR #58927 / commit 3be0c7801a: Handle empty raw materials in Workstation / Job Card ---

    [Fact]
    public async Task JobCardManager_GetRawMaterialsAsync_WithoutItems_Throws()
    {
        var jcRepo = Substitute.For<IRepository<JobCard, Guid>>();
        var wsRepo = Substitute.For<IRepository<Workstation, Guid>>();
        var woRepo = Substitute.For<IRepository<WorkOrder, Guid>>();

        var manager = new JobCardManager(jcRepo, wsRepo);

        var woId = Guid.NewGuid();
        var wo = new WorkOrder(woId, Guid.NewGuid(), "WO-NO-ITEMS", Guid.NewGuid(), Guid.NewGuid(), 10m);
        // wo has no RequiredItems
        woRepo.FindAsync(woId).Returns(wo);

        var jc = new JobCard(Guid.NewGuid(), Guid.NewGuid(), woId, Guid.NewGuid(), 10m, 1);

        var ex = await Should.ThrowAsync<BusinessException>(() => manager.GetRawMaterialsAsync(jc, woRepo));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        ex.Data["detail"].ShouldBe("This Job Card has no raw materials to transfer.");
    }

    [Fact]
    public async Task JobCardManager_GetRawMaterialsAsync_WithMatchingItems_ReturnsItems()
    {
        var jcRepo = Substitute.For<IRepository<JobCard, Guid>>();
        var wsRepo = Substitute.For<IRepository<Workstation, Guid>>();
        var woRepo = Substitute.For<IRepository<WorkOrder, Guid>>();

        var manager = new JobCardManager(jcRepo, wsRepo);

        var woId = Guid.NewGuid();
        var opRowId = Guid.NewGuid();
        var wo = new WorkOrder(woId, Guid.NewGuid(), "WO-WITH-ITEMS", Guid.NewGuid(), Guid.NewGuid(), 10m);
        wo.RequiredItems.Add(new WorkOrderItem(Guid.NewGuid(), woId, Guid.NewGuid(), "Raw Item 1", 20m)
        {
            BomOperationId = opRowId
        });
        wo.RequiredItems.Add(new WorkOrderItem(Guid.NewGuid(), woId, Guid.NewGuid(), "Raw Item 2", 15m)
        {
            BomOperationId = Guid.NewGuid() // different operation
        });
        woRepo.FindAsync(woId).Returns(wo);

        var jc = new JobCard(Guid.NewGuid(), Guid.NewGuid(), woId, Guid.NewGuid(), 10m, 1)
        {
            BomOperationId = opRowId
        };

        var items = await manager.GetRawMaterialsAsync(jc, woRepo);
        items.Count.ShouldBe(1);
        items[0].ItemName.ShouldBe("Raw Item 1");
    }

    // --- PR #58939 / commit 33a066d568: Handle empty/null BOM cost allocation & clamp FG percentage ---

    [Fact]
    public void BillOfMaterials_FgCostAllocationPercentage_HandlesNoSecondaryAllocation()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-001", Guid.NewGuid());
        // No secondary items added
        bom.FgCostAllocationPercentage.ShouldBe(100m);
    }

    [Fact]
    public void BillOfMaterials_FgCostAllocationPercentage_ClampsWhenSecondaryExceeds100()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-002", Guid.NewGuid());
        bom.SecondaryItems.Add(new BomSecondaryItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), SecondaryItemType.CoProduct, 5m)
        {
            CostAllocationPercentage = 70m
        });
        bom.SecondaryItems.Add(new BomSecondaryItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), SecondaryItemType.ByProduct, 2m)
        {
            CostAllocationPercentage = 50m // total 120%
        });

        // Clamped at 0, not negative
        bom.FgCostAllocationPercentage.ShouldBe(0m);
    }

    [Fact]
    public void BillOfMaterials_FgCostAllocationPercentage_CalculatesCorrectly()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-003", Guid.NewGuid());
        bom.SecondaryItems.Add(new BomSecondaryItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), SecondaryItemType.CoProduct, 5m)
        {
            CostAllocationPercentage = 25m
        });

        bom.FgCostAllocationPercentage.ShouldBe(75m);
    }

    // --- PR #58925 / commit f130c64530: Packing slip item fetches item name if empty ---

    [Fact]
    public async Task PackingSlipAppService_CreateAsync_FetchesItemName_WhenDescriptionEmpty()
    {
        var psRepo = Substitute.For<IRepository<PackingSlip, Guid>>();
        var dnRepo = Substitute.For<IRepository<DeliveryNote, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();

        var appService = new PackingSlipAppService(psRepo, dnRepo, itemRepo);
        var lazyProvider = Substitute.For<Volo.Abp.DependencyInjection.IAbpLazyServiceProvider>();
        var guidGen = Substitute.For<Volo.Abp.Guids.IGuidGenerator>();
        guidGen.Create().Returns(_ => Guid.NewGuid());
        lazyProvider.LazyGetService<Volo.Abp.Guids.IGuidGenerator>().Returns(guidGen);
        lazyProvider.LazyGetRequiredService<Volo.Abp.Guids.IGuidGenerator>().Returns(guidGen);
        lazyProvider.LazyGetService(typeof(Volo.Abp.Guids.IGuidGenerator)).Returns(guidGen);
        lazyProvider.LazyGetRequiredService(typeof(Volo.Abp.Guids.IGuidGenerator)).Returns(guidGen);
        appService.LazyServiceProvider = lazyProvider;

        var itemId = Guid.NewGuid();
        var dnId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var dnItemId = Guid.NewGuid();

        var dn = new DeliveryNote(dnId, companyId, customerId, warehouseId, "DN-001", DateTime.UtcNow);
        dn.AddItem(itemId, "Widget Alpha", 10m, 100m, 0m);
        // Leave in Draft status as Packing Slip requires Draft DN
        dnRepo.GetAsync(dnId).Returns(dn);
        dnRepo.FindAsync(dnId).Returns(dn);

        var item = new Item(itemId, companyId, "ITEM-WIDGET", "Widget Alpha Master", ItemType.Goods);
        itemRepo.FindAsync(itemId).Returns(item);

        psRepo.GetQueryableAsync().Returns(new List<PackingSlip>().AsQueryable());

        PackingSlip? inserted = null;
        await psRepo.InsertAsync(Arg.Do<PackingSlip>(ps => inserted = ps));

        var input = new CreatePackingSlipDto
        {
            CompanyId = companyId,
            DeliveryNoteId = dnId,
            FromCaseNo = 1,
            ToCaseNo = 1,
            Items = new List<CreatePackingSlipItemDto>
            {
                new()
                {
                    ItemId = itemId,
                    Qty = 5m,
                    NetWeight = 2.5m,
                    Description = null, // empty description -> must fetch ItemName
                    DeliveryNoteItemId = dn.Items.First().Id
                }
            }
        };

        var result = await appService.CreateAsync(input);

        inserted.ShouldNotBeNull();
        inserted.Items.Count.ShouldBe(1);
        inserted.Items.First().Description.ShouldBe("Widget Alpha Master");
        result.Items.First().Description.ShouldBe("Widget Alpha Master");
    }
}
