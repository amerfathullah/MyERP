using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using MyERP.Core;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using MyERP.Inventory.DomainServices;
using MyERP.Permissions;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using MyERP.Shared;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

[Authorize(MyERPPermissions.DeliveryNotes.Default)]
public class DeliveryNoteAppService : ApplicationService, IDeliveryNoteAppService
{
    private readonly IRepository<DeliveryNote, Guid> _repository;
    private readonly IRepository<SalesOrder, Guid> _salesOrderRepository;
    private readonly IRepository<DocumentActivityLog, Guid> _activityLogRepository;
    private readonly IDocumentNumberGenerator _numberGenerator;
    private readonly StockValuationService _valuationService;
    private readonly BinService _binService;
    private readonly DocumentPostingOrchestrator _postingOrchestrator;
    private readonly AutoReorderService _autoReorderService;
    private readonly QualityInspectionEnforcementService _qiEnforcement;
    private readonly BatchExpiryValidationService _batchValidation;
    private readonly ItemTransactionValidationService _itemValidation;
    private readonly CreditLimitService _creditLimitService;

    public DeliveryNoteAppService(
        IRepository<DeliveryNote, Guid> repository,
        IRepository<SalesOrder, Guid> salesOrderRepository,
        IRepository<DocumentActivityLog, Guid> activityLogRepository,
        IDocumentNumberGenerator numberGenerator,
        StockValuationService valuationService,
        BinService binService,
        DocumentPostingOrchestrator postingOrchestrator,
        AutoReorderService autoReorderService,
        QualityInspectionEnforcementService qiEnforcement,
        BatchExpiryValidationService batchValidation,
        ItemTransactionValidationService itemValidation,
        CreditLimitService creditLimitService)
    {
        _repository = repository;
        _salesOrderRepository = salesOrderRepository;
        _activityLogRepository = activityLogRepository;
        _numberGenerator = numberGenerator;
        _valuationService = valuationService;
        _binService = binService;
        _postingOrchestrator = postingOrchestrator;
        _autoReorderService = autoReorderService;
        _qiEnforcement = qiEnforcement;
        _batchValidation = batchValidation;
        _itemValidation = itemValidation;
        _creditLimitService = creditLimitService;
    }

    public async Task<DeliveryNoteDto> GetAsync(Guid id)
    {
        var dn = await _repository.GetAsync(id);
        return MapToDto(dn);
    }

    public async Task<PagedResultDto<DeliveryNoteDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();

        if (input.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter.ToLower();
            query = query.Where(x => x.DeliveryNumber.ToLower().Contains(filter));
        }

        if (!string.IsNullOrWhiteSpace(input.Status) && Enum.TryParse<Core.DocumentStatus>(input.Status, true, out var status))
            query = query.Where(x => x.Status == status);

        var totalCount = query.Count();
        var list = query
            .OrderByDescending(x => x.PostingDate)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        return new PagedResultDto<DeliveryNoteDto>(
            totalCount,
            list.Select(MapToDto).ToList());
    }

    [Authorize(MyERPPermissions.DeliveryNotes.Create)]
    public async Task<DeliveryNoteDto> CreateAsync(CreateDeliveryNoteDto input)
    {
        // Input validation
        Check.NotDefaultOrNull<Guid>(input.CompanyId, nameof(input.CompanyId));
        Check.NotDefaultOrNull<Guid>(input.CustomerId, nameof(input.CustomerId));
        Check.NotDefaultOrNull<Guid>(input.WarehouseId, nameof(input.WarehouseId));
        if (input.Items == null || input.Items.Count == 0)
            throw new Volo.Abp.BusinessException("MyERP:01007")
                .WithData("documentType", "Delivery Note");

        // Validate all items are active
        var itemIds = input.Items.Select(i => i.ItemId).ToList();
        await _itemValidation.ValidateItemsForTransactionAsync(itemIds);

        var deliveryNumber = await _numberGenerator.GenerateAsync("DeliveryNote", input.CompanyId);

        var dn = new DeliveryNote(
            GuidGenerator.Create(),
            input.CompanyId,
            input.CustomerId,
            input.WarehouseId,
            deliveryNumber,
            input.PostingDate,
            CurrentTenant.Id);

        dn.SalesOrderId = input.SalesOrderId;
        dn.ShippingAddress = input.ShippingAddress;
        dn.Transporter = input.Transporter;
        dn.TrackingNumber = input.TrackingNumber;
        dn.IsReturn = input.IsReturn;
        dn.ReturnAgainstId = input.ReturnAgainstId;
        dn.Notes = input.Notes;

    // Auto-fill addresses from linked SO or customer
    if (input.SalesOrderId.HasValue)
    {
        var soRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.SalesOrder, Guid>>();
        var so = await soRepo.FindAsync(input.SalesOrderId.Value);
        if (so != null)
        {
            dn.BillingAddressId = so.BillingAddressId;
            dn.ShippingAddressId = so.ShippingAddressId;
        }
    }
    else
    {
        var partyDefaults = LazyServiceProvider.LazyGetRequiredService<Core.DomainServices.PartyDefaultsService>();
        var shipping = await partyDefaults.GetShippingAddressAsync("Customer", input.CustomerId);
        if (shipping != null) dn.ShippingAddressId = shipping.Id;
        var billing = await partyDefaults.GetPrimaryAddressAsync("Customer", input.CustomerId);
        if (billing != null) dn.BillingAddressId = billing.Id;
    }
        return MapToDto(dn);
    }

    [Authorize(MyERPPermissions.DeliveryNotes.Submit)]
    public async Task<DeliveryNoteDto> SubmitAsync(Guid id)
    {
        var dn = await _repository.GetAsync(id);

        // Enforce Quality Inspection requirement (per item flags)
        var itemIds = dn.Items.Select(i => i.ItemId).ToArray();
        await _qiEnforcement.ValidateForDeliveryNoteAsync(dn.Id, itemIds, dn.TenantId);

        // Validate batch expiry (block expired batches on stock-out)
        var batchItems = dn.Items
            .Where(i => i.BatchId.HasValue)
            .Select(i => new BatchValidationItem(i.ItemId, i.BatchId, i.Description))
            .ToList();
        if (batchItems.Any())
        {
            await _batchValidation.ValidateForStockOutAsync(batchItems, dn.PostingDate);
        }

        // Over-delivery validation + SO status guard (domain service)
        if (!dn.IsReturn && dn.SalesOrderId.HasValue)
        {
            // Credit limit enforcement at DN submit
            await _creditLimitService.ValidateCreditLimitAsync(dn.CustomerId, dn.GrandTotal);

            // Selling price validation at DN submit (Warn mode)
            var valuationSvc = LazyServiceProvider.LazyGetRequiredService<Inventory.DomainServices.StockValuationService>();
            var dnItemData = dn.Items
                .Select(i => (i.ItemId, i.UnitPrice, i.Description))
                .ToList().AsReadOnly();
            SalesInvoiceManager.ValidateSellingPrice(dnItemData,
                itemId => valuationSvc.GetCurrentBalanceAsync(itemId, dn.WarehouseId)
                    .GetAwaiter().GetResult().ValuationRate,
                action: "Warn");

            var dnManager = LazyServiceProvider.LazyGetRequiredService<DeliveryNoteManager>();
            await dnManager.ValidateAgainstSalesOrderAsync(dn);
        }

        dn.Submit();

        if (dn.IsReturn)
        {
            // Validate return qty does not exceed original delivery qty (domain service)
            if (dn.ReturnAgainstId.HasValue)
            {
                var dnManager = LazyServiceProvider.LazyGetRequiredService<DeliveryNoteManager>();
                await dnManager.ValidateReturnAsync(dn);
            }

            // RETURN: Stock comes BACK to warehouse (positive SLE, positive Bin)
            var returnItemRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Inventory.Entities.Item, Guid>>();
            foreach (var item in dn.Items)
            {
                // Skip non-stock items
                var itemEntity = await returnItemRepo.FindAsync(item.ItemId);
                if (itemEntity != null && !itemEntity.MaintainStock)
                    continue;

                var returnQty = Math.Abs(item.Quantity); // Returns have negative qty, use absolute

                await _valuationService.CreateLedgerEntryAsync(
                    dn.CompanyId, item.ItemId, dn.WarehouseId,
                    dn.PostingDate, returnQty, item.UnitPrice,
                    voucherType: "DeliveryNote", voucherId: dn.Id,
                    tenantId: dn.TenantId);

                await _binService.ApplyStockMovementAsync(
                    item.ItemId, dn.WarehouseId,
                    returnQty, returnQty * item.UnitPrice, dn.TenantId);
                // No reserved qty release on returns — stock wasn't reserved for a return
            }

            // Reverse GL: DR Stock, CR COGS (opposite of normal DN)
            await _postingOrchestrator.PostDeliveryNoteAsync(dn);

            // Reduce linked SO DeliveredQty (return reverses prior delivery)
            if (dn.SalesOrderId.HasValue)
            {
                var so = await _salesOrderRepository.GetAsync(dn.SalesOrderId.Value);
                foreach (var dnItem in dn.Items)
                {
                    if (dnItem.SalesOrderItemId.HasValue)
                    {
                        var soItem = so.Items.FirstOrDefault(i => i.Id == dnItem.SalesOrderItemId.Value);
                        if (soItem != null)
                        {
                            soItem.DeliveredQty = Math.Max(0, soItem.DeliveredQty - Math.Abs(dnItem.Quantity));
                        }
                    }
                }
                so.UpdateFulfillmentStatus();
                await _salesOrderRepository.UpdateAsync(so);
            }
        }
        else
        {
            // NORMAL DELIVERY: Stock goes OUT (negative SLE, negative Bin)
            // Product Bundle decomposition: bundle items deliver COMPONENT stock, not parent
            var itemRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Inventory.Entities.Item, Guid>>();
            var bundleService = LazyServiceProvider.LazyGetRequiredService<ProductBundleDecompositionService>();

            // Identify which DN items are bundles
            var allItemIds = dn.Items.Select(i => i.ItemId).Distinct();
            var bundleItemIds = await bundleService.GetBundleItemIdsAsync(allItemIds);

            foreach (var item in dn.Items)
            {
                if (bundleItemIds.Contains(item.ItemId))
                {
                    // Bundle item: decompose into components and deliver each component
                    var components = await bundleService.DecomposeAsync(
                        item.ItemId, item.Quantity, item.UnitPrice);

                    foreach (var comp in components)
                    {
                        // Skip non-stock components
                        var compEntity = await itemRepo.FindAsync(comp.ComponentItemId);
                        if (compEntity != null && !compEntity.MaintainStock)
                            continue;

                        await _valuationService.CreateLedgerEntryAsync(
                            dn.CompanyId, comp.ComponentItemId, dn.WarehouseId,
                            dn.PostingDate, -comp.Qty, comp.Rate,
                            voucherType: "DeliveryNote", voucherId: dn.Id,
                            tenantId: dn.TenantId);

                        await _binService.ApplyStockMovementAsync(
                            comp.ComponentItemId, dn.WarehouseId,
                            -comp.Qty, -(comp.Qty * comp.Rate), dn.TenantId);

                        await _binService.UpdateReservedQtyAsync(
                            comp.ComponentItemId, dn.WarehouseId, -comp.Qty, dn.TenantId);
                    }

                    // Valuation for bundle = sum of component valuations
                    decimal bundleValuation = 0;
                    foreach (var comp in components)
                    {
                        var compBalance = await _valuationService.GetCurrentBalanceAsync(comp.ComponentItemId, dn.WarehouseId);
                        bundleValuation += compBalance.ValuationRate * comp.Qty;
                    }
                    item.ValuationRate = item.Quantity > 0 ? bundleValuation / item.Quantity : 0;
                }
                else
                {
                    // Regular item: stock operations on the item itself
                    var itemEntity = await itemRepo.FindAsync(item.ItemId);
                    if (itemEntity != null && !itemEntity.MaintainStock)
                        continue;

                    // Capture valuation rate before stock-out (for COGS/gross profit)
                    var balance = await _valuationService.GetCurrentBalanceAsync(item.ItemId, dn.WarehouseId);
                    item.ValuationRate = balance.ValuationRate;

                    await _valuationService.CreateLedgerEntryAsync(
                        dn.CompanyId, item.ItemId, dn.WarehouseId,
                        dn.PostingDate, -item.Quantity, item.UnitPrice,
                        voucherType: "DeliveryNote", voucherId: dn.Id,
                        tenantId: dn.TenantId);

                    await _binService.ApplyStockMovementAsync(
                        item.ItemId, dn.WarehouseId,
                        -item.Quantity, -(item.Quantity * item.UnitPrice), dn.TenantId);

                    // Release reserved qty (stock was reserved at SO, now delivered)
                    await _binService.UpdateReservedQtyAsync(
                        item.ItemId, dn.WarehouseId, -item.Quantity, dn.TenantId);
                }
            }

            // GL posting (perpetual inventory): DR COGS, CR Stock
            await _postingOrchestrator.PostDeliveryNoteAsync(dn);

            // Update linked Sales Order fulfillment tracking
            if (dn.SalesOrderId.HasValue)
            {
                var so = await _salesOrderRepository.GetAsync(dn.SalesOrderId.Value);
                foreach (var dnItem in dn.Items)
                {
                    if (dnItem.SalesOrderItemId.HasValue)
                    {
                        var soItem = so.Items.FirstOrDefault(i => i.Id == dnItem.SalesOrderItemId.Value);
                        if (soItem != null)
                        {
                            soItem.DeliveredQty += dnItem.Quantity;
                        }
                    }
                }
                so.UpdateFulfillmentStatus();
                await _salesOrderRepository.UpdateAsync(so);
            }

            // Check auto-reorder for items that had stock reduced
            foreach (var item in dn.Items)
            {
                var mrId = await _autoReorderService.CheckSingleItemAsync(
                    item.ItemId, dn.WarehouseId, dn.CompanyId, dn.TenantId);

                // Notify when auto-reorder creates a Material Request
                if (mrId.HasValue && CurrentUser.Id.HasValue)
                {
                    try
                    {
                        var notifSvc = LazyServiceProvider
                            .LazyGetRequiredService<Notification.DomainServices.BusinessNotificationService>();
                        await notifSvc.NotifyAutoReorderAsync(
                            CurrentUser.Id.Value, item.Description, item.Quantity, mrId.Value, dn.TenantId);
                    }
                    catch { /* Non-critical */ }
                }
            }
        }

        await _repository.UpdateAsync(dn, autoSave: true);

        // Audit trail
        await _activityLogRepository.InsertAsync(new DocumentActivityLog(
            GuidGenerator.Create(), "DeliveryNote", dn.Id, "Submitted",
            dn.CompanyId, dn.DeliveryNumber, "Draft", "Submitted",
            CurrentUser.Id, tenantId: dn.TenantId));

        return MapToDto(dn);
    }

    [Authorize(MyERPPermissions.DeliveryNotes.Cancel)]
    public async Task<DeliveryNoteDto> CancelAsync(Guid id)
    {
        var dn = await _repository.GetAsync(id);

        // Validate posting period is not frozen/closed
        await _postingOrchestrator.ValidatePostingPeriodAsync(dn.CompanyId, dn.PostingDate, "DeliveryNote");

        // Guard: cannot cancel if submitted Sales Invoices are linked to this DN's items
        var siRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.SalesInvoice, Guid>>();
        var siQuery = await siRepo.GetQueryableAsync();
        var hasSubmittedSI = siQuery.Any(si =>
            si.Items.Any(i => i.SalesOrderItemId.HasValue
                && dn.Items.Select(di => di.SalesOrderItemId).Contains(i.SalesOrderItemId))
            && si.Status != Core.DocumentStatus.Draft
            && si.Status != Core.DocumentStatus.Cancelled);
        if (hasSubmittedSI)
        {
            throw new Volo.Abp.BusinessException("MyERP:01010")
                .WithData("documentType", "Delivery Note")
                .WithData("dependent", "Sales Invoice");
        }

        dn.Cancel();

        // Reverse SLE + Bin for each item
        foreach (var item in dn.Items)
        {
            await _valuationService.CreateLedgerEntryAsync(
                dn.CompanyId, item.ItemId, dn.WarehouseId,
                dn.PostingDate, item.Quantity, item.UnitPrice,
                voucherType: "DeliveryNote", voucherId: dn.Id,
                tenantId: dn.TenantId);

            await _binService.ApplyStockMovementAsync(
                item.ItemId, dn.WarehouseId,
                item.Quantity, item.Quantity * item.UnitPrice, dn.TenantId);

            // Re-reserve qty
            await _binService.UpdateReservedQtyAsync(
                item.ItemId, dn.WarehouseId, item.Quantity, dn.TenantId);
        }

        // Reverse linked Sales Order fulfillment tracking
        if (dn.SalesOrderId.HasValue)
        {
            var so = await _salesOrderRepository.GetAsync(dn.SalesOrderId.Value);
            foreach (var dnItem in dn.Items)
            {
                if (dnItem.SalesOrderItemId.HasValue)
                {
                    var soItem = so.Items.FirstOrDefault(i => i.Id == dnItem.SalesOrderItemId.Value);
                    if (soItem != null)
                    {
                        soItem.DeliveredQty = Math.Max(0, soItem.DeliveredQty - dnItem.Quantity);
                    }
                }
            }
            so.UpdateFulfillmentStatus();
            await _salesOrderRepository.UpdateAsync(so);
        }

        await _repository.UpdateAsync(dn, autoSave: true);

        // Audit trail
        await _activityLogRepository.InsertAsync(new DocumentActivityLog(
            GuidGenerator.Create(), "DeliveryNote", dn.Id, "Cancelled",
            dn.CompanyId, dn.DeliveryNumber, "Submitted", "Cancelled",
            CurrentUser.Id, tenantId: dn.TenantId));

        return MapToDto(dn);
    }

    /// <summary>
    /// Amend a cancelled Delivery Note — creates a new draft copy.
    /// </summary>
    [Authorize(MyERPPermissions.DeliveryNotes.Create)]
    public async Task<DeliveryNoteDto> AmendAsync(Guid id)
    {
        var original = await _repository.GetAsync(id);
        var amendService = LazyServiceProvider.LazyGetRequiredService<Core.DomainServices.DocumentAmendmentService>();

        amendService.ValidateCanAmend(original.Status);
        var newNumber = amendService.GenerateAmendedNumber(original.DeliveryNumber, original.AmendmentIndex + 1);

        var amended = new DeliveryNote(
            GuidGenerator.Create(), original.CompanyId, original.CustomerId,
            original.WarehouseId, newNumber, DateTime.UtcNow.Date, CurrentTenant.Id);

        amended.AmendedFromId = original.Id;
        amended.AmendmentIndex = original.AmendmentIndex + 1;
        amended.SalesOrderId = original.SalesOrderId;
        amended.Notes = original.Notes;

        foreach (var item in original.Items)
        {
            amended.AddItem(item.ItemId, item.Description, item.Quantity, item.UnitPrice, item.TaxAmount, item.Uom, item.SalesOrderItemId);
        }

        await _repository.InsertAsync(amended, autoSave: true);
        return MapToDto(amended);
    }

    private static DeliveryNoteDto MapToDto(DeliveryNote dn) => new()
    {
        Id = dn.Id,
        CompanyId = dn.CompanyId,
        DeliveryNumber = dn.DeliveryNumber,
        PostingDate = dn.PostingDate,
        CustomerId = dn.CustomerId,
        SalesOrderId = dn.SalesOrderId,
        WarehouseId = dn.WarehouseId,
        ShippingAddress = dn.ShippingAddress,
        Transporter = dn.Transporter,
        TrackingNumber = dn.TrackingNumber,
        CurrencyCode = dn.CurrencyCode,
        NetTotal = dn.NetTotal,
        TaxAmount = dn.TaxAmount,
        GrandTotal = dn.GrandTotal,
        IsReturn = dn.IsReturn,
        ReturnAgainstId = dn.ReturnAgainstId,
        Status = dn.Status.ToString(),
        Items = dn.Items.Select(i => new DeliveryNoteItemDto
        {
            Id = i.Id,
            ItemId = i.ItemId,
            Description = i.Description,
            Uom = i.Uom,
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice,
            TaxAmount = i.TaxAmount,
            LineTotal = i.LineTotal,
            SalesOrderItemId = i.SalesOrderItemId
        }).ToList()
    };
}
