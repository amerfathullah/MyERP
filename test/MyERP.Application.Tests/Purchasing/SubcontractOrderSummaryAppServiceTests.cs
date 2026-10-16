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

public class SubcontractOrderSummaryAppServiceTests
{
    private readonly IRepository<SubcontractingOrder, Guid> _scoRepo;
    private readonly IRepository<Supplier, Guid> _supplierRepo;
    private readonly IRepository<Item, Guid> _itemRepo;
    private readonly SubcontractOrderSummaryAppService _service;

    public SubcontractOrderSummaryAppServiceTests()
    {
        _scoRepo = Substitute.For<IRepository<SubcontractingOrder, Guid>>();
        _supplierRepo = Substitute.For<IRepository<Supplier, Guid>>();
        _itemRepo = Substitute.For<IRepository<Item, Guid>>();

        _service = new SubcontractOrderSummaryAppService(
            _scoRepo,
            _supplierRepo,
            _itemRepo);
    }

    [Fact]
    public async Task GetReportAsync_Throws_WhenFromDateGreaterThanToDate()
    {
        var input = new SubcontractOrderSummaryFilterDto
        {
            CompanyId = Guid.NewGuid(),
            FromDate = new DateTime(2026, 6, 10),
            ToDate = new DateTime(2026, 6, 1)
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

        var input = new SubcontractOrderSummaryFilterDto
        {
            CompanyId = companyId
        };

        var result = await _service.GetReportAsync(input);

        result.Rows.ShouldBeEmpty();
        result.TotalOrders.ShouldBe(0);
        result.TotalOrderQty.ShouldBe(0);
    }

    [Fact]
    public async Task GetReportAsync_ReturnsIntegratedSummaryOfFgAndRm()
    {
        var companyId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var fgItemId = Guid.NewGuid();
        var rmItemId = Guid.NewGuid();
        var today = new DateTime(2026, 6, 15);

        var sco = new SubcontractingOrder(Guid.NewGuid(), companyId, "SCO-SUM-001", today, supplierId);
        sco.AddItem(new SubcontractingOrderItem(Guid.NewGuid(), sco.Id, fgItemId, "Custom Motor", qty: 10m, rate: 100m)
        {
            ReceivedQty = 6m
        });
        sco.AddSuppliedItem(new SubcontractingOrderSuppliedItem(Guid.NewGuid(), sco.Id, rmItemId, "Copper Wire", requiredQty: 50m)
        {
            TransferredQty = 40m,
            ConsumedQty = 30m
        });
        sco.Submit();

        var orders = new List<SubcontractingOrder> { sco }.AsQueryable();
        _scoRepo.WithDetailsAsync(Arg.Any<Expression<Func<SubcontractingOrder, object>>[]>())
            .Returns(Task.FromResult(orders));

        var suppliers = new List<Supplier>
        {
            new Supplier(supplierId, companyId, "Motor Subcontractor Inc")
        }.AsQueryable();
        _supplierRepo.GetQueryableAsync().Returns(Task.FromResult(suppliers));

        var items = new List<Item>
        {
            new Item(fgItemId, companyId, "FG-MOTOR", "Custom Motor", ItemType.Goods),
            new Item(rmItemId, companyId, "RM-WIRE", "Copper Wire", ItemType.Goods)
        }.AsQueryable();
        _itemRepo.GetQueryableAsync().Returns(Task.FromResult(items));

        var result = await _service.GetReportAsync(new SubcontractOrderSummaryFilterDto
        {
            CompanyId = companyId,
            FromDate = today.AddDays(-1),
            ToDate = today.AddDays(1)
        });

        result.Rows.Count.ShouldBe(1);
        var row = result.Rows[0];
        row.SubcontractOrderNumber.ShouldBe("SCO-SUM-001");
        row.SupplierName.ShouldBe("Motor Subcontractor Inc");
        row.FgItemCode.ShouldBe("FG-MOTOR");
        row.OrderQty.ShouldBe(10m);
        row.ReceivedQty.ShouldBe(6m);
        row.PendingFgQty.ShouldBe(4m);

        row.RmItemCode.ShouldBe("RM-WIRE");
        row.RequiredQty.ShouldBe(50m);
        row.SuppliedQty.ShouldBe(40m);
        row.ConsumedQty.ShouldBe(30m);
        row.PendingRmQty.ShouldBe(10m);

        result.TotalOrders.ShouldBe(1);
        result.TotalOrderQty.ShouldBe(10m);
        result.TotalReceivedQty.ShouldBe(6m);
        result.TotalRequiredQty.ShouldBe(50m);
        result.TotalSuppliedQty.ShouldBe(40m);
        result.TotalConsumedQty.ShouldBe(30m);
    }
}
