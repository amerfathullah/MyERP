using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
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
public class PurchaseOrderAppService : ApplicationService
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
        PurchaseOrderManager purchaseOrderManager)
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

        // Validate company restriction — items/supplier must allow this company
        var restrictionService = LazyServiceProvider.LazyGetRequiredService<Core.DomainServices.CompanyRestrictionValidationService>();
        await restrictionService.ValidateTransactionCompanyAsync(
            "PurchaseOrder", input.CompanyId,
            itemIds: itemIds,
            supplierIds: new[] { input.SupplierId });

        var orderNumber = await _numberGenerator.GenerateAsync("PurchaseOrder", input.CompanyId);
        var po = new PurchaseOrder(GuidGenerator.Create(), input.CompanyId, input.SupplierId, orderNumber, input.OrderDate);
        po.ExpectedDeliveryDate = input.ExpectedDeliveryDate;
        po.Notes = input.Notes;

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

        await _repository.InsertAsync(po, autoSave: true);
        return ObjectMapper.Map<PurchaseOrder, PurchaseOrderDto>(po);
    }

    [Authorize(MyERPPermissions.PurchaseOrders.Submit)]
    public async Task<PurchaseOrderDto> SubmitAsync(Guid id)
    {
        var po = await _repository.GetAsync(id);

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
            // Batch load all item IDs to avoid N+1 queries
            var poItemIds = po.Items.Select(i => i.ItemId).Distinct().ToArray();
            var itemQuery = await _itemRepository.GetQueryableAsync();
            var itemExpenseAccounts = itemQuery
                .Where(i => poItemIds.Contains(i.Id) && i.DefaultExpenseAccountId != null)
                .Select(i => new { i.Id, i.DefaultExpenseAccountId })
                .ToDictionary(i => i.Id, i => i.DefaultExpenseAccountId!.Value);

            var budgetItems = new List<BudgetCheckItem>();
            foreach (var poItem in po.Items)
            {
                if (itemExpenseAccounts.TryGetValue(poItem.ItemId, out var expenseAccountId))
                {
                    budgetItems.Add(new BudgetCheckItem(
                        expenseAccountId,
                        poItem.Quantity * poItem.UnitPrice));
                }
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

        await _repository.UpdateAsync(po, autoSave: true);

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

        // Guard: cannot cancel with submitted dependents (domain service)
        var prRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Purchasing.Entities.PurchaseReceipt, Guid>>();
        var piRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Purchasing.Entities.PurchaseInvoice, Guid>>();
        await _purchaseOrderManager.ValidateCanCancelAsync(po, prRepo, piRepo);

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

        order.OrderDate = input.OrderDate;
        order.ExpectedDeliveryDate = input.ExpectedDeliveryDate;
        order.SupplierId = input.SupplierId;
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
}

