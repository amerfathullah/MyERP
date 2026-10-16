using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Purchasing;

public class SubcontractedItemToBeReceivedAppServiceTests
{
    private readonly IRepository<SubcontractingOrder, Guid> _scoRepo;
    private readonly IRepository<Supplier, Guid> _supplierRepo;
    private readonly IRepository<Item, Guid> _itemRepo;
    private readonly SubcontractedItemToBeReceivedAppService _service;

    public SubcontractedItemToBeReceivedAppServiceTests()
    {
        _scoRepo = Substitute.For<IRepository<SubcontractingOrder, Guid>>();
        _supplierRepo = Substitute.For<IRepository<Supplier, Guid>>();
        _itemRepo = Substitute.For<IRepository<Item, Guid>>();

        _service = new SubcontractedItemToBeReceivedAppService(
            _scoRepo,
            _supplierRepo,
            _itemRepo);
    }

    [Fact]
    public async Task GetReportAsync_Throws_WhenFromDateGreaterThanToDate()
    {
        var input = new SubcontractedItemToBeReceivedFilterDto
        {
            CompanyId = Guid.NewGuid(),
            FromDate = new DateTime(2026, 5, 10),
            ToDate = new DateTime(2026, 5, 1)
        };

        var ex = await Should.ThrowAsync<BusinessException>(() => _service.GetReportAsync(input));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task GetReportAsync_ReturnsEmpty_WhenNoMatchingOrders()
    {
        var companyId = Guid.NewGuid();
        _scoRepo.WithDetailsAsync(Arg.Any<Expression<Func<SubcontractingOrder, object>>[]>())
            .Returns(Task.FromResult(new List<SubcontractingOrder>().AsQueryable()));

        var input = new SubcontractedItemToBeReceivedFilterDto
        {
            CompanyId = companyId
        };

        var result = await _service.GetReportAsync(input);

        result.Rows.ShouldBeEmpty();
        result.Summary.TotalOrders.ShouldBe(0);
        result.Summary.TotalPendingQty.ShouldBe(0);
    }

    [Fact]
    public async Task GetReportAsync_FiltersCompletedAndClosed_AndCalculatesPendingQtyCorrectly()
    {
        var companyId = Guid.NewGuid();
        var supplier1Id = Guid.NewGuid();
        var supplier2Id = Guid.NewGuid();
        var item1Id = Guid.NewGuid();
        var item2Id = Guid.NewGuid();
        var today = new DateTime(2026, 5, 15);

        // Order 1: Submitted / Open with 2 items (one partial received, one fully received)
        var sco1 = new SubcontractingOrder(Guid.NewGuid(), companyId, "SCO-2026-001", today, supplier1Id);
        sco1.AddItem(new SubcontractingOrderItem(Guid.NewGuid(), sco1.Id, item1Id, "Widget A", qty: 10m, rate: 50m)
        {
            ReceivedQty = 4m // Pending: 6
        });
        sco1.AddItem(new SubcontractingOrderItem(Guid.NewGuid(), sco1.Id, item2Id, "Widget B", qty: 5m, rate: 20m)
        {
            ReceivedQty = 5m // Pending: 0 -> should NOT appear
        });
        sco1.Submit();

        // Order 2: Draft order -> should be ignored
        var sco2 = new SubcontractingOrder(Guid.NewGuid(), companyId, "SCO-DRAFT", today, supplier1Id);
        sco2.AddItem(new SubcontractingOrderItem(Guid.NewGuid(), sco2.Id, item1Id, "Widget A", qty: 8m, rate: 50m));

        // Order 3: Closed order -> should be ignored
        var sco3 = new SubcontractingOrder(Guid.NewGuid(), companyId, "SCO-CLOSED", today, supplier2Id);
        sco3.AddItem(new SubcontractingOrderItem(Guid.NewGuid(), sco3.Id, item2Id, "Widget B", qty: 20m, rate: 20m)
        {
            ReceivedQty = 10m
        });
        sco3.Submit();
        sco3.Close();

        // Order 4: PartiallyReceived order for supplier 2
        var sco4 = new SubcontractingOrder(Guid.NewGuid(), companyId, "SCO-2026-004", today, supplier2Id);
        sco4.AddItem(new SubcontractingOrderItem(Guid.NewGuid(), sco4.Id, item2Id, "Widget B", qty: 15m, rate: 20m)
        {
            ReceivedQty = 3m // Pending: 12
        });
        sco4.Submit();
        sco4.MarkPartiallyReceived();

        var orders = new List<SubcontractingOrder> { sco1, sco2, sco3, sco4 };

        _scoRepo.WithDetailsAsync(Arg.Any<Expression<Func<SubcontractingOrder, object>>[]>())
            .Returns(Task.FromResult(orders.AsQueryable()));

        var supplier1 = new Supplier(supplier1Id, companyId, "Supplier Alpha");
        var supplier2 = new Supplier(supplier2Id, companyId, "Supplier Beta");
        _supplierRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Supplier> { supplier1, supplier2 }.AsQueryable()));

        var item1 = new Item(item1Id, companyId, "ITEM-WIDGET-A", "Widget A", ItemType.Goods);
        var item2 = new Item(item2Id, companyId, "ITEM-WIDGET-B", "Widget B", ItemType.Goods);
        _itemRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Item> { item1, item2 }.AsQueryable()));

        var input = new SubcontractedItemToBeReceivedFilterDto
        {
            CompanyId = companyId
        };

        var result = await _service.GetReportAsync(input);

        result.Rows.Count.ShouldBe(2);

        var row1 = result.Rows.First(r => r.SubcontractOrderNumber == "SCO-2026-001");
        row1.SupplierName.ShouldBe("Supplier Alpha");
        row1.FgItemCode.ShouldBe("ITEM-WIDGET-A");
        row1.ItemName.ShouldBe("Widget A");
        row1.RequiredQty.ShouldBe(10m);
        row1.ReceivedQty.ShouldBe(4m);
        row1.PendingQty.ShouldBe(6m);

        var row2 = result.Rows.First(r => r.SubcontractOrderNumber == "SCO-2026-004");
        row2.SupplierName.ShouldBe("Supplier Beta");
        row2.FgItemCode.ShouldBe("ITEM-WIDGET-B");
        row2.ItemName.ShouldBe("Widget B");
        row2.RequiredQty.ShouldBe(15m);
        row2.ReceivedQty.ShouldBe(3m);
        row2.PendingQty.ShouldBe(12m);

        result.Summary.TotalOrders.ShouldBe(2);
        result.Summary.TotalRequiredQty.ShouldBe(25m);
        result.Summary.TotalReceivedQty.ShouldBe(7m);
        result.Summary.TotalPendingQty.ShouldBe(18m);
    }
}
