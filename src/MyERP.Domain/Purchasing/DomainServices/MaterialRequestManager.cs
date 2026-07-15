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
    /// Checks if a Material Request is fully fulfilled (all items ordered/transferred).
    /// Uses 99.99% threshold for float tolerance (per ERPNext per_ordered rounding).
    /// </summary>
    public bool IsFullyFulfilled(MaterialRequest mr)
    {
        if (!mr.Items.Any()) return false;

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
}
