using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
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
        return ObjectMapper.Map<PurchaseReceipt, PurchaseReceiptDto>(receipt);
    }

    public async Task<PagedResultDto<PurchaseReceiptDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();

        if (input.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter; query = query.Where(x => x.ReceiptNumber.Contains(filter));
        }

        if (!string.IsNullOrWhiteSpace(input.Status) && Enum.TryParse<Core.DocumentStatus>(input.Status, true, out var status))
            query = query.Where(x => x.Status == status);

        if (input.FromDate.HasValue)
            query = query.Where(x => x.PostingDate >= input.FromDate.Value);

        if (input.ToDate.HasValue)
            query = query.Where(x => x.PostingDate <= input.ToDate.Value);

        var totalCount = query.Count();
        var sorted = SortingHelper.ApplySorting(query, input.Sorting,
            q => q.OrderByDescending(x => x.PostingDate),
            ("receiptNumber", x => x.ReceiptNumber),
            ("postingDate", x => x.PostingDate),
            ("grandTotal", x => x.GrandTotal),
            ("status", x => x.Status));
        var list = sorted
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        return new PagedResultDto<PurchaseReceiptDto>(
            totalCount,
            list.Select(x => ObjectMapper.Map<PurchaseReceipt, PurchaseReceiptDto>(x)).ToList());
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

        // Resolve UOM conversion factors
        var uomService = LazyServiceProvider.LazyGetRequiredService<Inventory.DomainServices.UomConversionService>();
        var itemRepoUom = LazyServiceProvider.LazyGetRequiredService<IRepository<Inventory.Entities.Item, Guid>>();
        foreach (var prItem in receipt.Items)
        {
            var itemEntity = await itemRepoUom.FindAsync(prItem.ItemId);
            if (itemEntity != null)
            {
                prItem.StockUom = itemEntity.Uom;
                if (!string.Equals(prItem.Uom, itemEntity.Uom, StringComparison.OrdinalIgnoreCase))
                    prItem.ConversionFactor = await uomService.GetConversionFactorAsync(
                        prItem.ItemId, prItem.Uom, itemEntity.Uom);
            }
        }

        await _repository.InsertAsync(receipt, autoSave: true);
        return ObjectMapper.Map<PurchaseReceipt, PurchaseReceiptDto>(receipt);
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

                // Use StockQty for return SLE (stock UOM)
                var returnStockQty = Math.Abs(item.StockQty);
                var ratePerStockUnit = item.ConversionFactor != 0
                    ? item.UnitPrice / item.ConversionFactor
                    : item.UnitPrice;

                await _valuationService.CreateLedgerEntryAsync(
                    receipt.CompanyId, item.ItemId, receipt.WarehouseId,
                    receipt.PostingDate, -returnStockQty, ratePerStockUnit,
                    voucherType: "PurchaseReceipt", voucherId: receipt.Id,
                    tenantId: receipt.TenantId);

                await _binService.ApplyStockMovementAsync(
                    item.ItemId, receipt.WarehouseId,
                    -returnStockQty, -(returnStockQty * ratePerStockUnit), receipt.TenantId);

                // Restore ordered qty in stock UOM
                await _binService.UpdateOrderedQtyAsync(
                    item.ItemId, receipt.WarehouseId, returnStockQty, receipt.TenantId);
            }

            // GL: reverse of normal receipt (DR SRBNB, CR Stock)
            await _postingOrchestrator.PostPurchaseReceiptAsync(receipt);

            // Reduce linked PO ReceivedQty (with concurrency retry)
            if (receipt.PurchaseOrderId.HasValue)
            {
                await UpdatePoFulfillmentWithRetryAsync(receipt.PurchaseOrderId.Value, receipt.Items, isReversal: true);
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

                // Use StockQty for SLE (respects UOM conversion factor)
                var stockQty = item.StockQty;
                var ratePerStockUnit = item.ConversionFactor != 0
                    ? item.UnitPrice / item.ConversionFactor
                    : item.UnitPrice;

                await _valuationService.CreateLedgerEntryAsync(
                    receipt.CompanyId, item.ItemId, receipt.WarehouseId,
                    receipt.PostingDate, stockQty, ratePerStockUnit,
                    voucherType: "PurchaseReceipt", voucherId: receipt.Id,
                    tenantId: receipt.TenantId);

                await _binService.ApplyStockMovementAsync(
                    item.ItemId, receipt.WarehouseId,
                    stockQty, stockQty * ratePerStockUnit, receipt.TenantId);

                // Reduce ordered qty (stock is no longer "on order" once received)
                await _binService.UpdateOrderedQtyAsync(
                    item.ItemId, receipt.WarehouseId, -stockQty, receipt.TenantId);
            }

            // GL posting (perpetual inventory): DR Stock, CR SRBNB
            await _postingOrchestrator.PostPurchaseReceiptAsync(receipt);

            // Update linked Purchase Order fulfillment tracking
            // Update linked PO fulfillment (with concurrency retry)
            if (receipt.PurchaseOrderId.HasValue)
            {
                await UpdatePoFulfillmentWithRetryAsync(receipt.PurchaseOrderId.Value, receipt.Items, isReversal: false);
            }
        }

        await _repository.UpdateAsync(receipt, autoSave: true);

        // Audit trail
        await _activityLogRepository.InsertAsync(new DocumentActivityLog(
            GuidGenerator.Create(), "PurchaseReceipt", receipt.Id, "Submitted",
            receipt.CompanyId, receipt.ReceiptNumber, "Draft", "Submitted",
            CurrentUser.Id, tenantId: receipt.TenantId));

        return ObjectMapper.Map<PurchaseReceipt, PurchaseReceiptDto>(receipt);
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

        // Reverse SLE + Bin for each item (in stock UOM)
        foreach (var item in receipt.Items)
        {
            var stockQty = item.StockQty;
            var ratePerStockUnit = item.ConversionFactor != 0
                ? item.UnitPrice / item.ConversionFactor
                : item.UnitPrice;

            await _valuationService.CreateLedgerEntryAsync(
                receipt.CompanyId, item.ItemId, receipt.WarehouseId,
                receipt.PostingDate, -stockQty, ratePerStockUnit,
                voucherType: "PurchaseReceipt", voucherId: receipt.Id,
                tenantId: receipt.TenantId);

            await _binService.ApplyStockMovementAsync(
                item.ItemId, receipt.WarehouseId,
                -stockQty, -(stockQty * ratePerStockUnit), receipt.TenantId);

            // Restore ordered qty in stock UOM
            await _binService.UpdateOrderedQtyAsync(
                item.ItemId, receipt.WarehouseId, stockQty, receipt.TenantId);
        }

        // Reverse linked Purchase Order fulfillment tracking
        // Cancel reversal: restore PO ReceivedQty (with concurrency retry)
        if (receipt.PurchaseOrderId.HasValue)
        {
            await UpdatePoFulfillmentWithRetryAsync(receipt.PurchaseOrderId.Value, receipt.Items, isReversal: true);
        }

        await _repository.UpdateAsync(receipt, autoSave: true);

        // Audit trail
        await _activityLogRepository.InsertAsync(new DocumentActivityLog(
            GuidGenerator.Create(), "PurchaseReceipt", receipt.Id, "Cancelled",
            receipt.CompanyId, receipt.ReceiptNumber, "Submitted", "Cancelled",
            CurrentUser.Id, tenantId: receipt.TenantId));

        return ObjectMapper.Map<PurchaseReceipt, PurchaseReceiptDto>(receipt);
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
        return ObjectMapper.Map<PurchaseReceipt, PurchaseReceiptDto>(amended);
    }

    private async Task UpdatePoFulfillmentWithRetryAsync(
        Guid purchaseOrderId,
        IReadOnlyCollection<PurchaseReceiptItem> prItems,
        bool isReversal)
    {
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                var po = await _purchaseOrderRepository.GetAsync(purchaseOrderId);
                foreach (var prItem in prItems)
                {
                    if (!prItem.PurchaseOrderItemId.HasValue) continue;
                    var poItem = po.Items.FirstOrDefault(i => i.Id == prItem.PurchaseOrderItemId.Value);
                    if (poItem == null) continue;

                    if (isReversal)
                        poItem.ReceivedQty = Math.Max(0, poItem.ReceivedQty - Math.Abs(prItem.Quantity));
                    else
                        poItem.ReceivedQty += prItem.Quantity;
                }
                po.UpdateFulfillmentStatus();
                await _purchaseOrderRepository.UpdateAsync(po, autoSave: true);
                return;
            }
            catch (Volo.Abp.Data.AbpDbConcurrencyException) when (attempt < 3)
            {
                Logger.LogWarning("Concurrency conflict updating PO {PoId} ReceivedQty (attempt {Attempt}/3)", purchaseOrderId, attempt);
                await Task.Delay(attempt * 10);
            }
        }
    }
}

