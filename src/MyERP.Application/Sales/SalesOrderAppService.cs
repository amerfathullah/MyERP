using System;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.DomainServices;
using MyERP.Inventory.DomainServices;
using MyERP.Permissions;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using MyERP.Sales;
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

    private async Task<string?> ResolveCustomerNameAsync(Guid customerId)
    {
        var customer = await _customerRepository.FindAsync(customerId);
        return customer?.Name;
    }

    public async Task<SalesOrderDto> GetAsync(Guid id)
    {
        var order = await _repository.GetAsync(id);
        var dto = ObjectMapper.Map<SalesOrder, SalesOrderDto>(order);
        dto.CustomerName = await ResolveCustomerNameAsync(order.CustomerId);
        return dto;
    }

    public async Task<PagedResultDto<SalesOrderDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
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

        var totalCount = query.Count();
        var sorted = SortingHelper.ApplySorting(query, input.Sorting,
            q => q.OrderByDescending(x => x.OrderDate),
            ("orderNumber", x => x.OrderNumber),
            ("orderDate", x => x.OrderDate),
            ("grandTotal", x => x.GrandTotal),
            ("status", x => x.Status));
        var orders = sorted
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        var customerIds = orders.Select(o => o.CustomerId).Distinct().ToArray();
        var customers = (await _customerRepository.GetQueryableAsync())
            .Where(c => customerIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name })
            .ToDictionary(c => c.Id, c => c.Name);

        var dtos = new List<SalesOrderDto>();
        foreach (var o in orders)
        {
            var dto = ObjectMapper.Map<SalesOrder, SalesOrderDto>(o);
            dto.CustomerName = customers.GetValueOrDefault(o.CustomerId);
            dtos.Add(dto);
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

        // Validate company restriction — items/customer must allow this company
        var restrictionService = LazyServiceProvider.LazyGetRequiredService<CompanyRestrictionValidationService>();
        await restrictionService.ValidateTransactionCompanyAsync(
            "SalesOrder", input.CompanyId,
            itemIds: itemIds,
            customerIds: new[] { input.CustomerId });

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
            if (item.WarehouseId.HasValue)
                order.Items[^1].WarehouseId = item.WarehouseId;
        }

        // Resolve UOM conversion factors for stock qty calculation
        var uomService = LazyServiceProvider.LazyGetRequiredService<Inventory.DomainServices.UomConversionService>();
        var itemRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Inventory.Entities.Item, Guid>>();
        foreach (var soItem in order.Items)
        {
            var itemEntity = await itemRepo.FindAsync(soItem.ItemId);
            if (itemEntity != null)
            {
                soItem.StockUom = itemEntity.Uom;
                if (!string.Equals(soItem.Uom, itemEntity.Uom, StringComparison.OrdinalIgnoreCase))
                {
                    soItem.ConversionFactor = await uomService.GetConversionFactorAsync(
                        soItem.ItemId, soItem.Uom, itemEntity.Uom);
                }
            }
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

        // Apply shipping rule if applicable (adds shipping charge to order)
        var shippingRuleRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<ShippingRule, Guid>>();
        var shippingRules = (await shippingRuleRepo.GetListAsync())
            .Where(r => r.RuleType == ShippingRuleType.Selling && r.IsEnabled)
            .OrderBy(r => r.Label)
            .ToList();

        if (shippingRules.Any())
        {
            var netTotal = order.Items.Sum(i => i.LineTotal);
            foreach (var rule in shippingRules)
            {
                // Check country restriction (if any)
                if (!string.IsNullOrEmpty(input.ShippingCountry) && !rule.AppliesToCountry(input.ShippingCountry))
                    continue;

                var shippingCharge = rule.Calculate(netTotal);
                if (shippingCharge > 0)
                {
                    order.ShippingCharge = shippingCharge;
                    break; // First matching rule wins
                }
            }
        }

        // Apply coupon code discount if provided
        if (!string.IsNullOrWhiteSpace(input.CouponCode))
        {
            var couponService = LazyServiceProvider.LazyGetRequiredService<CouponCodeAppService>();
            var pricingRuleId = await couponService.ValidateAndApplyAsync(
                input.CouponCode, input.CustomerId, input.OrderDate);
            order.Notes = string.IsNullOrEmpty(order.Notes)
                ? $"Coupon: {input.CouponCode}"
                : $"{order.Notes} | Coupon: {input.CouponCode}";
        }

        await _repository.InsertAsync(order, autoSave: true);

        // Check if customer has overdue invoices (advisory warning, not blocking)
        var dto = ObjectMapper.Map<SalesOrder, SalesOrderDto>(order);
        dto.CustomerName = await ResolveCustomerNameAsync(order.CustomerId);
        try
        {
            var siRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.SalesInvoice, Guid>>();
            var siQuery = await siRepo.GetQueryableAsync();
            var overdueCount = siQuery.Count(si =>
                si.CustomerId == input.CustomerId
                && si.CompanyId == input.CompanyId
                && si.Status == Core.DocumentStatus.Posted
                && (si.GrandTotal - si.AmountPaid) > 0
                && si.DueDate.HasValue
                && si.DueDate.Value < DateTime.UtcNow.Date);

            if (overdueCount > 0)
            {
                var totalOverdue = siQuery
                    .Where(si => si.CustomerId == input.CustomerId
                        && si.CompanyId == input.CompanyId
                        && si.Status == Core.DocumentStatus.Posted
                        && (si.GrandTotal - si.AmountPaid) > 0
                        && si.DueDate.HasValue
                        && si.DueDate.Value < DateTime.UtcNow.Date)
                    .Sum(si => si.GrandTotal - si.AmountPaid);

                dto.OverdueWarning = $"This customer has {overdueCount} overdue invoice(s) totalling {totalOverdue:N2}. Please follow up on outstanding payments.";
            }
        }
        catch (Exception ex) { Logger.LogWarning(ex, "Overdue warning check failed for customer {Id}", input.CustomerId); }

        return dto;
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

        // Credit limit check — per DO-NOT: "must also enforce at SO, DN and SI submit"
        var creditLimitService = LazyServiceProvider
            .LazyGetRequiredService<CreditLimitService>();
        await creditLimitService.ValidateCreditLimitAsync(order.CustomerId, order.GrandTotal, order.CompanyId);

        // Selling price validation — selling rate must be >= valuation rate
        var valuationService = LazyServiceProvider
            .LazyGetRequiredService<StockValuationService>();
        var soItemData = order.Items
            .Select(i => (i.ItemId, i.UnitPrice, i.Description))
            .ToList()
            .AsReadOnly();
        await SalesInvoiceManager.ValidateSellingPriceAsync(
            soItemData,
            async itemId =>
            {
                var warehouseId = order.Items
                    .FirstOrDefault(i => i.ItemId == itemId && i.WarehouseId.HasValue)?.WarehouseId;
                if (!warehouseId.HasValue) return 0m;
                var balance = await valuationService.GetCurrentBalanceAsync(itemId, warehouseId.Value);
                return balance.ValuationRate;
            },
            action: "Warn");

        // Reserve stock for each SO item (increases Bin.ReservedQty → reduces projected qty)
        // Product Bundles: reserve COMPONENT items, not the parent bundle item
        // Drop-ship items: SKIP stock reservation entirely (no warehouse involvement)
        var bundleService = LazyServiceProvider.LazyGetRequiredService<ProductBundleDecompositionService>();
        var soItemIds = order.Items.Select(i => i.ItemId).Distinct();
        var bundleItemIds = await bundleService.GetBundleItemIdsAsync(soItemIds);
        var dropShipItemIds = DropShipService.GetDropShipItemIds(order);

        foreach (var item in order.Items)
        {
            // Skip drop-ship items — they bypass warehouse entirely
            if (item.DeliveredBySupplier) continue;
            if (!item.WarehouseId.HasValue) continue;

            if (bundleItemIds.Contains(item.ItemId))
            {
                // Bundle item: reserve each component × order qty (in stock UOM)
                var components = await bundleService.DecomposeAsync(
                    item.ItemId, item.StockQty, item.UnitPrice);
                foreach (var comp in components)
                {
                    await _binService.UpdateReservedQtyAsync(
                        comp.ComponentItemId, item.WarehouseId.Value, comp.Qty, order.TenantId);
                }
            }
            else
            {
                await _binService.UpdateReservedQtyAsync(
                    item.ItemId, item.WarehouseId.Value, item.StockQty, order.TenantId);
            }
        }

        // Auto-create Purchase Orders for drop-ship items
        if (DropShipService.HasDropShipItems(order))
        {
            var dropShipSvc = LazyServiceProvider.LazyGetRequiredService<DropShipService>();
            await dropShipSvc.CreateDropShipPurchaseOrdersAsync(order,
                async (type, companyId) => await _numberGenerator.GenerateAsync(type, companyId));
        }

        await _repository.UpdateAsync(order, autoSave: true);
        var submitDto = ObjectMapper.Map<SalesOrder, SalesOrderDto>(order);
        submitDto.CustomerName = await ResolveCustomerNameAsync(order.CustomerId);
        return submitDto;
    }

    [Authorize(MyERPPermissions.SalesOrders.Submit)]
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

    [Authorize(MyERPPermissions.SalesOrders.Cancel)]
    public async Task<SalesOrderDto> CancelAsync(Guid id)
    {
        var order = await _repository.GetAsync(id);

        // Guard: cannot cancel with submitted dependents (domain service)
        var soManager = LazyServiceProvider.LazyGetRequiredService<SalesOrderManager>();
        var dnRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.DeliveryNote, Guid>>();
        var siRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.SalesInvoice, Guid>>();
        await soManager.ValidateCanCancelAsync(order, dnRepo, siRepo);

        order.Cancel();

        // Release reserved stock (reverse of submit — bundles release components)
        var cancelBundleService = LazyServiceProvider.LazyGetRequiredService<ProductBundleDecompositionService>();
        var cancelItemIds = order.Items.Select(i => i.ItemId).Distinct();
        var cancelBundleIds = await cancelBundleService.GetBundleItemIdsAsync(cancelItemIds);

        foreach (var item in order.Items)
        {
            if (!item.WarehouseId.HasValue) continue;

            if (cancelBundleIds.Contains(item.ItemId))
            {
                var components = await cancelBundleService.DecomposeAsync(
                    item.ItemId, item.StockQty, item.UnitPrice);
                foreach (var comp in components)
                {
                    await _binService.UpdateReservedQtyAsync(
                        comp.ComponentItemId, item.WarehouseId.Value, -comp.Qty, order.TenantId);
                }
            }
            else
            {
                await _binService.UpdateReservedQtyAsync(
                    item.ItemId, item.WarehouseId.Value, -item.StockQty, order.TenantId);
            }
        }

        await _repository.UpdateAsync(order, autoSave: true);
        var cancelDto = ObjectMapper.Map<SalesOrder, SalesOrderDto>(order);
        cancelDto.CustomerName = await ResolveCustomerNameAsync(order.CustomerId);
        return cancelDto;
    }

    [Authorize(MyERPPermissions.SalesOrders.Edit)]
    public async Task<SalesOrderDto> CloseAsync(Guid id)
    {
        var order = await _repository.GetAsync(id);
        order.Close();

        // Release remaining reserved stock for undelivered items (short-close)
        // Bundle-aware: release component items for bundles, skip drop-ship items
        var closeBundleService = LazyServiceProvider.LazyGetRequiredService<ProductBundleDecompositionService>();
        var closeItemIds = order.Items.Select(i => i.ItemId).Distinct();
        var closeBundleIds = await closeBundleService.GetBundleItemIdsAsync(closeItemIds);

        foreach (var item in order.Items)
        {
            if (item.DeliveredBySupplier) continue; // drop-ship: no stock to release
            var pendingQty = item.PendingDeliveryQty;
            if (pendingQty <= 0 || !item.WarehouseId.HasValue) continue;

            // Convert pending qty to stock UOM for Bin release
            var pendingStockQty = pendingQty * item.ConversionFactor;

            if (closeBundleIds.Contains(item.ItemId))
            {
                var components = await closeBundleService.DecomposeAsync(
                    item.ItemId, pendingStockQty, item.UnitPrice);
                foreach (var comp in components)
                {
                    await _binService.UpdateReservedQtyAsync(
                        comp.ComponentItemId, item.WarehouseId.Value, -comp.Qty, order.TenantId);
                }
            }
            else
            {
                await _binService.UpdateReservedQtyAsync(
                    item.ItemId, item.WarehouseId.Value, -pendingStockQty, order.TenantId);
            }
        }

        // Per DO-NOT: "Close Sales Order without cascading status to linked Subcontracting Inward Orders"
        var scioRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Purchasing.Entities.SubcontractingInwardOrder, Guid>>();
        var scioQuery = await scioRepo.GetQueryableAsync();
        var linkedScioList = scioQuery
            .Where(s => s.SalesOrderId == id &&
                        s.Status != Purchasing.Entities.SubcontractingInwardOrderStatus.Cancelled &&
                        s.Status != Purchasing.Entities.SubcontractingInwardOrderStatus.Closed)
            .ToList();
        foreach (var scio in linkedScioList)
        {
            scio.Close();
            await scioRepo.UpdateAsync(scio);
        }

        await _repository.UpdateAsync(order, autoSave: true);
        var closeDto = ObjectMapper.Map<SalesOrder, SalesOrderDto>(order);
        closeDto.CustomerName = await ResolveCustomerNameAsync(order.CustomerId);
        return closeDto;
    }

    [Authorize(MyERPPermissions.SalesOrders.Edit)]
    public async Task<SalesOrderDto> ReopenAsync(Guid id)
    {
        var order = await _repository.GetAsync(id);
        order.Reopen();

        // Re-reserve stock for pending delivery items (bundle-aware)
        var reopenBundleService = LazyServiceProvider.LazyGetRequiredService<ProductBundleDecompositionService>();
        var reopenItemIds = order.Items.Select(i => i.ItemId).Distinct();
        var reopenBundleIds = await reopenBundleService.GetBundleItemIdsAsync(reopenItemIds);

        foreach (var item in order.Items)
        {
            if (item.DeliveredBySupplier) continue; // drop-ship: no stock to reserve
            var pendingQty = item.PendingDeliveryQty;
            if (pendingQty <= 0 || !item.WarehouseId.HasValue) continue;

            // Convert pending qty to stock UOM for Bin reservation
            var pendingStockQty = pendingQty * item.ConversionFactor;

            if (reopenBundleIds.Contains(item.ItemId))
            {
                var components = await reopenBundleService.DecomposeAsync(
                    item.ItemId, pendingStockQty, item.UnitPrice);
                foreach (var comp in components)
                {
                    await _binService.UpdateReservedQtyAsync(
                        comp.ComponentItemId, item.WarehouseId.Value, comp.Qty, order.TenantId);
                }
            }
            else
            {
                await _binService.UpdateReservedQtyAsync(
                    item.ItemId, item.WarehouseId.Value, pendingStockQty, order.TenantId);
            }
        }

        await _repository.UpdateAsync(order, autoSave: true);
        var reopenDto = ObjectMapper.Map<SalesOrder, SalesOrderDto>(order);
        reopenDto.CustomerName = await ResolveCustomerNameAsync(order.CustomerId);
        return reopenDto;
    }

    [Authorize(MyERPPermissions.SalesOrders.Edit)]
    public async Task<SalesOrderDto> UpdateAsync(Guid id, CreateSalesOrderDto input)
    {
        var order = await _repository.GetAsync(id);
        if (order.Status != Core.DocumentStatus.Draft)
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("detail", "Only Draft sales orders can be edited");

        order.OrderDate = input.OrderDate;
        order.DeliveryDate = input.DeliveryDate;
        order.CustomerId = input.CustomerId;
        order.Notes = input.Notes;

        // Replace items
        order.ClearItems();
        foreach (var item in input.Items)
        {
            order.AddItem(item.ItemId, item.Description, item.Quantity, item.UnitPrice, item.TaxAmount, item.Uom);
            if (item.WarehouseId.HasValue)
                order.Items[^1].WarehouseId = item.WarehouseId;
        }

        await _repository.UpdateAsync(order, autoSave: true);
        var dto = ObjectMapper.Map<SalesOrder, SalesOrderDto>(order);
        dto.CustomerName = await ResolveCustomerNameAsync(order.CustomerId);
        return dto;
    }

    [Authorize(MyERPPermissions.SalesOrders.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        var order = await _repository.GetAsync(id);
        if (order.Status != Core.DocumentStatus.Draft)
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("detail", "Only Draft sales orders can be deleted");
        await _repository.DeleteAsync(id);
    }

    /// <summary>
    /// Gets delivery schedule entries for a sales order.
    /// Per ERPNext SO delivery schedule: planned delivery windows with qty tracking.
    /// </summary>
    [Authorize(MyERPPermissions.SalesOrders.Default)]
    public async Task<List<DeliveryScheduleEntryDto>> GetDeliveryScheduleAsync(Guid orderId)
    {
        var scheduleRepo = LazyServiceProvider
            .LazyGetRequiredService<IRepository<DeliveryScheduleEntry, Guid>>();
        var queryable = await scheduleRepo.GetQueryableAsync();
        var entries = queryable
            .Where(e => e.SalesOrderId == orderId)
            .OrderBy(e => e.ScheduledDate)
            .ToList();

        return entries.Select(e => new DeliveryScheduleEntryDto
        {
            Id = e.Id,
            SalesOrderItemId = e.SalesOrderItemId,
            ScheduledDate = e.ScheduledDate,
            ScheduledQty = e.ScheduledQty,
            DeliveredQty = e.DeliveredQty,
            PendingQty = e.PendingQty,
            IsFullyDelivered = e.IsFullyDelivered,
        }).ToList();
    }

    /// <summary>
    /// Generates delivery schedule entries for a sales order item by splitting qty across dates.
    /// Per ERPNext gotcha #108: SO Delivery Schedule splits by frequency.
    /// </summary>
    [Authorize(MyERPPermissions.SalesOrders.Edit)]
    public async Task<List<DeliveryScheduleEntryDto>> GenerateDeliveryScheduleAsync(
        Guid orderId, Guid itemId, string frequency)
    {
        var order = await _repository.GetAsync(orderId);
        var item = order.Items.FirstOrDefault(i => i.Id == itemId);
        if (item == null)
            throw new Volo.Abp.BusinessException("MyERP:01007")
                .WithData("detail", "Item not found on this order");

        if (!Enum.TryParse<MyERP.Sales.DomainServices.DeliveryFrequency>(frequency, true, out var freq))
            freq = MyERP.Sales.DomainServices.DeliveryFrequency.Monthly;

        var scheduleService = LazyServiceProvider
            .LazyGetRequiredService<DeliveryScheduleService>();
        var entries = scheduleService.GenerateSchedule(
            orderId, itemId, item.Quantity,
            order.OrderDate, order.DeliveryDate ?? order.OrderDate.AddMonths(3),
            freq);

        var scheduleRepo = LazyServiceProvider
            .LazyGetRequiredService<IRepository<DeliveryScheduleEntry, Guid>>();
        foreach (var entry in entries)
            await scheduleRepo.InsertAsync(entry);

        return entries.Select(e => new DeliveryScheduleEntryDto
        {
            Id = e.Id,
            SalesOrderItemId = e.SalesOrderItemId,
            ScheduledDate = e.ScheduledDate,
            ScheduledQty = e.ScheduledQty,
            DeliveredQty = e.DeliveredQty,
            PendingQty = e.PendingQty,
            IsFullyDelivered = e.IsFullyDelivered,
        }).ToList();
    }
}

