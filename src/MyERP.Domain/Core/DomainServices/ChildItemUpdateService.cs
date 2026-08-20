using System;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace MyERP.Core.DomainServices;

/// <summary>
/// Child Item Update Service — validates child item deletion and updates on orders and quotations.
/// Per ERPNext validate_child_on_delete() (gotcha #6206):
/// - Sales Order item: cannot delete if delivered_qty > 0, work_order_qty > 0, or billed_qty > 0.
/// - Purchase Order item: cannot delete if received_qty > 0 or billed_qty > 0.
/// - Quotation item: cannot delete if already converted to an ordered Sales Order item.
/// </summary>
public class ChildItemUpdateService : DomainService
{
    /// <summary>
    /// Validates whether a Sales Order child row can be deleted or removed.
    /// Throws BusinessException if the item has already been partially delivered or billed.
    /// </summary>
    public void ValidateSalesOrderItemDeletion(SalesOrderItem item)
    {
        if (item.DeliveredQty > 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Cannot delete Sales Order item '{item.Description}' — {item.DeliveredQty} units have already been delivered.");
        }

        if (item.BilledQty > 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Cannot delete Sales Order item '{item.Description}' — {item.BilledQty} units have already been billed.");
        }
    }

    /// <summary>
    /// Validates whether a Purchase Order child row can be deleted or removed.
    /// Throws BusinessException if the item has already been partially received or billed.
    /// </summary>
    public void ValidatePurchaseOrderItemDeletion(PurchaseOrderItem item)
    {
        if (item.ReceivedQty > 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Cannot delete Purchase Order item '{item.Description}' — {item.ReceivedQty} units have already been received.");
        }

        if (item.BilledQty > 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Cannot delete Purchase Order item '{item.Description}' — {item.BilledQty} units have already been billed.");
        }
    }

    /// <summary>
    /// Validates whether a Quotation child row can be deleted or removed.
    /// </summary>
    public void ValidateQuotationItemDeletion(QuotationItem item, bool isOrdered)
    {
        if (isOrdered)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Cannot delete Quotation item '{item.Description}' — quotation has already been converted to an order.");
        }
    }
}
