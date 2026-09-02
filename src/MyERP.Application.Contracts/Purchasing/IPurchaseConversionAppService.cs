using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Purchasing;

/// <summary>
/// Handles document-to-document conversion along the purchase pipeline.
/// Mirrors ERPNext's "Make" button functionality for purchasing.
/// </summary>
public interface IPurchaseConversionAppService : IApplicationService
{
    /// <summary>Convert a submitted Purchase Order into a Purchase Receipt.</summary>
    Task<PurchaseReceiptDto> ConvertPurchaseOrderToReceiptAsync(Guid purchaseOrderId);

    /// <summary>Convert a submitted Purchase Order into a Purchase Invoice.</summary>
    Task<PurchaseInvoiceDto> ConvertPurchaseOrderToInvoiceAsync(Guid purchaseOrderId);

    /// <summary>Convert a submitted Purchase Receipt into a Purchase Invoice.</summary>
    Task<PurchaseInvoiceDto> ConvertPurchaseReceiptToInvoiceAsync(Guid purchaseReceiptId);

    /// <summary>Convert a submitted Purchase Invoice into a Purchase Receipt (excluding returned items, PR #50971).</summary>
    Task<PurchaseReceiptDto> ConvertPurchaseInvoiceToReceiptAsync(Guid purchaseInvoiceId);

    /// <summary>Convert a submitted RFQ into a Supplier Quotation for a specific supplier.</summary>
    Task<SupplierQuotationDto> ConvertRfqToSupplierQuotationAsync(Guid rfqId, Guid supplierId);

    /// <summary>Convert a submitted Supplier Quotation into a Purchase Order.</summary>
    Task<PurchaseOrderDto> ConvertSupplierQuotationToPurchaseOrderAsync(Guid supplierQuotationId);

    /// <summary>
    /// Create Purchase Orders from MR with per-item supplier selection and qty adjustment.
    /// Per ERPNext PR #57676: creates one PO per supplier from selected items.
    /// </summary>
    Task<SupplierSelectionResultDto> CreatePurchaseOrdersFromMaterialRequestAsync(
        CreatePurchaseOrdersFromMrDto input);
}
