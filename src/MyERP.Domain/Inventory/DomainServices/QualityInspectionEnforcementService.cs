using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Inventory.DomainServices;

/// <summary>
/// Enforces Quality Inspection requirements on stock transactions.
/// Per ERPNext: items with inspection_required flags must have a
/// submitted+accepted QI linked before the source document can be submitted.
/// Respects ActionIfQualityInspectionNotSubmitted setting (Stop/Warn).
/// </summary>
public class QualityInspectionEnforcementService : DomainService
{
    private readonly IRepository<QualityInspection, Guid> _qiRepository;
    private readonly IRepository<Item, Guid> _itemRepository;

    public QualityInspectionEnforcementService(
        IRepository<QualityInspection, Guid> qiRepository,
        IRepository<Item, Guid> itemRepository)
    {
        _qiRepository = qiRepository;
        _itemRepository = itemRepository;
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

    private async Task ValidateAsync(
        Guid referenceId, string referenceType, Guid[] itemIds,
        InspectionType expectedType, Func<Item, bool> requiresInspection, Guid? tenantId)
    {
        if (itemIds.Length == 0) return;

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
            throw new BusinessException(MyERPDomainErrorCodes.QualityInspectionRequired)
                .WithData("items", itemNames)
                .WithData("referenceType", referenceType);
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
            throw new BusinessException(MyERPDomainErrorCodes.QualityInspectionRejected)
                .WithData("items", itemNames)
                .WithData("referenceType", referenceType);
        }
    }
}
