using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Purchasing.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Purchasing;

public class SupplierQuotationComparisonTests
{
    [Fact]
    public async Task GetComparisonByRfqAsync_CalculatesOrderStatusAndFilters()
    {
        var sqRepo = Substitute.For<IRepository<SupplierQuotation, Guid>>();
        var supplierRepo = Substitute.For<IRepository<Supplier, Guid>>();
        var rfqRepo = Substitute.For<IRepository<RequestForQuotation, Guid>>();

        var service = new SupplierQuotationComparisonAppService(sqRepo, supplierRepo, rfqRepo);

        var rfqId = Guid.NewGuid();
        var supplier1Id = Guid.NewGuid();
        var supplier2Id = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var sq1 = new SupplierQuotation(Guid.NewGuid(), Guid.NewGuid(), supplier1Id, DateTime.UtcNow)
        {
            RequestForQuotationId = rfqId,
            SupplierName = "Supplier 1"
        };
        sq1.AddItem(itemId, 10, 100m, "Widget");
        sq1.Submit();

        var sq2 = new SupplierQuotation(Guid.NewGuid(), Guid.NewGuid(), supplier2Id, DateTime.UtcNow)
        {
            RequestForQuotationId = rfqId,
            SupplierName = "Supplier 2"
        };
        sq2.AddItem(itemId, 10, 90m, "Widget");
        sq2.Submit();
        sq2.UpdateOrderedQty(itemId, 10m); // Ordered!

        var sqList = new List<SupplierQuotation> { sq1, sq2 };
        var supplierList = new List<Supplier>
        {
            new(supplier1Id, Guid.NewGuid(), "Supplier 1"),
            new(supplier2Id, Guid.NewGuid(), "Supplier 2")
        };

        sqRepo.GetQueryableAsync().Returns(Task.FromResult(sqList.AsQueryable()));
        supplierRepo.GetQueryableAsync().Returns(Task.FromResult(supplierList.AsQueryable()));

        // All quotations without orderStatus filter
        var resultAll = await service.GetComparisonByRfqAsync(rfqId);
        resultAll.Suppliers.Count.ShouldBe(2);
        resultAll.Suppliers.First(s => s.SupplierId == supplier1Id).OrderStatus.ShouldBe("Not Ordered");
        resultAll.Suppliers.First(s => s.SupplierId == supplier2Id).OrderStatus.ShouldBe("Ordered");

        // Filter: only Not Ordered
        var resultNotOrdered = await service.GetComparisonByRfqAsync(rfqId, orderStatus: "Not Ordered");
        resultNotOrdered.Suppliers.Count.ShouldBe(1);
        resultNotOrdered.Suppliers[0].SupplierId.ShouldBe(supplier1Id);

        // Filter: only Ordered
        var resultOrdered = await service.GetComparisonByRfqAsync(rfqId, orderStatus: "Ordered");
        resultOrdered.Suppliers.Count.ShouldBe(1);
        resultOrdered.Suppliers[0].SupplierId.ShouldBe(supplier2Id);
    }

    [Fact]
    public async Task GetComparisonByIdsAsync_FiltersByStatus()
    {
        var sqRepo = Substitute.For<IRepository<SupplierQuotation, Guid>>();
        var supplierRepo = Substitute.For<IRepository<Supplier, Guid>>();
        var rfqRepo = Substitute.For<IRepository<RequestForQuotation, Guid>>();

        var service = new SupplierQuotationComparisonAppService(sqRepo, supplierRepo, rfqRepo);

        var supplier1Id = Guid.NewGuid();
        var supplier2Id = Guid.NewGuid();
        var supplier3Id = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var sq1 = new SupplierQuotation(Guid.NewGuid(), Guid.NewGuid(), supplier1Id, DateTime.UtcNow)
        {
            SupplierName = "Supplier 1"
        };
        sq1.AddItem(itemId, 10, 100m, "Widget");
        sq1.Submit();

        var sq2 = new SupplierQuotation(Guid.NewGuid(), Guid.NewGuid(), supplier2Id, DateTime.UtcNow)
        {
            SupplierName = "Supplier 2"
        };
        sq2.AddItem(itemId, 10, 90m, "Widget");
        sq2.Submit();

        var sq3 = new SupplierQuotation(Guid.NewGuid(), Guid.NewGuid(), supplier3Id, DateTime.UtcNow)
        {
            SupplierName = "Supplier 3"
        };
        sq3.AddItem(itemId, 10, 80m, "Widget");

        var sqList = new List<SupplierQuotation> { sq1, sq2, sq3 };
        var supplierList = new List<Supplier>
        {
            new(supplier1Id, Guid.NewGuid(), "Supplier 1"),
            new(supplier2Id, Guid.NewGuid(), "Supplier 2"),
            new(supplier3Id, Guid.NewGuid(), "Supplier 3")
        };

        sqRepo.GetQueryableAsync().Returns(Task.FromResult(sqList.AsQueryable()));
        supplierRepo.GetQueryableAsync().Returns(Task.FromResult(supplierList.AsQueryable()));

        var allIds = sqList.Select(x => x.Id).ToList();

        var resultAll = await service.GetComparisonByIdsAsync(allIds);
        resultAll.Suppliers.Count.ShouldBe(3);

        var resultSubmitted = await service.GetComparisonByIdsAsync(allIds, status: "Submitted");
        resultSubmitted.Suppliers.Count.ShouldBe(2);
        resultSubmitted.Suppliers.ShouldNotContain(s => s.SupplierId == supplier3Id);

        await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
            service.GetComparisonByIdsAsync(allIds, status: "Draft"));
    }
}
