using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Shared;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

public class StockReservationEntryDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public Guid ItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public string VoucherType { get; set; } = null!;
    public Guid VoucherId { get; set; }
    public Guid? VoucherDetailId { get; set; }
    public decimal ReservedQty { get; set; }
    public decimal DeliveredQty { get; set; }
    public decimal AvailableQty { get; set; }
    public int Status { get; set; }
    public DateTime CreationTime { get; set; }
}

public class CreateStockReservationDto
{
    public Guid CompanyId { get; set; }
    public Guid ItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public string VoucherType { get; set; } = "SalesOrder";
    public Guid VoucherId { get; set; }
    public Guid? VoucherDetailId { get; set; }
    public decimal ReservedQty { get; set; }
    public Guid? BatchId { get; set; }
}

public class GetStockReservationListDto : CompanyFilteredPagedRequestDto
{
    public Guid? ItemId { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? VoucherId { get; set; }
    public new string? Status { get; set; }
}

/// <summary>
/// Application service for Stock Reservation Entry management.
/// Per DO-NOT: "Allow stock reservation beyond available qty (actual - already_reserved)"
/// Per DO-NOT: "Allow Stock Reservation Entry amendment (must cancel and recreate)"
/// </summary>
[Authorize(MyERPPermissions.StockEntries.Default)]
public class StockReservationAppService : ApplicationService
{
    private readonly IRepository<StockReservationEntry, Guid> _repository;

    public StockReservationAppService(IRepository<StockReservationEntry, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<PagedResultDto<StockReservationEntryDto>> GetListAsync(GetStockReservationListDto input)
    {
        var query = await _repository.GetQueryableAsync();
        if (input.CompanyId.HasValue)
            query = query.Where(s => s.CompanyId == input.CompanyId.Value);
        if (input.ItemId.HasValue)
            query = query.Where(s => s.ItemId == input.ItemId.Value);
        if (input.WarehouseId.HasValue)
            query = query.Where(s => s.WarehouseId == input.WarehouseId.Value);
        if (input.VoucherId.HasValue)
            query = query.Where(s => s.VoucherId == input.VoucherId.Value);
        if (!string.IsNullOrWhiteSpace(input.Status) && Enum.TryParse<DocumentStatus>(input.Status, true, out var status))
            query = query.Where(s => s.Status == status);

        var totalCount = query.Count();
        var items = query.OrderByDescending(s => s.CreationTime)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<StockReservationEntryDto>(totalCount, items.Select(x => ObjectMapper.Map<StockReservationEntry, StockReservationEntryDto>(x)).ToList());
    }

    public async Task<StockReservationEntryDto> GetAsync(Guid id)
    {
        var sre = await _repository.GetAsync(id);
        return ObjectMapper.Map<StockReservationEntry, StockReservationEntryDto>(sre);
    }

    /// <summary>
    /// Creates and submits a stock reservation entry.
    /// Validates available stock before reserving.
    /// </summary>
    [Authorize(MyERPPermissions.StockEntries.Create)]
    public async Task<StockReservationEntryDto> CreateAsync(CreateStockReservationDto input)
    {
        // Validate availability using domain service
        var reservationManager = LazyServiceProvider.LazyGetRequiredService<StockReservationManager>();
        await reservationManager.ValidateAvailabilityAsync(input.ItemId, input.WarehouseId, input.ReservedQty);

        var sre = new StockReservationEntry(
            GuidGenerator.Create(), input.CompanyId, input.ItemId, input.WarehouseId,
            input.VoucherType, input.VoucherId, input.ReservedQty, CurrentTenant.Id)
        {
            VoucherDetailId = input.VoucherDetailId,
            BatchId = input.BatchId,
        };

        sre.Submit(); // Auto-submit on creation (per ERPNext pattern)
        await _repository.InsertAsync(sre);

        // Update Bin reserved qty
        var binService = LazyServiceProvider.LazyGetRequiredService<BinService>();
        await binService.UpdateReservedQtyAsync(input.ItemId, input.WarehouseId, input.ReservedQty);

        return ObjectMapper.Map<StockReservationEntry, StockReservationEntryDto>(sre);
    }

    /// <summary>
    /// Cancels a stock reservation entry and releases the reserved stock.
    /// Per DO-NOT: amendment is not allowed — must cancel and recreate.
    /// </summary>
    [Authorize(MyERPPermissions.StockEntries.Cancel)]
    public async Task<StockReservationEntryDto> CancelAsync(Guid id)
    {
        var sre = await _repository.GetAsync(id);
        var remainingReserved = sre.AvailableQty;
        sre.Cancel();
        await _repository.UpdateAsync(sre);

        // Release reserved qty from Bin
        if (remainingReserved > 0)
        {
            var binService = LazyServiceProvider.LazyGetRequiredService<BinService>();
            await binService.UpdateReservedQtyAsync(sre.ItemId, sre.WarehouseId, -remainingReserved);
        }

        return ObjectMapper.Map<StockReservationEntry, StockReservationEntryDto>(sre);
    }

    /// <summary>
    /// Gets total reserved qty for an item at a warehouse.
    /// </summary>
    public async Task<decimal> GetReservedQtyAsync(Guid itemId, Guid warehouseId)
    {
        var reservationManager = LazyServiceProvider.LazyGetRequiredService<StockReservationManager>();
        return await reservationManager.GetReservedQtyAsync(itemId, warehouseId);
    }

    /// <summary>
    /// Cancels all active reservations for a Sales Order.
    /// Called when SO is cancelled or closed.
    /// </summary>
    [Authorize(MyERPPermissions.StockEntries.Cancel)]
    public async Task CancelForOrderAsync(Guid salesOrderId)
    {
        var reservationManager = LazyServiceProvider.LazyGetRequiredService<StockReservationManager>();
        await reservationManager.CancelReservationsForOrderAsync(salesOrderId);
    }
}
