using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using MyERP.Inventory.DomainServices;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using MyERP.Shared;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Purchasing;

[Authorize(MyERPPermissions.PurchaseReceipts.Default)]
public class PurchaseReceiptAppService : ApplicationService, IPurchaseReceiptAppService
{
    private readonly IRepository<PurchaseReceipt, Guid> _repository;
    private readonly IRepository<PurchaseOrder, Guid> _purchaseOrderRepository;
    private readonly IRepository<DocumentActivityLog, Guid> _activityLogRepository;
    private readonly IDocumentNumberGenerator _numberGenerator;
    private readonly StockValuationService _valuationService;
    private readonly BinService _binService;
    private readonly DocumentPostingOrchestrator _postingOrchestrator;
    private readonly QualityInspectionEnforcementService _qiEnforcement;
    private readonly ItemTransactionValidationService _itemValidation;

    public PurchaseReceiptAppService(
        IRepository<PurchaseReceipt, Guid> repository,
        IRepository<PurchaseOrder, Guid> purchaseOrderRepository,
        IRepository<DocumentActivityLog, Guid> activityLogRepository,
        IDocumentNumberGenerator numberGenerator,
        StockValuationService valuationService,
        BinService binService,
        DocumentPostingOrchestrator postingOrchestrator,
        QualityInspectionEnforcementService qiEnforcement,
        ItemTransactionValidationService itemValidation)
    {
        _repository = repository;
        _purchaseOrderRepository = purchaseOrderRepository;
        _activityLogRepository = activityLogRepository;
        _numberGenerator = numberGenerator;
        _valuationService = valuationService;
        _binService = binService;
        _postingOrchestrator = postingOrchestrator;
        _qiEnforcement = qiEnforcement;
        _itemValidation = itemValidation;
    }

    public async Task<PurchaseReceiptDto> GetAsync(Guid id)
    {
        var receipt = await _repository.GetAsync(id);
        return MapToDto(receipt);
    }

    public async Task<PagedResultDto<PurchaseReceiptDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();

        if (input.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter.ToLower();
            query = query.Where(x => x.ReceiptNumber.ToLower().Contains(filter));
        }

        if (!string.IsNullOrWhiteSpace(input.Status) && Enum.TryParse<Core.DocumentStatus>(input.Status, true, out var status))
            query = query.Where(x => x.Status == status);

        var totalCount = query.Count();
        var list = query
            .OrderByDescending(x => x.PostingDate)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        return new PagedResultDto<PurchaseReceiptDto>(
            totalCount,
            list.Select(MapToDto).ToList());
    }

    [Authorize(MyERPPermissions.PurchaseReceipts.Create)]
    public async Task<PurchaseReceiptDto> CreateAsync(CreatePurchaseReceiptDto input)
    {
        // Input validation
        Check.NotDefaultOrNull<Guid>(input.CompanyId, nameof(input.CompanyId));
        Check.NotDefaultOrNull<Guid>(input.SupplierId, nameof(input.SupplierId));
        Check.NotDefaultOrNull<Guid>(input.WarehouseId, nameof(input.WarehouseId));
        if (input.Items == null || input.Items.Count == 0)
            throw new Volo.Abp.BusinessException("MyERP:01007")
                .WithData("documentType", "Purchase Receipt");

        // Validate all items are active
        var itemIds = input.Items.Select(i => i.ItemId).ToList();
        await _itemValidation.ValidateItemsForTransactionAsync(itemIds);

        var receiptNumber = await _numberGenerator.GenerateAsync("PurchaseReceipt", input.CompanyId);

        var receipt = new PurchaseReceipt(
            GuidGenerator.Create(),
            input.CompanyId,
            input.SupplierId,
            input.WarehouseId,
            receiptNumber,
            input.PostingDate,
            CurrentTenant.Id);

        receipt.PurchaseOrderId = input.PurchaseOrderId;
        receipt.SupplierDeliveryNote = input.SupplierDeliveryNote;
        receipt.IsReturn = input.IsReturn;
        receipt.ReturnAgainstId = input.ReturnAgainstId;
        receipt.Notes = input.Notes;

        foreach (var item in input.Items)
        {
            receipt.AddItem(item.ItemId, item.Description, item.Quantity, item.UnitPrice, item.TaxAmount, item.Uom, item.PurchaseOrderItemId);
        }

        await _repository.InsertAsync(receipt, autoSave: true);
        return MapToDto(receipt);
    }

    [Authorize(MyERPPermissions.PurchaseReceipts.Submit)]
    public async Task<PurchaseReceiptDto> SubmitAsync(Guid id)
    {
        var receipt = await _repository.GetAsync(id);

        // Buying controller validations via domain manager
        var prManager = LazyServiceProvider
            .LazyGetRequiredService<MyERP.Purchasing.DomainServices.PurchaseReceiptManager>();

        // Temporal ordering + over-receipt + PO status validation
        await prManager.ValidateAgainstPurchaseOrderAsync(receipt);

        // Asset return blocking (submitted assets on original doc)
        var assetRepo = LazyServiceProvider
            .LazyGetRequiredService<IRepository<MyERP.Assets.Entities.Asset, Guid>>();
        await prManager.ValidateAssetReturnAsync(receipt, assetRepo);

        // From-warehouse validation (same warehouse + subcontracting blocks)
        prManager.ValidateFromWarehouse(receipt);

        // Enforce Quality Inspection requirement (per item flags) — skip for returns
        if (!receipt.IsReturn)
        {
            var itemIds = receipt.Items.Select(i => i.ItemId).ToArray();
            await _qiEnforcement.ValidateForPurchaseReceiptAsync(receipt.Id, itemIds, receipt.TenantId);

            // Over-receipt validation: PR qty cannot exceed PO pending receipt qty
            if (receipt.PurchaseOrderId.HasValue)
            {
                var po = await _purchaseOrderRepository.GetAsync(receipt.PurchaseOrderId.Value);

                // Block receipt against Cancelled or Closed POs
                if (po.Status == Core.DocumentStatus.Cancelled || po.Status == Core.DocumentStatus.Closed)
                {
                    throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                        .WithData("documentType", "Purchase Order")
                        .WithData("status", po.Status.ToString());
                }

                foreach (var prItem in receipt.Items.Where(i => i.PurchaseOrderItemId.HasValue))
                {
                    var poItem = po.Items.FirstOrDefault(i => i.Id == prItem.PurchaseOrderItemId!.Value);
                    if (poItem != null && prItem.Quantity > poItem.PendingReceiptQty)
                    {
                        throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.OverReceipt)
                            .WithData("item", prItem.Description)
                            .WithData("ordered", poItem.Quantity)
                            .WithData("received", poItem.ReceivedQty)
                            .WithData("attempted", prItem.Quantity);
                    }
                }
            }
        }

        receipt.Submit();

        if (receipt.IsReturn)
        {
            // Validate return qty does not exceed original receipt qty
            // Per DO-NOT: "Allow return qty to exceed (original qty - already returned qty)"
            if (receipt.ReturnAgainstId.HasValue)
            {
                var originalPr = await _repository.GetAsync(receipt.ReturnAgainstId.Value);
                foreach (var returnItem in receipt.Items)
                {
                    var originalItem = originalPr.Items
                        .FirstOrDefault(i => i.ItemId == returnItem.ItemId);
                    if (originalItem != null)
                    {
                        var maxReturnQty = originalItem.Quantity;
                        var returnQty = Math.Abs(returnItem.Quantity);
                        if (returnQty > maxReturnQty)
                        {
                            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ReturnQtyExceedsOriginal)
                                .WithData("item", returnItem.Description)
                                .WithData("originalQty", maxReturnQty)
                                .WithData("returnQty", returnQty);
                        }
                    }
                }
            }

            // RETURN TO SUPPLIER: Stock goes OUT (negative SLE, negative Bin)
            var returnItemRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Inventory.Entities.Item, Guid>>();
            foreach (var item in receipt.Items)
            {
                // Skip non-stock items
                var itemEntity = await returnItemRepo.FindAsync(item.ItemId);
                if (itemEntity != null && !itemEntity.MaintainStock)
                    continue;

                var returnQty = Math.Abs(item.Quantity); // Returns have negative qty

                await _valuationService.CreateLedgerEntryAsync(
                    receipt.CompanyId, item.ItemId, receipt.WarehouseId,
                    receipt.PostingDate, -returnQty, item.UnitPrice,
                    voucherType: "PurchaseReceipt", voucherId: receipt.Id,
                    tenantId: receipt.TenantId);

                await _binService.ApplyStockMovementAsync(
                    item.ItemId, receipt.WarehouseId,
                    -returnQty, -(returnQty * item.UnitPrice), receipt.TenantId);

                // Restore ordered qty (returned goods go back to "on order" conceptually)
                await _binService.UpdateOrderedQtyAsync(
                    item.ItemId, receipt.WarehouseId, returnQty, receipt.TenantId);
            }

            // GL: reverse of normal receipt (DR SRBNB, CR Stock)
            await _postingOrchestrator.PostPurchaseReceiptAsync(receipt);

            // Reduce linked PO ReceivedQty
            if (receipt.PurchaseOrderId.HasValue)
            {
                var po = await _purchaseOrderRepository.GetAsync(receipt.PurchaseOrderId.Value);
                foreach (var prItem in receipt.Items)
                {
                    if (prItem.PurchaseOrderItemId.HasValue)
                    {
                        var poItem = po.Items.FirstOrDefault(i => i.Id == prItem.PurchaseOrderItemId.Value);
                        if (poItem != null)
                        {
                            poItem.ReceivedQty = Math.Max(0, poItem.ReceivedQty - Math.Abs(prItem.Quantity));
                        }
                    }
                }
                po.UpdateFulfillmentStatus();
                await _purchaseOrderRepository.UpdateAsync(po);
            }
        }
        else
        {
            // NORMAL RECEIPT: Stock comes IN (positive SLE, positive Bin)
            var itemRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Inventory.Entities.Item, Guid>>();
            foreach (var item in receipt.Items)
            {
                // Skip non-stock items (service items don't create SLE)
                var itemEntity = await itemRepo.FindAsync(item.ItemId);
                if (itemEntity != null && !itemEntity.MaintainStock)
                    continue;

                await _valuationService.CreateLedgerEntryAsync(
                    receipt.CompanyId, item.ItemId, receipt.WarehouseId,
                    receipt.PostingDate, item.Quantity, item.UnitPrice,
                    voucherType: "PurchaseReceipt", voucherId: receipt.Id,
                    tenantId: receipt.TenantId);

                await _binService.ApplyStockMovementAsync(
                    item.ItemId, receipt.WarehouseId,
                    item.Quantity, item.Quantity * item.UnitPrice, receipt.TenantId);

                // Reduce ordered qty (stock is no longer "on order" once received)
                await _binService.UpdateOrderedQtyAsync(
                    item.ItemId, receipt.WarehouseId, -item.Quantity, receipt.TenantId);
            }

            // GL posting (perpetual inventory): DR Stock, CR SRBNB
            await _postingOrchestrator.PostPurchaseReceiptAsync(receipt);

            // Update linked Purchase Order fulfillment tracking
            if (receipt.PurchaseOrderId.HasValue)
            {
                var po = await _purchaseOrderRepository.GetAsync(receipt.PurchaseOrderId.Value);
                foreach (var prItem in receipt.Items)
                {
                    if (prItem.PurchaseOrderItemId.HasValue)
                    {
                        var poItem = po.Items.FirstOrDefault(i => i.Id == prItem.PurchaseOrderItemId.Value);
                        if (poItem != null)
                        {
                            poItem.ReceivedQty += prItem.Quantity;
                        }
                    }
                }
                po.UpdateFulfillmentStatus();
                await _purchaseOrderRepository.UpdateAsync(po);
            }
        }

        await _repository.UpdateAsync(receipt, autoSave: true);

        // Audit trail
        await _activityLogRepository.InsertAsync(new DocumentActivityLog(
            GuidGenerator.Create(), "PurchaseReceipt", receipt.Id, "Submitted",
            receipt.CompanyId, receipt.ReceiptNumber, "Draft", "Submitted",
            CurrentUser.Id, tenantId: receipt.TenantId));

        return MapToDto(receipt);
    }

    [Authorize(MyERPPermissions.PurchaseReceipts.Cancel)]
    public async Task<PurchaseReceiptDto> CancelAsync(Guid id)
    {
        var receipt = await _repository.GetAsync(id);

        // Validate posting period is not frozen/closed
        await _postingOrchestrator.ValidatePostingPeriodAsync(receipt.CompanyId, receipt.PostingDate, "PurchaseReceipt");

        // Guard: cannot cancel if submitted Purchase Invoices reference this receipt's items
        var piRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Purchasing.Entities.PurchaseInvoice, Guid>>();
        var piQuery = await piRepo.GetQueryableAsync();
        var hasSubmittedPI = piQuery.Any(pi =>
            pi.Items.Any(i => i.PurchaseReceiptItemId.HasValue
                && receipt.Items.Select(ri => ri.Id).Contains(i.PurchaseReceiptItemId.Value))
            && pi.Status != Core.DocumentStatus.Draft
            && pi.Status != Core.DocumentStatus.Cancelled);
        if (hasSubmittedPI)
        {
            throw new Volo.Abp.BusinessException("MyERP:01010")
                .WithData("documentType", "Purchase Receipt")
                .WithData("dependent", "Purchase Invoice");
        }

        receipt.Cancel();

        // Reverse SLE + Bin for each item
        foreach (var item in receipt.Items)
        {
            await _valuationService.CreateLedgerEntryAsync(
                receipt.CompanyId, item.ItemId, receipt.WarehouseId,
                receipt.PostingDate, -item.Quantity, item.UnitPrice,
                voucherType: "PurchaseReceipt", voucherId: receipt.Id,
                tenantId: receipt.TenantId);

            await _binService.ApplyStockMovementAsync(
                item.ItemId, receipt.WarehouseId,
                -item.Quantity, -(item.Quantity * item.UnitPrice), receipt.TenantId);

            // Restore ordered qty
            await _binService.UpdateOrderedQtyAsync(
                item.ItemId, receipt.WarehouseId, item.Quantity, receipt.TenantId);
        }

        // Reverse linked Purchase Order fulfillment tracking
        if (receipt.PurchaseOrderId.HasValue)
        {
            var po = await _purchaseOrderRepository.GetAsync(receipt.PurchaseOrderId.Value);
            foreach (var prItem in receipt.Items)
            {
                if (prItem.PurchaseOrderItemId.HasValue)
                {
                    var poItem = po.Items.FirstOrDefault(i => i.Id == prItem.PurchaseOrderItemId.Value);
                    if (poItem != null)
                    {
                        poItem.ReceivedQty = Math.Max(0, poItem.ReceivedQty - prItem.Quantity);
                    }
                }
            }
            po.UpdateFulfillmentStatus();
            await _purchaseOrderRepository.UpdateAsync(po);
        }

        await _repository.UpdateAsync(receipt, autoSave: true);

        // Audit trail
        await _activityLogRepository.InsertAsync(new DocumentActivityLog(
            GuidGenerator.Create(), "PurchaseReceipt", receipt.Id, "Cancelled",
            receipt.CompanyId, receipt.ReceiptNumber, "Submitted", "Cancelled",
            CurrentUser.Id, tenantId: receipt.TenantId));

        return MapToDto(receipt);
    }

    /// <summary>
    /// Amend a cancelled Purchase Receipt — creates a new draft copy.
    /// </summary>
    [Authorize(MyERPPermissions.PurchaseReceipts.Create)]
    public async Task<PurchaseReceiptDto> AmendAsync(Guid id)
    {
        var original = await _repository.GetAsync(id);
        var amendService = LazyServiceProvider.LazyGetRequiredService<Core.DomainServices.DocumentAmendmentService>();

        amendService.ValidateCanAmend(original.Status);
        var newNumber = amendService.GenerateAmendedNumber(original.ReceiptNumber, original.AmendmentIndex + 1);

        var amended = new PurchaseReceipt(
            GuidGenerator.Create(), original.CompanyId, original.SupplierId,
            original.WarehouseId, newNumber, DateTime.UtcNow.Date, CurrentTenant.Id);

        amended.AmendedFromId = original.Id;
        amended.AmendmentIndex = original.AmendmentIndex + 1;
        amended.PurchaseOrderId = original.PurchaseOrderId;
        amended.SupplierDeliveryNote = original.SupplierDeliveryNote;
        amended.Notes = original.Notes;

        foreach (var item in original.Items)
        {
            amended.AddItem(item.ItemId, item.Description, item.Quantity, item.UnitPrice, item.TaxAmount, item.Uom);
        }

        await _repository.InsertAsync(amended, autoSave: true);
        return MapToDto(amended);
    }

    private static PurchaseReceiptDto MapToDto(PurchaseReceipt r) => new()
    {
        Id = r.Id,
        CompanyId = r.CompanyId,
        ReceiptNumber = r.ReceiptNumber,
        PostingDate = r.PostingDate,
        SupplierId = r.SupplierId,
        PurchaseOrderId = r.PurchaseOrderId,
        WarehouseId = r.WarehouseId,
        SupplierDeliveryNote = r.SupplierDeliveryNote,
        CurrencyCode = r.CurrencyCode,
        NetTotal = r.NetTotal,
        TaxAmount = r.TaxAmount,
        GrandTotal = r.GrandTotal,
        IsReturn = r.IsReturn,
        ReturnAgainstId = r.ReturnAgainstId,
        Status = r.Status.ToString(),
        Items = r.Items.Select(i => new PurchaseReceiptItemDto
        {
            Id = i.Id,
            ItemId = i.ItemId,
            Description = i.Description,
            Uom = i.Uom,
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice,
            TaxAmount = i.TaxAmount,
            LineTotal = i.LineTotal,
            PurchaseOrderItemId = i.PurchaseOrderItemId
        }).ToList()
    };
}
