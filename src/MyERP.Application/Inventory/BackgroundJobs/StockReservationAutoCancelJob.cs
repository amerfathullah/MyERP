using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.Core;
using MyERP.Inventory.Entities;
using MyERP.Sales.Entities;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory.BackgroundJobs;

/// <summary>
/// Background job that automatically cancels stale or unfulfilled stock reservations
/// for completed, closed, or cancelled Sales Orders to release bin reserved quantities.
/// Per ERPNext: stock_reservation_entry.auto_cancel_stock_reservation_entries (daily scheduler).
/// </summary>
public class StockReservationAutoCancelJob : AsyncBackgroundJob<StockReservationAutoCancelJobArgs>, ITransientDependency
{
    private readonly IRepository<StockReservationEntry, Guid> _reservationRepository;
    private readonly IRepository<SalesOrder, Guid> _salesOrderRepository;
    private readonly ILogger<StockReservationAutoCancelJob> _logger;

    public StockReservationAutoCancelJob(
        IRepository<StockReservationEntry, Guid> reservationRepository,
        IRepository<SalesOrder, Guid> salesOrderRepository,
        ILogger<StockReservationAutoCancelJob> logger)
    {
        _reservationRepository = reservationRepository;
        _salesOrderRepository = salesOrderRepository;
        _logger = logger;
    }

    public override async Task ExecuteAsync(StockReservationAutoCancelJobArgs args)
    {
        _logger.LogInformation("StockReservationAutoCancelJob: Checking stale stock reservations for company {CompanyId}",
            args.CompanyId);

        var query = await _reservationRepository.GetQueryableAsync();
        var activeReservations = query
            .Where(r => r.CompanyId == args.CompanyId &&
                        r.Status == DocumentStatus.Submitted &&
                        r.VoucherType == "SalesOrder")
            .ToList();

        if (!activeReservations.Any())
            return;

        var soIds = activeReservations.Select(r => r.VoucherId).Distinct().ToList();
        var soQuery = await _salesOrderRepository.GetQueryableAsync();
        var salesOrders = soQuery
            .Where(s => soIds.Contains(s.Id))
            .ToList();

        var cancelledCount = 0;
        foreach (var reservation in activeReservations)
        {
            var so = salesOrders.FirstOrDefault(s => s.Id == reservation.VoucherId);

            // Cancel reservation if Sales Order is Cancelled, or 100% delivered, or has 0 available qty left
            if (so == null ||
                so.Status == DocumentStatus.Cancelled ||
                so.PerDelivered >= 100m ||
                reservation.AvailableQty <= 0)
            {
                try
                {
                    reservation.Cancel();
                    await _reservationRepository.UpdateAsync(reservation);
                    cancelledCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "StockReservationAutoCancelJob: Failed to cancel reservation {ReservationId}", reservation.Id);
                }
            }
        }

        _logger.LogInformation("StockReservationAutoCancelJob: Cancelled {Count} unneeded stock reservations for company {CompanyId}",
            cancelledCount, args.CompanyId);
    }
}

public class StockReservationAutoCancelJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
}
