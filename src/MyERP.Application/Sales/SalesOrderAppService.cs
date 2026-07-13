using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.DomainServices;
using MyERP.Inventory.DomainServices;
using MyERP.Permissions;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using MyERP.Shared;
using MyERP.Workflow.DomainServices;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

[Authorize(MyERPPermissions.SalesOrders.Default)]
public class SalesOrderAppService : ApplicationService, ISalesOrderAppService
{
    private readonly IRepository<SalesOrder, Guid> _repository;
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IDocumentNumberGenerator _numberGenerator;
    private readonly BinService _binService;
    private readonly ApprovalWorkflowManager _approvalManager;
    private readonly PricingRuleApplicationService _pricingRuleService;
    private readonly ItemTransactionValidationService _itemValidation;

    public SalesOrderAppService(
        IRepository<SalesOrder, Guid> repository,
        IRepository<Customer, Guid> customerRepository,
        IDocumentNumberGenerator numberGenerator,
        BinService binService,
        ApprovalWorkflowManager approvalManager,
        PricingRuleApplicationService pricingRuleService,
        ItemTransactionValidationService itemValidation)
    {
        _repository = repository;
        _customerRepository = customerRepository;
        _numberGenerator = numberGenerator;
        _binService = binService;
        _approvalManager = approvalManager;
        _pricingRuleService = pricingRuleService;
        _itemValidation = itemValidation;
    }

    public async Task<SalesOrderDto> GetAsync(Guid id)
    {
        var order = await _repository.GetAsync(id);
        return await MapToDtoAsync(order);
    }

    public async Task<PagedResultDto<SalesOrderDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
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

        var totalCount = query.Count();
        var orders = query
            .OrderByDescending(x => x.OrderDate)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        var dtos = new System.Collections.Generic.List<SalesOrderDto>();
        foreach (var o in orders)
        {
            dtos.Add(await MapToDtoAsync(o));
        }

        return new PagedResultDto<SalesOrderDto>(totalCount, dtos);
    }

    [Authorize(MyERPPermissions.SalesOrders.Create)]
    public async Task<SalesOrderDto> CreateAsync(CreateSalesOrderDto input)
    {
        // Input validation
        Check.NotDefaultOrNull<Guid>(input.CompanyId, nameof(input.CompanyId));
        Check.NotDefaultOrNull<Guid>(input.CustomerId, nameof(input.CustomerId));
        if (input.Items == null || input.Items.Count == 0)
            throw new Volo.Abp.BusinessException("MyERP:01007")
                .WithData("documentType", "Sales Order");

        // Validate all items are active before creating the order
        var itemIds = input.Items.Select(i => i.ItemId).ToArray();
        await _itemValidation.ValidateItemsForTransactionAsync(itemIds);

        var orderNumber = await _numberGenerator.GenerateAsync("SalesOrder", input.CompanyId);

        var order = new SalesOrder(
            GuidGenerator.Create(),
            input.CompanyId,
            input.CustomerId,
            orderNumber,
            input.OrderDate);

        order.DeliveryDate = input.DeliveryDate;
        order.CustomerPoNumber = input.CustomerPoNumber;
        order.CurrencyCode = input.CurrencyCode;
        order.Terms = input.Terms;
        order.Notes = input.Notes;

        // Auto-fill addresses from customer master
        var partyDefaults = LazyServiceProvider.LazyGetRequiredService<Core.DomainServices.PartyDefaultsService>();
        var billingAddress = await partyDefaults.GetPrimaryAddressAsync("Customer", input.CustomerId);
        if (billingAddress != null) order.BillingAddressId = billingAddress.Id;
        var shippingAddress = await partyDefaults.GetShippingAddressAsync("Customer", input.CustomerId);
        if (shippingAddress != null) order.ShippingAddressId = shippingAddress.Id;

        if (input.QuotationId.HasValue)
        {
            order.QuotationId = input.QuotationId;
        }

        foreach (var item in input.Items)
        {
            order.AddItem(item.ItemId, item.Description, item.Quantity, item.UnitPrice, item.TaxAmount, item.Uom);
        }

        // Apply pricing rules (auto-discount based on configured rules)
        var pricingContexts = order.Items.Select(i => new PricingRuleContext
        {
            ItemId = i.ItemId,
            ItemName = i.Description,
            Qty = i.Quantity,
            Rate = i.UnitPrice,
        }).ToList();

        if (pricingContexts.Any())
        {
            await _pricingRuleService.ApplyToItemsAsync(
                pricingContexts, order.OrderDate, "Selling",
                order.CustomerId, order.CompanyId);

            // Update item rates with discounted rates where applicable
            for (int idx = 0; idx < order.Items.Count; idx++)
            {
                var ctx = pricingContexts[idx];
                if (ctx.DiscountedRate > 0 && ctx.DiscountedRate != ctx.Rate)
                {
                    order.Items[idx].UnitPrice = ctx.DiscountedRate;
                }
            }
        }

        await _repository.InsertAsync(order, autoSave: true);
        return await MapToDtoAsync(order);
    }

    [Authorize(MyERPPermissions.SalesOrders.Submit)]
    public async Task<SalesOrderDto> SubmitAsync(Guid id)
    {
        var order = await _repository.GetAsync(id);

        // Check approval workflow — block submit if approval is pending
        var isFullyApproved = await _approvalManager.IsFullyApprovedAsync("SalesOrder", order.Id);
        if (!isFullyApproved)
        {
            // Initiate approval if not already done
            var needsApproval = await _approvalManager.InitiateApprovalAsync(
                "SalesOrder", order.Id, CurrentUser.Id ?? Guid.Empty,
                order.GrandTotal, order.CompanyId, order.TenantId);

            if (needsApproval)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ApprovalPending)
                    .WithData("documentType", "Sales Order")
                    .WithData("documentId", order.Id);
            }
        }

        order.Submit();

        // Reserve stock for each SO item (increases Bin.ReservedQty → reduces projected qty)
        foreach (var item in order.Items)
        {
            if (item.WarehouseId.HasValue)
            {
                await _binService.UpdateReservedQtyAsync(
                    item.ItemId, item.WarehouseId.Value, item.Quantity, order.TenantId);
            }
        }

        await _repository.UpdateAsync(order, autoSave: true);
        return await MapToDtoAsync(order);
    }

    [Authorize(MyERPPermissions.SalesOrders.Cancel)]
    public async Task<SalesOrderDto> CancelAsync(Guid id)
    {
        var order = await _repository.GetAsync(id);

        // Guard: cannot cancel if submitted Delivery Notes exist
        var dnRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.DeliveryNote, Guid>>();
        var dnQuery = await dnRepo.GetQueryableAsync();
        var hasSubmittedDN = dnQuery.Any(dn =>
            dn.SalesOrderId == id
            && dn.Status != Core.DocumentStatus.Draft
            && dn.Status != Core.DocumentStatus.Cancelled);
        if (hasSubmittedDN)
        {
            throw new Volo.Abp.BusinessException("MyERP:01010")
                .WithData("documentType", "Sales Order")
                .WithData("dependent", "Delivery Note");
        }

        // Guard: cannot cancel if submitted Sales Invoices link to this SO's items
        var siItemRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.SalesInvoice, Guid>>();
        var siQuery = await siItemRepo.GetQueryableAsync();
        var hasSubmittedSI = siQuery.Any(si =>
            si.Items.Any(i => i.SalesOrderItemId.HasValue
                && order.Items.Select(oi => oi.Id).Contains(i.SalesOrderItemId.Value))
            && si.Status != Core.DocumentStatus.Draft
            && si.Status != Core.DocumentStatus.Cancelled);
        if (hasSubmittedSI)
        {
            throw new Volo.Abp.BusinessException("MyERP:01010")
                .WithData("documentType", "Sales Order")
                .WithData("dependent", "Sales Invoice");
        }

        order.Cancel();

        // Release reserved stock
        foreach (var item in order.Items)
        {
            if (item.WarehouseId.HasValue)
            {
                await _binService.UpdateReservedQtyAsync(
                    item.ItemId, item.WarehouseId.Value, -item.Quantity, order.TenantId);
            }
        }

        await _repository.UpdateAsync(order, autoSave: true);
        return await MapToDtoAsync(order);
    }

    [Authorize(MyERPPermissions.SalesOrders.Edit)]
    public async Task<SalesOrderDto> CloseAsync(Guid id)
    {
        var order = await _repository.GetAsync(id);
        order.Close();

        // Release remaining reserved stock for undelivered items (short-close)
        foreach (var item in order.Items)
        {
            var pendingQty = item.PendingDeliveryQty;
            if (pendingQty > 0 && item.WarehouseId.HasValue)
            {
                await _binService.UpdateReservedQtyAsync(
                    item.ItemId, item.WarehouseId.Value, -pendingQty, order.TenantId);
            }
        }

        await _repository.UpdateAsync(order, autoSave: true);
        return await MapToDtoAsync(order);
    }

    [Authorize(MyERPPermissions.SalesOrders.Edit)]
    public async Task<SalesOrderDto> ReopenAsync(Guid id)
    {
        var order = await _repository.GetAsync(id);
        order.Reopen();

        // Re-reserve stock for pending delivery items
        foreach (var item in order.Items)
        {
            var pendingQty = item.PendingDeliveryQty;
            if (pendingQty > 0 && item.WarehouseId.HasValue)
            {
                await _binService.UpdateReservedQtyAsync(
                    item.ItemId, item.WarehouseId.Value, pendingQty, order.TenantId);
            }
        }

        await _repository.UpdateAsync(order, autoSave: true);
        return await MapToDtoAsync(order);
    }

    private async Task<SalesOrderDto> MapToDtoAsync(SalesOrder order)
    {
        string? customerName = null;
        try
        {
            var customer = await _customerRepository.GetAsync(order.CustomerId);
            customerName = customer.Name;
        }
        catch { /* customer may not exist */ }

        return new SalesOrderDto
        {
            Id = order.Id,
            CompanyId = order.CompanyId,
            OrderNumber = order.OrderNumber,
            OrderDate = order.OrderDate,
            DeliveryDate = order.DeliveryDate,
            CustomerId = order.CustomerId,
            CustomerName = customerName,
            CustomerPoNumber = order.CustomerPoNumber,
            CurrencyCode = order.CurrencyCode,
            NetTotal = order.NetTotal,
            TaxAmount = order.TaxAmount,
            GrandTotal = order.GrandTotal,
            Terms = order.Terms,
            Notes = order.Notes,
            Status = order.Status.ToString(),
            QuotationId = order.QuotationId,
            PerDelivered = order.PerDelivered,
            PerBilled = order.PerBilled,
            CreationTime = order.CreationTime,
            LastModificationTime = order.LastModificationTime,
            Items = order.Items.Select(i => new SalesOrderItemDto
            {
                Id = i.Id,
                ItemId = i.ItemId,
                Description = i.Description,
                Uom = i.Uom,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                TaxAmount = i.TaxAmount,
                LineTotal = i.LineTotal,
                DeliveredQty = i.DeliveredQty,
                BilledQty = i.BilledQty,
                WarehouseId = i.WarehouseId,
            }).ToList(),
        };
    }
}
