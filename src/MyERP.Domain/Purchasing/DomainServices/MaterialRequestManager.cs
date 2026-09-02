using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Purchasing.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Purchasing.DomainServices;

/// <summary>
/// Domain service for Material Request business rules.
/// Manages MR fulfillment tracking, type-specific validations, and over-fulfillment prevention.
/// </summary>
public class MaterialRequestManager : DomainService
{
    private readonly IRepository<MaterialRequest, Guid> _mrRepository;

    public MaterialRequestManager(IRepository<MaterialRequest, Guid> mrRepository)
    {
        _mrRepository = mrRepository;
    }

    /// <summary>
    /// Validates that a Material Request can be submitted.
    /// Must have at least one item.
    /// </summary>
    public void ValidateForSubmission(MaterialRequest mr)
    {
        if (!mr.Items.Any())
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustHaveItems);
    }

    /// <summary>
    /// Updates ordered/transferred/received quantities on MR items when downstream documents are submitted.
    /// Per DO-NOT: "Allow Material Request over-fulfillment beyond mr_qty_allowance percentage"
    /// </summary>
    public async Task UpdateFulfillmentAsync(Guid mrId, Guid mrItemId, decimal qty, bool reverse = false)
    {
        var mr = await _mrRepository.GetAsync(mrId);
        var mrItem = mr.Items.FirstOrDefault(i => i.Id == mrItemId);
        if (mrItem == null) return;

        if (reverse)
        {
            mrItem.OrderedQuantity = Math.Max(0, mrItem.OrderedQuantity - qty);
        }
        else
        {
            mrItem.OrderedQuantity += qty;
        }

        await _mrRepository.UpdateAsync(mr);
    }

    /// <summary>
    /// Validates ordered/transferred quantity against allowed MR quantity.
    /// Per ERPNext PR #53049 / commit 30c3ff2efe: uses StockQty instead of transaction Qty for allowed threshold calculation.
    /// </summary>
    public void ValidateOrderedQty(MaterialRequestItem item, decimal attemptingOrderedQty, decimal mrQtyAllowancePct = 0m)
    {
        var stockQty = item.StockQty > 0 ? item.StockQty : item.Quantity;
        var allowedQty = stockQty + (stockQty * (mrQtyAllowancePct / 100m));
        if (attemptingOrderedQty > allowedQty)
        {
            throw new BusinessException(MyERPDomainErrorCodes.OverTransfer)
                .WithData("item", item.ItemName)
                .WithData("maxAllowed", allowedQty)
                .WithData("attempted", attemptingOrderedQty)
                .WithData("alreadyTransferred", item.OrderedQuantity);
        }
    }

    /// <summary>
    /// Checks if a Material Request is fully fulfilled (all items ordered/transferred or received).
    /// Uses 99.99% threshold for float tolerance (per ERPNext per_ordered/per_received rounding).
    /// Per ERPNext PR #56621: if PerReceived >= 99.99m, it is fulfilled even if PerOrdered is less.
    /// </summary>
    public bool IsFullyFulfilled(MaterialRequest mr)
    {
        if (!mr.Items.Any()) return false;

        var allReceived = mr.Items.All(item =>
        {
            if (item.Quantity <= 0) return true;
            var perReceived = (item.ReceivedQuantity / item.Quantity) * 100;
            return perReceived >= 99.99m;
        });

        if (allReceived) return true;

        return mr.Items.All(item =>
        {
            if (item.Quantity <= 0) return true;
            var perOrdered = (item.OrderedQuantity / item.Quantity) * 100;
            return perOrdered >= 99.99m;
        });
    }

    /// <summary>
    /// Gets the pending (unfulfilled) quantity for an MR item.
    /// </summary>
    public static decimal GetPendingQty(MaterialRequestItem item)
    {
        return Math.Max(0, item.Quantity - item.OrderedQuantity);
    }

    /// <summary>
    /// Validates that Material Request items linked to a Sales Order line match the item and company (gotcha PR #58443).
    /// </summary>
    public async Task ValidateWithSalesOrderAsync(MaterialRequest mr, IRepository<Sales.Entities.SalesOrder, Guid> soRepository)
    {
        var soItems = mr.Items.Where(i => i.SalesOrderId.HasValue).ToList();
        if (!soItems.Any()) return;

        var soIds = soItems.Select(i => i.SalesOrderId!.Value).Distinct().ToList();
        foreach (var soId in soIds)
        {
            var so = await soRepository.FindAsync(soId);
            if (so == null) continue;

            if (so.CompanyId != mr.CompanyId)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Material Request company does not match Sales Order {so.OrderNumber} company.");
            }

            var soItemRows = soItems.Where(i => i.SalesOrderId == soId).ToList();
            foreach (var mrItem in soItemRows)
            {
                if (mrItem.SalesOrderItemId.HasValue)
                {
                    var targetSoItem = so.Items.FirstOrDefault(i => i.Id == mrItem.SalesOrderItemId.Value);
                    if (targetSoItem != null && targetSoItem.ItemId != mrItem.ItemId)
                    {
                        throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                            .WithData("detail", "Material Request item does not match linked Sales Order item row.");
                    }
                }
            }
        }
    }
}
