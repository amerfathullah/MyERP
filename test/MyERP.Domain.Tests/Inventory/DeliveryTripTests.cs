using System;
using MyERP.Inventory.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Inventory;

public class DeliveryTripTests
{
    private static DeliveryTrip CreateTrip() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "MAT-DT-2026-0001", "Driver John", "VAN-01", DateTime.UtcNow);

    [Fact]
    public void Create_SetsDefaults()
    {
        var trip = CreateTrip();
        trip.Status.ShouldBe(DeliveryTripStatus.Draft);
        trip.TripNumber.ShouldBe("MAT-DT-2026-0001");
        trip.Driver.ShouldBe("Driver John");
        trip.Vehicle.ShouldBe("VAN-01");
        trip.DeliveryStops.ShouldBeEmpty();
        trip.TotalDistance.ShouldBe(0);
    }

    [Fact]
    public void AddStop_CalculatesTotalDistance()
    {
        var trip = CreateTrip();
        trip.AddStop("123 Main St", distance: 15.5m);
        trip.AddStop("456 Market St", distance: 20.5m);

        trip.DeliveryStops.Count.ShouldBe(2);
        trip.TotalDistance.ShouldBe(36.0m);
    }

    [Fact]
    public void RemoveStop_RecalculatesDistance()
    {
        var trip = CreateTrip();
        var s1 = trip.AddStop("Stop 1", distance: 10m);
        var s2 = trip.AddStop("Stop 2", distance: 20m);

        trip.RemoveStop(s1.Id);
        trip.DeliveryStops.Count.ShouldBe(1);
        trip.TotalDistance.ShouldBe(20m);
    }

    [Fact]
    public void Schedule_WithoutStops_Throws()
    {
        var trip = CreateTrip();
        Should.Throw<BusinessException>(() => trip.Schedule());
    }

    [Fact]
    public void StateTransitions_FullLifecycle()
    {
        var trip = CreateTrip();
        trip.AddStop("Stop 1", distance: 10m);

        trip.Schedule();
        trip.Status.ShouldBe(DeliveryTripStatus.Scheduled);

        trip.StartTransit();
        trip.Status.ShouldBe(DeliveryTripStatus.InTransit);

        trip.Complete();
        trip.Status.ShouldBe(DeliveryTripStatus.Completed);
    }

    [Fact]
    public void Cancel_CompletedTrip_Throws()
    {
        var trip = CreateTrip();
        trip.AddStop("Stop 1", distance: 10m);
        trip.Schedule();
        trip.StartTransit();
        trip.Complete();

        Should.Throw<BusinessException>(() => trip.Cancel());
    }
}
