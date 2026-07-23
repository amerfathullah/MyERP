using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Inventory.DomainServices;

/// <summary>
/// Manages 2-step inter-warehouse transfers via transit warehouse.
/// Per ERPNext pattern: Source → Transit (SendToWarehouse) → Destination (ReceiveAtWarehouse).
/// 
/// This ensures:
/// 1. Stock is properly tracked during transit (not double-counted)
/// 2. Cancel of the first leg reverses transit stock
/// 3. Second leg can only be created from a posted first leg
/// 4. Second leg references the first via OutgoingStockEntryId
/// 
/// Per gotcha #23: Stock Entry transit cancel cascade.
/// Per gotcha #239: SE on_submit releases WO reservation BEFORE SLE creation.
/// Per DO-NOT: "Allow same-warehouse Material Transfer when all inventory dimensions are identical"
/// </summary>
public class TransitTransferService : DomainService
{
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;
    private readonly IRepository<StockEntry, Guid> _stockEntryRepository;

    public TransitTransferService(
        IRepository<Company, Guid> companyRepository,
        IRepository<Warehouse, Guid> warehouseRepository,
        IRepository<StockEntry, Guid> stockEntryRepository)
    {
        _companyRepository = companyRepository;
        _warehouseRepository = warehouseRepository;
        _stockEntryRepository = stockEntryRepository;
    }

    /// <summary>
    /// Creates the first leg of a transit transfer: Source Warehouse → Transit Warehouse.
    /// Entry type = SendToWarehouse.
    /// The transit warehouse is resolved from Company.DefaultInTransitWarehouseId.
    /// </summary>
    public async Task<StockEntry> CreateSendToWarehouseAsync(
        Guid companyId,
        Guid sourceWarehouseId,
        Guid finalDestinationWarehouseId,
        TransitTransferItem[] items,
        DateTime postingDate,
        string? notes = null,
        Guid? tenantId = null)
    {
        // Resolve transit warehouse from company
        var company = await _companyRepository.GetAsync(companyId);
        var transitWarehouseId = await ResolveTransitWarehouseAsync(companyId);

        if (transitWarehouseId == Guid.Empty)
        {
            throw new BusinessException("MyERP:05037")
                .WithData("companyName", company.Name);
        }

        // Validate source ≠ destination
        if (sourceWarehouseId == finalDestinationWarehouseId)
        {
            throw new BusinessException(MyERPDomainErrorCodes.SameWarehouseTransfer);
        }

        // Create SendToWarehouse stock entry (leg 1: source → transit)
        var entry = new StockEntry(
            Guid.NewGuid(), companyId, StockEntryType.SendToWarehouse, postingDate, tenantId);

        entry.Notes = notes ?? $"Transit transfer to {finalDestinationWarehouseId}";

        foreach (var item in items)
        {
            entry.AddItem(
                itemId: item.ItemId,
                quantity: item.Quantity,
                sourceWarehouseId: sourceWarehouseId,
                targetWarehouseId: transitWarehouseId,
                valuationRate: item.ValuationRate);
        }

        return entry;
    }

    /// <summary>
    /// Creates the second leg of a transit transfer: Transit Warehouse → Destination Warehouse.
    /// Entry type = ReceiveAtWarehouse.
    /// Must reference the outgoing (first-leg) stock entry.
    /// Per ERPNext: second leg can only be created from a posted first leg.
    /// </summary>
    public async Task<StockEntry> CreateReceiveAtWarehouseAsync(
        Guid outgoingStockEntryId,
        Guid destinationWarehouseId,
        DateTime postingDate,
        string? notes = null)
    {
        // Load and validate the outgoing entry
        var outgoing = await _stockEntryRepository.GetAsync(outgoingStockEntryId);

        if (outgoing.EntryType != StockEntryType.SendToWarehouse)
        {
            throw new BusinessException("MyERP:05038")
                .WithData("entryType", outgoing.EntryType.ToString());
        }

        if (outgoing.Status != DocumentStatus.Posted)
        {
            throw new BusinessException("MyERP:05039")
                .WithData("status", outgoing.Status.ToString());
        }

        // Resolve transit warehouse (the target of the outgoing entry)
        var transitWarehouseId = outgoing.Items.FirstOrDefault()?.TargetWarehouseId
            ?? throw new BusinessException("MyERP:05037");

        // Validate destination ≠ transit
        if (destinationWarehouseId == transitWarehouseId)
        {
            throw new BusinessException(MyERPDomainErrorCodes.SameWarehouseTransfer);
        }

        // Create ReceiveAtWarehouse stock entry (leg 2: transit → destination)
        var entry = new StockEntry(
            Guid.NewGuid(), outgoing.CompanyId, StockEntryType.ReceiveAtWarehouse,
            postingDate, outgoing.TenantId);

        entry.Notes = notes ?? $"Received from transit (ref: {outgoing.EntryNumber})";

        // Copy items from outgoing entry, swapping warehouses
        foreach (var item in outgoing.Items)
        {
            entry.AddItem(
                itemId: item.ItemId,
                quantity: item.Quantity,
                sourceWarehouseId: transitWarehouseId,
                targetWarehouseId: destinationWarehouseId,
                valuationRate: item.ValuationRate);
        }

        // Link back to the outgoing entry for audit trail
        entry.ReferenceType = "StockEntry";
        entry.ReferenceId = outgoingStockEntryId;

        return entry;
    }

    /// <summary>
    /// Validates that a SendToWarehouse entry can be cancelled.
    /// Per gotcha #23: transit cancel requires checking if a ReceiveAtWarehouse entry exists.
    /// If a second leg exists and is Posted, the first leg cannot be cancelled (must cancel second first).
    /// </summary>
    public async Task ValidateTransitCancelAsync(Guid stockEntryId)
    {
        var query = await _stockEntryRepository.GetQueryableAsync();
        var receivingEntry = query.FirstOrDefault(e =>
            e.ReferenceType == "StockEntry"
            && e.ReferenceId == stockEntryId
            && e.EntryType == StockEntryType.ReceiveAtWarehouse
            && e.Status != DocumentStatus.Cancelled);

        if (receivingEntry != null)
        {
            throw new BusinessException("MyERP:05040")
                .WithData("receivingEntryNumber", receivingEntry.EntryNumber ?? receivingEntry.Id.ToString());
        }
    }

    /// <summary>
    /// Gets pending transit transfers (sent but not yet received) for a company.
    /// Useful for dashboard/alerts — shows stock "in transit" that needs action.
    /// </summary>
    public async Task<PendingTransitTransfer[]> GetPendingTransfersAsync(Guid companyId)
    {
        var query = await _stockEntryRepository.GetQueryableAsync();

        // Find all posted SendToWarehouse entries
        var sentEntries = query
            .Where(e => e.CompanyId == companyId
                && e.EntryType == StockEntryType.SendToWarehouse
                && e.Status == DocumentStatus.Posted)
            .ToList();

        if (!sentEntries.Any()) return Array.Empty<PendingTransitTransfer>();

        // Find which have been received
        var sentIds = sentEntries.Select(e => e.Id).ToList();
        var receivedIds = query
            .Where(e => e.ReferenceType == "StockEntry"
                && sentIds.Contains(e.ReferenceId!.Value)
                && e.EntryType == StockEntryType.ReceiveAtWarehouse
                && e.Status == DocumentStatus.Posted)
            .Select(e => e.ReferenceId!.Value)
            .ToList();

        // Return those NOT yet received
        return sentEntries
            .Where(e => !receivedIds.Contains(e.Id))
            .Select(e => new PendingTransitTransfer(
                e.Id,
                e.EntryNumber ?? e.Id.ToString(),
                e.PostingDate,
                e.Items.FirstOrDefault()?.SourceWarehouseId ?? Guid.Empty,
                e.Items.Sum(i => i.Quantity),
                e.Items.Count))
            .ToArray();
    }

    /// <summary>
    /// Resolves the transit warehouse for a company.
    /// Per ERPNext: Company has default_in_transit_warehouse (seeded as "Goods In Transit").
    /// Fallback: searches for warehouse with WarehouseType == Transit.
    /// </summary>
    private async Task<Guid> ResolveTransitWarehouseAsync(Guid companyId)
    {
        var query = await _warehouseRepository.GetQueryableAsync();

        // First: look for a warehouse named "Goods In Transit" for this company
        var transit = query.FirstOrDefault(w =>
            w.CompanyId == companyId
            && w.Name.Contains("In Transit")
            && !w.IsGroup);

        if (transit != null) return transit.Id;

        // Fallback: any non-group warehouse containing "transit" in name
        transit = query.FirstOrDefault(w =>
            w.CompanyId == companyId
            && w.Name.ToLower().Contains("transit")
            && !w.IsGroup);

        return transit?.Id ?? Guid.Empty;
    }
}

/// <summary>Input for transit transfer item creation.</summary>
public record TransitTransferItem(Guid ItemId, decimal Quantity, decimal? ValuationRate = null);

/// <summary>Result record for pending transit transfers dashboard.</summary>
public record PendingTransitTransfer(
    Guid StockEntryId,
    string EntryNumber,
    DateTime PostingDate,
    Guid SourceWarehouseId,
    decimal TotalQuantity,
    int ItemCount);
