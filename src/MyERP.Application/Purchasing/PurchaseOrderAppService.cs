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
        PricingRuleApplicationService pricingRuleService)
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
    }

    public async Task<PurchaseOrderDto> GetAsync(Guid id)
    {
        var po = await _repository.GetAsync(id);
        return MapToDto(po);
    }

    public async Task<PagedResultDto<PurchaseOrderDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();

        if (input.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter.ToLower();
            query = query.Where(x => x.OrderNumber.ToLower().Contains(filter));
        }

        if (!string.IsNullOrWhiteSpace(input.Status) && Enum.TryParse<Core.DocumentStatus>(input.Status, true, out var status))
            query = query.Where(x => x.Status == status);

        var count = query.Count();
        var list = query
            .OrderByDescending(x => x.OrderDate)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        return new PagedResultDto<PurchaseOrderDto>(count, list.Select(MapToDto).ToList());
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

        var orderNumber = await _numberGenerator.GenerateAsync("PurchaseOrder", input.CompanyId);
        var po = new PurchaseOrder(GuidGenerator.Create(), input.CompanyId, input.SupplierId, orderNumber, input.OrderDate);
        po.ExpectedDeliveryDate = input.ExpectedDeliveryDate;
        po.Notes = input.Notes;

        // Auto-fill billing address from supplier master
        var partyDefaults = LazyServiceProvider.LazyGetRequiredService<Core.DomainServices.PartyDefaultsService>();
        var billingAddress = await partyDefaults.GetPrimaryAddressAsync("Supplier", input.SupplierId);
        if (billingAddress != null) po.BillingAddressId = billingAddress.Id;

        foreach (var item in input.Items)
            po.AddItem(item.ItemId, item.Description, item.Quantity, item.UnitPrice, item.TaxAmount, item.Uom);

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
        return MapToDto(po);
    }

    public async Task<PurchaseOrderDto> SubmitAsync(Guid id)
    {
        var po = await _repository.GetAsync(id);

        // Supplier hold enforcement: HoldType.All blocks POs
        var supplier = await _supplierRepository.GetAsync(po.SupplierId);
        if (supplier.HoldType == SupplierHoldType.All)
        {
            throw new BusinessException(MyERPDomainErrorCodes.SupplierOnHold)
                .WithData("supplierName", supplier.Name)
                .WithData("holdType", supplier.HoldType.ToString());
        }

        // Supplier scorecard enforcement: prevent_pos blocks PO submission
        if (supplier.PreventPurchaseOrders)
        {
            throw new BusinessException("MyERP:04006")
                .WithData("supplierName", supplier.Name);
        }

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
                var item = await _itemRepository.FindAsync(poItem.ItemId);
                if (item?.DefaultExpenseAccountId != null)
                {
                    budgetItems.Add(new BudgetCheckItem(
                        item.DefaultExpenseAccountId.Value,
                        poItem.Quantity * poItem.UnitPrice));
                }
            }

            if (budgetItems.Any())
            {
                await _budgetValidation.ValidateForPurchaseOrderAsync(
                    po.CompanyId, fiscalYear.Id, po.OrderDate, budgetItems, po.TenantId);
            }
        }

        // Minimum order quantity validation (hard error per ERPNext rules)
        foreach (var poItem in po.Items)
        {
            var item = await _itemRepository.FindAsync(poItem.ItemId);
            if (item != null && item.MinOrderQty > 0 && poItem.Quantity < item.MinOrderQty)
            {
                throw new BusinessException("MyERP:04005")
                    .WithData("itemName", item.ItemName)
                    .WithData("minQty", item.MinOrderQty)
                    .WithData("orderedQty", poItem.Quantity);
            }
        }

        po.Submit();

        // Update Bin.OrderedQty for each item (increases projected qty)
        foreach (var item in po.Items)
        {
            if (item.WarehouseId.HasValue)
            {
                await _binService.UpdateOrderedQtyAsync(
                    item.ItemId, item.WarehouseId.Value, item.Quantity, po.TenantId);
            }
        }

        // Update linked Material Request OrderedQuantity
        var mrItemIds = po.Items
            .Where(i => i.MaterialRequestItemId.HasValue)
            .Select(i => i.MaterialRequestItemId!.Value)
            .ToList();

        if (mrItemIds.Any())
        {
            var mrQuery = await _materialRequestRepository.GetQueryableAsync();
            var affectedMRs = mrQuery
                .Where(mr => mr.Items.Any(i => mrItemIds.Contains(i.Id)))
                .ToList();

            foreach (var mr in affectedMRs)
            {
                foreach (var poItem in po.Items.Where(i => i.MaterialRequestItemId.HasValue))
                {
                    var mrItem = mr.Items.FirstOrDefault(i => i.Id == poItem.MaterialRequestItemId!.Value);
                    if (mrItem != null)
                    {
                        mrItem.OrderedQuantity += poItem.Quantity;
                    }
                }
                await _materialRequestRepository.UpdateAsync(mr);
            }
        }

        await _repository.UpdateAsync(po, autoSave: true);

        // Audit trail
        await _activityLogRepository.InsertAsync(new DocumentActivityLog(
            GuidGenerator.Create(), "PurchaseOrder", po.Id, "Submitted",
            po.CompanyId, po.OrderNumber, "Draft", "ToDeliverAndBill",
            CurrentUser.Id, tenantId: po.TenantId));

        return MapToDto(po);
    }

    public async Task<PurchaseOrderDto> CancelAsync(Guid id)
    {
        var po = await _repository.GetAsync(id);

        // Guard: cannot cancel if submitted Purchase Receipts exist
        var prRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Purchasing.Entities.PurchaseReceipt, Guid>>();
        var prQuery = await prRepo.GetQueryableAsync();
        var hasSubmittedPR = prQuery.Any(pr =>
            pr.PurchaseOrderId == id
            && pr.Status != Core.DocumentStatus.Draft
            && pr.Status != Core.DocumentStatus.Cancelled);
        if (hasSubmittedPR)
        {
            throw new Volo.Abp.BusinessException("MyERP:01010")
                .WithData("documentType", "Purchase Order")
                .WithData("dependent", "Purchase Receipt");
        }

        // Guard: cannot cancel if submitted Purchase Invoices link to this PO's items
        var piRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Purchasing.Entities.PurchaseInvoice, Guid>>();
        var piQuery = await piRepo.GetQueryableAsync();
        var hasSubmittedPI = piQuery.Any(pi =>
            pi.Items.Any(i => i.PurchaseOrderItemId.HasValue
                && po.Items.Select(oi => oi.Id).Contains(i.PurchaseOrderItemId.Value))
            && pi.Status != Core.DocumentStatus.Draft
            && pi.Status != Core.DocumentStatus.Cancelled);
        if (hasSubmittedPI)
        {
            throw new Volo.Abp.BusinessException("MyERP:01010")
                .WithData("documentType", "Purchase Order")
                .WithData("dependent", "Purchase Invoice");
        }

        po.Cancel();

        // Reverse Bin.OrderedQty
        foreach (var item in po.Items)
        {
            if (item.WarehouseId.HasValue)
            {
                await _binService.UpdateOrderedQtyAsync(
                    item.ItemId, item.WarehouseId.Value, -item.Quantity, po.TenantId);
            }
        }

        await _repository.UpdateAsync(po, autoSave: true);

        // Audit trail
        await _activityLogRepository.InsertAsync(new DocumentActivityLog(
            GuidGenerator.Create(), "PurchaseOrder", po.Id, "Cancelled",
            po.CompanyId, po.OrderNumber, "ToDeliverAndBill", "Cancelled",
            CurrentUser.Id, tenantId: po.TenantId));

        return MapToDto(po);
    }

    public async Task<PurchaseOrderDto> CloseAsync(Guid id)
    {
        var po = await _repository.GetAsync(id);
        po.Close();

        // Release pending ordered qty from Bin (short-close: items not yet received)
        foreach (var item in po.Items)
        {
            if (item.WarehouseId.HasValue && item.PendingReceiptQty > 0)
            {
                await _binService.UpdateOrderedQtyAsync(
                    item.ItemId, item.WarehouseId.Value, -item.PendingReceiptQty, po.TenantId);
            }

            // Reverse MR OrderedQuantity for unreceived items linked to Material Requests
            if (item.MaterialRequestItemId.HasValue && item.PendingReceiptQty > 0)
            {
                var mrQuery = await _materialRequestRepository.GetQueryableAsync();
                var mr = mrQuery.FirstOrDefault(m => m.Items.Any(i => i.Id == item.MaterialRequestItemId.Value));
                if (mr != null)
                {
                    var mrItem = mr.Items.FirstOrDefault(i => i.Id == item.MaterialRequestItemId.Value);
                    if (mrItem != null)
                    {
                        mrItem.OrderedQuantity = Math.Max(0, mrItem.OrderedQuantity - item.PendingReceiptQty);
                        await _materialRequestRepository.UpdateAsync(mr);
                    }
                }
            }
        }

        await _repository.UpdateAsync(po, autoSave: true);
        return MapToDto(po);
    }

    public async Task<PurchaseOrderDto> ReopenAsync(Guid id)
    {
        var po = await _repository.GetAsync(id);
        po.Reopen();

        // Re-reserve ordered qty on reopen (restore pending receipt to projected qty)
        foreach (var item in po.Items)
        {
            if (item.WarehouseId.HasValue && item.PendingReceiptQty > 0)
            {
                await _binService.UpdateOrderedQtyAsync(
                    item.ItemId, item.WarehouseId.Value, item.PendingReceiptQty, po.TenantId);
            }
        }

        await _repository.UpdateAsync(po, autoSave: true);
        return MapToDto(po);
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
        return MapToDto(amended);
    }

    private static PurchaseOrderDto MapToDto(PurchaseOrder po) => new()
    {
        Id = po.Id,
        CompanyId = po.CompanyId,
        OrderNumber = po.OrderNumber,
        OrderDate = po.OrderDate,
        ExpectedDeliveryDate = po.ExpectedDeliveryDate,
        SupplierId = po.SupplierId,
        NetTotal = po.NetTotal,
        TaxAmount = po.TaxAmount,
        GrandTotal = po.GrandTotal,
        Status = po.Status.ToString(),
        PerReceived = po.PerReceived,
        PerBilled = po.PerBilled,
        Items = po.Items.Select(i => new PurchaseOrderItemDto
        {
            Id = i.Id, ItemId = i.ItemId, Description = i.Description,
            Uom = i.Uom, Quantity = i.Quantity, UnitPrice = i.UnitPrice,
            TaxAmount = i.TaxAmount, LineTotal = i.LineTotal,
            ReceivedQty = i.ReceivedQty, BilledQty = i.BilledQty,
            WarehouseId = i.WarehouseId,
        }).ToList()
    };
}
