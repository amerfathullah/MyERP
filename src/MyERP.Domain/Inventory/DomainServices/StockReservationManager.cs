using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Inventory.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Inventory.DomainServices;

/// <summary>
/// Domain service for Stock Reservation Entry management.
/// Handles FIFO consumption on delivery, cancel/recreate pattern, and availability validation.
/// Per DO-NOT: "Allow stock reservation beyond available qty (actual - already_reserved)"
/// Per DO-NOT: "Allow Stock Reservation Entry amendment (must cancel and recreate)"
/// Per DO-NOT: "Allow pick list modification after stock reservation entries exist"
/// </summary>
public class StockReservationManager : DomainService
{
    private readonly IRepository<StockReservationEntry, Guid> _sreRepository;
    private readonly IRepository<Bin, Guid> _binRepository;

    public StockReservationManager(
        IRepository<StockReservationEntry, Guid> sreRepository,
        IRepository<Bin, Guid> binRepository)
    {
        _sreRepository = sreRepository;
        _binRepository = binRepository;
    }

    /// <summary>
    /// Validates that sufficient unreserved stock exists before creating a reservation.
    /// Available = Bin.ActualQty - SUM(active SRE reserved qty for same item+warehouse).
    /// </summary>
    public async Task ValidateAvailabilityAsync(Guid itemId, Guid warehouseId, decimal requestedQty)
    {
        // Get actual stock
        var binQueryable = await _binRepository.GetQueryableAsync();
        var actualQty = binQueryable
            .Where(b => b.ItemId == itemId && b.WarehouseId == warehouseId)
            .Select(b => b.ActualQty)
            .FirstOrDefault();

        // Get already reserved
        var sreQueryable = await _sreRepository.GetQueryableAsync();
        var reservedQty = sreQueryable
            .Where(s => s.ItemId == itemId
                && s.WarehouseId == warehouseId
                && s.Status == DocumentStatus.Submitted)
            .Sum(s => s.ReservedQty - s.DeliveredQty);

        var available = actualQty - reservedQty;

        if (requestedQty > available)
        {
            throw new BusinessException("MyERP:05031")
                .WithData("itemId", itemId)
                .WithData("warehouseId", warehouseId)
                .WithData("requested", requestedQty)
                .WithData("available", available);
        }
    }

    /// <summary>
    /// Consumes reserved stock when delivery is made (FIFO by creation date).
    /// Per ERPNext: oldest reservations consumed first.
    /// Returns list of consumed SRE IDs with quantities.
    /// </summary>
    public async Task<ReservationConsumption[]> ConsumeOnDeliveryAsync(
        Guid itemId, Guid warehouseId, decimal deliveredQty, Guid? salesOrderId = null)
    {
        var queryable = await _sreRepository.GetQueryableAsync();

        // FIFO: oldest first, matching item+warehouse
        var activeSres = queryable
            .Where(s => s.ItemId == itemId
                && s.WarehouseId == warehouseId
                && s.Status == DocumentStatus.Submitted
                && (salesOrderId == null || s.VoucherId == salesOrderId))
            .OrderBy(s => s.CreationTime)
            .ToList();

        var consumed = new System.Collections.Generic.List<ReservationConsumption>();
        var remaining = deliveredQty;

        foreach (var sre in activeSres)
        {
            if (remaining <= 0) break;

            var available = sre.ReservedQty - sre.DeliveredQty;
            if (available <= 0) continue;

            var consume = Math.Min(remaining, available);
            sre.RecordDelivery(consume);
            await _sreRepository.UpdateAsync(sre);

            consumed.Add(new ReservationConsumption
            {
                StockReservationEntryId = sre.Id,
                ConsumedQty = consume
            });

            remaining -= consume;
        }

        return consumed.ToArray();
    }

    /// <summary>
    /// Cancels all active reservations for a Sales Order (used on SO cancel/close).
    /// </summary>
    public async Task CancelReservationsForOrderAsync(Guid salesOrderId)
    {
        var queryable = await _sreRepository.GetQueryableAsync();
        var activeSres = queryable
            .Where(s => s.VoucherId == salesOrderId
                && s.Status == DocumentStatus.Submitted)
            .ToList();

        foreach (var sre in activeSres)
        {
            sre.Cancel();
            await _sreRepository.UpdateAsync(sre);
        }
    }

    /// <summary>
    /// Gets total reserved qty for an item+warehouse across all active SREs.
    /// </summary>
    public async Task<decimal> GetReservedQtyAsync(Guid itemId, Guid warehouseId)
    {
        var queryable = await _sreRepository.GetQueryableAsync();
        return queryable
            .Where(s => s.ItemId == itemId
                && s.WarehouseId == warehouseId
                && s.Status == DocumentStatus.Submitted)
            .Sum(s => s.ReservedQty - s.DeliveredQty);
    }
}

public class ReservationConsumption
{
    public Guid StockReservationEntryId { get; set; }
    public decimal ConsumedQty { get; set; }
}
