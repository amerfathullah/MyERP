using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.Inventory.Entities;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Emailing;

namespace MyERP.Inventory.BackgroundJobs;

/// <summary>
/// Background job that manages Delivery Trip progression and sends dispatch notifications.
/// Automatically starts trips scheduled for today and dispatches email notifications to recipients.
/// Per ERPNext: delivery_trip.notify_customers and delivery_trip.update_status (daily scheduler).
/// </summary>
public class DeliveryTripNotificationJob : AsyncBackgroundJob<DeliveryTripNotificationJobArgs>, ITransientDependency
{
    private readonly IRepository<DeliveryTrip, Guid> _tripRepository;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<DeliveryTripNotificationJob> _logger;

    public DeliveryTripNotificationJob(
        IRepository<DeliveryTrip, Guid> tripRepository,
        IEmailSender emailSender,
        ILogger<DeliveryTripNotificationJob> logger)
    {
        _tripRepository = tripRepository;
        _emailSender = emailSender;
        _logger = logger;
    }

    public override async Task ExecuteAsync(DeliveryTripNotificationJobArgs args)
    {
        var asOfDate = args.AsOfDate ?? DateTime.UtcNow.Date;
        _logger.LogInformation("DeliveryTripNotificationJob: Checking delivery trips for company {CompanyId} as of {Date}",
            args.CompanyId, asOfDate);

        var query = await _tripRepository.WithDetailsAsync(t => t.DeliveryStops);
        var activeTrips = query
            .Where(t => t.CompanyId == args.CompanyId &&
                        (t.Status == DeliveryTripStatus.Scheduled || t.Status == DeliveryTripStatus.InTransit))
            .ToList();

        if (!activeTrips.Any())
            return;

        var notifiedCount = 0;
        foreach (var trip in activeTrips)
        {
            // Auto-start transit if departure time is reached
            if (trip.Status == DeliveryTripStatus.Scheduled && trip.DepartureTime <= DateTime.UtcNow)
            {
                trip.StartTransit();
            }

            // Send notification if not yet sent
            if (!trip.EmailNotificationSent && !string.IsNullOrEmpty(trip.DriverEmail))
            {
                try
                {
                    var subject = $"[DISPATCH] Delivery Trip {trip.TripNumber} Schedule";
                    var body = $@"<h3>Delivery Trip Dispatch Notice</h3>
<p><strong>Trip Number:</strong> {trip.TripNumber}</p>
<p><strong>Driver:</strong> {trip.DriverName ?? trip.Driver}</p>
<p><strong>Vehicle:</strong> {trip.Vehicle}</p>
<p><strong>Departure Time:</strong> {trip.DepartureTime:yyyy-MM-dd HH:mm}</p>
<p><strong>Total Stops:</strong> {trip.DeliveryStops.Count}</p>
<ul>
{string.Join("", trip.DeliveryStops.Select(s => $"<li><strong>{s.CustomerName ?? "Customer"}</strong> - {s.Address} (DN: {s.DeliveryNoteNumber ?? "N/A"})</li>"))}
</ul>";

                    await _emailSender.SendAsync(trip.DriverEmail, subject, body, isBodyHtml: true);
                    trip.EmailNotificationSent = true;
                    notifiedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "DeliveryTripNotificationJob: Failed to send dispatch email for trip {TripNumber}", trip.TripNumber);
                }
            }

            // Check if all stops visited/delivered
            if (trip.DeliveryStops.Any() && trip.DeliveryStops.All(s => s.Visited))
            {
                trip.Complete();
            }

            await _tripRepository.UpdateAsync(trip);
        }

        _logger.LogInformation("DeliveryTripNotificationJob: Processed {Total} trips (notified {Notified}) for company {CompanyId}",
            activeTrips.Count, notifiedCount, args.CompanyId);
    }
}

public class DeliveryTripNotificationJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime? AsOfDate { get; set; }
}
