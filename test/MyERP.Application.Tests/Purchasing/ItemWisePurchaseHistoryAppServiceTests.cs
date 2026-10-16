using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Purchasing;

public class ItemWisePurchaseHistoryAppServiceTests
{
    private readonly IRepository<PurchaseOrder, Guid> _purchaseOrderRepo = Substitute.For<IRepository<PurchaseOrder, Guid>>();
    private readonly IRepository<Supplier, Guid> _supplierRepo = Substitute.For<IRepository<Supplier, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<Company, Guid> _companyRepo = Substitute.For<IRepository<Company, Guid>>();
    private readonly IRepository<SupplierGroup, Guid> _supplierGroupRepo = Substitute.For<IRepository<SupplierGroup, Guid>>();
    private readonly IRepository<ItemGroup, Guid> _itemGroupRepo = Substitute.For<IRepository<ItemGroup, Guid>>();

    private readonly ItemWisePurchaseHistoryAppService _appService;

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _supplier1Id = Guid.NewGuid();
    private readonly Guid _item1Id = Guid.NewGuid();
    private readonly Guid _item2Id = Guid.NewGuid();
    private readonly Guid _supplierGroupId = Guid.NewGuid();
    private readonly Guid _itemGroupId = Guid.NewGuid();

    public ItemWisePurchaseHistoryAppServiceTests()
    {
        _appService = new ItemWisePurchaseHistoryAppService(
            _purchaseOrderRepo,
            _supplierRepo,
            _itemRepo,
            _companyRepo,
            _supplierGroupRepo,
            _itemGroupRepo);
    }

    [Fact]
    public async Task Validation_FromDateGreaterThanToDate_Throws()
    {
        var input = new GetItemWisePurchaseHistoryDto
        {
            FromDate = new DateTime(2025, 5, 10),
            ToDate = new DateTime(2025, 5, 1)
        };

        var ex = await Should.ThrowAsync<BusinessException>(() => _appService.GetHistoryAsync(input));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task ReturnsOrderRowsWithCorrectBaseRateAndAmount_AndReceivedBilledQuantities()
    {
        var fromDate = new DateTime(2025, 1, 1);
        var toDate = new DateTime(2025, 1, 31);

        var sup1 = new Supplier(_supplier1Id, _companyId, "Supplier Global")
        {
            SupplierGroupId = _supplierGroupId,
            SupplierCode = "SUP-001"
        };
        _supplierRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Supplier> { sup1 }.AsQueryable()));

        var comp = new Company(_companyId, "Manufacturing Corp") { CurrencyCode = "MYR" };
        _companyRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Company> { comp }.AsQueryable()));

        var item1 = new Item(_item1Id, _companyId, "RAW-001", "Steel Plate", ItemType.Goods) { ItemGroupId = _itemGroupId };
        var item2 = new Item(_item2Id, _companyId, "RAW-002", "Copper Wire", ItemType.Goods);
        _itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { item1, item2 }.AsQueryable()));

        _supplierGroupRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SupplierGroup> { new SupplierGroup(_supplierGroupId, "Hardware") }.AsQueryable()));
        _itemGroupRepo.GetQueryableAsync().Returns(Task.FromResult(new List<ItemGroup> { new ItemGroup(_itemGroupId, "Raw Materials") }.AsQueryable()));

        // PO 1 (Submitted): multi-currency USD with ExchangeRate 4.5
        var po1 = new PurchaseOrder(Guid.NewGuid(), _companyId, _supplier1Id, "PO-001", new DateTime(2025, 1, 15))
        {
            CurrencyCode = "USD",
            ExchangeRate = 4.5m
        };
        po1.AddItem(_item1Id, "Steel Plate desc", 10m, 100m, 0m); // LineTotal 1000
        po1.AddItem(_item2Id, "Copper Wire desc", 5m, 50m, 0m);   // LineTotal 250
        po1.Submit();
        po1.Items[0].ReceivedQty = 6m;
        po1.Items[0].BilledQty = 4m;
        po1.Items[1].ReceivedQty = 5m;
        po1.Items[1].BilledQty = 5m;

        // PO 2 (Draft): should be excluded
        var poDraft = new PurchaseOrder(Guid.NewGuid(), _companyId, _supplier1Id, "PO-DRAFT", new DateTime(2025, 1, 16));
        poDraft.AddItem(_item1Id, "Draft item", 1m, 100m, 0m);

        // PO 3 (Cancelled): should be excluded
        var poCancelled = new PurchaseOrder(Guid.NewGuid(), _companyId, _supplier1Id, "PO-CANCELLED", new DateTime(2025, 1, 17));
        poCancelled.AddItem(_item1Id, "Cancelled item", 1m, 100m, 0m);
        poCancelled.Submit();
        poCancelled.Cancel();

        _purchaseOrderRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<PurchaseOrder, object>>[]>())
            .Returns(Task.FromResult(new List<PurchaseOrder> { po1, poDraft, poCancelled }.AsQueryable()));

        var result = await _appService.GetHistoryAsync(new GetItemWisePurchaseHistoryDto
        {
            CompanyId = _companyId,
            FromDate = fromDate,
            ToDate = toDate
        });

        result.Rows.Count.ShouldBe(2);
        result.TotalOrders.ShouldBe(1);
        result.TotalQuantity.ShouldBe(15m);
        // Base amount: 1000 * 4.5 = 4500; 250 * 4.5 = 1125 => 5625
        result.TotalAmount.ShouldBe(5625m);
        result.TotalReceivedQuantity.ShouldBe(11m);
        // Billed: (4 * 100 * 4.5) + (5 * 50 * 4.5) = 1800 + 1125 = 2925
        result.TotalBilledAmount.ShouldBe(2925m);

        var r1 = result.Rows.First(r => r.ItemId == _item1Id);
        r1.ItemCode.ShouldBe("RAW-001");
        r1.ItemName.ShouldBe("Steel Plate");
        r1.ItemGroup.ShouldBe("Raw Materials");
        r1.SupplierName.ShouldBe("Supplier Global");
        r1.SupplierGroup.ShouldBe("Hardware");
        r1.Rate.ShouldBe(450m);
        r1.Amount.ShouldBe(4500m);
        r1.ReceivedQuantity.ShouldBe(6m);
        r1.BilledAmount.ShouldBe(1800m);

        result.ChartData.Count.ShouldBe(2);
        result.ChartData[0].ItemCode.ShouldBe("RAW-001");
        result.ChartData[0].Amount.ShouldBe(4500m);
    }
}
