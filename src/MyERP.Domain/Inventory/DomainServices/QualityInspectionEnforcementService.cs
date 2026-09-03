using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.Inventory.Entities;
using MyERP.Settings;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Settings;

namespace MyERP.Inventory.DomainServices;

/// <summary>
/// Enforces Quality Inspection requirements on stock transactions.
/// Per ERPNext: items with inspection_required flags must have a
/// submitted+accepted QI linked before the source document can be submitted.
/// Respects ActionIfQualityInspectionNotSubmitted / ActionIfQualityInspectionRejected
/// settings (Stop blocks the transaction, Warn logs and lets it proceed).
/// </summary>
public class QualityInspectionEnforcementService : DomainService
{
    private readonly IRepository<QualityInspection, Guid> _qiRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly ISettingProvider _settingProvider;

    public QualityInspectionEnforcementService(
        IRepository<QualityInspection, Guid> qiRepository,
        IRepository<Item, Guid> itemRepository,
        ISettingProvider settingProvider)
    {
        _qiRepository = qiRepository;
        _itemRepository = itemRepository;
        _settingProvider = settingProvider;
    }

    private async Task<bool> ShouldStopAsync(string settingName)
    {
        var action = await _settingProvider.GetOrNullAsync(settingName);
        return !string.Equals(action, "Warn", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Validates that all items requiring QI in a Purchase Receipt have
    /// a submitted+accepted Quality Inspection linked.
    /// </summary>
    public async Task ValidateForPurchaseReceiptAsync(
        Guid receiptId, Guid[] itemIds, Guid? tenantId)
    {
        await ValidateAsync(receiptId, "PurchaseReceipt", itemIds,
            InspectionType.Incoming, i => i.InspectionRequiredBeforePurchase, tenantId);
    }

    /// <summary>
    /// Validates that all items requiring QI in a Delivery Note have
    /// a submitted+accepted Quality Inspection linked.
    /// </summary>
    public async Task ValidateForDeliveryNoteAsync(
        Guid deliveryNoteId, Guid[] itemIds, Guid? tenantId)
    {
        await ValidateAsync(deliveryNoteId, "DeliveryNote", itemIds,
            InspectionType.Outgoing, i => i.InspectionRequiredBeforeDelivery, tenantId);
    }

    /// <summary>
    /// Validates QI for Stock Entry outward stock movements.
    /// Only applies to Material Issue and Material Transfer types (outgoing stock).
    /// Per DO-NOT: Material Consumption for Manufacture is explicitly excluded.
    /// </summary>
    public async Task ValidateForStockEntryAsync(
        Guid stockEntryId, Guid[] itemIds, string purpose, Guid? tenantId)
    {
        // Material Consumption for Manufacture explicitly excluded per DO-NOT
        if (purpose == "MaterialConsumptionForManufacture") return;

        // Only validate outward purposes (Issue, Transfer, Send to Subcontractor)
        var outwardPurposes = new[] { "MaterialIssue", "MaterialTransfer", "SendToSubcontractor" };
        if (!outwardPurposes.Contains(purpose)) return;

        await ValidateAsync(stockEntryId, "StockEntry", itemIds,
            InspectionType.Outgoing, i => i.InspectionRequiredBeforeDelivery, tenantId);
    }

    /// <summary>
    /// Validates QI for Sales Invoice with UpdateStock=true (direct sales).
    /// </summary>
    public async Task ValidateForSalesInvoiceAsync(
        Guid invoiceId, Guid[] itemIds, Guid? tenantId)
    {
        await ValidateAsync(invoiceId, "SalesInvoice", itemIds,
            InspectionType.Outgoing, i => i.InspectionRequiredBeforeDelivery, tenantId);
    }

    /// <summary>
    /// Validates QI for manufactured Finished Goods before they enter stock.
    /// Per ERPNext: Manufacture Stock Entry FG items with inspection_required_before_delivery
    /// must have a submitted+accepted QI before production is recorded.
    /// The QI is linked to the Work Order (not the SE) for the FG item.
    /// </summary>
    public async Task ValidateForManufactureAsync(
        Guid workOrderId, Guid fgItemId, Guid? tenantId)
    {
        var item = await _itemRepository.FindAsync(fgItemId);
        if (item == null || !item.InspectionRequiredBeforeDelivery) return;

        var qiQueryable = await _qiRepository.GetQueryableAsync();
        var hasAccepted = qiQueryable
            .Any(qi => qi.ReferenceType == "WorkOrder"
                    && qi.ReferenceId == workOrderId
                    && qi.ItemId == fgItemId
                    && qi.DocStatus == Core.DocumentStatus.Submitted
                    && qi.Status == InspectionStatus.Accepted);

        if (!hasAccepted)
        {
            var hasRejected = qiQueryable
                .Any(qi => qi.ReferenceType == "WorkOrder"
                        && qi.ReferenceId == workOrderId
                        && qi.ItemId == fgItemId
                        && qi.DocStatus == Core.DocumentStatus.Submitted
                        && qi.Status == InspectionStatus.Rejected);

            if (hasRejected)
            {
                if (!await ShouldStopAsync(MyERPSettings.Stock.ActionIfQualityInspectionRejected))
                {
                    Logger.LogWarning("QI rejected for item {ItemName} on WorkOrder {WorkOrderId}, allowed to proceed (Warn action).", item.ItemName, workOrderId);
                    return;
                }

                throw new BusinessException(MyERPDomainErrorCodes.QualityInspectionRejected)
                    .WithData("items", item.ItemName)
                    .WithData("referenceType", "WorkOrder");
            }

            if (!await ShouldStopAsync(MyERPSettings.Stock.ActionIfQualityInspectionNotSubmitted))
            {
                Logger.LogWarning("QI not submitted for item {ItemName} on WorkOrder {WorkOrderId}, allowed to proceed (Warn action).", item.ItemName, workOrderId);
                return;
            }

            throw new BusinessException(MyERPDomainErrorCodes.QualityInspectionRequired)
                .WithData("items", item.ItemName)
                .WithData("referenceType", "WorkOrder");
        }
    }

    private async Task ValidateAsync(
        Guid referenceId, string referenceType, Guid[] itemIds,
        InspectionType expectedType, Func<Item, bool> requiresInspection, Guid? tenantId)
    {
        if (itemIds.Length == 0) return;

        // Per ERPNext commit fad1a32e63: allow making quality inspection after purchase or delivery
        if (referenceType is "PurchaseReceipt" or "PurchaseInvoice" or "SalesInvoice" or "DeliveryNote")
        {
            var allowAfterStr = await _settingProvider.GetOrNullAsync(MyERPSettings.Stock.AllowToMakeQualityInspectionAfterPurchaseOrDelivery);
            if (bool.TryParse(allowAfterStr, out var allowAfter) && allowAfter) return;
        }

        // Load items to check which require inspection
        var items = await _itemRepository.GetListAsync(i => itemIds.Contains(i.Id));
        var requiresQi = items.Where(requiresInspection).ToList();

        if (!requiresQi.Any()) return;

        // Check for submitted+accepted QIs for this document
        var qiQueryable = await _qiRepository.GetQueryableAsync();
        var existingQis = qiQueryable
            .Where(qi => qi.ReferenceType == referenceType
                      && qi.ReferenceId == referenceId
                      && qi.DocStatus == Core.DocumentStatus.Submitted
                      && qi.Status == InspectionStatus.Accepted)
            .Select(qi => qi.ItemId)
            .ToHashSet();

        // Find items that require QI but don't have one
        var missingQi = requiresQi.Where(item => !existingQis.Contains(item.Id)).ToList();

        if (missingQi.Any())
        {
            var itemNames = string.Join(", ", missingQi.Select(i => i.ItemName));
            if (!await ShouldStopAsync(MyERPSettings.Stock.ActionIfQualityInspectionNotSubmitted))
            {
                Logger.LogWarning("QI not submitted for items {ItemNames} on {ReferenceType} {ReferenceId}, allowed to proceed (Warn action).", itemNames, referenceType, referenceId);
            }
            else
            {
                throw new BusinessException(MyERPDomainErrorCodes.QualityInspectionRequired)
                    .WithData("items", itemNames)
                    .WithData("referenceType", referenceType);
            }
        }

        // Also check for any rejected QIs (hard block)
        var rejectedQis = qiQueryable
            .Where(qi => qi.ReferenceType == referenceType
                      && qi.ReferenceId == referenceId
                      && qi.DocStatus == Core.DocumentStatus.Submitted
                      && qi.Status == InspectionStatus.Rejected)
            .Select(qi => qi.ItemId)
            .ToHashSet();

        var rejectedItems = requiresQi.Where(item => rejectedQis.Contains(item.Id)).ToList();
        if (rejectedItems.Any())
        {
            var itemNames = string.Join(", ", rejectedItems.Select(i => i.ItemName));
            if (!await ShouldStopAsync(MyERPSettings.Stock.ActionIfQualityInspectionRejected))
            {
                Logger.LogWarning("QI rejected for items {ItemNames} on {ReferenceType} {ReferenceId}, allowed to proceed (Warn action).", itemNames, referenceType, referenceId);
                return;
            }

            throw new BusinessException(MyERPDomainErrorCodes.QualityInspectionRejected)
                .WithData("items", itemNames)
                .WithData("referenceType", referenceType);
        }
    }
}
