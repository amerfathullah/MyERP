using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Xunit;

namespace MyERP.Domain.Tests.Sales;

/// <summary>
/// Unit tests for Drop Ship Purchase Order address handling (ERPNext PR #59415 / commit 56e9e6f8ed):
/// Keep customer shipping address on drop ship purchase order.
/// </summary>
public class DropShipPurchaseOrderAddressTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _customerShippingAddressId = Guid.NewGuid();
    private readonly Guid _supplierId = Guid.NewGuid();

    [Fact]
    public async Task CreateDropShipPurchaseOrdersAsync_PreservesCustomerAndShippingAddress()
    {
        var salesOrder = new SalesOrder(Guid.NewGuid(), _companyId, _customerId, "SO-DS-001", DateTime.UtcNow)
        {
            ShippingAddressId = _customerShippingAddressId
        };
        salesOrder.AddItem(Guid.NewGuid(), "Drop Ship Widget", 5m, 100m, 0m);
        salesOrder.Items[0].DeliveredBySupplier = true;
        salesOrder.Items[0].SupplierId = _supplierId;

        PurchaseOrder? savedPo = null;

        var poRepo = Substitute.For<IRepository<PurchaseOrder, Guid>>();
        poRepo.InsertAsync(Arg.Do<PurchaseOrder>(po => savedPo = po), Arg.Any<bool>())
            .Returns(callInfo => Task.FromResult(callInfo.Arg<PurchaseOrder>()));

        var supplierRepo = Substitute.For<IRepository<Supplier, Guid>>();
        supplierRepo.FindAsync(_supplierId).Returns(Task.FromResult<Supplier?>(new Supplier(Guid.NewGuid(), _companyId, "Supplier 1") { SupplierCode = "SUP-001" }));

        var companyRepo = Substitute.For<IRepository<Company, Guid>>();
        companyRepo.FindAsync(_companyId).Returns(Task.FromResult<Company?>(new Company(_companyId, "Test Co")));

        var guidGen = Substitute.For<IGuidGenerator>();
        guidGen.Create().Returns(Guid.NewGuid());

        var dropShipService = new DropShipService(poRepo, supplierRepo, companyRepo, guidGen);

        var poIds = await dropShipService.CreateDropShipPurchaseOrdersAsync(
            salesOrder,
            (docType, compId) => Task.FromResult("PO-DS-001"));

        poIds.Count.ShouldBe(1);
        savedPo.ShouldNotBeNull();
        savedPo.CustomerId.ShouldBe(_customerId);
        savedPo.ShippingAddressId.ShouldBe(_customerShippingAddressId);
        savedPo.IsDropShip.ShouldBeTrue();
    }

    [Fact]
    public void PurchaseOrder_IsDropShip_FalseWhenNoItemsDeliveredBySupplier()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), _companyId, _supplierId, "PO-REG-001", DateTime.UtcNow);
        po.AddItem(Guid.NewGuid(), "Standard Item", 10m, 50m, 0m, "Unit");

        po.IsDropShip.ShouldBeFalse();
    }
}
