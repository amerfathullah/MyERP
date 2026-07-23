using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
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
        var dto = ObjectMapper.Map<DeliveryNote, DeliveryNoteDto>(dn);

        // Resolve customer name
        var customerRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.Customer, Guid>>();
        var customer = await customerRepo.FindAsync(dn.CustomerId);
        if (customer != null)
            dto.CustomerName = customer.Name;

        return dto;
    }

    public async Task<PagedResultDto<DeliveryNoteDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();

        if (input.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter; query = query.Where(x => x.DeliveryNumber.Contains(filter));
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
            ("deliveryNumber", x => x.DeliveryNumber),
            ("postingDate", x => x.PostingDate),
            ("grandTotal", x => x.GrandTotal),
            ("status", x => x.Status));
        var list = sorted
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        var dtos = list.Select(x => ObjectMapper.Map<DeliveryNote, DeliveryNoteDto>(x)).ToList();

        // Batch-resolve customer names
        var customerIds = list.Select(x => x.CustomerId).Distinct().ToList();
        var customerRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.Customer, Guid>>();
        var custQuery = await customerRepo.GetQueryableAsync();
        var customerNames = custQuery.Where(c => customerIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name }).ToList()
            .ToDictionary(c => c.Id, c => c.Name);
        foreach (var dto in dtos)
            dto.CustomerName = customerNames.GetValueOrDefault(dto.CustomerId);

        return new PagedResultDto<DeliveryNoteDto>(totalCount, dtos);
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

        // Validate company restriction — items/customer must allow this company
        var restrictionService = LazyServiceProvider.LazyGetRequiredService<Core.DomainServices.CompanyRestrictionValidationService>();
        await restrictionService.ValidateTransactionCompanyAsync(
            "DeliveryNote", input.CompanyId,
            itemIds: itemIds,
            customerIds: new[] { input.CustomerId });

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

        // Add items + resolve UOM conversion
        var uomService = LazyServiceProvider.LazyGetRequiredService<Inventory.DomainServices.UomConversionService>();
        var itemRepoForUom = LazyServiceProvider.LazyGetRequiredService<IRepository<Inventory.Entities.Item, Guid>>();
        foreach (var item in input.Items)
        {
            dn.AddItem(item.ItemId, item.Description, item.Quantity, item.UnitPrice,
                item.TaxAmount, item.Uom, item.SalesOrderItemId);
            var lastItem = dn.Items[^1];
            var itemEntity = await itemRepoForUom.FindAsync(item.ItemId);
            if (itemEntity != null)
            {
                lastItem.StockUom = itemEntity.Uom;
                if (!string.Equals(lastItem.Uom, itemEntity.Uom, StringComparison.OrdinalIgnoreCase))
                    lastItem.ConversionFactor = await uomService.GetConversionFactorAsync(
                        item.ItemId, lastItem.Uom, itemEntity.Uom);
            }
        }

        await _repository.InsertAsync(dn, autoSave: true);
        return ObjectMapper.Map<DeliveryNote, DeliveryNoteDto>(dn);
    }

    [Authorize(MyERPPermissions.DeliveryNotes.Edit)]
    public async Task<DeliveryNoteDto> UpdateAsync(Guid id, CreateDeliveryNoteDto input)
    {
        var dn = await _repository.GetAsync(id);
        if (dn.Status != Core.DocumentStatus.Draft)
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("detail", "Only Draft delivery notes can be edited");

        dn.PostingDate = input.PostingDate;
        dn.ShippingAddress = input.ShippingAddress;
        dn.Transporter = input.Transporter;
        dn.TrackingNumber = input.TrackingNumber;
        dn.Notes = input.Notes;

        dn.ClearItems();
        foreach (var item in input.Items)
        {
            dn.AddItem(item.ItemId, item.Description, item.Quantity, item.UnitPrice, item.TaxAmount, item.Uom, item.SalesOrderItemId);
        }

        await _repository.UpdateAsync(dn, autoSave: true);
        return ObjectMapper.Map<DeliveryNote, DeliveryNoteDto>(dn);
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
            await _creditLimitService.ValidateCreditLimitAsync(dn.CustomerId, dn.GrandTotal, dn.CompanyId);

            // Selling price validation at DN submit (Warn mode)
            var valuationSvc = LazyServiceProvider.LazyGetRequiredService<Inventory.DomainServices.StockValuationService>();
            var dnItemData = dn.Items
                .Select(i => (i.ItemId, i.UnitPrice, i.Description))
                .ToList().AsReadOnly();
            await SalesInvoiceManager.ValidateSellingPriceAsync(dnItemData,
                async itemId => (await valuationSvc.GetCurrentBalanceAsync(itemId, dn.WarehouseId)).ValuationRate,
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

                // Use StockQty for SLE (returns in stock UOM)
                var returnStockQty = Math.Abs(item.StockQty);
                var ratePerStockUnit = item.ConversionFactor != 0
                    ? item.UnitPrice / item.ConversionFactor
                    : item.UnitPrice;

                await _valuationService.CreateLedgerEntryAsync(
                    dn.CompanyId, item.ItemId, dn.WarehouseId,
                    dn.PostingDate, returnStockQty, ratePerStockUnit,
                    voucherType: "DeliveryNote", voucherId: dn.Id,
                    tenantId: dn.TenantId);

                await _binService.ApplyStockMovementAsync(
                    item.ItemId, dn.WarehouseId,
                    returnStockQty, returnStockQty * ratePerStockUnit, dn.TenantId);
                // No reserved qty release on returns — stock wasn't reserved for a return
            }

            // Reverse GL: DR Stock, CR COGS (opposite of normal DN)
            await _postingOrchestrator.PostDeliveryNoteAsync(dn);

            // Reduce linked SO DeliveredQty (return reverses prior delivery — with concurrency retry)
            if (dn.SalesOrderId.HasValue)
            {
                await UpdateSoFulfillmentWithRetryAsync(dn.SalesOrderId.Value, dn.Items, isReversal: true);
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

                    // Capture component valuation rates BEFORE stock removal (Bug #4 fix)
                    decimal bundleCostTotal = 0;
                    foreach (var comp in components)
                    {
                        var compEntity = await itemRepo.FindAsync(comp.ComponentItemId);
                        if (compEntity != null && !compEntity.MaintainStock)
                            continue;

                        // Get valuation rate BEFORE consuming stock
                        var compBalance = await _valuationService.GetCurrentBalanceAsync(comp.ComponentItemId, dn.WarehouseId);
                        var compValuationRate = compBalance.ValuationRate;
                        bundleCostTotal += compValuationRate * comp.Qty;

                        await _valuationService.CreateLedgerEntryAsync(
                            dn.CompanyId, comp.ComponentItemId, dn.WarehouseId,
                            dn.PostingDate, -comp.Qty, compValuationRate,
                            voucherType: "DeliveryNote", voucherId: dn.Id,
                            tenantId: dn.TenantId);

                        // Bin value uses valuation rate (Bug #3 fix — was using selling price)
                        await _binService.ApplyStockMovementAsync(
                            comp.ComponentItemId, dn.WarehouseId,
                            -comp.Qty, -(comp.Qty * compValuationRate), dn.TenantId);

                        await _binService.UpdateReservedQtyAsync(
                            comp.ComponentItemId, dn.WarehouseId, -comp.Qty, dn.TenantId);
                    }

                    // Valuation for bundle = sum of component valuations captured before removal
                    item.ValuationRate = item.Quantity > 0 ? bundleCostTotal / item.Quantity : 0;
                }
                else
                {
                    // Regular item: stock operations on the item itself
                    var itemEntity = await itemRepo.FindAsync(item.ItemId);
                    if (itemEntity != null && !itemEntity.MaintainStock)
                        continue;

                    // Capture valuation rate BEFORE stock-out (for COGS/gross profit)
                    var balance = await _valuationService.GetCurrentBalanceAsync(item.ItemId, dn.WarehouseId);
                    item.ValuationRate = balance.ValuationRate;

                    await _valuationService.CreateLedgerEntryAsync(
                        dn.CompanyId, item.ItemId, dn.WarehouseId,
                        dn.PostingDate, -item.StockQty, item.ValuationRate,
                        voucherType: "DeliveryNote", voucherId: dn.Id,
                        tenantId: dn.TenantId);

                    // Bin value uses valuation rate (Bug #3 fix — was using selling price)
                    await _binService.ApplyStockMovementAsync(
                        item.ItemId, dn.WarehouseId,
                        -item.StockQty, -(item.StockQty * item.ValuationRate), dn.TenantId);

                    // Release reserved qty (stock was reserved at SO in stock UOM, now delivered)
                    await _binService.UpdateReservedQtyAsync(
                        item.ItemId, dn.WarehouseId, -item.StockQty, dn.TenantId);
                }
            }

            // Calculate StockCostTotal for COGS GL posting (Bug #2 fix)
            // Per ERPNext: GL entry uses actual consumed stock cost, NOT selling price
            dn.StockCostTotal = dn.Items
                .Where(i => i.ValuationRate > 0)
                .Sum(i => i.StockQty * i.ValuationRate);

            // GL posting (perpetual inventory): DR COGS, CR Stock
            await _postingOrchestrator.PostDeliveryNoteAsync(dn);

            // Update linked Sales Order fulfillment tracking (with concurrency retry)
            if (dn.SalesOrderId.HasValue)
            {
                await UpdateSoFulfillmentWithRetryAsync(dn.SalesOrderId.Value, dn.Items, isReversal: false);

                // Update Delivery Schedule entries (record progressive delivery)
                await UpdateDeliveryScheduleAsync(dn.SalesOrderId.Value, dn.Items);
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
                    catch (Exception ex) { Logger.LogWarning(ex, "Auto-reorder notification failed"); }
                }
            }
        }

        await _repository.UpdateAsync(dn, autoSave: true);

        // Audit trail
        await _activityLogRepository.InsertAsync(new DocumentActivityLog(
            GuidGenerator.Create(), "DeliveryNote", dn.Id, "Submitted",
            dn.CompanyId, dn.DeliveryNumber, "Draft", "Submitted",
            CurrentUser.Id, tenantId: dn.TenantId));

        return ObjectMapper.Map<DeliveryNote, DeliveryNoteDto>(dn);
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

        // Reverse SLE + Bin for each item (in stock UOM)
        foreach (var item in dn.Items)
        {
            var stockQty = item.StockQty;
            var ratePerStockUnit = item.ConversionFactor != 0
                ? item.UnitPrice / item.ConversionFactor
                : item.UnitPrice;

            await _valuationService.CreateLedgerEntryAsync(
                dn.CompanyId, item.ItemId, dn.WarehouseId,
                dn.PostingDate, stockQty, ratePerStockUnit,
                voucherType: "DeliveryNote", voucherId: dn.Id,
                tenantId: dn.TenantId);

            await _binService.ApplyStockMovementAsync(
                item.ItemId, dn.WarehouseId,
                stockQty, stockQty * ratePerStockUnit, dn.TenantId);

            // Re-reserve qty in stock UOM
            await _binService.UpdateReservedQtyAsync(
                item.ItemId, dn.WarehouseId, stockQty, dn.TenantId);
        }

        // Reverse linked Sales Order fulfillment tracking
        if (dn.SalesOrderId.HasValue)
        {
            await UpdateSoFulfillmentWithRetryAsync(dn.SalesOrderId.Value, dn.Items, isReversal: true);
        }

        await _repository.UpdateAsync(dn, autoSave: true);

        // Audit trail
        await _activityLogRepository.InsertAsync(new DocumentActivityLog(
            GuidGenerator.Create(), "DeliveryNote", dn.Id, "Cancelled",
            dn.CompanyId, dn.DeliveryNumber, "Submitted", "Cancelled",
            CurrentUser.Id, tenantId: dn.TenantId));

        return ObjectMapper.Map<DeliveryNote, DeliveryNoteDto>(dn);
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
        return ObjectMapper.Map<DeliveryNote, DeliveryNoteDto>(amended);
    }

    /// <summary>
    /// Updates SO fulfillment counters with optimistic concurrency retry.
    /// Prevents lost updates when concurrent DN submissions modify the same SO.
    /// </summary>
    private async Task UpdateSoFulfillmentWithRetryAsync(
        Guid salesOrderId,
        IReadOnlyCollection<DeliveryNoteItem> dnItems,
        bool isReversal)
    {
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                var so = await _salesOrderRepository.GetAsync(salesOrderId);
                foreach (var dnItem in dnItems)
                {
                    if (!dnItem.SalesOrderItemId.HasValue) continue;
                    var soItem = so.Items.FirstOrDefault(i => i.Id == dnItem.SalesOrderItemId.Value);
                    if (soItem == null) continue;

                    if (isReversal)
                        soItem.DeliveredQty = Math.Max(0, soItem.DeliveredQty - Math.Abs(dnItem.Quantity));
                    else
                        soItem.DeliveredQty += dnItem.Quantity;
                }
                so.UpdateFulfillmentStatus();
                await _salesOrderRepository.UpdateAsync(so, autoSave: true);
                return;
            }
            catch (Volo.Abp.Data.AbpDbConcurrencyException) when (attempt < 3)
            {
                Logger.LogWarning("Concurrency conflict updating SO {SoId} DeliveredQty (attempt {Attempt}/3)", salesOrderId, attempt);
                await Task.Delay(attempt * 10);
            }
        }
    }

    /// <summary>
    /// Updates Delivery Schedule entries to record progressive delivery against scheduled dates.
    /// Per ERPNext: uses FIFO allocation (earliest scheduled date filled first).
    /// Per gotcha #108: frequency-based split deliveries with whole-number enforcement.
    /// </summary>
    private async Task UpdateDeliveryScheduleAsync(Guid salesOrderId, IReadOnlyCollection<DeliveryNoteItem> dnItems)
    {
        var scheduleRepo = LazyServiceProvider
            .LazyGetRequiredService<IRepository<DeliveryScheduleEntry, Guid>>();

        var scheduleQueryable = await scheduleRepo.GetQueryableAsync();
        var scheduleEntries = scheduleQueryable
            .Where(e => e.SalesOrderId == salesOrderId)
            .OrderBy(e => e.ScheduledDate)
            .ToList();

        if (!scheduleEntries.Any()) return;

        // FIFO allocation: allocate delivered qty to earliest pending schedule entries
        foreach (var dnItem in dnItems)
        {
            if (!dnItem.SalesOrderItemId.HasValue) continue;

            var itemSchedules = scheduleEntries
                .Where(e => e.SalesOrderItemId == dnItem.SalesOrderItemId.Value && !e.IsFullyDelivered)
                .ToList();

            var remainingToAllocate = Math.Abs(dnItem.Quantity);

            foreach (var schedule in itemSchedules)
            {
                if (remainingToAllocate <= 0) break;

                var allocatable = Math.Min(remainingToAllocate, schedule.PendingQty);
                schedule.RecordDelivery(allocatable);
                remainingToAllocate -= allocatable;
            }
        }

        // Persist updated schedule entries
        foreach (var entry in scheduleEntries.Where(e => e.DeliveredQty > 0))
        {
            await scheduleRepo.UpdateAsync(entry);
        }
    }
}

