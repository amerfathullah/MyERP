using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Purchasing.DomainServices;

/// <summary>
/// Domain service for Purchase Invoice business rules.
/// Validates return documents, supplier hold, over-billing, duplicate supplier invoice numbers,
/// and temporal ordering against linked purchase orders.
/// </summary>
public class PurchaseInvoiceManager : DomainService
{
    private readonly IRepository<Supplier, Guid> _supplierRepository;
    private readonly IRepository<PurchaseInvoice, Guid> _invoiceRepository;
    private readonly IRepository<PurchaseOrder, Guid> _poRepository;

    public PurchaseInvoiceManager(
        IRepository<Supplier, Guid> supplierRepository,
        IRepository<PurchaseInvoice, Guid> invoiceRepository,
        IRepository<PurchaseOrder, Guid> poRepository)
    {
        _supplierRepository = supplierRepository;
        _invoiceRepository = invoiceRepository;
        _poRepository = poRepository;
    }

    /// <summary>
    /// Validates PI posting date is not before any linked PO's transaction date.
    /// Per ERPNext validate_posting_date_with_po: temporal ordering enforcement.
    /// </summary>
    public async Task ValidatePostingDateWithPOAsync(PurchaseInvoice invoice)
    {
        var poIds = invoice.Items
            .Where(i => i.PurchaseOrderItemId.HasValue)
            .Select(i => i.PurchaseOrderItemId!.Value)
            .Distinct()
            .ToList();

        if (!poIds.Any()) return;

        // Get all linked POs via item references (PurchaseOrderItemId → find parent PO)
        var poQuery = await _poRepository.GetQueryableAsync();
        var linkedPOs = poQuery
            .Where(po => po.Items.Any(i => poIds.Contains(i.Id)))
            .Select(po => new { po.OrderNumber, po.OrderDate })
            .ToList();

        foreach (var po in linkedPOs)
        {
            if (po.OrderDate > invoice.IssueDate)
            {
                throw new BusinessException(MyERPDomainErrorCodes.PostingDateBeforePODate)
                    .WithData("postingDate", invoice.IssueDate)
                    .WithData("poDate", po.OrderDate)
                    .WithData("poNumber", po.OrderNumber);
            }
        }
    }

    /// <summary>
    /// Validates that no submitted Assets exist on the original document before allowing return.
    /// Per DO-NOT: "Allow purchase return (PR/PI) when submitted Assets exist on the original document"
    /// </summary>
    public async Task ValidateAssetReturnAsync(
        PurchaseInvoice returnInvoice,
        IRepository<Assets.Entities.Asset, Guid> assetRepository)
    {
        if (!returnInvoice.IsReturn || !returnInvoice.ReturnAgainstId.HasValue) return;

        var assetQuery = await assetRepository.GetQueryableAsync();
        var hasSubmittedAssets = assetQuery.Any(a =>
            a.PurchaseInvoiceId == returnInvoice.ReturnAgainstId.Value
            && a.Status != Assets.AssetStatus.Draft
            && a.Status != Assets.AssetStatus.Cancelled);

        if (hasSubmittedAssets)
        {
            throw new BusinessException(MyERPDomainErrorCodes.AssetExistsOnReturnDocument)
                .WithData("documentType", "Purchase Invoice")
                .WithData("returnAgainst", returnInvoice.ReturnAgainstId.Value);
        }
    }

    /// <summary>
    /// Validates supplier eligibility for purchase invoices.
    /// HoldType.All or HoldType.Invoices blocks PI submission.
    /// Returns (debit notes) are allowed even when supplier is on hold.
    /// </summary>
    public async Task ValidateSupplierForInvoiceAsync(Guid supplierId, bool isReturn)
    {
        if (isReturn) return; // Debit notes always allowed per ERPNext

        var supplier = await _supplierRepository.GetAsync(supplierId);

        if (supplier.HoldType == SupplierHoldType.All ||
            supplier.HoldType == SupplierHoldType.Invoices)
        {
            throw new BusinessException(MyERPDomainErrorCodes.SupplierOnHold)
                .WithData("supplierName", supplier.Name)
                .WithData("holdType", supplier.HoldType.ToString());
        }
    }

    /// <summary>
    /// Validates return invoice (debit note) business rules.
    /// Per DO-NOT: negative qty required, must reference original, exchange rate must match,
    /// return qty cannot exceed original.
    /// </summary>
    public async Task ValidateReturnAsync(PurchaseInvoice returnInvoice)
    {
        if (!returnInvoice.IsReturn) return;

        // Must have negative quantities
        if (returnInvoice.Items.Any(i => i.Quantity > 0))
        {
            throw new BusinessException("MyERP:08001")
                .WithData("documentType", "Purchase Invoice");
        }

        // Must reference original invoice
        if (!returnInvoice.ReturnAgainstId.HasValue)
        {
            throw new BusinessException("MyERP:08002")
                .WithData("documentType", "Purchase Invoice");
        }

        // Load original to validate exchange rate and qty caps
        var original = await _invoiceRepository.GetAsync(returnInvoice.ReturnAgainstId.Value);

        if (returnInvoice.ExchangeRate != original.ExchangeRate)
        {
            throw new BusinessException("MyERP:08003")
                .WithData("expected", original.ExchangeRate)
                .WithData("actual", returnInvoice.ExchangeRate);
        }

        // Return qty per item cannot exceed original qty
        foreach (var returnItem in returnInvoice.Items)
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
    /// Validates no duplicate supplier invoice numbers exist for the same supplier + company.
    /// Prevents accidental double-entry of the same vendor bill.
    /// </summary>
    public async Task ValidateNoDuplicateSupplierInvoiceAsync(
        Guid supplierId, Guid companyId, string? supplierInvoiceNumber, Guid? excludeId = null)
    {
        if (string.IsNullOrWhiteSpace(supplierInvoiceNumber)) return;

        var query = await _invoiceRepository.GetQueryableAsync();
        var exists = query.Any(pi =>
            pi.SupplierId == supplierId
            && pi.CompanyId == companyId
            && pi.SupplierInvoiceNumber == supplierInvoiceNumber
            && pi.Status != Core.DocumentStatus.Cancelled
            && (!excludeId.HasValue || pi.Id != excludeId.Value));

        if (exists)
        {
            throw new BusinessException("MyERP:04009")
                .WithData("supplierInvoiceNumber", supplierInvoiceNumber);
        }
    }

    /// <summary>
    /// Validates that a PI cannot be cancelled if it has been paid.
    /// Must reverse payments before cancelling.
    /// </summary>
    public void ValidateCanCancel(PurchaseInvoice invoice)
    {
        if (invoice.AmountPaid > 0)
        {
            throw new BusinessException("MyERP:01002")
                .WithData("documentType", "Purchase Invoice")
                .WithData("amountPaid", invoice.AmountPaid);
        }
    }
}
