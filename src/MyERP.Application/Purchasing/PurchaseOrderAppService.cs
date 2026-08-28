using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using MyERP.Inventory.DomainServices;
using Microsoft.Extensions.Logging;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using MyERP.Sales;
using MyERP.Sales.DomainServices;
using MyERP.Purchasing.DomainServices;
using MyERP.Shared;
using MyERP.Workflow.DomainServices;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Purchasing;

[Authorize(MyERPPermissions.PurchaseOrders.Default)]
public class PurchaseOrderAppService : ApplicationService, IPurchaseOrderAppService
{
    private readonly IRepository<PurchaseOrder, Guid> _repository;
    private readonly IRepository<MaterialRequest, Guid> _materialRequestRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Supplier, Guid> _supplierRepository;
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepository;
    private readonly IRepository<DocumentActivityLog, Guid> _activityLogRepository;
    private readonly IDocumentNumberGenerator _numberGenerator;
    private readonly BinService _binService;
    private readonly BudgetValidationService _budgetValidation;
    private readonly ApprovalWorkflowManager _approvalManager;
    private readonly TransactionValidationService _transactionValidation;
    private readonly ItemTransactionValidationService _itemValidation;
    private readonly PricingRuleApplicationService _pricingRuleService;
    private readonly PurchaseOrderManager _purchaseOrderManager;
    private readonly ChildItemUpdateService _childItemUpdateService;
    private readonly ItemDefaultsResolutionService _itemDefaultsResolution;

    public PurchaseOrderAppService(
        IRepository<PurchaseOrder, Guid> repository,
        IRepository<MaterialRequest, Guid> materialRequestRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<Supplier, Guid> supplierRepository,
        IRepository<FiscalYear, Guid> fiscalYearRepository,
        IRepository<DocumentActivityLog, Guid> activityLogRepository,
        IDocumentNumberGenerator numberGenerator,
        BinService binService,
        BudgetValidationService budgetValidation,
        ApprovalWorkflowManager approvalManager,
        TransactionValidationService transactionValidation,
        ItemTransactionValidationService itemValidation,
        PricingRuleApplicationService pricingRuleService,
        PurchaseOrderManager purchaseOrderManager,
        ChildItemUpdateService childItemUpdateService,
        ItemDefaultsResolutionService itemDefaultsResolution)
    {
        _repository = repository;
        _materialRequestRepository = materialRequestRepository;
        _itemRepository = itemRepository;
        _supplierRepository = supplierRepository;
        _fiscalYearRepository = fiscalYearRepository;
        _activityLogRepository = activityLogRepository;
        _numberGenerator = numberGenerator;
        _binService = binService;
        _budgetValidation = budgetValidation;
        _approvalManager = approvalManager;
        _transactionValidation = transactionValidation;
        _itemValidation = itemValidation;
        _pricingRuleService = pricingRuleService;
        _purchaseOrderManager = purchaseOrderManager;
        _childItemUpdateService = childItemUpdateService;
        _itemDefaultsResolution = itemDefaultsResolution;
    }

    public async Task<PurchaseOrderDto> GetAsync(Guid id)
    {
        var po = await _repository.GetAsync(id);
        var dto = ObjectMapper.Map<PurchaseOrder, PurchaseOrderDto>(po);
        dto.SupplierName = await ResolveSupplierNameAsync(po.SupplierId);
        return dto;
    }

    public async Task<PagedResultDto<PurchaseOrderDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();

        if (input.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter; query = query.Where(x => x.OrderNumber.Contains(filter));
        }

        if (!string.IsNullOrWhiteSpace(input.Status) && Enum.TryParse<Core.DocumentStatus>(input.Status, true, out var status))
            query = query.Where(x => x.Status == status);

        if (input.FromDate.HasValue)
            query = query.Where(x => x.OrderDate >= input.FromDate.Value);

        if (input.ToDate.HasValue)
            query = query.Where(x => x.OrderDate <= input.ToDate.Value);

        var count = query.Count();
        var sorted = SortingHelper.ApplySorting(query, input.Sorting,
            q => q.OrderByDescending(x => x.OrderDate),
            ("orderNumber", x => x.OrderNumber),
            ("orderDate", x => x.OrderDate),
            ("grandTotal", x => x.GrandTotal),
            ("status", x => x.Status));
        var list = sorted
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        var dtos = list.Select(x => ObjectMapper.Map<PurchaseOrder, PurchaseOrderDto>(x)).ToList();

        // Batch-resolve supplier names
        var supplierIds = list.Select(x => x.SupplierId).Distinct().ToList();
        var supplierRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Purchasing.Entities.Supplier, Guid>>();
        var suppQuery = await supplierRepo.GetQueryableAsync();
        var supplierNames = suppQuery.Where(s => supplierIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Name }).ToList()
            .ToDictionary(s => s.Id, s => s.Name);
        foreach (var dto in dtos)
            dto.SupplierName = supplierNames.GetValueOrDefault(dto.SupplierId);

        return new PagedResultDto<PurchaseOrderDto>(count, dtos);
    }

    public async Task<PurchaseOrderDto> CreateAsync(CreatePurchaseOrderDto input)
    {
        // Input validation
        Check.NotDefaultOrNull<Guid>(input.CompanyId, nameof(input.CompanyId));
        Check.NotDefaultOrNull<Guid>(input.SupplierId, nameof(input.SupplierId));
        if (input.Items == null || input.Items.Count == 0)
            throw new Volo.Abp.BusinessException("MyERP:01007")
                .WithData("documentType", "Purchase Order");

        // Validate posting date is not in future
        _transactionValidation.ValidatePostingDate(input.OrderDate);

        // Validate all items are active
        var itemIds = input.Items.Select(i => i.ItemId).ToList();
        await _itemValidation.ValidateItemsForTransactionAsync(itemIds);

        var companyRestriction = LazyServiceProvider.LazyGetRequiredService<Core.DomainServices.CompanyRestrictionValidationService>();
        await companyRestriction.ValidateTransactionCompanyAsync("PurchaseOrder", input.CompanyId, itemIds, supplierIds: new[] { input.SupplierId });

        var supplierForStatus = await _supplierRepository.GetAsync(input.SupplierId);
        LazyServiceProvider.LazyGetRequiredService<Core.DomainServices.PartyValidationService>()
            .ValidatePartyStatus("Supplier", isFrozen: false, isDisabled: !supplierForStatus.IsActive, supplierForStatus.Name);

        var orderNumber = await _numberGenerator.GenerateAsync("PurchaseOrder", input.CompanyId);
        var po = new PurchaseOrder(GuidGenerator.Create(), input.CompanyId, input.SupplierId, orderNumber, input.OrderDate);
        po.ExpectedDeliveryDate = input.ExpectedDeliveryDate;
        po.Notes = input.Notes;
        po.CostCenterId = input.CostCenterId;
        po.ProjectId = input.ProjectId;

        // Per ERPNext: Price List defaults from the supplier's own default when not given explicitly.
        po.PriceListId = input.PriceListId
            ?? (await _supplierRepository.FindAsync(input.SupplierId))?.DefaultPriceListId;

        // Auto-fill billing address from supplier master
        var partyDefaults = LazyServiceProvider.LazyGetRequiredService<Core.DomainServices.PartyDefaultsService>();
        var billingAddress = await partyDefaults.GetPrimaryAddressAsync("Supplier", input.SupplierId);
        if (billingAddress != null) po.BillingAddressId = billingAddress.Id;

        foreach (var item in input.Items)
        {
            po.AddItem(item.ItemId, item.Description, item.Quantity, item.UnitPrice, item.TaxAmount, item.Uom);
            if (item.WarehouseId.HasValue)
                po.Items[^1].WarehouseId = item.WarehouseId;
        }

        // Resolve UOM conversion factors for stock qty calculation
        var uomService = LazyServiceProvider.LazyGetRequiredService<Inventory.DomainServices.UomConversionService>();
        var itemRepo2 = LazyServiceProvider.LazyGetRequiredService<IRepository<Inventory.Entities.Item, Guid>>();
        foreach (var poItem in po.Items)
        {
            var itemEntity = await itemRepo2.FindAsync(poItem.ItemId);
            if (itemEntity != null)
            {
                poItem.StockUom = itemEntity.Uom;
                if (!string.Equals(poItem.Uom, itemEntity.Uom, StringComparison.OrdinalIgnoreCase))
                {
                    poItem.ConversionFactor = await uomService.GetConversionFactorAsync(
                        poItem.ItemId, poItem.Uom, itemEntity.Uom);
                }
            }
        }

        // Apply pricing rules for buying (auto-discount based on configured rules)
        var pricingContexts = po.Items.Select(i => new PricingRuleContext
        {
            ItemId = i.ItemId,
            ItemName = i.Description,
            Qty = i.Quantity,
            Rate = i.UnitPrice,
        }).ToList();

        await _pricingRuleService.ApplyToItemsAsync(
            pricingContexts, po.OrderDate, "Buying",
            po.SupplierId, po.CompanyId);

        for (int idx = 0; idx < po.Items.Count; idx++)
        {
            var ctx = pricingContexts[idx];
            if (ctx.DiscountedRate > 0 && ctx.DiscountedRate != ctx.Rate)
            {
                po.Items[idx].UnitPrice = ctx.DiscountedRate;
            }
        }

        // Auto-fill per-item expected delivery dates from Item.LeadTimeDays
        var poManager = LazyServiceProvider.LazyGetRequiredService<PurchaseOrderManager>();
        await poManager.AutoFillExpectedDeliveryDatesAsync(po);

        await _repository.InsertAsync(po, autoSave: true);
        return ObjectMapper.Map<PurchaseOrder, PurchaseOrderDto>(po);
    }

    [Authorize(MyERPPermissions.PurchaseOrders.Submit)]
    public async Task<PurchaseOrderDto> SubmitAsync(Guid id)
    {
        var po = await _repository.GetAsync(id);

        // Authorization control: high-value transaction approval check
        // Per ERPNext: Authorization Rules check based on GrandTotal/Discount
        var authControl = LazyServiceProvider.LazyGetRequiredService<MyERP.Core.DomainServices.AuthorizationControlService>();
        var userRoles = (CurrentUser.Roles ?? Array.Empty<string>()).ToArray();
        await authControl.ValidateApprovingAuthorityAsync(
            "PurchaseOrder", po.CompanyId,
            CurrentUser.Id ?? Guid.Empty, userRoles, po.GrandTotal);

        // Supplier hold + scorecard enforcement (domain service)
        await _purchaseOrderManager.ValidateSupplierEligibilityAsync(po.SupplierId);

        // Check approval workflow — block submit if approval is pending
        var isFullyApproved = await _approvalManager.IsFullyApprovedAsync("PurchaseOrder", po.Id);
        if (!isFullyApproved)
        {
            var needsApproval = await _approvalManager.InitiateApprovalAsync(
                "PurchaseOrder", po.Id, CurrentUser.Id ?? Guid.Empty,
                po.GrandTotal, po.CompanyId, po.TenantId);

            if (needsApproval)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ApprovalPending)
                    .WithData("documentType", "Purchase Order")
                    .WithData("documentId", po.Id);
            }
        }

        // Budget validation (Level 2: PO enforcement)
        var fiscalYear = (await _fiscalYearRepository.GetQueryableAsync())
            .FirstOrDefault(fy => fy.CompanyId == po.CompanyId
                               && fy.StartDate <= po.OrderDate
                               && fy.EndDate >= po.OrderDate);

        if (fiscalYear != null)
        {
            var budgetItems = new List<BudgetCheckItem>();
            foreach (var poItem in po.Items)
            {
                // Falls back to Item Group hierarchy when the item has no expense account of its own
                var expenseAccountId = await _itemDefaultsResolution.ResolveExpenseAccountAsync(poItem.ItemId);
                if (!expenseAccountId.HasValue) continue;

                budgetItems.Add(new BudgetCheckItem(
                    expenseAccountId.Value,
                    poItem.Quantity * poItem.UnitPrice));
            }

            if (budgetItems.Any())
            {
                await _budgetValidation.ValidateForPurchaseOrderAsync(
                    po.CompanyId, fiscalYear.Id, po.OrderDate, budgetItems, po.TenantId);
            }
        }

        // Minimum order quantity validation (domain service)
        await _purchaseOrderManager.ValidateMinimumOrderQtyAsync(po);

        po.Submit();

        // Update Bin.OrderedQty for each item in stock UOM (increases projected qty)
        foreach (var item in po.Items)
        {
            if (item.WarehouseId.HasValue)
            {
                await _binService.UpdateOrderedQtyAsync(
                    item.ItemId, item.WarehouseId.Value, item.StockQty, po.TenantId);
            }
        }

        // Update linked Material Request OrderedQuantity (domain service)
        await _purchaseOrderManager.UpdateMaterialRequestOrderedQtyAsync(po);

        // Auto-insert item prices (per ERPNext: auto_insert_price_list_rate_if_missing)
        try
        {
            var priceAutoInsert = LazyServiceProvider
                .LazyGetRequiredService<Inventory.DomainServices.ItemPriceAutoInsertService>();

            {
                // Use default buying price list
                var priceListRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Inventory.Entities.PriceList, Guid>>();
                var plQuery = await priceListRepo.GetQueryableAsync();
                var defaultPl = plQuery.FirstOrDefault(p => p.IsBuying && p.IsDefault && p.IsActive);
                var priceListId = defaultPl?.Id ?? Guid.Empty;
                if (priceListId != Guid.Empty)
                {
                    await priceAutoInsert.AutoInsertFromTransactionAsync(new Inventory.DomainServices.AutoInsertPriceContext
                    {
                        IsEnabled = true,
                        PriceListId = priceListId,
                        PartyId = po.SupplierId,
                        IsSelling = false,
                        TransactionDate = po.OrderDate,
                        CurrencyCode = po.CurrencyCode,
                        TenantId = po.TenantId,
                        Items = po.Items.Select(i => new Inventory.DomainServices.AutoInsertPriceItem
                        {
                            ItemId = i.ItemId, Rate = i.UnitPrice, Uom = i.Uom,
                        }).ToArray(),
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Item price auto-insert failed for PO {PoId}", po.Id);
        }

        await _repository.UpdateAsync(po, autoSave: true);

        // Auto-create Subcontracting Order for subcontracted POs
        // Per ERPNext PO.on_submit: auto_create_subcontracting_order creates SCO with RM from BOM
        if (po.IsSubcontracted)
        {
            try
            {
                var scoService = LazyServiceProvider.LazyGetRequiredService<SubcontractingAppService>();
                var scoDto = new CreateSubcontractingOrderDto
                {
                    CompanyId = po.CompanyId,
                    SupplierId = po.SupplierId,
                    PurchaseOrderId = po.Id,
                    OrderDate = po.OrderDate,
                    Items = po.Items.Select(i => new CreateScoItemDto
                    {
                        ItemId = i.ItemId,
                        ItemName = i.Description ?? "—",
                        Qty = i.Quantity,
                        Rate = i.UnitPrice,
                    }).ToList(),
                };
                var sco = await scoService.CreateOrderAsync(scoDto);
                Logger.LogInformation("Auto-created SCO {ScoId} from subcontracted PO {PoNumber}", sco.Id, po.OrderNumber);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Auto-create SCO failed for PO {PoId} — create manually", po.Id);
            }
        }

        // Audit trail
        await _activityLogRepository.InsertAsync(new DocumentActivityLog(
            GuidGenerator.Create(), "PurchaseOrder", po.Id, "Submitted",
            po.CompanyId, po.OrderNumber, "Draft", "ToDeliverAndBill",
            CurrentUser.Id, tenantId: po.TenantId));

        return ObjectMapper.Map<PurchaseOrder, PurchaseOrderDto>(po);
    }

    [Authorize(MyERPPermissions.PurchaseOrders.Submit)]
    public async Task<BulkOperationResultDto> BulkSubmitAsync(List<Guid> ids)
    {
        var results = new BulkOperationResultDto();
        foreach (var id in ids)
        {
            try
            {
                await SubmitAsync(id);
                results.Succeeded++;
            }
            catch (Exception ex)
            {
                results.Failed++;
                results.Errors.Add(new BulkOperationError { Id = id, Message = ex.Message });
            }
        }
        return results;
    }

    [Authorize(MyERPPermissions.PurchaseOrders.Cancel)]
    public async Task<PurchaseOrderDto> CancelAsync(Guid id)
    {
        var po = await _repository.GetAsync(id);

        // Guard: cannot cancel with submitted dependents (domain service). scoRepository is an
        // optional param on ValidateCanCancelAsync — it defaulted to null here, silently skipping
        // the "submitted Subcontracting Order blocks cancel" check the method already implements.
        var prRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Purchasing.Entities.PurchaseReceipt, Guid>>();
        var piRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Purchasing.Entities.PurchaseInvoice, Guid>>();
        var scoRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Purchasing.Entities.SubcontractingOrder, Guid>>();
        await _purchaseOrderManager.ValidateCanCancelAsync(po, prRepo, piRepo, scoRepo);

        po.Cancel();

        // Reverse Bin.OrderedQty (in stock UOM)
        foreach (var item in po.Items)
        {
            if (item.WarehouseId.HasValue)
            {
                await _binService.UpdateOrderedQtyAsync(
                    item.ItemId, item.WarehouseId.Value, -item.StockQty, po.TenantId);
            }
        }

        await _repository.UpdateAsync(po, autoSave: true);

        // Audit trail
        await _activityLogRepository.InsertAsync(new DocumentActivityLog(
            GuidGenerator.Create(), "PurchaseOrder", po.Id, "Cancelled",
            po.CompanyId, po.OrderNumber, "ToDeliverAndBill", "Cancelled",
            CurrentUser.Id, tenantId: po.TenantId));

        return ObjectMapper.Map<PurchaseOrder, PurchaseOrderDto>(po);
    }

    [Authorize(MyERPPermissions.PurchaseOrders.Edit)]
    public async Task<PurchaseOrderDto> CloseAsync(Guid id)
    {
        var po = await _repository.GetAsync(id);
        po.Close();

        // Release pending ordered qty from Bin in stock UOM (short-close)
        foreach (var item in po.Items)
        {
            if (item.WarehouseId.HasValue && item.PendingReceiptQty > 0)
            {
                var pendingStockQty = item.PendingReceiptQty * item.ConversionFactor;
                await _binService.UpdateOrderedQtyAsync(
                    item.ItemId, item.WarehouseId.Value, -pendingStockQty, po.TenantId);
            }
        }

        // Reverse MR OrderedQuantity for unreceived items (domain service)
        await _purchaseOrderManager.UpdateMaterialRequestOrderedQtyAsync(po, reverse: true);

        await _repository.UpdateAsync(po, autoSave: true);
        return ObjectMapper.Map<PurchaseOrder, PurchaseOrderDto>(po);
    }

    [Authorize(MyERPPermissions.PurchaseOrders.Edit)]
    public async Task<PurchaseOrderDto> ReopenAsync(Guid id)
    {
        var po = await _repository.GetAsync(id);
        po.Reopen();

        // Re-reserve ordered qty on reopen in stock UOM
        foreach (var item in po.Items)
        {
            if (item.WarehouseId.HasValue && item.PendingReceiptQty > 0)
            {
                var pendingStockQty = item.PendingReceiptQty * item.ConversionFactor;
                await _binService.UpdateOrderedQtyAsync(
                    item.ItemId, item.WarehouseId.Value, pendingStockQty, po.TenantId);
            }
        }

        await _repository.UpdateAsync(po, autoSave: true);
        return ObjectMapper.Map<PurchaseOrder, PurchaseOrderDto>(po);
    }

    /// <summary>
    /// Amend a cancelled Purchase Order — creates a new draft copy with amendment link.
    /// </summary>
    [Authorize(MyERPPermissions.PurchaseOrders.Create)]
    public async Task<PurchaseOrderDto> AmendAsync(Guid id)
    {
        var original = await _repository.GetAsync(id);
        var amendService = LazyServiceProvider.LazyGetRequiredService<Core.DomainServices.DocumentAmendmentService>();

        amendService.ValidateCanAmend(original.Status);
        var newNumber = amendService.GenerateAmendedNumber(original.OrderNumber, original.AmendmentIndex + 1);

        var amended = new PurchaseOrder(
            GuidGenerator.Create(), original.CompanyId, original.SupplierId, newNumber, DateTime.UtcNow.Date);

        amended.AmendedFromId = original.Id;
        amended.AmendmentIndex = original.AmendmentIndex + 1;
        amended.ExpectedDeliveryDate = original.ExpectedDeliveryDate;
        amended.CurrencyCode = original.CurrencyCode;
        amended.ExchangeRate = original.ExchangeRate;
        amended.PriceListId = original.PriceListId;
        amended.Terms = original.Terms;
        amended.Notes = original.Notes;

        foreach (var item in original.Items)
        {
            amended.AddItem(item.ItemId, item.Description, item.Quantity, item.UnitPrice, item.TaxAmount, item.Uom);
        }

        await _repository.InsertAsync(amended, autoSave: true);
        return ObjectMapper.Map<PurchaseOrder, PurchaseOrderDto>(amended);
    }

    [Authorize(MyERPPermissions.PurchaseOrders.Edit)]
    public async Task<PurchaseOrderDto> UpdateAsync(Guid id, CreatePurchaseOrderDto input)
    {
        var order = await _repository.GetAsync(id);
        if (order.Status != Core.DocumentStatus.Draft)
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("detail", "Only Draft purchase orders can be edited");

        var updateItemIds = input.Items.Select(i => i.ItemId).ToList();
        var updateCompanyRestriction = LazyServiceProvider.LazyGetRequiredService<Core.DomainServices.CompanyRestrictionValidationService>();
        await updateCompanyRestriction.ValidateTransactionCompanyAsync("PurchaseOrder", order.CompanyId, updateItemIds, supplierIds: new[] { input.SupplierId });

        order.OrderDate = input.OrderDate;
        order.ExpectedDeliveryDate = input.ExpectedDeliveryDate;
        order.SupplierId = input.SupplierId;
        order.PriceListId = input.PriceListId;
        order.Notes = input.Notes;

        order.ClearItems();
        foreach (var item in input.Items)
        {
            order.AddItem(item.ItemId, item.Description, item.Quantity, item.UnitPrice, item.TaxAmount, item.Uom);
            if (item.WarehouseId.HasValue)
                order.Items[^1].WarehouseId = item.WarehouseId;
        }

        await _repository.UpdateAsync(order, autoSave: true);
        return ObjectMapper.Map<PurchaseOrder, PurchaseOrderDto>(order);
    }

    /// <summary>
    /// Update items on a submitted Purchase Order without cancel/amend cycle.
    /// Per ERPNext update_child_qty_rate: modifies qty/rate on active orders with guards.
    /// </summary>
    [Authorize(MyERPPermissions.PurchaseOrders.Edit)]
    public async Task<UpdateOrderItemsResultDto> UpdateItemsAsync(Guid id, UpdateOrderItemsDto input)
    {
        var po = await _repository.GetAsync(id);

        // Only active submitted orders can have items updated
        if (po.Status == Core.DocumentStatus.Draft || po.Status == Core.DocumentStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("detail", "Only submitted orders can have items updated. Use Edit for draft orders.");

        // Guard: cannot update items once a Subcontracting Order exists for this PO — per
        // ERPNext can_update_items(), even a DRAFT SCO blocks this (stricter than the cancel
        // guard above, which only blocks on submitted SCOs). Only a Cancelled SCO clears the way.
        var scoRepoForUpdate = LazyServiceProvider.LazyGetRequiredService<IRepository<Purchasing.Entities.SubcontractingOrder, Guid>>();
        var scoUpdateQuery = await scoRepoForUpdate.GetQueryableAsync();
        var hasActiveSco = scoUpdateQuery.Any(sco =>
            sco.PurchaseOrderId == po.Id && sco.Status != SubcontractingOrderStatus.Cancelled);
        if (hasActiveSco)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("detail", "Cannot update items — a Subcontracting Order already exists for this Purchase Order. Cancel it first.");

        var previousGrandTotal = po.GrandTotal;
        var warnings = new List<string>();
        var updatedCount = 0;

        foreach (var removeId in input.RemovedItemIds)
        {
            var poItemToRemove = po.Items.FirstOrDefault(i => i.Id == removeId);
            if (poItemToRemove == null)
            {
                warnings.Add($"Item row {removeId} not found on this order — skipped.");
                continue;
            }

            _childItemUpdateService.ValidatePurchaseOrderItemDeletion(poItemToRemove);

            if (poItemToRemove.WarehouseId.HasValue)
            {
                await _binService.UpdateOrderedQtyAsync(
                    poItemToRemove.ItemId, poItemToRemove.WarehouseId.Value, -poItemToRemove.StockQty, po.TenantId);
            }

            po.RemoveItem(removeId);
        }

        foreach (var update in input.Items)
        {
            var poItem = po.Items.FirstOrDefault(i => i.Id == update.ItemId);
            if (poItem == null)
            {
                warnings.Add($"Item {update.ItemId} not found on this order — skipped.");
                continue;
            }

            // Guard: cannot reduce qty below already received
            if (update.Quantity < poItem.ReceivedQty)
                throw new BusinessException("MyERP:04019")
                    .WithData("itemId", poItem.ItemId)
                    .WithData("receivedQty", poItem.ReceivedQty)
                    .WithData("requestedQty", update.Quantity);

            // Guard: cannot reduce rate below billed amount per unit
            if (poItem.BilledQty > 0 && update.UnitPrice < poItem.UnitPrice)
            {
                var minRate = poItem.BilledQty > 0 ? (poItem.BilledQty * poItem.UnitPrice) / poItem.BilledQty : 0;
                if (update.UnitPrice < minRate && update.UnitPrice != 0)
                    throw new BusinessException("MyERP:04020")
                        .WithData("itemId", poItem.ItemId)
                        .WithData("billedRate", minRate)
                        .WithData("requestedRate", update.UnitPrice);
            }

            // Track old qty for Bin ordered qty adjustment
            var oldStockQty = poItem.StockQty;

            // Update fields
            poItem.Quantity = update.Quantity;
            poItem.UnitPrice = update.UnitPrice;
            if (update.WarehouseId.HasValue)
                poItem.WarehouseId = update.WarehouseId;

            // Adjust Bin.OrderedQty for qty changes (delta in stock UOM)
            var newStockQty = poItem.StockQty;
            var qtyDelta = newStockQty - oldStockQty;
            if (qtyDelta != 0 && poItem.WarehouseId.HasValue)
            {
                await _binService.UpdateOrderedQtyAsync(
                    poItem.ItemId, poItem.WarehouseId.Value, qtyDelta, po.TenantId);
            }

            updatedCount++;
        }

        await _repository.UpdateAsync(po, autoSave: true);

        // Audit trail
        await _activityLogRepository.InsertAsync(new DocumentActivityLog(
            GuidGenerator.Create(), "PurchaseOrder", po.Id, "ItemsUpdated",
            po.CompanyId, po.OrderNumber, po.Status.ToString(), po.Status.ToString(),
            CurrentUser.Id, $"Updated {updatedCount} items, removed {input.RemovedItemIds.Count}. Grand total: {previousGrandTotal} → {po.GrandTotal}",
            tenantId: po.TenantId));

        return new UpdateOrderItemsResultDto
        {
            ItemsUpdated = updatedCount,
            NewGrandTotal = po.GrandTotal,
            PreviousGrandTotal = previousGrandTotal,
            Warnings = warnings,
        };
    }

    [Authorize(MyERPPermissions.PurchaseOrders.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        var order = await _repository.GetAsync(id);
        if (order.Status != Core.DocumentStatus.Draft)
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("detail", "Only Draft purchase orders can be deleted");
        await _repository.DeleteAsync(id);
    }

    private async Task<string?> ResolveSupplierNameAsync(Guid supplierId)
    {
        var supplierRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Purchasing.Entities.Supplier, Guid>>();
        var supplier = await supplierRepo.FindAsync(supplierId);
        return supplier?.Name;
    }

    /// <summary>
    /// Get payment entries linked to this purchase order (advance payments to supplier).
    /// </summary>
    public async Task<List<OrderPaymentDto>> GetOrderPaymentsAsync(Guid id)
    {
        var peRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PaymentEntry, Guid>>();
        var peQuery = await peRepo.GetQueryableAsync();
        var payments = peQuery
            .Where(pe => pe.AgainstOrderId == id && pe.AgainstOrderType == "PurchaseOrder")
            .OrderByDescending(pe => pe.PostingDate)
            .Select(pe => new OrderPaymentDto
            {
                PaymentEntryId = pe.Id,
                PaymentNumber = pe.PaymentNumber ?? pe.Id.ToString().Substring(0, 8),
                PostingDate = pe.PostingDate,
                PaidAmount = pe.PaidAmount,
                PaymentType = pe.PaymentType.ToString(),
                ReferenceNumber = pe.ReferenceNumber,
                Status = pe.Status.ToString()
            }).ToList();
        return payments;
    }

    /// <summary>
    /// Get receipt entries linked to this purchase order (for receipt tracking).
    /// </summary>
    public async Task<List<OrderReceiptDto>> GetOrderReceiptsAsync(Guid id)
    {
        var prRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PurchaseReceipt, Guid>>();
        var prQuery = await prRepo.GetQueryableAsync();
        var receipts = prQuery
            .Where(pr => pr.PurchaseOrderId == id)
            .OrderByDescending(pr => pr.PostingDate)
            .Select(pr => new OrderReceiptDto
            {
                PurchaseReceiptId = pr.Id,
                ReceiptNumber = pr.ReceiptNumber,
                PostingDate = pr.PostingDate,
                Status = pr.Status.ToString(),
                ItemCount = pr.Items.Count
            }).ToList();
        return receipts;
    }

    /// <summary>
    /// Marks drop-ship PO items as delivered without a Purchase Receipt.
    /// Per ERPNext PO.update_dropship_received_qty: directly updates received_qty on PO items
    /// and cascades delivery status to the linked Sales Order.
    /// Only valid for items that are delivered_by_supplier on the source SO.
    /// </summary>
    [Authorize(MyERPPermissions.PurchaseOrders.Edit)]
    public async Task<PurchaseOrderDto> UpdateDropShipDeliveredQtyAsync(Guid id, UpdateDropShipDeliveredQtyDto input)
    {
        var po = await _repository.GetAsync(id);
        if (po.Status == DocumentStatus.Draft || po.Status == DocumentStatus.Cancelled || po.Status == DocumentStatus.Closed)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("documentType", "PurchaseOrder")
                .WithData("status", po.Status.ToString());

        if (!input.Items.Any())
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustHaveItems);

        foreach (var deliveryItem in input.Items)
        {
            var poItem = po.Items.FirstOrDefault(i => i.Id == deliveryItem.PurchaseOrderItemId);
            if (poItem == null)
                throw new BusinessException(MyERPDomainErrorCodes.PurchaseOrderItemNotFoundForDelivery)
                    .WithData("itemId", deliveryItem.PurchaseOrderItemId);

            // Validate: negative qty change cannot exceed current received_qty
            if (deliveryItem.QtyChange < 0 && Math.Abs(deliveryItem.QtyChange) > poItem.ReceivedQty)
                throw new BusinessException(MyERPDomainErrorCodes.DropShipQtyReductionExceeded)
                    .WithData("itemCode", poItem.Description)
                    .WithData("maxReduction", poItem.ReceivedQty);

            // Validate: positive qty change cannot exceed remaining (qty - received_qty)
            if (deliveryItem.QtyChange > 0 && poItem.ReceivedQty + deliveryItem.QtyChange > poItem.Quantity)
                throw new BusinessException("MyERP:04018")
                    .WithData("itemCode", poItem.Description)
                    .WithData("maxIncrease", poItem.Quantity - poItem.ReceivedQty);

            poItem.ReceivedQty += deliveryItem.QtyChange;
        }

        // Recalculate PO fulfillment status
        po.UpdateFulfillmentStatus();
        await _repository.UpdateAsync(po, autoSave: true);

        // Cascade delivery status to linked Sales Order(s)
        await UpdateLinkedSalesOrderDeliveryStatusAsync(po);

        // Activity log
        await _activityLogRepository.InsertAsync(new DocumentActivityLog(
            Guid.NewGuid(), "PurchaseOrder", po.Id,
            "DropShipDelivered", po.CompanyId,
            po.OrderNumber, po.Status.ToString(), po.Status.ToString(),
            CurrentUser.Id ?? Guid.Empty,
            $"Drop-ship delivery qty updated for {input.Items.Count} item(s)",
            po.TenantId));

        return ObjectMapper.Map<PurchaseOrder, PurchaseOrderDto>(po);
    }

    /// <summary>
    /// After updating drop-ship received qty on PO, cascades the delivery status
    /// to linked Sales Orders (per ERPNext DropShipService.update_delivered_qty_in_sales_order).
    /// </summary>
    private async Task UpdateLinkedSalesOrderDeliveryStatusAsync(PurchaseOrder po)
    {
        var soRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.SalesOrder, Guid>>();

        // Find PO items that link back to SO items (via the SO→PO drop-ship creation flow)
        // The SO item has DeliveredBySupplier=true and the PO was created from it
        var soQuery = await soRepo.GetQueryableAsync();
        // Look for SOs that have drop-ship items linking to this PO's supplier
        // In ERPNext: PO Item has sales_order_item field linking to SO Item
        // In MyERP: the link is established during SO→PO conversion (SO.Notes references the SO)
        // For now, we find SOs referenced in PO notes or via item linkage
        if (po.Notes != null && po.Notes.Contains("Drop-ship order for SO"))
        {
            // Extract SO reference from notes — production would use a proper FK
            var allSOs = soQuery.Where(so =>
                so.CompanyId == po.CompanyId &&
                so.Status != DocumentStatus.Draft &&
                so.Status != DocumentStatus.Cancelled).ToList();

            foreach (var so in allSOs)
            {
                var hasDropShipItems = so.Items.Any(i => i.DeliveredBySupplier && i.SupplierId == po.SupplierId);
                if (!hasDropShipItems) continue;

                // Update SO item delivered qty from PO received qty
                foreach (var soItem in so.Items.Where(i => i.DeliveredBySupplier && i.SupplierId == po.SupplierId))
                {
                    // Find matching PO item by ItemId
                    var matchingPoItem = po.Items.FirstOrDefault(pi => pi.ItemId == soItem.ItemId);
                    if (matchingPoItem != null)
                    {
                        soItem.DeliveredQty = matchingPoItem.ReceivedQty;
                    }
                }
                so.UpdateFulfillmentStatus();
                await soRepo.UpdateAsync(so);
            }
        }
    }

    /// <summary>
    /// Records supplier confirmation/acknowledgment of a purchase order.
    /// Per ERPNext: suppliers confirm receipt, provide their reference number and promised delivery date.
    /// </summary>
    [Authorize(MyERPPermissions.PurchaseOrders.Edit)]
    public async Task<PurchaseOrderDto> RecordSupplierConfirmationAsync(Guid id, RecordSupplierConfirmationDto input)
    {
        var po = await _repository.GetAsync(id);
        po.RecordSupplierConfirmation(
            input.ConfirmationNumber,
            input.ConfirmationDate ?? DateTime.UtcNow,
            input.PromisedDeliveryDate);
        await _repository.UpdateAsync(po);

        // Activity log
        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<Volo.Abp.Domain.Repositories.IRepository<MyERP.Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new MyERP.Core.Entities.DocumentActivityLog(
            Guid.NewGuid(), "PurchaseOrder", id,
            "SupplierConfirmed", po.CompanyId,
            po.OrderNumber, po.Status.ToString(), po.Status.ToString(),
            CurrentUser.Id ?? Guid.Empty,
            $"Supplier confirmed: {input.ConfirmationNumber ?? "N/A"}, Promised: {input.PromisedDeliveryDate?.ToString("yyyy-MM-dd") ?? "N/A"}",
            po.TenantId));

        return ObjectMapper.Map<PurchaseOrder, PurchaseOrderDto>(po);
    }

    /// <summary>
    /// Gets pending Material Request items (Purchase type) that haven't been fully ordered.
    /// Used by PO form "Get Items from Material Request" button.
    /// Per ERPNext: MR items with PendingQty = Quantity - OrderedQuantity > 0.
    /// </summary>
    public async Task<List<PendingMaterialRequestItemDto>> GetPendingMaterialRequestItemsAsync(
        Guid? companyId = null, Guid? supplierId = null)
    {
        var mrQuery = await _materialRequestRepository.GetQueryableAsync();

        var query = mrQuery.Where(mr =>
            mr.RequestType == Purchasing.MaterialRequestType.Purchase &&
            mr.Status != Core.DocumentStatus.Draft &&
            mr.Status != Core.DocumentStatus.Cancelled);

        if (companyId.HasValue)
            query = query.Where(mr => mr.CompanyId == companyId.Value);

        var requests = query.ToList();

        // Batch-resolve item names
        var allItemIds = requests.SelectMany(mr => mr.Items).Select(i => i.ItemId).Distinct().ToList();
        var itemRepo = LazyServiceProvider.LazyGetRequiredService<Volo.Abp.Domain.Repositories.IRepository<Inventory.Entities.Item, Guid>>();
        var itemQuery = await itemRepo.GetQueryableAsync();
        var itemNames = itemQuery
            .Where(i => allItemIds.Contains(i.Id))
            .Select(i => new { i.Id, i.ItemCode, i.ItemName })
            .ToList()
            .ToDictionary(i => i.Id, i => $"{i.ItemCode} - {i.ItemName}");

        var result = new List<PendingMaterialRequestItemDto>();
        foreach (var mr in requests)
        {
            foreach (var item in mr.Items)
            {
                var pendingQty = item.Quantity - item.OrderedQuantity;
                if (pendingQty > 0)
                {
                    result.Add(new PendingMaterialRequestItemDto
                    {
                        MaterialRequestId = mr.Id,
                        MaterialRequestNumber = mr.RequestNumber,
                        RequestDate = mr.RequestDate,
                        RequiredByDate = mr.RequiredByDate,
                        MaterialRequestItemId = item.Id,
                        ItemId = item.ItemId,
                        ItemName = itemNames.GetValueOrDefault(item.ItemId) ?? item.ItemName,
                        PendingQty = pendingQty,
                        Uom = item.Uom,
                        WarehouseId = item.WarehouseId,
                    });
                }
            }
        }

        return result.OrderBy(r => r.RequestDate).ThenBy(r => r.ItemName).ToList();
    }

    /// <summary>
    /// Returns POs grouped by fulfillment stage for a Kanban-style tracking board.
    /// Per ERPNext: procurement managers use this daily to track order pipeline.
    /// </summary>
    public async Task<PurchaseOrderTrackingBoardDto> GetTrackingBoardAsync(Guid companyId)
    {
        var queryable = await _repository.GetQueryableAsync();
        var orders = queryable
            .Where(po => po.CompanyId == companyId
                && po.Status != DocumentStatus.Draft
                && po.Status != DocumentStatus.Cancelled)
            .OrderByDescending(po => po.OrderDate)
            .Take(200)
            .ToList();

        var supplierIds = orders.Select(o => o.SupplierId).Distinct().ToList();
        var supplierQuery = await _supplierRepository.GetQueryableAsync();
        var supplierNames = supplierQuery
            .Where(s => supplierIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Name })
            .ToDictionary(s => s.Id, s => s.Name);

        var today = DateTime.UtcNow.Date;
        var cards = orders.Select(po =>
        {
            var perReceived = po.Items.Count > 0
                ? po.Items.Min(i => i.Quantity > 0 ? Math.Min(100, i.ReceivedQty / i.Quantity * 100) : 100)
                : 0m;
            var perBilled = po.Items.Count > 0
                ? po.Items.Min(i => i.Quantity > 0 ? Math.Min(100, i.BilledQty / i.Quantity * 100) : 100)
                : 0m;

            var stage = perReceived >= 99.99m && perBilled >= 99.99m ? "Completed"
                : perReceived >= 99.99m ? "FullyReceived"
                : perReceived > 0 ? "PartiallyReceived"
                : "Ordered";

            var effectiveDate = po.ExpectedDeliveryDate ?? po.OrderDate.AddDays(14);
            var isOverdue = stage != "Completed" && stage != "FullyReceived" && effectiveDate < today;
            var daysOverdue = isOverdue ? (int)(today - effectiveDate).TotalDays : 0;

            return new TrackingBoardCardDto
            {
                OrderId = po.Id,
                OrderNumber = po.OrderNumber,
                SupplierName = supplierNames.GetValueOrDefault(po.SupplierId, "—"),
                OrderDate = po.OrderDate,
                ExpectedDate = po.ExpectedDeliveryDate,
                GrandTotal = po.GrandTotal,
                PerReceived = Math.Round(perReceived, 1),
                PerBilled = Math.Round(perBilled, 1),
                Stage = stage,
                IsOverdue = isOverdue,
                DaysOverdue = daysOverdue,
                ItemCount = po.Items.Count
            };
        }).ToList();

        return new PurchaseOrderTrackingBoardDto
        {
            Ordered = cards.Where(c => c.Stage == "Ordered").ToList(),
            PartiallyReceived = cards.Where(c => c.Stage == "PartiallyReceived").ToList(),
            FullyReceived = cards.Where(c => c.Stage == "FullyReceived").ToList(),
            Completed = cards.Where(c => c.Stage == "Completed").ToList(),
            TotalOrders = cards.Count,
            OverdueCount = cards.Count(c => c.IsOverdue),
            TotalValue = cards.Sum(c => c.GrandTotal)
        };
    }
}

