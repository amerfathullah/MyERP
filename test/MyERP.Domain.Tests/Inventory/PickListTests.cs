using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Inventory.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Inventory;

public class PickListTests
{
    private static PickList CreatePickList() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "Delivery");

    [Fact]
    public void Create_SetsDefaults()
    {
        var pl = CreatePickList();
        pl.Status.ShouldBe(Core.DocumentStatus.Draft);
        pl.Purpose.ShouldBe("Delivery");
        pl.Items.ShouldBeEmpty();
    }

    [Fact]
    public void AddItem_Succeeds()
    {
        var pl = CreatePickList();
        pl.AddItem(Guid.NewGuid(), Guid.NewGuid(), 100m);
        pl.Items.Count.ShouldBe(1);
        pl.Items[0].PendingQty.ShouldBe(100m);
    }

    [Fact]
    public void Submit_WithItems_Succeeds()
    {
        var pl = CreatePickList();
        pl.AddItem(Guid.NewGuid(), Guid.NewGuid(), 50m);
        pl.Submit();
        pl.Status.ShouldBe(Core.DocumentStatus.Submitted);
    }

    [Fact]
    public void Submit_Empty_Throws()
    {
        var pl = CreatePickList();
        Should.Throw<BusinessException>(() => pl.Submit());
    }

    [Fact]
    public void RecordTransfer_ReducesPending()
    {
        var pl = CreatePickList();
        pl.AddItem(Guid.NewGuid(), Guid.NewGuid(), 100m);
        pl.Submit();
        pl.Items[0].RecordTransfer(40m);
        pl.Items[0].TransferredQty.ShouldBe(40m);
        pl.Items[0].PendingQty.ShouldBe(60m);
        pl.IsPartiallyTransferred.ShouldBeTrue();
        pl.IsFullyTransferred.ShouldBeFalse();
    }

    [Fact]
    public void RecordTransfer_Full_MarksComplete()
    {
        var pl = CreatePickList();
        pl.AddItem(Guid.NewGuid(), Guid.NewGuid(), 50m);
        pl.Submit();
        pl.Items[0].RecordTransfer(50m);
        pl.IsFullyTransferred.ShouldBeTrue();
    }

    [Fact]
    public void RecordTransfer_Excess_Throws()
    {
        var pl = CreatePickList();
        pl.AddItem(Guid.NewGuid(), Guid.NewGuid(), 50m);
        pl.Submit();
        Should.Throw<BusinessException>(() => pl.Items[0].RecordTransfer(60m));
    }

    [Fact]
    public void Cancel_WithTransferred_Throws()
    {
        var pl = CreatePickList();
        pl.AddItem(Guid.NewGuid(), Guid.NewGuid(), 50m);
        pl.Submit();
        pl.Items[0].RecordTransfer(10m);
        Should.Throw<BusinessException>(() => pl.Cancel());
    }

    [Fact]
    public void Cancel_NoTransfers_Succeeds()
    {
        var pl = CreatePickList();
        pl.AddItem(Guid.NewGuid(), Guid.NewGuid(), 50m);
        pl.Submit();
        pl.Cancel();
        pl.Status.ShouldBe(Core.DocumentStatus.Cancelled);
    }

    [Fact]
    public void RecordDelivery_UpdatesDeliveredQtyAndPerDelivered()
    {
        var pl = CreatePickList();
        pl.AddItem(Guid.NewGuid(), Guid.NewGuid(), 100m);
        pl.AddItem(Guid.NewGuid(), Guid.NewGuid(), 100m);
        pl.Submit();

        pl.Items[0].RecordDelivery(50m);
        pl.PerDelivered.ShouldBe(25m); // 50 / 200 = 25%
        pl.IsPartiallyDelivered.ShouldBeTrue();
        pl.IsFullyDelivered.ShouldBeFalse();

        pl.Items[0].RecordDelivery(50m);
        pl.Items[1].RecordDelivery(100m);
        pl.PerDelivered.ShouldBe(100m);
        pl.IsFullyDelivered.ShouldBeTrue();
    }

    [Fact]
    public void Cancel_WithDelivered_Throws()
    {
        var pl = CreatePickList();
        pl.AddItem(Guid.NewGuid(), Guid.NewGuid(), 50m);
        pl.Submit();
        pl.Items[0].RecordDelivery(10m);
        Should.Throw<BusinessException>(() => pl.Cancel());
    }

    [Fact]
    public void PickListManager_UpdateDeliveredQuantities_MatchesComponents()
    {
        var plRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<PickList, Guid>>();
        var binRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<Bin, Guid>>();
        var manager = new MyERP.Inventory.DomainServices.PickListManager(plRepo, binRepo);

        var pl = CreatePickList();
        var comp1Id = Guid.NewGuid();
        var comp2Id = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var soDetailId = Guid.NewGuid();

        pl.AddItem(comp1Id, warehouseId, 10m);
        pl.AddItem(comp2Id, warehouseId, 20m);
        pl.Items[0].SourceDocumentItemId = soDetailId;
        pl.Items[1].SourceDocumentItemId = soDetailId;
        pl.Submit();

        var delivered = new List<MyERP.Inventory.DomainServices.DeliveredComponentItem>
        {
            new(comp1Id, warehouseId, DeliveredQty: 5m, SourceDocumentItemId: soDetailId),
            new(comp2Id, warehouseId, DeliveredQty: 10m, SourceDocumentItemId: soDetailId)
        };

        manager.UpdateDeliveredQuantities(pl, delivered);

        pl.Items[0].DeliveredQty.ShouldBe(5m);
        pl.Items[1].DeliveredQty.ShouldBe(10m);
        pl.PerDelivered.ShouldBe(50m); // (5 + 10) / (10 + 20) = 15/30 = 50%
        pl.IsPartiallyDelivered.ShouldBeTrue();
    }

    [Fact]
    public async System.Threading.Tasks.Task PickListManager_GetStockAvailabilityAsync_CalculatesFreeQty()
    {
        var plRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<PickList, Guid>>();
        var binRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<Bin, Guid>>();
        var manager = new MyERP.Inventory.DomainServices.PickListManager(plRepo, binRepo);

        var itemId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();

        var bin = new Bin(Guid.NewGuid(), itemId, warehouseId);
        bin.ApplyStockMovement(100m, 1000m); // ActualQty = 100

        binRepo.GetQueryableAsync().Returns(System.Threading.Tasks.Task.FromResult(
            new List<Bin> { bin }.AsQueryable()));

        var existingPl = CreatePickList();
        existingPl.AddItem(itemId, warehouseId, 40m);
        existingPl.Submit();

        plRepo.GetQueryableAsync().Returns(System.Threading.Tasks.Task.FromResult(
            new List<PickList> { existingPl }.AsQueryable()));

        var availability = await manager.GetStockAvailabilityAsync(itemId, warehouseId);

        availability.ActualQty.ShouldBe(100m);
        availability.PickedQty.ShouldBe(40m);
        availability.ReservedQty.ShouldBe(0m);
        availability.FreeQty.ShouldBe(60m);

        // When excluding the existing pick list (e.g. during edit)
        var editAvailability = await manager.GetStockAvailabilityAsync(itemId, warehouseId, excludePickListId: existingPl.Id);
        editAvailability.PickedQty.ShouldBe(0m);
        editAvailability.FreeQty.ShouldBe(100m);
    }
}
