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
}
