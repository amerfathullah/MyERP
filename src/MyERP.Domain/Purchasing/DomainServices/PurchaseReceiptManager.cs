using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Assets.Entities;
using MyERP.Purchasing.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Purchasing.DomainServices;

/// <summary>
/// Domain service for Purchase Receipt business rules.
/// Validates receipt against linked PO, return documents, and dependent cancellation guards.
/// </summary>
public class PurchaseReceiptManager : DomainService
{
    private readonly IRepository<PurchaseOrder, Guid> _poRepository;
    private readonly IRepository<PurchaseReceipt, Guid> _prRepository;

    public PurchaseReceiptManager(
        IRepository<PurchaseOrder, Guid> poRepository,
        IRepository<PurchaseReceipt, Guid> prRepository)
    {
        _poRepository = poRepository;
        _prRepository = prRepository;
    }

    /// <summary>
    /// Validates receipt quantities and posting date against the linked Purchase Order.
    /// Prevents over-receipt and temporal ordering violations.
    /// </summary>
    public async Task ValidateAgainstPurchaseOrderAsync(PurchaseReceipt receipt)
    {
        if (receipt.IsReturn || !receipt.PurchaseOrderId.HasValue) return;

        var po = await _poRepository.GetAsync(receipt.PurchaseOrderId.Value);

        // PO must be in an active fulfillment state
        if (po.Status == Core.DocumentStatus.Cancelled || po.Status == Core.DocumentStatus.Closed)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("documentType", "Purchase Order")
                .WithData("status", po.Status.ToString());
        }

        // Temporal ordering: cannot receive before ordering
        if (receipt.PostingDate < po.OrderDate)
        {
            throw new BusinessException(MyERPDomainErrorCodes.PostingDateBeforePODate)
                .WithData("postingDate", receipt.PostingDate)
                .WithData("poDate", po.OrderDate)
                .WithData("poNumber", po.OrderNumber);
        }

        foreach (var prItem in receipt.Items)
        {
            var poItem = po.Items.FirstOrDefault(i => i.ItemId == prItem.ItemId);
            if (poItem != null && prItem.Quantity > poItem.PendingReceiptQty)
            {
                throw new BusinessException("MyERP:08006")
                    .WithData("itemName", prItem.Description)
                    .WithData("orderedQty", poItem.Quantity)
                    .WithData("receivedQty", poItem.ReceivedQty)
                    .WithData("attemptedQty", prItem.Quantity);
            }
        }
    }

    /// <summary>
    /// Validates that no submitted Assets exist on the original document before allowing return.
    /// Per DO-NOT: "Allow purchase return (PR/PI) when submitted Assets exist on the original document"
    /// </summary>
    public async Task ValidateAssetReturnAsync(
        PurchaseReceipt returnReceipt,
        IRepository<Asset, Guid> assetRepository)
    {
        if (!returnReceipt.IsReturn || !returnReceipt.ReturnAgainstId.HasValue) return;

        var assetQuery = await assetRepository.GetQueryableAsync();
        var hasSubmittedAssets = assetQuery.Any(a =>
            a.PurchaseReceiptId == returnReceipt.ReturnAgainstId.Value
            && a.Status != Assets.AssetStatus.Draft
            && a.Status != Assets.AssetStatus.Cancelled);

        if (hasSubmittedAssets)
        {
            throw new BusinessException(MyERPDomainErrorCodes.AssetExistsOnReturnDocument)
                .WithData("documentType", "Purchase Receipt")
                .WithData("returnAgainst", returnReceipt.ReturnAgainstId.Value);
        }
    }

    /// <summary>
    /// Validates from_warehouse rules on purchase document items.
    /// (1) from_warehouse cannot equal target warehouse (no-op transfer blocked)
    /// (2) from_warehouse cannot be set on subcontracted documents
    /// </summary>
    public void ValidateFromWarehouse(PurchaseReceipt receipt)
    {
        foreach (var item in receipt.Items)
        {
            if (item.FromWarehouseId.HasValue && item.FromWarehouseId == item.WarehouseId)
            {
                throw new BusinessException(MyERPDomainErrorCodes.FromWarehouseEqualsTargetWarehouse)
                    .WithData("row", item.Description);
            }

            if (item.FromWarehouseId.HasValue && receipt.IsSubcontracted)
            {
                throw new BusinessException(MyERPDomainErrorCodes.FromWarehouseOnSubcontractedDocument)
                    .WithData("row", item.Description);
            }
        }
    }

    /// <summary>
    /// Validates return receipt (goods return to supplier) business rules.
    /// Return qty per item cannot exceed original receipt qty.
    /// </summary>
    public async Task ValidateReturnAsync(PurchaseReceipt returnReceipt)
    {
        if (!returnReceipt.IsReturn || !returnReceipt.ReturnAgainstId.HasValue) return;

        var original = await _prRepository.GetAsync(returnReceipt.ReturnAgainstId.Value);

        foreach (var returnItem in returnReceipt.Items)
        {
            var originalItem = original.Items.FirstOrDefault(i => i.ItemId == returnItem.ItemId);
            if (originalItem != null && Math.Abs(returnItem.Quantity) > originalItem.Quantity)
            {
                throw new BusinessException("MyERP:08004")
                    .WithData("itemName", returnItem.Description)
                    .WithData("originalQty", originalItem.Quantity)
                    .WithData("returnQty", Math.Abs(returnItem.Quantity));
            }
        }
    }

    /// <summary>
    /// Validates a PR can be cancelled — blocks if submitted Purchase Invoices reference this receipt.
    /// Per DO-NOT: must cancel children first.
    /// </summary>
    public async Task ValidateCanCancelAsync(
        PurchaseReceipt receipt,
        IRepository<PurchaseInvoice, Guid> piRepository)
    {
        var piQuery = await piRepository.GetQueryableAsync();
        var prItemIds = receipt.Items.Select(i => i.Id).ToList();
        var hasSubmittedPI = piQuery.Any(pi =>
            pi.Items.Any(i => i.PurchaseReceiptItemId.HasValue && prItemIds.Contains(i.PurchaseReceiptItemId.Value))
            && pi.Status != Core.DocumentStatus.Draft
            && pi.Status != Core.DocumentStatus.Cancelled);

        if (hasSubmittedPI)
        {
            throw new BusinessException("MyERP:01010")
                .WithData("documentType", "Purchase Receipt")
                .WithData("dependent", "Purchase Invoice");
        }
    }
}
