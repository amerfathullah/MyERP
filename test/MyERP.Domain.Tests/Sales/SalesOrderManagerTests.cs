using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Sales;

public class SalesOrderManagerTests
{
    [Fact]
    public async Task ValidateCanCancelAsync_DraftSalesInvoiceExists_Throws()
    {
        var so = CreateSO();
        so.AddItem(Guid.NewGuid(), "Widget", 10, 100, 0);

        var si = new SalesInvoice(Guid.NewGuid(), so.CompanyId, so.CustomerId, "SI-001", DateTime.UtcNow);
        si.AddItem(Guid.NewGuid(), "Widget", 10, 100, 0);
        si.Items[0].SalesOrderItemId = so.Items[0].Id;
        // Left in Draft — never submitted.

        var manager = new SalesOrderManager(null!);
        var dnRepo = Substitute.For<IRepository<DeliveryNote, Guid>>();
        dnRepo.GetQueryableAsync().Returns(Task.FromResult(new List<DeliveryNote>().AsQueryable()));
        var siRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        siRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SalesInvoice> { si }.AsQueryable()));

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            manager.ValidateCanCancelAsync(so, dnRepo, siRepo));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.CannotCancelWithDraftDependents);
    }

    [Fact]
    public async Task ValidateCanCancelAsync_NoDependents_Passes()
    {
        var so = CreateSO();
        so.AddItem(Guid.NewGuid(), "Widget", 10, 100, 0);

        var manager = new SalesOrderManager(null!);
        var dnRepo = Substitute.For<IRepository<DeliveryNote, Guid>>();
        dnRepo.GetQueryableAsync().Returns(Task.FromResult(new List<DeliveryNote>().AsQueryable()));
        var siRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        siRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SalesInvoice>().AsQueryable()));

        await manager.ValidateCanCancelAsync(so, dnRepo, siRepo);
    }

    [Fact]
    public void ValidateDeliveryQty_WithinLimit_Succeeds()
    {
        var so = CreateSO();
        var itemId = Guid.NewGuid();
        so.AddItem(itemId, "Widget", 100, 10, 0);

        var manager = new SalesOrderManager(null!);
        manager.ValidateDeliveryQty(so, itemId, 50); // 50 <= 100 pending
    }

    [Fact]
    public void ValidateDeliveryQty_ExceedsLimit_Throws()
    {
        var so = CreateSO();
        var itemId = Guid.NewGuid();
        so.AddItem(itemId, "Widget", 100, 10, 0);
        so.Items[0].DeliveredQty = 80; // 20 remaining

        var manager = new SalesOrderManager(null!);
        var ex = Should.Throw<BusinessException>(() =>
            manager.ValidateDeliveryQty(so, itemId, 30)); // 30 > 20
        ex.Code.ShouldBe("MyERP:08005");
    }

    [Fact]
    public void ValidateDeliveryQty_ExactPending_Succeeds()
    {
        var so = CreateSO();
        var itemId = Guid.NewGuid();
        so.AddItem(itemId, "Widget", 100, 10, 0);
        so.Items[0].DeliveredQty = 60; // 40 remaining

        var manager = new SalesOrderManager(null!);
        manager.ValidateDeliveryQty(so, itemId, 40); // exactly at limit
    }

    [Fact]
    public void ValidateDeliveryQty_UnknownItem_NoThrow()
    {
        var so = CreateSO();
        so.AddItem(Guid.NewGuid(), "Widget", 100, 10, 0);

        var manager = new SalesOrderManager(null!);
        // Unknown itemId — no matching SO item to validate
        manager.ValidateDeliveryQty(so, Guid.NewGuid(), 999);
    }

    [Fact]
    public void SO_PendingDeliveryQty_NeverNegative()
    {
        var so = CreateSO();
        so.AddItem(Guid.NewGuid(), "Widget", 100, 10, 0);
        so.Items[0].DeliveredQty = 120; // Over-delivered (shouldn't happen, but guard)

        so.Items[0].PendingDeliveryQty.ShouldBe(0); // Max(0, ...)
    }

    [Fact]
    public void SO_PerDelivered_Uses_MinFormula()
    {
        var so = CreateSO();
        var itemA = Guid.NewGuid();
        var itemB = Guid.NewGuid();
        so.AddItem(itemA, "Widget A", 100, 10, 0);
        so.AddItem(itemB, "Widget B", 50, 20, 0);

        so.Items[0].DeliveredQty = 100; // 100%
        so.Items[1].DeliveredQty = 25;  // 50%

        // Min(100%, 50%) = 50%
        so.PerDelivered.ShouldBe(50m);
    }

    [Fact]
    public void SO_FullyDeliveredAndBilled_Completed()
    {
        var so = CreateSO();
        so.AddItem(Guid.NewGuid(), "Widget", 10, 100, 0);
        so.Submit();

        so.Items[0].DeliveredQty = 10;
        so.Items[0].BilledQty = 10;
        so.UpdateFulfillmentStatus();

        so.Status.ShouldBe(DocumentStatus.Completed);
    }

    [Fact]
    public void SO_FullyDelivered_NotBilled_ToBill()
    {
        var so = CreateSO();
        so.AddItem(Guid.NewGuid(), "Widget", 10, 100, 0);
        so.Submit();

        so.Items[0].DeliveredQty = 10;
        so.Items[0].BilledQty = 0;
        so.UpdateFulfillmentStatus();

        so.Status.ShouldBe(DocumentStatus.ToBill);
    }

    [Fact]
    public void SO_Close_Succeeds_FromActive()
    {
        var so = CreateSO();
        so.AddItem(Guid.NewGuid(), "Widget", 10, 100, 0);
        so.Submit();
        so.Close();

        so.Status.ShouldBe(DocumentStatus.Closed);
    }

    [Fact]
    public void SO_Close_FromDraft_Throws()
    {
        var so = CreateSO();
        Should.Throw<BusinessException>(() => so.Close());
    }

    [Fact]
    public void SO_Cancel_FromClosed_Throws()
    {
        var so = CreateSO();
        so.AddItem(Guid.NewGuid(), "Widget", 10, 100, 0);
        so.Submit();
        so.Close();

        // Per ERPNext on_cancel(): a Closed SO must be reopened before it can be cancelled.
        Should.Throw<BusinessException>(() => so.Cancel());
    }

    [Fact]
    public void SO_Cancel_FromSubmitted_Succeeds()
    {
        var so = CreateSO();
        so.AddItem(Guid.NewGuid(), "Widget", 10, 100, 0);
        so.Submit();

        so.Cancel();

        so.Status.ShouldBe(DocumentStatus.Cancelled);
    }

    [Fact]
    public void SO_Reopen_Recalculates_Status()
    {
        var so = CreateSO();
        so.AddItem(Guid.NewGuid(), "Widget", 10, 100, 0);
        so.Submit();
        so.Items[0].DeliveredQty = 10;
        so.Close();
        so.Reopen();

        // Fully delivered, not billed → ToBill
        so.Status.ShouldBe(DocumentStatus.ToBill);
    }

    private static SalesOrder CreateSO()
    {
        return new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-001", DateTime.UtcNow);
    }
}
