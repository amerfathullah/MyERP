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
}
