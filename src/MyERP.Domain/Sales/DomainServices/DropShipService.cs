using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Volo.Abp;
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
    private readonly IRepository<Supplier, Guid> _supplierRepository;
    private readonly IRepository<MyERP.Core.Entities.Company, Guid> _companyRepository;
    private readonly IGuidGenerator _guidGenerator;

    public DropShipService(
        IRepository<PurchaseOrder, Guid> poRepository,
        IRepository<Supplier, Guid> supplierRepository,
        IRepository<MyERP.Core.Entities.Company, Guid> companyRepository,
        IGuidGenerator guidGenerator)
    {
        _poRepository = poRepository;
        _supplierRepository = supplierRepository;
        _companyRepository = companyRepository;
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

            var supplier = await _supplierRepository.FindAsync(supplierId);
            var company = await _companyRepository.FindAsync(salesOrder.CompanyId);
            po.CurrencyCode = supplier?.DefaultCurrency ?? company?.CurrencyCode ?? "MYR";
            po.CustomerId = salesOrder.CustomerId;
            po.ShippingAddressId = salesOrder.ShippingAddressId;
            po.Notes = $"Drop-ship order for SO {salesOrder.OrderNumber}";

            foreach (var soItem in supplierGroup)
            {
                var poItem = po.AddItem(
                    soItem.ItemId,
                    soItem.Description,
                    soItem.Quantity,
                    soItem.UnitPrice, // buying rate should ideally come from supplier pricing
                    soItem.TaxAmount,
                    soItem.Uom,
                    deliveredBySupplier: true);
                // Link back to the SO row so PO submit/cancel keeps SO Item.OrderedQty in sync.
                poItem.SalesOrderId = salesOrder.Id;
                poItem.SalesOrderItemId = soItem.Id;
                poItem.ConversionFactor = soItem.ConversionFactor > 0 ? soItem.ConversionFactor : 1m;
            }

            await _poRepository.InsertAsync(po, autoSave: true);
            createdPoIds.Add(po.Id);
        }

        return createdPoIds;
    }

    /// <summary>
    /// Resolves the Sales Order rows (and quantities) to order from each supplier for the manual
    /// "Purchase Order" action. Per ERPNext sales_order/mapper.py make_purchase_order
    /// (commit 92a4ccffab): each selection must identify exactly one SO row (by row id, so repeated
    /// item codes are not conflated); a row is included only when it is not closed and not yet fully
    /// ordered; qty = qty - ordered_qty / conversion_factor. When a row is selected twice, the last
    /// supplier wins (dictionary semantics).
    /// </summary>
    public static IReadOnlyList<SalesOrderPurchaseLine> ResolvePurchaseLines(
        SalesOrder salesOrder,
        IEnumerable<(Guid SalesOrderItemId, Guid SupplierId)> selections)
    {
        var supplierByRow = new Dictionary<Guid, Guid>();
        foreach (var (rowId, supplierId) in selections)
        {
            if (salesOrder.Items.Count(i => i.Id == rowId) != 1)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Select a valid Sales Order row for Item {rowId}.");
            }
            supplierByRow[rowId] = supplierId;
        }

        var lines = new List<SalesOrderPurchaseLine>();
        foreach (var item in salesOrder.Items)
        {
            if (!supplierByRow.TryGetValue(item.Id, out var supplierId)) continue;
            if (item.IsClosed || item.OrderedQty >= item.StockQty) continue;

            var conversionFactor = item.ConversionFactor > 0 ? item.ConversionFactor : 1m;
            var qty = item.Quantity - item.OrderedQty / conversionFactor;
            if (qty <= 0) continue;

            lines.Add(new SalesOrderPurchaseLine(supplierId, item, Math.Round(qty, 4)));
        }

        return lines;
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

/// <summary>One Sales Order row to order from a supplier, with the still-unordered quantity.</summary>
public record SalesOrderPurchaseLine(Guid SupplierId, SalesOrderItem Item, decimal Quantity);
