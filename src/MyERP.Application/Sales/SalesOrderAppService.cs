using System;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core.DomainServices;
using MyERP.Inventory.DomainServices;
using MyERP.Permissions;
using MyERP.Purchasing;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using MyERP.Sales;
using MyERP.Settings;
using MyERP.Shared;
using MyERP.Workflow.DomainServices;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Settings;

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
    private readonly ChildItemUpdateService _childItemUpdateService;

    public SalesOrderAppService(
        IRepository<SalesOrder, Guid> repository,
        IRepository<Customer, Guid> customerRepository,
        IDocumentNumberGenerator numberGenerator,
        BinService binService,
        ApprovalWorkflowManager approvalManager,
        PricingRuleApplicationService pricingRuleService,
        ItemTransactionValidationService itemValidation,
        ChildItemUpdateService childItemUpdateService)
    {
        _repository = repository;
        _customerRepository = customerRepository;
        _numberGenerator = numberGenerator;
        _binService = binService;
        _approvalManager = approvalManager;
        _pricingRuleService = pricingRuleService;
        _itemValidation = itemValidation;
        _childItemUpdateService = childItemUpdateService;
    }

    private async Task<string?> ResolveCustomerNameAsync(Guid customerId)
    {
        var customer = await _customerRepository.FindAsync(customerId);
        return customer?.Name;
    }

    private async Task ResolveFulfillmentDatesAsync(SalesOrderDto dto, Guid orderId)
    {
        try
        {
            var dnRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.DeliveryNote, Guid>>();
            var dnQuery = await dnRepo.GetQueryableAsync();
            var dnDates = dnQuery
                .Where(dn => dn.SalesOrderId == orderId && dn.Status != Core.DocumentStatus.Cancelled && !dn.IsReturn)
                .Select(dn => dn.PostingDate)
                .ToList();
            if (dnDates.Count > 0)
            {
                dto.FirstDeliveryDate = dnDates.Min();
                dto.LastDeliveryDate = dnDates.Max();
            }

            var siRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.SalesInvoice, Guid>>();
            var siQuery = await siRepo.GetQueryableAsync();
            var soItemIds = dto.Items.Select(i => i.Id).ToHashSet();
            var billedInvoices = siQuery
                .Where(si => si.Status != Core.DocumentStatus.Cancelled && !si.IsReturn)
                .Where(si => si.Items.Any(item => item.SalesOrderItemId != null && soItemIds.Contains(item.SalesOrderItemId!.Value)))
                .OrderBy(si => si.IssueDate)
                .Select(si => si.IssueDate)
                .ToList();
            if (billedInvoices.Count > 0) dto.FirstBilledDate = billedInvoices.First();

            var peRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Accounting.Entities.PaymentEntry, Guid>>();
            var peQuery = await peRepo.GetQueryableAsync();
            var paymentDate = peQuery
                .Where(pe => pe.AgainstOrderId == orderId && pe.AgainstOrderType == "SalesOrder"
                    && pe.Status == Core.DocumentStatus.Posted)
                .OrderBy(pe => pe.PostingDate)
                .Select(pe => pe.PostingDate)
                .FirstOrDefault();
            if (paymentDate != default) dto.FirstPaymentDate = paymentDate;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to resolve fulfillment dates for SO {OrderId}", orderId);
        }
    }

    private async Task SaveSalesTeamAsync(SalesOrder order, List<SalesTeamAllocationInputDto>? salesTeam)
    {
        if (salesTeam == null || salesTeam.Count == 0) return;

        var totalPercentage = salesTeam.Sum(s => s.AllocatedPercentage);
        if (Math.Round(totalPercentage, 2) != 100m)
            throw new BusinessException(MyERPDomainErrorCodes.SalesTeamPercentageMustTotal100)
                .WithData("total", Math.Round(totalPercentage, 2));

        var spRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<SalesPerson, Guid>>();
        var teamRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<SalesTeamEntry, Guid>>();
        var itemRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Inventory.Entities.Item, Guid>>();

        var itemIds = order.Items.Select(i => i.ItemId).Distinct().ToList();
        var itemEntities = await itemRepo.GetListAsync(i => itemIds.Contains(i.Id));
        var itemGrantMap = itemEntities.ToDictionary(i => i.Id, i => i.GrantCommission);

        var eligibleAmount = order.Items
            .Where(i => !itemGrantMap.TryGetValue(i.ItemId, out var grant) || grant)
            .Sum(i => i.LineTotal);

        foreach (var row in salesTeam)
        {
            var salesPerson = await spRepo.GetAsync(row.SalesPersonId);
            if (salesPerson.IsGroup)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Cannot assign Group Sales Person '{salesPerson.Name}' to Sales Team.");
            }
            if (!salesPerson.IsEnabled)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Cannot assign Disabled Sales Person '{salesPerson.Name}' to Sales Team.");
            }

            var commissionRate = row.CommissionRate ?? salesPerson.CommissionRate;

            var entry = new SalesTeamEntry(
                GuidGenerator.Create(), row.SalesPersonId, "SalesOrder", order.Id,
                row.AllocatedPercentage, eligibleAmount, commissionRate);
            await teamRepo.InsertAsync(entry);
        }
    }

    private async Task AttachSalesTeamAsync(SalesOrderDto dto)
    {
        var teamRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<SalesTeamEntry, Guid>>();
        var teamQuery = await teamRepo.GetQueryableAsync();
        var entries = teamQuery.Where(e => e.ParentType == "SalesOrder" && e.ParentId == dto.Id).ToList();
        if (entries.Count == 0) return;

        var spRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<SalesPerson, Guid>>();
        var spQuery = await spRepo.GetQueryableAsync();
        var spIds = entries.Select(e => e.SalesPersonId).Distinct().ToList();
        var spNames = spQuery.Where(sp => spIds.Contains(sp.Id))
            .Select(sp => new { sp.Id, sp.Name }).ToList()
            .ToDictionary(sp => sp.Id, sp => sp.Name);

        dto.SalesTeam = entries.Select(e => new SalesTeamEntryDto
        {
            SalesPersonId = e.SalesPersonId,
            SalesPersonName = spNames.GetValueOrDefault(e.SalesPersonId),
            AllocatedPercentage = e.AllocatedPercentage,
            AllocatedAmount = e.AllocatedAmount,
            CommissionRate = e.CommissionRate,
            Incentives = e.Incentives,
        }).ToList();
    }

    public async Task<SalesOrderDto> GetAsync(Guid id)
    {
        var order = await _repository.GetAsync(id);
        var dto = ObjectMapper.Map<SalesOrder, SalesOrderDto>(order);
        dto.CustomerName = await ResolveCustomerNameAsync(order.CustomerId);
        dto.AdvancePaid = order.AdvancePaid;
        dto.PerAdvancePaid = order.PerAdvancePaid;
        await ResolveFulfillmentDatesAsync(dto, id);
        await AttachSalesTeamAsync(dto);
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

        var companyRestriction = LazyServiceProvider.LazyGetRequiredService<Core.DomainServices.CompanyRestrictionValidationService>();
        await companyRestriction.ValidateTransactionCompanyAsync("SalesOrder", input.CompanyId, itemIds, customerIds: new[] { input.CustomerId });

        var customerForStatus = await _customerRepository.GetAsync(input.CustomerId);
        LazyServiceProvider.LazyGetRequiredService<Core.DomainServices.PartyValidationService>()
            .ValidatePartyStatus("Customer", isFrozen: false, isDisabled: !customerForStatus.IsActive, customerForStatus.Name);

        var orderNumber = await _numberGenerator.GenerateAsync("SalesOrder", input.CompanyId);

        var order = new SalesOrder(
            GuidGenerator.Create(),
            input.CompanyId,
            input.CustomerId,
            orderNumber,
            input.OrderDate);

        order.DeliveryDate = input.DeliveryDate;
        order.CustomerPoNumber = input.CustomerPoNumber;
        order.ContactPersonId = input.ContactPersonId;
        order.ShippingContactPersonId = input.ShippingContactPersonId;
        order.CurrencyCode = input.CurrencyCode;
        order.Terms = input.Terms;
        order.Notes = input.Notes;
        order.CostCenterId = input.CostCenterId;
        order.ProjectId = input.ProjectId;

        // Per ERPNext: Price List defaults from the customer's own default when not given explicitly.
        order.PriceListId = input.PriceListId
            ?? (await _customerRepository.FindAsync(input.CustomerId))?.DefaultPriceListId;

        // Per gotcha #468: project-customer cross-validation
        if (input.ProjectId.HasValue)
        {
            var projectRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Projects.Entities.Project, Guid>>();
            var project = await projectRepo.FindAsync(input.ProjectId.Value);
            if (project != null && project.CustomerId.HasValue && project.CustomerId.Value != input.CustomerId)
            {
                throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Project {project.ProjectName} belongs to a different Customer.");
            }
        }

        // Auto-fill addresses from customer master
        var partyDefaults = LazyServiceProvider.LazyGetRequiredService<Core.DomainServices.PartyDefaultsService>();
        var billingAddress = await partyDefaults.GetPrimaryAddressAsync("Customer", input.CustomerId);
        if (billingAddress != null) order.BillingAddressId = billingAddress.Id;
        var shippingAddress = await partyDefaults.GetShippingAddressAsync("Customer", input.CustomerId);
        if (shippingAddress != null) order.ShippingAddressId = shippingAddress.Id;

        if (input.QuotationId.HasValue)
        {
            var quotationRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Quotation, Guid>>();
            var quotation = await quotationRepo.FindAsync(input.QuotationId.Value);
            if (quotation != null && quotation.IsExpired)
            {
                var allowExpired = await SettingProvider.GetOrNullAsync(MyERPSettings.Selling.AllowSalesOrderCreationForExpiredQuotation);
                if (allowExpired != "true")
                {
                    throw new BusinessException(MyERPDomainErrorCodes.QuotationExpired)
                        .WithData("quotationNumber", quotation.QuotationNumber)
                        .WithData("validUntil", quotation.ValidUntil?.ToString("yyyy-MM-dd") ?? "");
                }
            }
            order.QuotationId = input.QuotationId;
        }

        foreach (var item in input.Items)
        {
            order.AddItem(item.ItemId, item.Description, item.Quantity, item.UnitPrice, item.TaxAmount, item.Uom, item.DeliveryDate, item.QuotationItemId);
            if (item.WarehouseId.HasValue)
                order.Items[^1].WarehouseId = item.WarehouseId;
            if (item.BlanketOrderId.HasValue)
                order.Items[^1].BlanketOrderId = item.BlanketOrderId;
        }

        order.ValidateDeliveryDates();

        // Resolve UOM conversion factors for stock qty calculation & service items skip delivery
        var skipDnForService = (await SettingProvider.GetOrNullAsync(MyERPSettings.Selling.SkipDeliveryNoteForServiceItems)) == "true";
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
                if (skipDnForService && !itemEntity.MaintainStock)
                {
                    soItem.SkipDelivery = true;
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

            // Only compute net weight when a rule actually needs it — Calculate() was being fed
            // netTotal unconditionally before, so a "Based on Net Weight" rule matched its tiers
            // against the order's currency total instead of its weight.
            decimal netWeight = 0m;
            if (shippingRules.Any(r => r.CalculationMode == ShippingCalculationMode.BasedOnNetWeight))
            {
                foreach (var soItem in order.Items)
                {
                    var itemEntity = await itemRepo.FindAsync(soItem.ItemId);
                    netWeight += soItem.Quantity * (itemEntity?.WeightPerUnit ?? 0m);
                }
            }

            foreach (var rule in shippingRules)
            {
                // Check country restriction (if any)
                if (!string.IsNullOrEmpty(input.ShippingCountry) && !rule.AppliesToCountry(input.ShippingCountry))
                    continue;

                var value = rule.CalculationMode == ShippingCalculationMode.BasedOnNetWeight
                    ? netWeight
                    : netTotal;

                var shippingCharge = rule.Calculate(value);
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
            order.CouponCode = input.CouponCode;
            order.Notes = string.IsNullOrEmpty(order.Notes)
                ? $"Coupon: {input.CouponCode}"
                : $"{order.Notes} | Coupon: {input.CouponCode}";
        }

        await _repository.InsertAsync(order, autoSave: true);

        // Resolve per-item stock availability for warehouse visibility
        var dto = ObjectMapper.Map<SalesOrder, SalesOrderDto>(order);
        dto.CustomerName = await ResolveCustomerNameAsync(order.CustomerId);
        try
        {
            var binQuery = await LazyServiceProvider
                .LazyGetRequiredService<IRepository<Inventory.Entities.Bin, Guid>>()
                .GetQueryableAsync();
            foreach (var itemDto in dto.Items)
            {
                var warehouseId = order.Items.FirstOrDefault(i => i.Id == itemDto.Id)?.WarehouseId;
                decimal available;
                if (warehouseId.HasValue)
                {
                    available = binQuery
                        .Where(b => b.ItemId == itemDto.ItemId && b.WarehouseId == warehouseId.Value)
                        .Select(b => b.ActualQty - b.ReservedQty)
                        .FirstOrDefault();
                }
                else
                {
                    available = binQuery
                        .Where(b => b.ItemId == itemDto.ItemId)
                        .Sum(b => b.ActualQty - b.ReservedQty);
                }
                itemDto.AvailableQty = available;
                itemDto.IsInsufficientStock = itemDto.Quantity > available;
            }
        }
        catch (Exception ex) { Logger.LogWarning(ex, "Stock availability check failed for SO {Id}", order.Id); }
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

        await SaveSalesTeamAsync(order, input.SalesTeam);
        await AttachSalesTeamAsync(dto);

        return dto;
    }

    [Authorize(MyERPPermissions.SalesOrders.Submit)]
    public async Task<SalesOrderDto> SubmitAsync(Guid id)
    {
        var order = await _repository.GetAsync(id);

        // Duplicate customer PO number check — per ERPNext validate_po(): a customer's PO
        // number should map to exactly one SO unless the multiple-POs setting is enabled.
        if (!string.IsNullOrWhiteSpace(order.CustomerPoNumber))
        {
            var allowMultiplePOs = await SettingProvider.IsTrueAsync(
                MyERP.Settings.MyERPSettings.Selling.AllowAgainstMultiplePurchaseOrders);
            if (!allowMultiplePOs)
            {
                var duplicateQuery = await _repository.GetQueryableAsync();
                var duplicate = duplicateQuery.FirstOrDefault(so =>
                    so.Id != order.Id
                    && so.CustomerId == order.CustomerId
                    && so.CustomerPoNumber == order.CustomerPoNumber
                    && so.Status != Core.DocumentStatus.Draft
                    && so.Status != Core.DocumentStatus.Cancelled);
                if (duplicate != null)
                {
                    throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.DuplicateCustomerPoNumber)
                        .WithData("poNumber", order.CustomerPoNumber)
                        .WithData("existingOrderNumber", duplicate.OrderNumber);
                }
            }
        }

        // Authorization control: high-value transaction approval check
        // Per ERPNext: Authorization Rules check based on GrandTotal/Discount
        var authControl = LazyServiceProvider.LazyGetRequiredService<MyERP.Core.DomainServices.AuthorizationControlService>();
        var userRoles = (CurrentUser.Roles ?? Array.Empty<string>()).ToArray();
        await authControl.ValidateApprovingAuthorityAsync(
            "SalesOrder", order.CompanyId,
            CurrentUser.Id ?? Guid.Empty, userRoles, order.GrandTotal);

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

        // Project must belong to the order's customer (prevents billing/costing
        // against a different customer's project) — same rule as Sales Invoice.
        if (order.ProjectId.HasValue)
        {
            var projectValidation = LazyServiceProvider
                .LazyGetRequiredService<MyERP.Core.DomainServices.TransactionValidationService>();
            await projectValidation.ValidateProjectCustomerAsync(order.ProjectId, order.CustomerId);
        }

        // Credit limit check — per DO-NOT: "must also enforce at SO, DN and SI submit"
        // isAtSalesOrder=true: honors Customer.BypassCreditLimitCheckAtSalesOrder, an SO-only
        // bypass — DN/SI submit still enforce the limit for such a customer regardless.
        var creditLimitService = LazyServiceProvider
            .LazyGetRequiredService<CreditLimitService>();
        await creditLimitService.ValidateCreditLimitAsync(
            order.CustomerId, order.GrandTotal, order.CompanyId, userRoles, isAtSalesOrder: true);

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

        // Deduct consumed qty from any linked Blanket Order allocations
        await ConsumeBlanketOrdersAsync(order);

        // Auto-create Purchase Orders for drop-ship items
        if (DropShipService.HasDropShipItems(order))
        {
            var dropShipSvc = LazyServiceProvider.LazyGetRequiredService<DropShipService>();
            await dropShipSvc.CreateDropShipPurchaseOrdersAsync(order,
                async (type, companyId) => await _numberGenerator.GenerateAsync(type, companyId));
        }

        // Inter-company: auto-create PO in target company when customer represents another company
        try
        {
            var interCompanyService = LazyServiceProvider
                .LazyGetRequiredService<MyERP.Core.DomainServices.InterCompanyTransactionService>();
            await interCompanyService.CreatePurchaseOrderFromSalesOrderAsync(
                order,
                async (type, companyId) => await _numberGenerator.GenerateAsync(type, companyId),
                order.TenantId);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Inter-company PO creation failed for SO {OrderId}", order.Id);
        }

        // Update linked Quotation status and item ordered quantities (PR #52822)
        if (order.QuotationId.HasValue)
        {
            var quotationRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Quotation, Guid>>();
            var quotation = await quotationRepo.FindAsync(order.QuotationId.Value);
            if (quotation != null)
            {
                quotation.ConvertedToSalesOrderId = order.Id;
                foreach (var soItem in order.Items)
                {
                    var qItem = soItem.QuotationItemId.HasValue
                        ? quotation.Items.FirstOrDefault(i => i.Id == soItem.QuotationItemId.Value)
                        : quotation.Items.FirstOrDefault(i => i.ItemId == soItem.ItemId);
                    if (qItem != null)
                    {
                        // Per ERPNext PR #58603 (commit c755e24731): tracks ordered qty in stock UOM
                        qItem.OrderedQty += soItem.StockQty;
                    }
                }
                await quotationRepo.UpdateAsync(quotation, autoSave: true);
            }
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

        if (!string.IsNullOrWhiteSpace(order.CouponCode))
        {
            var couponServiceCancel = LazyServiceProvider.LazyGetRequiredService<CouponCodeAppService>();
            await couponServiceCancel.ReverseUsageAsync(order.CouponCode);
        }

        // Release consumed Blanket Order allocations (reverse of submit)
        await ReleaseBlanketOrdersAsync(order);

        // Cancel any Stock Reservation Entries raised against this order (Bin release handled separately below)
        var cancelReservationManager = LazyServiceProvider.LazyGetRequiredService<Inventory.DomainServices.StockReservationManager>();
        await cancelReservationManager.CancelReservationsForOrderAsync(order.Id);

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

        // Reverse linked Quotation item ordered quantities (PR #52822)
        if (order.QuotationId.HasValue)
        {
            var quotationRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Quotation, Guid>>();
            var quotation = await quotationRepo.FindAsync(order.QuotationId.Value);
            if (quotation != null)
            {
                foreach (var soItem in order.Items)
                {
                    var qItem = soItem.QuotationItemId.HasValue
                        ? quotation.Items.FirstOrDefault(i => i.Id == soItem.QuotationItemId.Value)
                        : quotation.Items.FirstOrDefault(i => i.ItemId == soItem.ItemId);
                    if (qItem != null)
                    {
                        // Per ERPNext PR #58603 (commit c755e24731): tracks ordered qty in stock UOM
                        qItem.OrderedQty = Math.Max(0, qItem.OrderedQty - soItem.StockQty);
                    }
                }
                if (quotation.Items.All(i => i.OrderedQty <= 0))
                {
                    quotation.ConvertedToSalesOrderId = null;
                }
                await quotationRepo.UpdateAsync(quotation, autoSave: true);
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

        // Cancel any Stock Reservation Entries raised against this order (Bin release handled separately below)
        var closeReservationManager = LazyServiceProvider.LazyGetRequiredService<Inventory.DomainServices.StockReservationManager>();
        await closeReservationManager.CancelReservationsForOrderAsync(order.Id);

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
                        s.Status != Purchasing.SubcontractingInwardOrderStatus.Cancelled &&
                        s.Status != Purchasing.SubcontractingInwardOrderStatus.Closed)
            .ToList();
        foreach (var scio in linkedScioList)
        {
            scio.Close();
            await scioRepo.UpdateAsync(scio);
        }

        // Release Blanket Order allocation for undelivered quantities (PR #54593)
        await ReleaseBlanketOrdersOnCloseAsync(order);

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

        // Credit limit re-check on reopen — per ERPNext SO Status Service: the limit may have
        // been reduced, or other invoices consumed the available credit, since this order was
        // closed. Without this, reopening bypasses the same check SubmitAsync enforces.
        var reopenCreditLimitService = LazyServiceProvider.LazyGetRequiredService<CreditLimitService>();
        var reopenUserRoles = (CurrentUser.Roles ?? Array.Empty<string>()).ToArray();
        await reopenCreditLimitService.ValidateCreditLimitAsync(
            order.CustomerId, order.GrandTotal, order.CompanyId, reopenUserRoles, isAtSalesOrder: true);

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

        // Re-record Blanket Order allocation for pending delivery quantities (PR #54593)
        await ConsumeBlanketOrdersOnReopenAsync(order);

        await _repository.UpdateAsync(order, autoSave: true);
        var reopenDto = ObjectMapper.Map<SalesOrder, SalesOrderDto>(order);
        reopenDto.CustomerName = await ResolveCustomerNameAsync(order.CustomerId);
        return reopenDto;
    }

    [Authorize(MyERPPermissions.SalesOrders.Edit)]
    public async Task<SalesOrderDto> CloseItemAsync(Guid id, Guid itemId)
    {
        var order = await _repository.GetAsync(id);
        var item = order.Items.FirstOrDefault(i => i.Id == itemId);
        if (item == null)
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ItemNotFound);

        var pendingQty = item.PendingDeliveryQty;
        order.CloseItem(itemId);

        // Release reserved stock for closed item
        if (pendingQty > 0 && item.WarehouseId.HasValue && !item.DeliveredBySupplier)
        {
            var pendingStockQty = pendingQty * item.ConversionFactor;
            await _binService.UpdateReservedQtyAsync(
                item.ItemId, item.WarehouseId.Value, -pendingStockQty, order.TenantId);
        }

        // Release Blanket Order allocation for closed item (PR #54593)
        if (item.BlanketOrderId.HasValue && pendingQty > 0)
        {
            var pendingStockQty = pendingQty * item.ConversionFactor;
            var boRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<BlanketOrder, Guid>>();
            var bo = await boRepo.FindAsync(item.BlanketOrderId.Value);
            if (bo != null)
            {
                var boItem = bo.Items.FirstOrDefault(i => i.ItemId == item.ItemId);
                boItem?.UnrecordOrder(pendingStockQty);
                await boRepo.UpdateAsync(bo);
            }
        }

        await _repository.UpdateAsync(order, autoSave: true);
        var dto = ObjectMapper.Map<SalesOrder, SalesOrderDto>(order);
        dto.CustomerName = await ResolveCustomerNameAsync(order.CustomerId);
        return dto;
    }

    [Authorize(MyERPPermissions.SalesOrders.Edit)]
    public async Task<SalesOrderDto> ReopenItemAsync(Guid id, Guid itemId)
    {
        var order = await _repository.GetAsync(id);
        var item = order.Items.FirstOrDefault(i => i.Id == itemId);
        if (item == null)
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ItemNotFound);

        order.ReopenItem(itemId);

        // Re-reserve stock for reopened item
        if (item.PendingDeliveryQty > 0 && item.WarehouseId.HasValue && !item.DeliveredBySupplier)
        {
            var pendingStockQty = item.PendingDeliveryQty * item.ConversionFactor;
            await _binService.UpdateReservedQtyAsync(
                item.ItemId, item.WarehouseId.Value, pendingStockQty, order.TenantId);
        }

        // Re-record Blanket Order allocation for reopened item (PR #54593)
        if (item.BlanketOrderId.HasValue && item.PendingDeliveryQty > 0)
        {
            var pendingStockQty = item.PendingDeliveryQty * item.ConversionFactor;
            var allowancePct = await SettingProvider.GetAsync(
                MyERP.Settings.MyERPSettings.Selling.BlanketOrderAllowance, 0m);
            var boRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<BlanketOrder, Guid>>();
            var bo = await boRepo.FindAsync(item.BlanketOrderId.Value);
            if (bo != null)
            {
                var boItem = bo.Items.FirstOrDefault(i => i.ItemId == item.ItemId);
                boItem?.RecordOrder(pendingStockQty, allowancePct);
                await boRepo.UpdateAsync(bo);
            }
        }

        await _repository.UpdateAsync(order, autoSave: true);
        var dto = ObjectMapper.Map<SalesOrder, SalesOrderDto>(order);
        dto.CustomerName = await ResolveCustomerNameAsync(order.CustomerId);
        return dto;
    }

    [Authorize(MyERPPermissions.SalesOrders.Edit)]
    public async Task<SalesOrderDto> UpdateAsync(Guid id, CreateSalesOrderDto input)
    {
        var order = await _repository.GetAsync(id);
        if (order.Status != Core.DocumentStatus.Draft)
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("detail", "Only Draft sales orders can be edited");

        var updateItemIds = input.Items.Select(i => i.ItemId).ToArray();
        var updateCompanyRestriction = LazyServiceProvider.LazyGetRequiredService<Core.DomainServices.CompanyRestrictionValidationService>();
        await updateCompanyRestriction.ValidateTransactionCompanyAsync("SalesOrder", order.CompanyId, updateItemIds, customerIds: new[] { input.CustomerId });

        order.OrderDate = input.OrderDate;
        order.DeliveryDate = input.DeliveryDate;
        order.CustomerId = input.CustomerId;
        order.Notes = input.Notes;
        order.PriceListId = input.PriceListId;

        // Replace items
        order.ClearItems();
        var updateSkipDnForService = (await SettingProvider.GetOrNullAsync(MyERPSettings.Selling.SkipDeliveryNoteForServiceItems)) == "true";
        var updateUomService = LazyServiceProvider.LazyGetRequiredService<Inventory.DomainServices.UomConversionService>();
        var updateItemRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Inventory.Entities.Item, Guid>>();
        foreach (var item in input.Items)
        {
            order.AddItem(item.ItemId, item.Description, item.Quantity, item.UnitPrice, item.TaxAmount, item.Uom, item.DeliveryDate, item.QuotationItemId);
            var lastSoItem = order.Items[^1];
            if (item.WarehouseId.HasValue)
                lastSoItem.WarehouseId = item.WarehouseId;
            if (item.BlanketOrderId.HasValue)
                lastSoItem.BlanketOrderId = item.BlanketOrderId;

            var itemEntity = await updateItemRepo.FindAsync(item.ItemId);
            if (itemEntity != null)
            {
                lastSoItem.StockUom = itemEntity.Uom;
                if (!string.Equals(lastSoItem.Uom, itemEntity.Uom, StringComparison.OrdinalIgnoreCase))
                {
                    lastSoItem.ConversionFactor = await updateUomService.GetConversionFactorAsync(
                        lastSoItem.ItemId, lastSoItem.Uom, itemEntity.Uom);
                }
                if (updateSkipDnForService && !itemEntity.MaintainStock)
                {
                    lastSoItem.SkipDelivery = true;
                }
            }
        }

        order.ValidateDeliveryDates();

        await _repository.UpdateAsync(order, autoSave: true);
        var dto = ObjectMapper.Map<SalesOrder, SalesOrderDto>(order);
        dto.CustomerName = await ResolveCustomerNameAsync(order.CustomerId);
        return dto;
    }

    /// <summary>
    /// Deducts SO item qty from its linked Blanket Order's allocation (per line, via
    /// BlanketOrderItem.RecordOrder — validated against Qty × (1 + allowance%)).
    /// Allowance comes from MyERP.Selling.BlanketOrderAllowance (company-wide, matches
    /// the setting already surfaced in Selling Settings but never consumed until now).
    /// </summary>
    private async Task ConsumeBlanketOrdersAsync(SalesOrder order)
    {
        var linkedItems = order.Items.Where(i => i.BlanketOrderId.HasValue).ToList();
        if (linkedItems.Count == 0) return;

        var allowancePct = await SettingProvider.GetAsync(
            MyERP.Settings.MyERPSettings.Selling.BlanketOrderAllowance, 0m);
        var boRepository = LazyServiceProvider.LazyGetRequiredService<IRepository<BlanketOrder, Guid>>();

        foreach (var group in linkedItems.GroupBy(i => i.BlanketOrderId!.Value))
        {
            var bo = await boRepository.FindAsync(group.Key);
            if (bo == null) continue;
            foreach (var item in group)
            {
                var boItem = bo.Items.FirstOrDefault(i => i.ItemId == item.ItemId);
                boItem?.RecordOrder(item.StockQty, allowancePct);
            }
            await boRepository.UpdateAsync(bo);
        }
    }

    /// <summary>Reverses ConsumeBlanketOrdersAsync's deduction (called from CancelAsync).</summary>
    private async Task ReleaseBlanketOrdersAsync(SalesOrder order)
    {
        var linkedItems = order.Items.Where(i => i.BlanketOrderId.HasValue).ToList();
        if (linkedItems.Count == 0) return;

        var boRepository = LazyServiceProvider.LazyGetRequiredService<IRepository<BlanketOrder, Guid>>();

        foreach (var group in linkedItems.GroupBy(i => i.BlanketOrderId!.Value))
        {
            var bo = await boRepository.FindAsync(group.Key);
            if (bo == null) continue;
            foreach (var item in group)
            {
                var boItem = bo.Items.FirstOrDefault(i => i.ItemId == item.ItemId);
                boItem?.UnrecordOrder(item.StockQty);
            }
            await boRepository.UpdateAsync(bo);
        }
    }

    /// <summary>Releases Blanket Order allocation for undelivered quantities when an order is closed (PR #54593).</summary>
    private async Task ReleaseBlanketOrdersOnCloseAsync(SalesOrder order)
    {
        var linkedItems = order.Items.Where(i => i.BlanketOrderId.HasValue && i.PendingDeliveryQty > 0).ToList();
        if (linkedItems.Count == 0) return;

        var boRepository = LazyServiceProvider.LazyGetRequiredService<IRepository<BlanketOrder, Guid>>();
        foreach (var group in linkedItems.GroupBy(i => i.BlanketOrderId!.Value))
        {
            var bo = await boRepository.FindAsync(group.Key);
            if (bo == null) continue;
            foreach (var item in group)
            {
                var pendingStockQty = item.PendingDeliveryQty * item.ConversionFactor;
                var boItem = bo.Items.FirstOrDefault(i => i.ItemId == item.ItemId);
                boItem?.UnrecordOrder(pendingStockQty);
            }
            await boRepository.UpdateAsync(bo);
        }
    }

    /// <summary>Re-records Blanket Order allocation for pending delivery quantities when an order is reopened (PR #54593).</summary>
    private async Task ConsumeBlanketOrdersOnReopenAsync(SalesOrder order)
    {
        var linkedItems = order.Items.Where(i => i.BlanketOrderId.HasValue && i.PendingDeliveryQty > 0).ToList();
        if (linkedItems.Count == 0) return;

        var allowancePct = await SettingProvider.GetAsync(
            MyERP.Settings.MyERPSettings.Selling.BlanketOrderAllowance, 0m);
        var boRepository = LazyServiceProvider.LazyGetRequiredService<IRepository<BlanketOrder, Guid>>();
        foreach (var group in linkedItems.GroupBy(i => i.BlanketOrderId!.Value))
        {
            var bo = await boRepository.FindAsync(group.Key);
            if (bo == null) continue;
            foreach (var item in group)
            {
                var pendingStockQty = item.PendingDeliveryQty * item.ConversionFactor;
                var boItem = bo.Items.FirstOrDefault(i => i.ItemId == item.ItemId);
                boItem?.RecordOrder(pendingStockQty, allowancePct);
            }
            await boRepository.UpdateAsync(bo);
        }
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

    /// <summary>
    /// Returns all payment entries linked to this sales order (advance payments).
    /// Per ERPNext: PE with AgainstOrderType=SalesOrder and matching AgainstOrderId.
    /// </summary>
    public async Task<List<OrderPaymentDto>> GetOrderPaymentsAsync(Guid orderId)
    {
        var peRepo = LazyServiceProvider
            .LazyGetRequiredService<IRepository<Accounting.Entities.PaymentEntry, Guid>>();
        var queryable = await peRepo.GetQueryableAsync();
        var payments = queryable
            .Where(pe => pe.AgainstOrderType == "SalesOrder" && pe.AgainstOrderId == orderId)
            .OrderByDescending(pe => pe.PostingDate)
            .ToList();

        return payments.Select(pe => new OrderPaymentDto
        {
            PaymentEntryId = pe.Id,
            PaymentNumber = pe.PaymentNumber ?? pe.Id.ToString()[..8],
            PostingDate = pe.PostingDate,
            PaidAmount = pe.PaidAmount,
            PaymentType = pe.PaymentType.ToString(),
            ReferenceNumber = pe.ReferenceNumber,
            Status = pe.Status.ToString(),
        }).ToList();
    }

    /// <summary>
    /// Updates qty/rate on submitted SO items (post-submit editing per ERPNext update_child_qty_rate).
    /// Guards: qty cannot go below DeliveredQty, rate cannot go below billed amount per unit.
    /// Adjusts Bin.ReservedQty for qty changes in stock UOM.
    /// </summary>
    [Authorize(MyERPPermissions.SalesOrders.Edit)]
    public async Task<UpdateOrderItemsResultDto> UpdateItemsAsync(Guid id, UpdateOrderItemsDto input)
    {
        var so = await _repository.GetAsync(id);

        if (so.Status == Core.DocumentStatus.Draft || so.Status == Core.DocumentStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("detail", "Only submitted orders can have items updated. Use Edit for draft orders.");

        var previousGrandTotal = so.GrandTotal;
        var warnings = new List<string>();
        var updatedCount = 0;

        foreach (var removeId in input.RemovedItemIds)
        {
            var soItemToRemove = so.Items.FirstOrDefault(i => i.Id == removeId);
            if (soItemToRemove == null)
            {
                warnings.Add($"Item row {removeId} not found on this order — skipped.");
                continue;
            }

            _childItemUpdateService.ValidateSalesOrderItemDeletion(soItemToRemove);

            if (soItemToRemove.WarehouseId.HasValue && !soItemToRemove.DeliveredBySupplier)
            {
                await _binService.UpdateReservedQtyAsync(
                    soItemToRemove.ItemId, soItemToRemove.WarehouseId.Value, -soItemToRemove.StockQty, so.TenantId);
            }

            so.RemoveItem(removeId);
        }

        foreach (var update in input.Items)
        {
            var soItem = so.Items.FirstOrDefault(i => i.Id == update.ItemId);
            if (soItem == null)
            {
                warnings.Add($"Item {update.ItemId} not found on this order — skipped.");
                continue;
            }

            _childItemUpdateService.ValidateSalesOrderItemUpdate(soItem, update.Quantity, update.UnitPrice);

            var newConversionFactor = update.ConversionFactor.HasValue && update.ConversionFactor.Value > 0
                ? update.ConversionFactor.Value
                : soItem.ConversionFactor;

            // Guard: cannot reduce qty below already delivered in stock UOM (per ERPNext PR #58603)
            var newStockQty = update.Quantity * newConversionFactor;
            var deliveredStockQty = soItem.DeliveredQty * soItem.ConversionFactor;
            if (newStockQty < deliveredStockQty)
                throw new BusinessException("MyERP:03024")
                    .WithData("itemId", soItem.ItemId)
                    .WithData("deliveredQty", soItem.DeliveredQty)
                    .WithData("requestedQty", update.Quantity)
                    .WithData("detail", "Cannot set quantity less than delivered quantity");

            // Guard: cannot reduce rate below billed amount per unit
            if (soItem.BilledQty > 0 && update.UnitPrice < soItem.UnitPrice)
            {
                var minRate = soItem.BilledQty > 0 ? (soItem.BilledQty * soItem.UnitPrice) / soItem.BilledQty : 0;
                if (update.UnitPrice < minRate && update.UnitPrice != 0)
                    throw new BusinessException("MyERP:03025")
                        .WithData("itemId", soItem.ItemId)
                        .WithData("billedRate", minRate)
                        .WithData("requestedRate", update.UnitPrice);
            }

            var oldStockQty = soItem.StockQty;

            soItem.Quantity = update.Quantity;
            soItem.ConversionFactor = newConversionFactor;
            soItem.UnitPrice = update.UnitPrice;
            if (update.DeliveryDate.HasValue)
                soItem.DeliveryDate = update.DeliveryDate;
            if (update.WarehouseId.HasValue)
                soItem.WarehouseId = update.WarehouseId;

            // Adjust Bin.ReservedQty for qty changes (delta in stock UOM)
            var qtyDelta = soItem.StockQty - oldStockQty;
            if (qtyDelta != 0 && soItem.WarehouseId.HasValue && !soItem.DeliveredBySupplier)
            {
                await _binService.UpdateReservedQtyAsync(
                    soItem.ItemId, soItem.WarehouseId.Value, qtyDelta, so.TenantId);
            }

            updatedCount++;
        }

        so.RecalculateTotals();
        so.UpdateFulfillmentStatus();
        await _repository.UpdateAsync(so, autoSave: true);

        var activityLogRepo = LazyServiceProvider
            .LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "SalesOrder", so.Id, "ItemsUpdated",
            so.CompanyId, so.OrderNumber, so.Status.ToString(), so.Status.ToString(),
            CurrentUser.Id, $"Updated {updatedCount} items, removed {input.RemovedItemIds.Count}. Grand total: {previousGrandTotal} → {so.GrandTotal}",
            so.TenantId));

        return new UpdateOrderItemsResultDto
        {
            ItemsUpdated = updatedCount,
            NewGrandTotal = so.GrandTotal,
            PreviousGrandTotal = previousGrandTotal,
            Warnings = warnings,
        };
    }

    public async Task<SalesOrderTrackingBoardDto> GetTrackingBoardAsync(Guid companyId)
    {
        var queryable = await _repository.GetQueryableAsync();
        var orders = await AsyncExecuter.ToListAsync(
            queryable
                .Where(o => o.CompanyId == companyId
                    && o.Status != Core.DocumentStatus.Draft
                    && o.Status != Core.DocumentStatus.Cancelled)
                .OrderByDescending(o => o.OrderDate)
                .Take(200));

        var customerIds = orders.Select(o => o.CustomerId).Distinct().ToList();
        var customerRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Customer, Guid>>();
        var customerQueryable = await customerRepo.GetQueryableAsync();
        var customerNames = (await AsyncExecuter.ToListAsync(
            customerQueryable.Where(c => customerIds.Contains(c.Id))
                .Select(c => new { c.Id, c.Name })))
            .ToDictionary(c => c.Id, c => c.Name);

        var result = new SalesOrderTrackingBoardDto();

        foreach (var order in orders)
        {
            var perDelivered = order.Items.Count > 0
                ? order.Items.Min(i => i.Quantity > 0 ? (i.DeliveredQty / i.Quantity) * 100m : 100m)
                : 0m;
            var perBilled = order.Items.Count > 0
                ? order.Items.Min(i => i.Quantity > 0 ? (i.BilledQty / i.Quantity) * 100m : 100m)
                : 0m;

            var stage = perDelivered >= 100m && perBilled >= 100m ? TrackingBoardStage.Completed
                : perDelivered >= 100m ? TrackingBoardStage.FullyDelivered
                : perDelivered > 0m ? TrackingBoardStage.PartiallyDelivered
                : TrackingBoardStage.Ordered;

            var effectiveDate = order.DeliveryDate ?? order.OrderDate.AddDays(14);
            var isOverdue = stage != TrackingBoardStage.Completed
                && stage != TrackingBoardStage.FullyDelivered
                && effectiveDate < DateTime.UtcNow.Date;
            var daysOverdue = isOverdue ? (int)(DateTime.UtcNow.Date - effectiveDate).TotalDays : 0;

            var card = new SalesOrderTrackingBoardCardDto
            {
                OrderId = order.Id,
                OrderNumber = order.OrderNumber ?? order.Id.ToString()[..8],
                CustomerName = customerNames.GetValueOrDefault(order.CustomerId) ?? "—",
                GrandTotal = order.GrandTotal,
                ItemCount = order.Items.Count,
                PerDelivered = Math.Round(perDelivered, 1),
                PerBilled = Math.Round(perBilled, 1),
                Stage = stage,
                OrderDate = order.OrderDate,
                ExpectedDeliveryDate = effectiveDate,
                IsOverdue = isOverdue,
                DaysOverdue = daysOverdue,
            };

            switch (stage)
            {
                case TrackingBoardStage.Ordered:
                    result.Ordered.Add(card);
                    break;
                case TrackingBoardStage.PartiallyDelivered:
                    result.PartiallyDelivered.Add(card);
                    break;
                case TrackingBoardStage.FullyDelivered:
                    result.FullyDelivered.Add(card);
                    break;
                case TrackingBoardStage.Completed:
                    result.Completed.Add(card);
                    break;
            }

            if (isOverdue) result.OverdueCount++;
            result.TotalValue += order.GrandTotal;
        }

        result.TotalOrders = orders.Count;
        return result;
    }
}

public class SalesOrderTrackingBoardDto
{
    public List<SalesOrderTrackingBoardCardDto> Ordered { get; set; } = new();
    public List<SalesOrderTrackingBoardCardDto> PartiallyDelivered { get; set; } = new();
    public List<SalesOrderTrackingBoardCardDto> FullyDelivered { get; set; } = new();
    public List<SalesOrderTrackingBoardCardDto> Completed { get; set; } = new();
    public int TotalOrders { get; set; }
    public int OverdueCount { get; set; }
    public decimal TotalValue { get; set; }
}

public class SalesOrderTrackingBoardCardDto
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal GrandTotal { get; set; }
    public int ItemCount { get; set; }
    public decimal PerDelivered { get; set; }
    public decimal PerBilled { get; set; }
    public TrackingBoardStage Stage { get; set; }
    public DateTime OrderDate { get; set; }
    public DateTime ExpectedDeliveryDate { get; set; }
    public bool IsOverdue { get; set; }
    public int DaysOverdue { get; set; }
}

public enum TrackingBoardStage
{
    Ordered = 0,
    PartiallyDelivered = 1,
    FullyDelivered = 2,
    Completed = 3,
}
