using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Purchasing.DomainServices;
using MyERP.Purchasing.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Purchasing;

public class PurchaseReturnClosedPoTests
{
    private readonly IRepository<Supplier, Guid> _supplierRepository;
    private readonly IRepository<PurchaseOrder, Guid> _poRepository;
    private readonly IRepository<PurchaseReceipt, Guid> _prRepository;
    private readonly IRepository<PurchaseInvoice, Guid> _piRepository;
    private readonly PurchaseReceiptManager _prManager;
    private readonly PurchaseInvoiceManager _piManager;

    public PurchaseReturnClosedPoTests()
    {
        _supplierRepository = Substitute.For<IRepository<Supplier, Guid>>();
        _poRepository = Substitute.For<IRepository<PurchaseOrder, Guid>>();
        _prRepository = Substitute.For<IRepository<PurchaseReceipt, Guid>>();
        _piRepository = Substitute.For<IRepository<PurchaseInvoice, Guid>>();
        _prManager = new PurchaseReceiptManager(_poRepository, _prRepository);
        _piManager = new PurchaseInvoiceManager(_supplierRepository, _piRepository, _poRepository);
    }

    [Fact]
    public async Task PurchaseReceipt_ReturnAgainstClosedPO_Succeeds()
    {
        var poId = Guid.NewGuid();
        var po = new PurchaseOrder(poId, Guid.NewGuid(), Guid.NewGuid(), "PO-001", DateTime.UtcNow.AddDays(-5));
        po.AddItem(Guid.NewGuid(), "Item", 10, 100, 0);
        po.Submit();
        po.Close();

        _poRepository.GetAsync(poId).Returns(Task.FromResult(po));

        var pr = new PurchaseReceipt(Guid.NewGuid(), po.CompanyId, po.SupplierId, Guid.NewGuid(), "PR-RET-001", DateTime.UtcNow)
        {
            PurchaseOrderId = poId,
            IsReturn = true
        };
        pr.AddItem(Guid.NewGuid(), "Item", -2, 100, 0);

        await Should.NotThrowAsync(async () => await _prManager.ValidateAgainstPurchaseOrderAsync(pr));
    }

    [Fact]
    public async Task PurchaseReceipt_NonReturnAgainstClosedPO_ThrowsException()
    {
        var poId = Guid.NewGuid();
        var po = new PurchaseOrder(poId, Guid.NewGuid(), Guid.NewGuid(), "PO-002", DateTime.UtcNow.AddDays(-5));
        po.AddItem(Guid.NewGuid(), "Item", 10, 100, 0);
        po.Submit();
        po.Close();

        _poRepository.GetAsync(poId).Returns(Task.FromResult(po));

        var pr = new PurchaseReceipt(Guid.NewGuid(), po.CompanyId, po.SupplierId, Guid.NewGuid(), "PR-001", DateTime.UtcNow)
        {
            PurchaseOrderId = poId,
            IsReturn = false
        };
        pr.AddItem(Guid.NewGuid(), "Item", 2, 100, 0);

        var ex = await Should.ThrowAsync<BusinessException>(async () => await _prManager.ValidateAgainstPurchaseOrderAsync(pr));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.InvalidStatusTransition);
    }

    [Fact]
    public async Task PurchaseReceipt_ReturnAgainstCancelledPO_ThrowsException()
    {
        var poId = Guid.NewGuid();
        var po = new PurchaseOrder(poId, Guid.NewGuid(), Guid.NewGuid(), "PO-003", DateTime.UtcNow.AddDays(-5));
        po.AddItem(Guid.NewGuid(), "Item", 10, 100, 0);
        po.Cancel();

        _poRepository.GetAsync(poId).Returns(Task.FromResult(po));

        var pr = new PurchaseReceipt(Guid.NewGuid(), po.CompanyId, po.SupplierId, Guid.NewGuid(), "PR-RET-002", DateTime.UtcNow)
        {
            PurchaseOrderId = poId,
            IsReturn = true
        };
        pr.AddItem(Guid.NewGuid(), "Item", -2, 100, 0);

        var ex = await Should.ThrowAsync<BusinessException>(async () => await _prManager.ValidateAgainstPurchaseOrderAsync(pr));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.InvalidStatusTransition);
    }

    [Fact]
    public async Task PurchaseInvoice_ReturnAgainstClosedPO_Succeeds()
    {
        var poId = Guid.NewGuid();
        var po = new PurchaseOrder(poId, Guid.NewGuid(), Guid.NewGuid(), "PO-004", DateTime.UtcNow.AddDays(-5));
        po.AddItem(Guid.NewGuid(), "Item", 10, 100, 0);
        po.Submit();
        po.Close();

        var poQueryable = new List<PurchaseOrder> { po }.AsQueryable();
        _poRepository.GetQueryableAsync().Returns(Task.FromResult(poQueryable));

        var pi = new PurchaseInvoice(Guid.NewGuid(), po.CompanyId, po.SupplierId, "PINV-RET-001", DateTime.UtcNow)
        {
            IsReturn = true
        };
        pi.AddItem(Guid.NewGuid(), "Item", -2, 100, 0);
        pi.Items[0].PurchaseOrderItemId = po.Items[0].Id;

        await Should.NotThrowAsync(async () => await _piManager.ValidatePurchaseOrderStatusAsync(pi));
    }

    [Fact]
    public async Task PurchaseInvoice_NonReturnAgainstClosedPO_ThrowsException()
    {
        var poId = Guid.NewGuid();
        var po = new PurchaseOrder(poId, Guid.NewGuid(), Guid.NewGuid(), "PO-005", DateTime.UtcNow.AddDays(-5));
        po.AddItem(Guid.NewGuid(), "Item", 10, 100, 0);
        po.Submit();
        po.Close();

        var poQueryable = new List<PurchaseOrder> { po }.AsQueryable();
        _poRepository.GetQueryableAsync().Returns(Task.FromResult(poQueryable));

        var pi = new PurchaseInvoice(Guid.NewGuid(), po.CompanyId, po.SupplierId, "PINV-001", DateTime.UtcNow)
        {
            IsReturn = false
        };
        pi.AddItem(Guid.NewGuid(), "Item", 2, 100, 0);
        pi.Items[0].PurchaseOrderItemId = po.Items[0].Id;

        var ex = await Should.ThrowAsync<BusinessException>(async () => await _piManager.ValidatePurchaseOrderStatusAsync(pi));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.InvalidStatusTransition);
    }

    [Fact]
    public async Task PurchaseReceipt_ReturnOfFullyRejectedReceipt_Succeeds()
    {
        // Per ERPNext PR #59280 / commit b3d55db893:
        // A receipt that rejected every unit can be sent back.
        var companyId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var rejectedWarehouseId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var originalId = Guid.NewGuid();
        var originalPr = new PurchaseReceipt(originalId, companyId, supplierId, warehouseId, "PR-REJ-001", DateTime.UtcNow);
        originalPr.AddItem(itemId, "Defective Item", 0, 100m, 0m, rejectedQty: 10m, rejectedWarehouseId: rejectedWarehouseId);

        _prRepository.GetAsync(originalId).Returns(Task.FromResult(originalPr));
        _prRepository.GetQueryableAsync().Returns(Task.FromResult(new List<PurchaseReceipt>().AsQueryable()));

        var returnPr = new PurchaseReceipt(Guid.NewGuid(), companyId, supplierId, warehouseId, "PR-RET-REJ-001", DateTime.UtcNow)
        {
            IsReturn = true,
            ReturnAgainstId = originalId
        };
        returnPr.AddItem(itemId, "Defective Item", 0, 100m, 0m, rejectedQty: -10m, rejectedWarehouseId: rejectedWarehouseId);

        await Should.NotThrowAsync(async () => await _prManager.ValidateReturnAsync(returnPr));
    }

    [Fact]
    public async Task PurchaseReceipt_ReturnOfFullyRejectedReceipt_ExceedingQty_Throws()
    {
        // Per ERPNext PR #59280:
        // Returning more rejected quantity than originally received must throw.
        var companyId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var rejectedWarehouseId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var originalId = Guid.NewGuid();
        var originalPr = new PurchaseReceipt(originalId, companyId, supplierId, warehouseId, "PR-REJ-002", DateTime.UtcNow);
        originalPr.AddItem(itemId, "Defective Item", 0, 100m, 0m, rejectedQty: 10m, rejectedWarehouseId: rejectedWarehouseId);

        _prRepository.GetAsync(originalId).Returns(Task.FromResult(originalPr));
        _prRepository.GetQueryableAsync().Returns(Task.FromResult(new List<PurchaseReceipt>().AsQueryable()));

        var returnPr = new PurchaseReceipt(Guid.NewGuid(), companyId, supplierId, warehouseId, "PR-RET-REJ-002", DateTime.UtcNow)
        {
            IsReturn = true,
            ReturnAgainstId = originalId
        };
        returnPr.AddItem(itemId, "Defective Item", 0, 100m, 0m, rejectedQty: -15m, rejectedWarehouseId: rejectedWarehouseId);

        var ex = await Should.ThrowAsync<BusinessException>(async () => await _prManager.ValidateReturnAsync(returnPr));
        ex.Code.ShouldBe("MyERP:08004");
    }

    [Fact]
    public void PurchaseReceipt_ReturnWithZeroAcceptedAndZeroRejectedQty_Throws()
    {
        // Must reject return item if neither accepted nor rejected qty is negative.
        var companyId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var returnPr = new PurchaseReceipt(Guid.NewGuid(), companyId, supplierId, warehouseId, "PR-RET-ZERO", DateTime.UtcNow)
        {
            IsReturn = true,
            ReturnAgainstId = Guid.NewGuid()
        };

        var ex = Should.Throw<ArgumentException>(() =>
            returnPr.AddItem(itemId, "Zero Item", 0, 100m, 0m, rejectedQty: 0m));
        ex.ParamName.ShouldBe("quantity");
    }
}
