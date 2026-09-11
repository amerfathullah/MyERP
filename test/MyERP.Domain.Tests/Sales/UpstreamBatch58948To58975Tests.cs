using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using MyERP.Sales;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using MyERP.Tax.Entities;
using NSubstitute;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.Sales;

/// <summary>
/// Unit tests for upstream batch PRs:
/// - PR #58966: Exclude fully billed orders from invoice picker / unbilled items
/// - PR #58953: Recalculate billing status of returned delivery notes
/// - PR #58948: Item Price company restriction inherited from Item
/// - PR #58975: Company check on Invoice Discounting get_invoices
/// </summary>
public class UpstreamBatch58948To58975Tests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _supplierId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();

    #region PR #58953: Delivery Note Return Billing Status Recalculation

    [Fact]
    public void DeliveryNote_PerBilled_WithReturns_CalculatesCorrectly()
    {
        // Per ERPNext PR #58953 / commit be8208e7cb test_billing_status_repair_patch:
        // Delivery Note invoiced for 2 of 5 qty, the remaining 3 returned -> fully billed (100%)
        var dn = new DeliveryNote(Guid.NewGuid(), _companyId, _customerId, _warehouseId, "DN-2026-001", DateTime.UtcNow);
        dn.AddItem(Guid.NewGuid(), "Item 1", 5m, 100m, 0m);
        dn.Submit();

        var item = dn.Items[0];
        item.BilledQty = 2m;
        item.ReturnedQty = 3m;

        // Net unreturned qty = 5 - 3 = 2. Billed qty = 2. PerBilled = 2 / 2 * 100 = 100%
        Assert.Equal(2m, item.BillableQty);
        Assert.Equal(0m, item.PendingBillingQty);
        Assert.Equal(100m, dn.PerBilled);
        Assert.Equal("Completed", dn.BillingStatus);
    }

    [Fact]
    public void DeliveryNote_PerBilled_PartiallyReturned_CalculatesRatioOnUnreturnedQty()
    {
        // Per ERPNext PR #58953:
        // 5 delivered, nothing invoiced, 2 returned -> PerBilled = 0%, BillingStatus = "To Bill"
        var dn = new DeliveryNote(Guid.NewGuid(), _companyId, _customerId, _warehouseId, "DN-2026-002", DateTime.UtcNow);
        dn.AddItem(Guid.NewGuid(), "Item 2", 5m, 100m, 0m);
        dn.Submit();

        var item = dn.Items[0];
        item.BilledQty = 0m;
        item.ReturnedQty = 2m;

        // Net unreturned qty = 5 - 2 = 3. Billed qty = 0. PerBilled = 0%
        Assert.Equal(3m, item.BillableQty);
        Assert.Equal(3m, item.PendingBillingQty);
        Assert.Equal(0m, dn.PerBilled);
        Assert.Equal("To Bill", dn.BillingStatus);
    }

    [Fact]
    public void DeliveryNote_PerBilled_PartiallyBilledAndPartiallyReturned()
    {
        // 5 delivered, 1 billed, 1 returned -> net = 4, billed = 1 -> 25% billed -> Partially Billed
        var dn = new DeliveryNote(Guid.NewGuid(), _companyId, _customerId, _warehouseId, "DN-2026-003", DateTime.UtcNow);
        dn.AddItem(Guid.NewGuid(), "Item 3", 5m, 100m, 0m);
        dn.Submit();

        var item = dn.Items[0];
        item.BilledQty = 1m;
        item.ReturnedQty = 1m;

        Assert.Equal(4m, item.BillableQty);
        Assert.Equal(3m, item.PendingBillingQty);
        Assert.Equal(25m, dn.PerBilled);
        Assert.Equal("Partially Billed", dn.BillingStatus);
    }

    [Fact]
    public void DeliveryNote_PerBilled_FullyReturned_BecomesCompleted()
    {
        // 5 delivered, 0 billed, 5 returned -> net = 0 -> 100% billed / settled
        var dn = new DeliveryNote(Guid.NewGuid(), _companyId, _customerId, _warehouseId, "DN-2026-004", DateTime.UtcNow);
        dn.AddItem(Guid.NewGuid(), "Item 4", 5m, 100m, 0m);
        dn.Submit();

        var item = dn.Items[0];
        item.BilledQty = 0m;
        item.ReturnedQty = 5m;

        Assert.Equal(0m, item.BillableQty);
        Assert.Equal(0m, item.PendingBillingQty);
        Assert.Equal(100m, dn.PerBilled);
        Assert.Equal("Completed", dn.BillingStatus);
    }

    #endregion

    #region PR #58966: Exclude Fully Billed Orders From Unbilled Order Items

    [Fact]
    public async Task SalesInvoice_GetUnbilledOrderItems_ExcludesFullyBilledOrders()
    {
        // Per ERPNext PR #58966 / commit 5f216c5d55:
        // Fully billed orders (per_billed == 100) must be excluded from invoice picker/unbilled items
        var soRepo = Substitute.For<IRepository<SalesOrder, Guid>>();

        var order1 = new SalesOrder(Guid.NewGuid(), _companyId, _customerId, "SO-001", DateTime.UtcNow);
        order1.AddItem(Guid.NewGuid(), "Item A", 10m, 50m, 0m);
        order1.Submit();
        order1.Items[0].BilledQty = 10m; // 100% billed
        Assert.True(order1.PerBilled >= 100m);

        var order2 = new SalesOrder(Guid.NewGuid(), _companyId, _customerId, "SO-002", DateTime.UtcNow);
        order2.AddItem(Guid.NewGuid(), "Item B", 10m, 50m, 0m);
        order2.Submit();
        order2.Items[0].BilledQty = 4m; // 40% billed, 6 pending
        Assert.True(order2.PerBilled < 100m);

        var orders = new List<SalesOrder> { order1, order2 }.AsQueryable();
        soRepo.GetQueryableAsync().Returns(Task.FromResult(orders));

        var appService = new SalesInvoiceAppService(
            Substitute.For<IRepository<SalesInvoice, Guid>>(),
            Substitute.For<IRepository<Customer, Guid>>(),
            soRepo,
            Substitute.For<IRepository<Company, Guid>>(),
            Substitute.For<IRepository<TransactionTaxRow, Guid>>(),
            Substitute.For<IRepository<PaymentTermsTemplate, Guid>>(),
            Substitute.For<IRepository<PaymentScheduleEntry, Guid>>(),
            Substitute.For<IDocumentNumberGenerator>(),
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);

        var result = await appService.GetUnbilledOrderItemsAsync(_customerId, _companyId);

        // Only Item B from order 2 should be returned
        Assert.Single(result);
        Assert.Equal(order2.Id, result[0].SalesOrderId);
        Assert.Equal(6m, result[0].Quantity);
    }

    [Fact]
    public async Task PurchaseInvoice_GetUnbilledPurchaseOrderItems_ExcludesFullyBilledOrders()
    {
        // Per ERPNext PR #58966 / commit 5f216c5d55:
        // Fully billed POs (per_billed == 100) must be excluded from unbilled PO items query
        var poRepo = Substitute.For<IRepository<PurchaseOrder, Guid>>();

        var po1 = new PurchaseOrder(Guid.NewGuid(), _companyId, _supplierId, "PO-001", DateTime.UtcNow);
        po1.AddItem(Guid.NewGuid(), "Raw Mat 1", 20m, 10m, 0m);
        po1.Submit();
        po1.Items[0].BilledQty = 20m; // 100% billed
        Assert.True(po1.PerBilled >= 100m);

        var po2 = new PurchaseOrder(Guid.NewGuid(), _companyId, _supplierId, "PO-002", DateTime.UtcNow);
        po2.AddItem(Guid.NewGuid(), "Raw Mat 2", 15m, 10m, 0m);
        po2.Submit();
        po2.Items[0].BilledQty = 5m; // 33% billed, 10 pending
        Assert.True(po2.PerBilled < 100m);

        var poList = new List<PurchaseOrder> { po1, po2 }.AsQueryable();
        poRepo.GetQueryableAsync().Returns(Task.FromResult(poList));

        var appService = new PurchaseInvoiceAppService(
            Substitute.For<IRepository<PurchaseInvoice, Guid>>(),
            poRepo,
            Substitute.For<IRepository<Supplier, Guid>>(),
            Substitute.For<IRepository<TransactionTaxRow, Guid>>(),
            Substitute.For<IRepository<PaymentScheduleEntry, Guid>>(),
            Substitute.For<IRepository<Company, Guid>>(),
            Substitute.For<IRepository<FiscalYear, Guid>>(),
            Substitute.For<IRepository<Item, Guid>>(),
            Substitute.For<IDocumentNumberGenerator>(),
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);

        var result = await appService.GetUnbilledPurchaseOrderItemsAsync(_supplierId, _companyId);

        Assert.Single(result);
        Assert.Equal(po2.Id, result[0].PurchaseOrderId);
        Assert.Equal(10m, result[0].Quantity);
    }

    #endregion

    #region PR #58948: Item Price Company Restriction

    [Fact]
    public async Task ItemPrice_GetListAsync_WithCompanyId_FiltersByCompanyItems()
    {
        // Per ERPNext PR #58948 / commit 4671d1a665:
        // Item Price inherits company restriction from Item
        var itemPriceRepo = Substitute.For<IRepository<ItemPrice, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var priceListRepo = Substitute.For<IRepository<PriceList, Guid>>();
        var customerRepo = Substitute.For<IRepository<Customer, Guid>>();
        var supplierRepo = Substitute.For<IRepository<Supplier, Guid>>();

        var otherCompanyId = Guid.NewGuid();
        var itemA = new Item(Guid.NewGuid(), _companyId, "ITEM-A", "Item A", ItemType.Goods);
        var itemB = new Item(Guid.NewGuid(), otherCompanyId, "ITEM-B", "Item B", ItemType.Goods);

        var plId = Guid.NewGuid();
        var pl = new PriceList(plId, "Standard Selling", "USD", isBuying: false, isSelling: true);

        var priceA = new ItemPrice(Guid.NewGuid(), itemA.Id, plId, 100m, "Unit", "USD");
        var priceB = new ItemPrice(Guid.NewGuid(), itemB.Id, plId, 200m, "Unit", "USD");

        var prices = new List<ItemPrice> { priceA, priceB }.AsQueryable();
        var items = new List<Item> { itemA, itemB }.AsQueryable();
        var priceLists = new List<PriceList> { pl }.AsQueryable();

        itemPriceRepo.GetQueryableAsync().Returns(Task.FromResult(prices));
        itemRepo.GetQueryableAsync().Returns(Task.FromResult(items));
        priceListRepo.GetQueryableAsync().Returns(Task.FromResult(priceLists));
        customerRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Customer>().AsQueryable()));
        supplierRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Supplier>().AsQueryable()));

        var appService = new ItemPriceAppService(itemPriceRepo, itemRepo, priceListRepo, customerRepo, supplierRepo);

        var result = await appService.GetListAsync(new GetItemPriceListDto
        {
            CompanyId = _companyId,
            MaxResultCount = 50,
            SkipCount = 0
        });

        // Only Item A should be returned for _companyId
        Assert.Single(result.Items);
        Assert.Equal(itemA.Id, result.Items[0].ItemId);
        Assert.Equal(100m, result.Items[0].PriceListRate);
    }

    #endregion

    #region PR #58975: Invoice Discounting Company Requirement

    [Fact]
    public async Task InvoiceDiscounting_GetEligibleInvoicesAsync_EmptyCompany_ThrowsValidationException()
    {
        // Per ERPNext PR #58975 / commit b481083ff0:
        // Must validate company on document before requesting for invoices
        var appService = new InvoiceDiscountingAppService(
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            appService.GetEligibleInvoicesAsync(Guid.Empty));

        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
    }

    #endregion
}
