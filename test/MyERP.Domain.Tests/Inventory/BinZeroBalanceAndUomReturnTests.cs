using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.DomainServices;
using MyERP.Purchasing.Entities;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Inventory;

public class BinZeroBalanceAndUomReturnTests
{
    [Fact]
    public void Bin_UpdateActualQty_WhenZero_ResetsStockValueAndValuationRate()
    {
        var bin = new Bin(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        bin.UpdateActualQty(10m, 500m);
        bin.ActualQty.ShouldBe(10m);
        bin.StockValue.ShouldBe(500m);
        bin.ValuationRate.ShouldBe(50m);

        bin.UpdateActualQty(0m, 50m);
        bin.ActualQty.ShouldBe(0m);
        bin.StockValue.ShouldBe(0m);
        bin.ValuationRate.ShouldBe(0m);
    }

    [Fact]
    public void Bin_ApplyStockMovement_WhenReachingZero_ResetsStockValueAndValuationRate()
    {
        var bin = new Bin(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        bin.ApplyStockMovement(10m, 500m);
        bin.ActualQty.ShouldBe(10m);
        bin.StockValue.ShouldBe(500m);
        bin.ValuationRate.ShouldBe(50m);

        bin.ApplyStockMovement(-10m, -499.99m);
        bin.ActualQty.ShouldBe(0m);
        bin.StockValue.ShouldBe(0m);
        bin.ValuationRate.ShouldBe(0m);
    }

    [Fact]
    public async Task SalesInvoiceManager_ReturnDifferentUom_ValidatesStockQuantityBounds()
    {
        var invoiceRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        var orderRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var manager = new SalesInvoiceManager(invoiceRepo, orderRepo, itemRepo);

        var originalId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var original = new SalesInvoice(originalId, companyId, customerId, "INV-001", DateTime.UtcNow);
        original.AddItem(itemId, "Bulk Item", 48m, 10m, 0m, "Nos");
        original.Items[0].ConversionFactor = 0.5m; // 48 Nos = 24 Kg in stock
        original.Submit();

        invoiceRepo.GetAsync(originalId).Returns(original);

        // First return: -24 Nos (12 Kg) -> should succeed
        var return1 = new SalesInvoice(Guid.NewGuid(), companyId, customerId, "RET-001", DateTime.UtcNow);
        return1.IsReturn = true;
        return1.ReturnAgainstId = originalId;
        return1.AddItem(itemId, "Bulk Item", -24m, 10m, 0m, "Nos");
        return1.Items[0].ConversionFactor = 0.5m;

        invoiceRepo.GetQueryableAsync().Returns(new List<SalesInvoice> { original }.AsQueryable());
        await Should.NotThrowAsync(() => manager.ValidateReturnAsync(return1));

        // Second return: -25 Nos (12.5 Kg) when 12 Kg already returned -> 24.5 Kg > 24 Kg -> should throw
        return1.Submit();
        var return2 = new SalesInvoice(Guid.NewGuid(), companyId, customerId, "RET-002", DateTime.UtcNow);
        return2.IsReturn = true;
        return2.ReturnAgainstId = originalId;
        return2.AddItem(itemId, "Bulk Item", -25m, 10m, 0m, "Nos");
        return2.Items[0].ConversionFactor = 0.5m;

        invoiceRepo.GetQueryableAsync().Returns(new List<SalesInvoice> { original, return1 }.AsQueryable());
        var ex = await Should.ThrowAsync<BusinessException>(() => manager.ValidateReturnAsync(return2));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ReturnQtyExceedsOriginal);
    }

    [Fact]
    public async Task DeliveryNoteManager_ReturnDifferentUom_ValidatesStockQuantityBounds()
    {
        var dnRepo = Substitute.For<IRepository<DeliveryNote, Guid>>();
        var orderRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
        var companyRepo = Substitute.For<IRepository<Company, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var manager = new DeliveryNoteManager(dnRepo, orderRepo, companyRepo, itemRepo);

        var originalId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var original = new DeliveryNote(originalId, companyId, customerId, Guid.NewGuid(), "DN-001", DateTime.UtcNow);
        original.AddItem(itemId, "Bulk Item", 10m, 20m, 0m, "Box");
        original.Items[0].ConversionFactor = 12m; // 10 Box = 120 Units
        original.Submit();

        dnRepo.GetAsync(originalId).Returns(original);

        // Return -60 Units (conversion factor 1m) -> 60 Units returned
        var return1 = new DeliveryNote(Guid.NewGuid(), companyId, customerId, Guid.NewGuid(), "RET-DN-001", DateTime.UtcNow);
        return1.IsReturn = true;
        return1.ReturnAgainstId = originalId;
        return1.AddItem(itemId, "Bulk Item", -60m, 20m, 0m, "Unit");
        return1.Items[0].ConversionFactor = 1m;

        dnRepo.GetQueryableAsync().Returns(new List<DeliveryNote> { original }.AsQueryable());
        await Should.NotThrowAsync(() => manager.ValidateReturnAsync(return1));

        // Return -70 Units when 60 already returned -> 130 > 120 -> should throw
        return1.Submit();
        var return2 = new DeliveryNote(Guid.NewGuid(), companyId, customerId, Guid.NewGuid(), "RET-DN-002", DateTime.UtcNow);
        return2.IsReturn = true;
        return2.ReturnAgainstId = originalId;
        return2.AddItem(itemId, "Bulk Item", -70m, 20m, 0m, "Unit");
        return2.Items[0].ConversionFactor = 1m;

        dnRepo.GetQueryableAsync().Returns(new List<DeliveryNote> { original, return1 }.AsQueryable());
        var ex = await Should.ThrowAsync<BusinessException>(() => manager.ValidateReturnAsync(return2));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ReturnQtyExceedsOriginal);
    }
}
