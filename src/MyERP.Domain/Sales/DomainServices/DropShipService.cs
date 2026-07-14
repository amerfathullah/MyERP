using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;

namespace MyERP.Sales.DomainServices;

/// <summary>
/// Handles Drop-Ship items on Sales Orders.
/// Drop-ship = supplier ships directly to customer. No warehouse stock movement.
/// On SO submit: auto-creates Purchase Order(s) to the drop-ship supplier(s).
/// On PO receipt: updates SO delivered qty (delivery tracked via PO, not DN).
/// 
/// Per ERPNext:
/// - Drop-ship items do NOT create stock ledger entries (bypass warehouse)
/// - Drop-ship items are NOT reserved in Bin (no stock involvement)
/// - Supplier cannot be changed on SO after PO exists (locked once ordered_qty > 0)
/// - Per DO-NOT: "Allow drop-ship items to create stock ledger entries"
/// </summary>
public class DropShipService : DomainService
{
    private readonly IRepository<PurchaseOrder, Guid> _poRepository;
    private readonly IGuidGenerator _guidGenerator;

    public DropShipService(
        IRepository<PurchaseOrder, Guid> poRepository,
        IGuidGenerator guidGenerator)
    {
        _poRepository = poRepository;
        _guidGenerator = guidGenerator;
    }

    /// <summary>
    /// Creates Purchase Orders for drop-ship items on a submitted Sales Order.
    /// Groups items by supplier, creating one PO per supplier.
    /// </summary>
    public async Task<List<Guid>> CreateDropShipPurchaseOrdersAsync(
        SalesOrder salesOrder,
        Func<string, Guid, Task<string>> generatePoNumber)
    {
        var dropShipItems = salesOrder.Items
            .Where(i => i.DeliveredBySupplier && i.SupplierId.HasValue)
            .ToList();

        if (!dropShipItems.Any())
            return new List<Guid>();

        var createdPoIds = new List<Guid>();

        // Group by supplier (one PO per supplier)
        var grouped = dropShipItems.GroupBy(i => i.SupplierId!.Value);

        foreach (var supplierGroup in grouped)
        {
            var supplierId = supplierGroup.Key;
            var poNumber = await generatePoNumber("PurchaseOrder", salesOrder.CompanyId);

            var po = new PurchaseOrder(
                _guidGenerator.Create(),
                salesOrder.CompanyId,
                supplierId,
                poNumber,
                salesOrder.OrderDate,
                salesOrder.TenantId);

            po.Notes = $"Drop-ship order for SO {salesOrder.OrderNumber}";

            foreach (var soItem in supplierGroup)
            {
                po.AddItem(
                    soItem.ItemId,
                    soItem.Description,
                    soItem.Quantity,
                    soItem.UnitPrice, // buying rate should ideally come from supplier pricing
                    soItem.TaxAmount,
                    soItem.Uom);
            }

            await _poRepository.InsertAsync(po, autoSave: true);
            createdPoIds.Add(po.Id);
        }

        return createdPoIds;
    }

    /// <summary>
    /// Checks whether a Sales Order has any drop-ship items.
    /// </summary>
    public static bool HasDropShipItems(SalesOrder order)
    {
        return order.Items.Any(i => i.DeliveredBySupplier);
    }

    /// <summary>
    /// Gets item IDs that should be EXCLUDED from stock reservation (drop-ship items).
    /// Drop-ship items bypass the warehouse entirely.
    /// </summary>
    public static HashSet<Guid> GetDropShipItemIds(SalesOrder order)
    {
        return order.Items
            .Where(i => i.DeliveredBySupplier)
            .Select(i => i.ItemId)
            .ToHashSet();
    }
}
