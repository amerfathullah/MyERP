using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Sales.Entities;
using NSubstitute;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Emailing;
using Xunit;

namespace MyERP.Domain.Tests.Inventory;

/// <summary>
/// Unit tests for Delivery Trip workflow, stop mapping from Delivery Notes, customer notification dispatch,
/// and route ETA calculation. Verifies rules from erpnext/stock/doctype/delivery_trip/delivery_trip.js (#5993).
/// </summary>
public class DeliveryTripWorkflowTests
{
    private readonly IRepository<DeliveryTrip, Guid> _tripRepository = Substitute.For<IRepository<DeliveryTrip, Guid>>();
    private readonly IRepository<DeliveryNote, Guid> _deliveryNoteRepository = Substitute.For<IRepository<DeliveryNote, Guid>>();
    private readonly IRepository<Customer, Guid> _customerRepository = Substitute.For<IRepository<Customer, Guid>>();
    private readonly IEmailSender _emailSender = Substitute.For<IEmailSender>();
    private readonly DeliveryTripAppService _appService;

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();

    public DeliveryTripWorkflowTests()
    {
        _appService = new DeliveryTripAppService(
            _tripRepository,
            _deliveryNoteRepository,
            _customerRepository,
            _emailSender);
    }

    [Fact]
    public async Task GetStopsFromDeliveryNotes_MapsSubmittedNotes_Correctly()
    {
        var dn1Id = Guid.NewGuid();
        var dn1 = new DeliveryNote(dn1Id, _companyId, _customerId, Guid.NewGuid(), "DN-2026-0001", DateTime.UtcNow)
        {
            ShippingAddress = "123 Industrial Park, Sector 5",
            GrandTotal = 1500m
        };
        dn1.AddItem(Guid.NewGuid(), "Goods Description", 10m, 150m, 0m);
        dn1.Submit();

        var customer = new Customer(_customerId, _companyId, "Acme Logistics Sdn Bhd")
        {
            ContactPerson = "Ali Hassan",
            Email = "ali@acmelogistics.my",
            Phone = "+60123456789",
            Address = "123 Industrial Park, Sector 5"
        };

        var dnList = new List<DeliveryNote> { dn1 };
        _deliveryNoteRepository.GetQueryableAsync().Returns(Task.FromResult(dnList.AsQueryable()));

        var custList = new List<Customer> { customer };
        _customerRepository.GetQueryableAsync().Returns(Task.FromResult(custList.AsQueryable()));

        var input = new GetStopsFromDeliveryNotesInput
        {
            CompanyId = _companyId,
            DeliveryNoteIds = new List<Guid> { dn1Id }
        };

        var result = await _appService.GetStopsFromDeliveryNotesAsync(input);

        Assert.NotNull(result);
        Assert.Single(result);
        var stop = result[0];
        Assert.Equal(dn1Id, stop.DeliveryNoteId);
        Assert.Equal("DN-2026-0001", stop.DeliveryNoteNumber);
        Assert.Equal(_customerId, stop.CustomerId);
        Assert.Equal("Acme Logistics Sdn Bhd", stop.CustomerName);
        Assert.Equal("123 Industrial Park, Sector 5", stop.Address);
        Assert.Equal("Ali Hassan", stop.ContactName);
        Assert.Equal("ali@acmelogistics.my", stop.CustomerContact);
        Assert.Equal(1500m, stop.GrandTotal);
    }

    [Fact]
    public async Task GetStopsFromDeliveryNotes_UnsubmittedNote_ThrowsValidationException()
    {
        var dnId = Guid.NewGuid();
        var draftDn = new DeliveryNote(dnId, _companyId, _customerId, Guid.NewGuid(), "DN-2026-DRAFT", DateTime.UtcNow); // Draft status (not submitted)

        var dnList = new List<DeliveryNote> { draftDn };
        _deliveryNoteRepository.GetQueryableAsync().Returns(Task.FromResult(dnList.AsQueryable()));

        var input = new GetStopsFromDeliveryNotesInput
        {
            CompanyId = _companyId,
            DeliveryNoteIds = new List<Guid> { dnId }
        };

        var ex = await Assert.ThrowsAsync<BusinessException>(() => _appService.GetStopsFromDeliveryNotesAsync(input));
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
    }

    [Fact]
    public async Task NotifyCustomers_ScheduledTrip_SendsEmailsAndMarksSent()
    {
        var tripId = Guid.NewGuid();
        var trip = new DeliveryTrip(tripId, _companyId, "TRIP-2026-001", "Driver Bob", "Truck-01", DateTime.UtcNow.AddHours(2));
        trip.AddStop("456 Cyberjaya Hub", _customerId, "Tech Hub", Guid.NewGuid(), "DN-001", 800m);
        trip.Schedule();

        var stop = trip.DeliveryStops.First();
        stop.CustomerContact = "dispatch@techhub.my";

        var trips = new List<DeliveryTrip> { trip };
        _tripRepository.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<DeliveryTrip, object>>[]>())
            .Returns(Task.FromResult(trips.AsQueryable()));

        var custList = new List<Customer>();
        _customerRepository.GetQueryableAsync().Returns(Task.FromResult(custList.AsQueryable()));

        var result = await _appService.NotifyCustomersAsync(tripId);

        Assert.NotNull(result);
        Assert.True(trip.EmailNotificationSent);
        Assert.Equal("dispatch@techhub.my", trip.DeliveryStops.First().EmailSentTo);

        await _emailSender.Received(1).SendAsync(
            "dispatch@techhub.my",
            Arg.Is<string>(s => s.Contains("TRIP-2026-001")),
            Arg.Any<string>(),
            isBodyHtml: true);
    }

    [Fact]
    public async Task CalculateArrivalTimes_MissingDriverAddress_ThrowsValidationException()
    {
        var tripId = Guid.NewGuid();
        var trip = new DeliveryTrip(tripId, _companyId, "TRIP-2026-002", "Driver Dan", "Van-02", DateTime.UtcNow.AddHours(1))
        {
            DriverAddress = null // Missing required driver starting address
        };

        var trips = new List<DeliveryTrip> { trip };
        _tripRepository.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<DeliveryTrip, object>>[]>())
            .Returns(Task.FromResult(trips.AsQueryable()));

        var input = new CalculateArrivalTimesInput
        {
            OptimizeRoute = false,
            AverageSpeedKmH = 50m
        };

        var ex = await Assert.ThrowsAsync<BusinessException>(() => _appService.CalculateArrivalTimesAsync(tripId, input));
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
    }

    [Fact]
    public async Task CalculateArrivalTimes_ValidStops_CalculatesArrivalTimes()
    {
        var tripId = Guid.NewGuid();
        var departureTime = new DateTime(2026, 8, 27, 9, 0, 0, DateTimeKind.Utc);
        var trip = new DeliveryTrip(tripId, _companyId, "TRIP-2026-003", "Driver Dan", "Van-02", departureTime)
        {
            DriverAddress = "HQ Depot, Port Klang"
        };

        trip.AddStop("Stop 1, Shah Alam", _customerId, "Client A", null, null, 0m, distance: 20m);
        trip.AddStop("Stop 2, Petaling Jaya", _customerId, "Client B", null, null, 0m, distance: 15m);

        var trips = new List<DeliveryTrip> { trip };
        _tripRepository.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<DeliveryTrip, object>>[]>())
            .Returns(Task.FromResult(trips.AsQueryable()));

        var input = new CalculateArrivalTimesInput
        {
            OptimizeRoute = false,
            AverageSpeedKmH = 40m // 20km = 30 min travel time + 10 min stop = 40 min
        };

        var result = await _appService.CalculateArrivalTimesAsync(tripId, input);

        Assert.NotNull(result);
        var stops = trip.DeliveryStops.ToList();
        Assert.NotNull(stops[0].EstimatedArrival);
        Assert.NotNull(stops[1].EstimatedArrival);
        Assert.True(stops[1].EstimatedArrival > stops[0].EstimatedArrival);
    }
}
