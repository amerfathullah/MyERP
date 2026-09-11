using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Inventory.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Inventory;

/// <summary>
/// Tests for Item batch shelf life validation (ERPNext PR #58911 / commit b2bdeaa672)
/// and Delivery Trip empty placeholder stops cleanup (ERPNext PR #58896 / commit 4b23cee2ea).
/// </summary>
public class ItemBatchSettingsValidationTests
{
    private static Item CreateTestItem(string code = "ITEM-001")
    {
        return new Item(Guid.NewGuid(), Guid.NewGuid(), code, "Test Item", ItemType.Goods);
    }

    [Fact]
    public void ValidateBatchSettings_AutoBatchWithExpiry_RequiresShelfLifeGreaterThanZero()
    {
        var item = CreateTestItem();
        item.HasBatchNo = true;
        item.CreateNewBatch = true;
        item.HasExpiryDate = true;
        item.ShelfLifeInDays = null;

        var ex = Should.Throw<BusinessException>(() => item.ValidateBatchSettings());
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ShelfLifeMustBeGreaterThanZero);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void ValidateBatchSettings_AutoBatchWithExpiry_ZeroOrNegativeShelfLife_Throws(int shelfLife)
    {
        var item = CreateTestItem();
        item.HasBatchNo = true;
        item.CreateNewBatch = true;
        item.HasExpiryDate = true;
        item.ShelfLifeInDays = shelfLife;

        var ex = Should.Throw<BusinessException>(() => item.ValidateBatchSettings());
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ShelfLifeMustBeGreaterThanZero);
    }

    [Fact]
    public void ValidateBatchSettings_AutoBatchWithExpiry_PositiveShelfLife_Succeeds()
    {
        var item = CreateTestItem();
        item.HasBatchNo = true;
        item.CreateNewBatch = true;
        item.HasExpiryDate = true;
        item.ShelfLifeInDays = 90;

        Should.NotThrow(() => item.ValidateBatchSettings());
    }

    [Fact]
    public void ValidateBatchSettings_BatchSeriesMustEndWithHash()
    {
        var item = CreateTestItem();
        item.HasBatchNo = true;
        item.CreateNewBatch = true;
        item.BatchNumberSeries = "BATCH-NO-HASH";

        var ex = Should.Throw<BusinessException>(() => item.ValidateBatchSettings());
        ex.Code.ShouldBe(MyERPDomainErrorCodes.BatchSeriesMustEndWithHash);
    }

    [Fact]
    public void ValidateBatchSettings_BatchSeriesWithHash_Succeeds()
    {
        var item = CreateTestItem();
        item.HasBatchNo = true;
        item.CreateNewBatch = true;
        item.BatchNumberSeries = "BATCH-.#####";

        Should.NotThrow(() => item.ValidateBatchSettings());
    }

    [Fact]
    public void ValidateBatchSettings_RetainSampleWithoutBatchNo_Throws()
    {
        var item = CreateTestItem();
        item.HasBatchNo = false;
        item.RetainSample = true;
        item.SampleQuantity = 5;

        var ex = Should.Throw<BusinessException>(() => item.ValidateBatchSettings());
        ex.Code.ShouldBe(MyERPDomainErrorCodes.RetainSampleOnlyForBatchItems);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ValidateBatchSettings_RetainSample_ZeroOrNegativeQuantity_Throws(int sampleQty)
    {
        var item = CreateTestItem();
        item.HasBatchNo = true;
        item.RetainSample = true;
        item.SampleQuantity = sampleQty;

        var ex = Should.Throw<BusinessException>(() => item.ValidateBatchSettings());
        ex.Code.ShouldBe(MyERPDomainErrorCodes.SampleQuantityMustBeGreaterThanZero);
    }

    [Fact]
    public void ValidateBatchSettings_RetainSample_ValidBatchItem_Succeeds()
    {
        var item = CreateTestItem();
        item.HasBatchNo = true;
        item.RetainSample = true;
        item.SampleQuantity = 10;

        Should.NotThrow(() => item.ValidateBatchSettings());
    }

    [Fact]
    public void DeliveryTrip_RemoveEmptyStops_DropsPlaceholderStops()
    {
        var trip = new DeliveryTrip(
            Guid.NewGuid(), Guid.NewGuid(), "TRIP-001", "Driver 1", "Van-01", DateTime.UtcNow);

        // Valid stop with address and DN
        trip.AddStop(
            address: "123 Main St",
            customerId: Guid.NewGuid(),
            customerName: "Customer A",
            deliveryNoteId: Guid.NewGuid(),
            deliveryNoteNumber: "DN-001");

        // Manually simulate an empty placeholder stop added via grid mapping
        var emptyStop = new DeliveryStop(
            Guid.NewGuid(), trip.Id, address: string.Empty, customerId: null, customerName: null, deliveryNoteId: null, deliveryNoteNumber: null);
        trip.DeliveryStops.Add(emptyStop);

        trip.DeliveryStops.Count.ShouldBe(2);

        trip.RemoveEmptyStops();

        trip.DeliveryStops.Count.ShouldBe(1);
        trip.DeliveryStops.ShouldContain(s => s.Address == "123 Main St" && s.DeliveryNoteNumber == "DN-001");
    }

    [Fact]
    public void DeliveryTrip_RemoveEmptyStops_KeepsPartiallyFilledStop()
    {
        var trip = new DeliveryTrip(
            Guid.NewGuid(), Guid.NewGuid(), "TRIP-002", "Driver 2", "Van-02", DateTime.UtcNow);

        // Partially filled stop: customer set, but no DN
        trip.AddStop(
            address: "456 Side St",
            customerId: Guid.NewGuid(),
            customerName: "Customer B");

        // Completely empty placeholder stop
        var emptyStop = new DeliveryStop(
            Guid.NewGuid(), trip.Id, address: string.Empty, customerId: null, customerName: null, deliveryNoteId: null, deliveryNoteNumber: null);
        trip.DeliveryStops.Add(emptyStop);

        trip.DeliveryStops.Count.ShouldBe(2);

        trip.RemoveEmptyStops();

        trip.DeliveryStops.Count.ShouldBe(1);
        trip.DeliveryStops.First().CustomerName.ShouldBe("Customer B");
    }

    [Fact]
    public void DeliveryTrip_AddStop_EmptyAddress_ThrowsValidationFailed()
    {
        var trip = new DeliveryTrip(
            Guid.NewGuid(), Guid.NewGuid(), "TRIP-003", "Driver 3", "Van-03", DateTime.UtcNow);

        var ex = Should.Throw<BusinessException>(() =>
            trip.AddStop(address: "   ", customerId: Guid.NewGuid(), customerName: "Customer C"));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public void DeliveryTrip_ValidateStopAddresses_PopulatesCustomerAddressWhenEmpty()
    {
        var trip = new DeliveryTrip(
            Guid.NewGuid(), Guid.NewGuid(), "TRIP-004", "Driver 4", "Van-04", DateTime.UtcNow);

        var stop = trip.AddStop(address: "789 Pine Rd");
        stop.CustomerAddress = null;

        trip.ValidateStopAddresses();

        stop.CustomerAddress.ShouldBe("789 Pine Rd");
    }
}
