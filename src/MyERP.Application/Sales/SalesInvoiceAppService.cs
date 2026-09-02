using System;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using MyERP.Inventory.DomainServices;
using MyERP.Permissions;
using MyERP.Sales.DomainServices;using MyERP.Sales.Entities;
using MyERP.Shared;
using MyERP.Tax.DomainServices;
using MyERP.Tax.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Settings;

namespace MyERP.Sales;

[Authorize(MyERPPermissions.SalesInvoices.Default)]
public class SalesInvoiceAppService : ApplicationService, ISalesInvoiceAppService
{
    private readonly IRepository<SalesInvoice, Guid> _repository;
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IRepository<SalesOrder, Guid> _salesOrderRepository;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<TransactionTaxRow, Guid> _taxRowRepository;
    private readonly IRepository<PaymentTermsTemplate, Guid> _paymentTermsRepository;
    private readonly IRepository<PaymentScheduleEntry, Guid> _paymentScheduleRepository;
    private readonly IDocumentNumberGenerator _numberGenerator;
    private readonly DocumentPostingOrchestrator _postingOrchestrator;
    private readonly TaxesAndTotalsService _taxService;
    private readonly CreditLimitService _creditLimitService;
    private readonly CurrencyExchangeService _exchangeService;
    private readonly PricingRuleApplicationService _pricingRuleService;
    private readonly StockValuationService _valuationService;
    private readonly BinService _binService;
    private readonly DocumentActivityLogService _activityLog;
    private readonly ItemTransactionValidationService _itemValidation;
    private readonly SalesInvoiceManager _invoiceManager;

    public SalesInvoiceAppService(
        IRepository<SalesInvoice, Guid> repository,
        IRepository<Customer, Guid> customerRepository,
        IRepository<SalesOrder, Guid> salesOrderRepository,
        IRepository<Company, Guid> companyRepository,
        IRepository<TransactionTaxRow, Guid> taxRowRepository,
        IRepository<PaymentTermsTemplate, Guid> paymentTermsRepository,
        IRepository<PaymentScheduleEntry, Guid> paymentScheduleRepository,
        IDocumentNumberGenerator numberGenerator,
        DocumentPostingOrchestrator postingOrchestrator,
        TaxesAndTotalsService taxService,
        CreditLimitService creditLimitService,
        CurrencyExchangeService exchangeService,
        PricingRuleApplicationService pricingRuleService,
        StockValuationService valuationService,
        BinService binService,
        DocumentActivityLogService activityLog,
        ItemTransactionValidationService itemValidation,
        SalesInvoiceManager invoiceManager)
    {
        _repository = repository;
        _customerRepository = customerRepository;
        _salesOrderRepository = salesOrderRepository;
        _companyRepository = companyRepository;
        _taxRowRepository = taxRowRepository;
        _paymentTermsRepository = paymentTermsRepository;
        _paymentScheduleRepository = paymentScheduleRepository;
        _numberGenerator = numberGenerator;
        _postingOrchestrator = postingOrchestrator;
        _taxService = taxService;
        _creditLimitService = creditLimitService;
        _exchangeService = exchangeService;
        _pricingRuleService = pricingRuleService;
        _valuationService = valuationService;
        _binService = binService;
        _activityLog = activityLog;
        _itemValidation = itemValidation;
        _invoiceManager = invoiceManager;
    }

    public async Task<SalesInvoiceDto> GetAsync(Guid id)
    {
        var invoice = await _repository.GetAsync(id);
        var dto = ObjectMapper.Map<SalesInvoice, SalesInvoiceDto>(invoice);

        // Resolve customer name
        var customerRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.Customer, Guid>>();
        var customer = await customerRepo.FindAsync(invoice.CustomerId);
        if (customer != null) dto.CustomerName = customer.Name;

        if (invoice.InterCompanyPurchaseInvoiceId.HasValue)
        {
            var piRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Purchasing.Entities.PurchaseInvoice, Guid>>();
            var pi = await piRepo.FindAsync(invoice.InterCompanyPurchaseInvoiceId.Value);
            if (pi != null)
            {
                dto.InterCompanyPurchaseInvoiceNumber = pi.InvoiceNumber;
                var companyRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.Company, Guid>>();
                var company = await companyRepo.FindAsync(pi.CompanyId);
                dto.InterCompanyCompanyName = company?.Name;
            }
        }

        await AttachSalesTeamAsync(dto);

        return dto;
    }

    /// <summary>
    /// Per ERPNext selling_controller.calculate_contribution(): allocated_amount is a percentage
    /// split of the amount eligible for commission (filtered by Item.GrantCommission — gotcha #6156),
    /// and incentives = allocated_amount × the row's commission rate (falling back to the Sales Person's own rate).
    /// </summary>
    private async Task CreateSalesTeamEntriesAsync(SalesInvoice invoice, List<SalesTeamAllocationInputDto> salesTeam)
    {
        var totalPercentage = salesTeam.Sum(s => s.AllocatedPercentage);
        if (Math.Round(totalPercentage, 2) != 100m)
            throw new BusinessException(MyERPDomainErrorCodes.SalesTeamPercentageMustTotal100)
                .WithData("total", Math.Round(totalPercentage, 2));

        var spRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<SalesPerson, Guid>>();
        var teamRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<SalesTeamEntry, Guid>>();
        var itemRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Inventory.Entities.Item, Guid>>();

        // Filter commission-eligible items (per ERPNext grant_commission flag, gotcha #6156)
        var itemIds = invoice.Items.Select(i => i.ItemId).Distinct().ToList();
        var itemEntities = await itemRepo.GetListAsync(i => itemIds.Contains(i.Id));
        var itemGrantMap = itemEntities.ToDictionary(i => i.Id, i => i.GrantCommission);

        var eligibleAmount = invoice.Items
            .Where(i => !itemGrantMap.TryGetValue(i.ItemId, out var grant) || grant)
            .Sum(i => i.LineTotal);

        foreach (var row in salesTeam)
        {
            var commissionRate = row.CommissionRate;
            if (!commissionRate.HasValue)
            {
                var salesPerson = await spRepo.FindAsync(row.SalesPersonId);
                commissionRate = salesPerson?.CommissionRate ?? 0m;
            }

            var entry = new SalesTeamEntry(
                GuidGenerator.Create(), row.SalesPersonId, "SalesInvoice", invoice.Id,
                row.AllocatedPercentage, eligibleAmount, commissionRate.Value);
            await teamRepo.InsertAsync(entry);
        }
    }

    private async Task AttachSalesTeamAsync(SalesInvoiceDto dto)
    {
        var teamRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<SalesTeamEntry, Guid>>();
        var teamQuery = await teamRepo.GetQueryableAsync();
        var entries = teamQuery.Where(e => e.ParentType == "SalesInvoice" && e.ParentId == dto.Id).ToList();
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
        dto.TotalCommission = entries.Sum(e => e.Incentives);
    }

    public async Task<List<PaymentScheduleDto>> GetPaymentScheduleAsync(Guid invoiceId)
    {
        var query = await _paymentScheduleRepository.GetQueryableAsync();
        return query
            .Where(e => e.ParentId == invoiceId && e.ParentType == "SalesInvoice")
            .OrderBy(e => e.DueDate)
            .Select(ObjectMapper.Map<Accounting.Entities.PaymentScheduleEntry, PaymentScheduleDto>).ToList();
    }

    public async Task<List<InvoicePaymentHistoryDto>> GetPaymentHistoryAsync(Guid invoiceId)
    {
        var peRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Accounting.Entities.PaymentEntry, Guid>>();
        var query = await peRepo.GetQueryableAsync();

        // Find payments allocated against this invoice (legacy single-invoice path)
        var payments = query
            .Where(pe => pe.AgainstInvoiceId == invoiceId && pe.AgainstInvoiceType == "SalesInvoice"
                && pe.Status != Core.DocumentStatus.Draft && pe.Status != Core.DocumentStatus.Cancelled)
            .OrderByDescending(pe => pe.PostingDate)
            .Select(pe => new InvoicePaymentHistoryDto
            {
                Id = pe.Id,
                PaymentNumber = pe.PaymentNumber,
                PostingDate = pe.PostingDate,
                PaymentType = pe.PaymentType.ToString(),
                Amount = pe.PaidAmount
            })
            .ToList();

        // Also find multi-reference payments
        var refRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Accounting.Entities.PaymentEntryReference, Guid>>();
        var refQuery = await refRepo.GetQueryableAsync();
        var multiRefPaymentIds = refQuery
            .Where(r => r.ReferenceId == invoiceId && r.ReferenceType == "SalesInvoice")
            .Select(r => new { r.PaymentEntryId, r.AllocatedAmount })
            .ToList();

        if (multiRefPaymentIds.Any())
        {
            var ids = multiRefPaymentIds.Select(r => r.PaymentEntryId).ToList();
            var multiPayments = query
                .Where(pe => ids.Contains(pe.Id)
                    && pe.Status != Core.DocumentStatus.Draft && pe.Status != Core.DocumentStatus.Cancelled)
                .Select(pe => new { pe.Id, pe.PaymentNumber, pe.PostingDate, pe.PaymentType })
                .ToList();

            var refDict = multiRefPaymentIds.ToDictionary(r => r.PaymentEntryId, r => r.AllocatedAmount);
            foreach (var mp in multiPayments)
            {
                if (!payments.Any(p => p.Id == mp.Id))
                {
                    payments.Add(new InvoicePaymentHistoryDto
                    {
                        Id = mp.Id,
                        PaymentNumber = mp.PaymentNumber,
                        PostingDate = mp.PostingDate,
                        PaymentType = mp.PaymentType.ToString(),
                        Amount = refDict.GetValueOrDefault(mp.Id)
                    });
                }
            }
        }

        return payments.OrderByDescending(p => p.PostingDate).ToList();
    }

    public async Task<PagedResultDto<SalesInvoiceDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();

        if (input.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter; query = query.Where(x => x.InvoiceNumber.Contains(filter));
        }

        if (!string.IsNullOrWhiteSpace(input.Status) && Enum.TryParse<Core.DocumentStatus>(input.Status, true, out var status))
            query = query.Where(x => x.Status == status);

        if (input.FromDate.HasValue)
            query = query.Where(x => x.IssueDate >= input.FromDate.Value);

        if (input.ToDate.HasValue)
            query = query.Where(x => x.IssueDate <= input.ToDate.Value);

        var totalCount = query.Count();
        var sorted = SortingHelper.ApplySorting(query, input.Sorting,
            q => q.OrderByDescending(x => x.IssueDate),
            ("invoiceNumber", x => x.InvoiceNumber),
            ("issueDate", x => x.IssueDate),
            ("grandTotal", x => x.GrandTotal),
            ("status", x => x.Status));
        var invoices = sorted
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        var dtos = invoices.Select(ObjectMapper.Map<SalesInvoice, SalesInvoiceDto>).ToList();

        // Batch-resolve customer names (avoid N+1)
        var customerIds = invoices.Select(i => i.CustomerId).Distinct().ToList();
        if (customerIds.Count > 0)
        {
            var customerQuery = await _customerRepository.GetQueryableAsync();
            var customerNames = customerQuery
                .Where(c => customerIds.Contains(c.Id))
                .Select(c => new { c.Id, c.Name })
                .ToDictionary(c => c.Id, c => c.Name);

            foreach (var dto in dtos)
            {
                if (customerNames.TryGetValue(dto.CustomerId, out var name))
                    dto.CustomerName = name;
            }
        }

        // Calculate overdue indicators per invoice
        var today = DateTime.UtcNow.Date;
        foreach (var dto in dtos)
        {
            if (dto.DueDate.HasValue && dto.OutstandingAmount > 0.01m
                && dto.Status == "Posted" && !dto.IsReturn
                && dto.DueDate.Value.Date < today)
            {
                dto.DaysOverdue = (int)(today - dto.DueDate.Value.Date).TotalDays;
                dto.IsOverdue = true;
            }
        }

        return new PagedResultDto<SalesInvoiceDto>(totalCount, dtos);
    }

    /// <summary>
    /// Returns aggregate KPI summary: outstanding, overdue, monthly revenue.
    /// Uses IQueryable for server-side aggregation (no full entity materialization).
    /// </summary>
    public async Task<SalesInvoiceListSummaryDto> GetListSummaryAsync(Guid? companyId)
    {
        var queryable = await _repository.GetQueryableAsync();
        var posted = queryable.Where(i => i.Status == DocumentStatus.Posted && !i.IsReturn);

        if (companyId.HasValue)
            posted = posted.Where(i => i.CompanyId == companyId.Value);

        var today = DateTime.UtcNow.Date;
        var monthStart = new DateTime(today.Year, today.Month, 1);

        // Total outstanding (server-side SUM)
        var totalOutstanding = posted
            .Select(i => i.GrandTotal - i.AmountPaid - i.WriteOffAmount - i.TotalAdvance)
            .Where(o => o > 0)
            .Sum();

        // Overdue: past due date with outstanding > 0
        var overdueInvoices = posted
            .Where(i => i.DueDate != null && i.DueDate < today)
            .Where(i => (i.GrandTotal - i.AmountPaid - i.WriteOffAmount - i.TotalAdvance) > 0.01m);
        var overdueCount = overdueInvoices.Count();
        var overdueAmount = overdueInvoices
            .Select(i => i.GrandTotal - i.AmountPaid - i.WriteOffAmount - i.TotalAdvance)
            .Sum();

        // Monthly revenue (invoices posted this calendar month)
        var monthlyInvoices = posted.Where(i => i.IssueDate >= monthStart);
        var monthlyRevenue = monthlyInvoices.Sum(i => i.GrandTotal);
        var monthlyCount = monthlyInvoices.Count();

        // Total posted count
        var postedCount = posted.Count();

        return new SalesInvoiceListSummaryDto
        {
            TotalOutstanding = Math.Max(0, totalOutstanding),
            OverdueCount = overdueCount,
            OverdueAmount = Math.Max(0, overdueAmount),
            MonthlyRevenue = monthlyRevenue,
            MonthlyInvoiceCount = monthlyCount,
            PostedInvoiceCount = postedCount,
        };
    }

    [Authorize(MyERPPermissions.SalesInvoices.Create)]
    public async Task<SalesInvoiceDto> CreateAsync(CreateSalesInvoiceDto input)
    {
        // Input validation
        Check.NotDefaultOrNull<Guid>(input.CompanyId, nameof(input.CompanyId));
        Check.NotDefaultOrNull<Guid>(input.CustomerId, nameof(input.CustomerId));
        if (input.IssueDate == default)
            input.IssueDate = DateTime.UtcNow.Date;
        if (input.Items == null || input.Items.Count == 0)
            throw new Volo.Abp.BusinessException("MyERP:01007")
                .WithData("documentType", "Sales Invoice");

        // Validate all items are active
        var siItemIds = input.Items.Select(i => i.ItemId).ToArray();
        await _itemValidation.ValidateItemsForTransactionAsync(siItemIds);

        var companyRestriction = LazyServiceProvider.LazyGetRequiredService<Core.DomainServices.CompanyRestrictionValidationService>();
        await companyRestriction.ValidateTransactionCompanyAsync("SalesInvoice", input.CompanyId, siItemIds, customerIds: new[] { input.CustomerId });

        var customerForStatus = await _customerRepository.GetAsync(input.CustomerId);
        LazyServiceProvider.LazyGetRequiredService<Core.DomainServices.PartyValidationService>()
            .ValidatePartyStatus("Customer", isFrozen: false, isDisabled: !customerForStatus.IsActive, customerForStatus.Name);

        var invoiceNumber = await _numberGenerator.GenerateAsync("SalesInvoice", input.CompanyId);

        var invoice = new SalesInvoice(
            GuidGenerator.Create(),
            input.CompanyId,
            input.CustomerId,
            invoiceNumber,
            input.IssueDate);

        invoice.DueDate = input.DueDate;
        invoice.CurrencyCode = input.CurrencyCode;
        invoice.ContactPersonId = input.ContactPersonId;
        invoice.ShippingContactPersonId = input.ShippingContactPersonId;

        // Per ERPNext: Price List defaults from the customer's own default when not given explicitly.
        invoice.PriceListId = input.PriceListId
            ?? (await _customerRepository.FindAsync(input.CustomerId))?.DefaultPriceListId;
        invoice.IsReturn = input.IsReturn;
        invoice.ReturnAgainstId = input.ReturnAgainstId;
        invoice.IsOpening = input.IsOpening;
        invoice.UpdateStock = input.UpdateStock;
        // Skip stock update for items already delivered via Delivery Note to prevent double deduction (PR #55311)
        if (input.Items.Any(i => i.DeliveryNoteItemId.HasValue))
        {
            invoice.UpdateStock = false;
        }
        invoice.WarehouseId = input.WarehouseId;
        invoice.CostCenterId = input.CostCenterId;
        invoice.ProjectId = input.ProjectId;

        // Per gotcha #468: project-customer cross-validation
        if (input.ProjectId.HasValue)
        {
            var projectRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Projects.Entities.Project, Guid>>();
            var project = await projectRepo.FindAsync(input.ProjectId.Value);
            if (project != null && project.CustomerId.HasValue && project.CustomerId.Value != input.CustomerId)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ProjectCustomerMismatch)
                    .WithData("projectName", project.ProjectName);
            }
        }

        // Set party account (debit_to):
        // Returns: inherit from original invoice (ensures account match validation works)
        // Normal: company default receivable account
        var companyForAcct = await _companyRepository.GetAsync(input.CompanyId);
        if (input.IsReturn && input.ReturnAgainstId.HasValue)
        {
            var originalInvoice = await _repository.GetAsync(input.ReturnAgainstId.Value);
            invoice.DebitToAccountId = originalInvoice.DebitToAccountId;
        }
        else if (companyForAcct.DefaultReceivableAccountId.HasValue)
        {
            invoice.DebitToAccountId = companyForAcct.DefaultReceivableAccountId.Value;
        }

        // Opening invoices: clear payment terms (accounting-only, no schedule needed)
        // Per DO-NOT: "Skip Payment Schedule opening invoice exclusion (is_opening=Yes must clear)"
        if (invoice.IsOpening)
        {
            invoice.PaymentTermsTemplateId = null;
        }

        // Payment terms resolution: explicit → customer default → null (skip for opening invoices)
        if (!invoice.IsOpening && input.PaymentTermsTemplateId.HasValue)
        {
            invoice.PaymentTermsTemplateId = input.PaymentTermsTemplateId;
        }
        else if (!invoice.IsOpening)
        {
            var customerRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.Customer, Guid>>();
            var customer = await customerRepo.FindAsync(input.CustomerId);
            if (customer?.DefaultPaymentTermsTemplateId.HasValue == true)
            {
                invoice.PaymentTermsTemplateId = customer.DefaultPaymentTermsTemplateId;
            }
        }

        // Auto-fill addresses from customer
        var partyDefaults = LazyServiceProvider.LazyGetRequiredService<Core.DomainServices.PartyDefaultsService>();
        var billingAddr = await partyDefaults.GetPrimaryAddressAsync("Customer", input.CustomerId);
        if (billingAddr != null) invoice.BillingAddressId = billingAddr.Id;
        var shippingAddr = await partyDefaults.GetShippingAddressAsync("Customer", input.CustomerId);
        if (shippingAddr != null) invoice.ShippingAddressId = shippingAddr.Id;

        // Auto-resolve exchange rate for multi-currency invoices
        if (!string.IsNullOrEmpty(input.CurrencyCode))
        {
            var company = await _companyRepository.GetAsync(input.CompanyId);
            if (input.CurrencyCode != company.CurrencyCode)
            {
                invoice.ExchangeRate = await _exchangeService.GetExchangeRateAsync(
                    input.CurrencyCode, company.CurrencyCode, input.IssueDate);
                await EnsureExchangeRateNotStaleAsync(input.CurrencyCode, company.CurrencyCode);
            }
        }

        foreach (var item in input.Items)
        {
            invoice.AddItem(item.ItemId, item.Description, item.Quantity, item.UnitPrice, item.TaxAmount, item.Uom);
            var added = invoice.Items.Last();
            added.SalesOrderItemId = item.SalesOrderItemId;
            added.DeliveryNoteItemId = item.DeliveryNoteItemId;
            added.IsFixedAsset = item.IsFixedAsset;
            added.AssetId = item.AssetId;
            if (item.EnableDeferredRevenue)
            {
                added.EnableDeferredRevenue = true;
                added.DeferredRevenueAccountId = item.DeferredRevenueAccountId;
                added.ServiceStartDate = item.ServiceStartDate;
                added.ServiceEndDate = item.ServiceEndDate;
                added.ServiceStopDate = item.ServiceStopDate;
            }
        }

        // Resolve UOM conversion factors for direct SI creation (when UpdateStock=true, stock needs StockQty)
        var uomSvc = LazyServiceProvider.LazyGetRequiredService<Inventory.DomainServices.UomConversionService>();
        var itemRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Inventory.Entities.Item, Guid>>();
        foreach (var siItem in invoice.Items)
        {
            var itemEntity = await itemRepo.FindAsync(siItem.ItemId);
            if (itemEntity != null)
            {
                siItem.StockUom = itemEntity.Uom ?? "Unit";
                if (!string.IsNullOrEmpty(siItem.Uom) && siItem.Uom != siItem.StockUom)
                {
                    siItem.ConversionFactor = await uomSvc.GetConversionFactorAsync(
                        siItem.ItemId, siItem.Uom, siItem.StockUom);
                }
            }
        }

        // Accounting dimensions
        invoice.CostCenterId = input.CostCenterId;
        invoice.ProjectId = input.ProjectId;

        // Timesheet-in-SI auto-fetch: when project is set, populate unbilled timesheet entries
        // Per ERPNext Projects Settings.fetch_timesheet_in_sales_invoice
        if (input.ProjectId.HasValue && !invoice.IsReturn)
        {
            var tsRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Projects.Entities.Timesheet, Guid>>();
            var tsQuery = await tsRepo.GetQueryableAsync();
            var timesheets = tsQuery
                .Where(t => t.CompanyId == input.CompanyId
                    && t.Status == MyERP.Projects.TimesheetStatus.Submitted)
                .ToList();

            var unbilledDetails = timesheets
                .SelectMany(ts => ts.Details.Where(d =>
                    d.IsBillable && d.SalesInvoiceId == null && d.BillingAmount > 0
                    && d.ProjectId == input.ProjectId))
                .ToList();

            if (unbilledDetails.Any())
            {
                foreach (var detail in unbilledDetails)
                {
                    invoice.AddItem(
                        detail.Id, // use detail ID for traceability
                        $"Timesheet: {detail.ActivityType} - {detail.Hours:F1}h",
                        detail.Hours,
                        detail.BillingRate,
                        0,
                        "Hour");
                }

                // Mark details as billed
                foreach (var detail in unbilledDetails)
                {
                    detail.SalesInvoiceId = invoice.Id;
                }
                foreach (var ts in timesheets.Where(t => t.Details.Any(d => d.SalesInvoiceId == invoice.Id)))
                {
                    await tsRepo.UpdateAsync(ts);
                }
            }
        }

        // Apply pricing rules (auto-discount based on configured rules)
        if (!invoice.IsReturn)
        {
            var pricingContexts = invoice.Items.Select(i => new PricingRuleContext
            {
                ItemId = i.ItemId,
                ItemName = i.Description,
                Qty = i.Quantity,
                Rate = i.UnitPrice,
            }).ToList();

            await _pricingRuleService.ApplyToItemsAsync(
                pricingContexts, invoice.IssueDate, "Selling",
                invoice.CustomerId, invoice.CompanyId);

            for (int idx = 0; idx < invoice.Items.Count; idx++)
            {
                var ctx = pricingContexts[idx];
                if (ctx.DiscountedRate > 0 && ctx.DiscountedRate != ctx.Rate)
                {
                    invoice.Items[idx].UnitPrice = ctx.DiscountedRate;
                }
            }
        }

        // Auto-generate due date from Payment Terms Template
        // Per DO-NOT: opening invoices with is_opening="Yes" must clear payment_terms_template entirely
        if (input.PaymentTermsTemplateId.HasValue && !input.DueDate.HasValue && !invoice.IsOpening)
        {
            var template = await _paymentTermsRepository.GetAsync(input.PaymentTermsTemplateId.Value);
            var schedule = template.GenerateSchedule(input.IssueDate, invoice.GrandTotal);
            if (schedule.Count > 0)
            {
                // Set DueDate to the last (final) payment due date
                invoice.DueDate = schedule.Max(s => s.DueDate);
            }
        }

        // Apply coupon code discount if provided
        if (!string.IsNullOrWhiteSpace(input.CouponCode) && !input.IsReturn)
        {
            var couponService = LazyServiceProvider.LazyGetRequiredService<CouponCodeAppService>();
            var pricingRuleId = await couponService.ValidateAndApplyAsync(
                input.CouponCode, input.CustomerId, input.IssueDate);
            invoice.CouponCode = input.CouponCode;
            invoice.Notes = string.IsNullOrEmpty(invoice.Notes)
                ? $"Coupon: {input.CouponCode}"
                : $"{invoice.Notes} | Coupon: {input.CouponCode}";
        }

        // Apply loyalty points redemption if requested
        if (input.LoyaltyPointsToRedeem > 0 && !input.IsReturn)
        {
            var customer = await _customerRepository.GetAsync(input.CustomerId);
            if (customer.LoyaltyProgramId.HasValue)
            {
                var programRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.LoyaltyProgram, Guid>>();
                var program = await programRepo.GetAsync(customer.LoyaltyProgramId.Value);

                // Determine current tier for redemption factor
                var tier = program.Tiers.OrderBy(t => t.MinSpent).FirstOrDefault();
                if (tier != null)
                {
                    var redemptionValue = program.CalculateRedemptionValue(input.LoyaltyPointsToRedeem, tier);

                    // Cap at grand_total to avoid negative payable (per gotcha #109)
                    var maxRedeemable = invoice.GrandTotal;
                    if (redemptionValue > maxRedeemable)
                        redemptionValue = maxRedeemable;

                    invoice.LoyaltyPointsRedeemed = input.LoyaltyPointsToRedeem;
                    invoice.LoyaltyRedemptionAmount = redemptionValue;
                    invoice.LoyaltyProgramId = customer.LoyaltyProgramId;
                }
            }
        }

        await _repository.InsertAsync(invoice, autoSave: true);

        if (input.SalesTeam is { Count: > 0 })
        {
            await CreateSalesTeamEntriesAsync(invoice, input.SalesTeam);
        }

        // Persist payment schedule entries (after invoice saved so we have the ID)
        if (input.PaymentTermsTemplateId.HasValue && !invoice.IsOpening)
        {
            var template = await _paymentTermsRepository.GetAsync(input.PaymentTermsTemplateId.Value);
            var schedule = template.GenerateSchedule(input.IssueDate, invoice.GrandTotal);
            foreach (var line in schedule)
            {
                var entry = new PaymentScheduleEntry(
                    GuidGenerator.Create(), "SalesInvoice", invoice.Id,
                    line.DueDate, line.InvoicePortion, line.PaymentAmount,
                    line.Description);
                await _paymentScheduleRepository.InsertAsync(entry);
            }
        }

        // Auto-adjust advance from linked Sales Order
        // Per ERPNext: set_advances() on SI creation auto-fetches advance payments
        // and deducts from grand total via TotalAdvance field
        if (!invoice.IsReturn && !invoice.IsOpening)
        {
            await AdjustAdvanceFromSalesOrderAsync(invoice);
        }

        return ObjectMapper.Map<SalesInvoice, SalesInvoiceDto>(invoice);
    }

    [Authorize(MyERPPermissions.SalesInvoices.Submit)]
    public async Task<SalesInvoiceDto> SubmitAsync(Guid id)
    {
        var invoice = await _repository.GetAsync(id);

        // Authorization control: high-value transaction approval check
        // Per ERPNext: Authorization Rules check based on GrandTotal/Discount/Customerwise
        var authControl = LazyServiceProvider.LazyGetRequiredService<MyERP.Core.DomainServices.AuthorizationControlService>();
        var userRoles = (CurrentUser.Roles ?? Array.Empty<string>()).ToArray();
        await authControl.ValidateApprovingAuthorityAsync(
            "SalesInvoice", invoice.CompanyId,
            CurrentUser.Id ?? Guid.Empty, userRoles, invoice.GrandTotal);

        // Return document validation (domain service)
        if (invoice.IsReturn)
        {
            await _invoiceManager.ValidateReturnAsync(invoice);
            // Block zero-qty items on stock-affecting returns (corrupts FIFO queue)
            SalesInvoiceManager.ValidateReturnWithStockNoZeroQty(invoice);
        }

        // Fixed asset sales quantity validation: aggregated by AssetId (PR #51363 / commit 23b094f151)
        var fixedAssetItems = invoice.Items.Where(i => i.IsFixedAsset && i.AssetId.HasValue).ToList();
        if (fixedAssetItems.Any())
        {
            var assetRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Assets.Entities.Asset, Guid>>();
            foreach (var group in fixedAssetItems.GroupBy(i => i.AssetId!.Value))
            {
                var totalSaleQty = group.Sum(i => i.Quantity);
                var asset = await assetRepo.FindAsync(group.Key);
                if (asset != null && totalSaleQty > asset.AssetQuantity)
                {
                    throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                        .WithData("detail", $"Sell quantity cannot exceed the asset quantity. Asset {asset.AssetName} has only {asset.AssetQuantity} item(s).");
                }
            }
        }

        // Credit limit validation (enforced at SI submit per ERPNext rules, skip for returns)
        if (!invoice.IsReturn)
        {
            await _creditLimitService.ValidateCreditLimitAsync(invoice.CustomerId, invoice.GrandTotal, invoice.CompanyId);

            // Overdue billing threshold check (Malaysia compliance)
            // Per ERPNext check_overdue_billing_threshold: blocks new SI when overdue exceeds threshold
            await _creditLimitService.ValidateOverdueBillingThresholdAsync(invoice.CustomerId, invoice.CompanyId, userRoles);

            // Credit utilization warning: notify when approaching limit (80%+)
            try
            {
                var customerRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.Customer, Guid>>();
                var customer = await customerRepo.GetAsync(invoice.CustomerId);
                if (customer.CreditLimit > 0)
                {
                    var outstanding = customer.CreditLimit > 0 ? invoice.GrandTotal : 0;
                    var utilization = outstanding / customer.CreditLimit * 100;
                    if (utilization >= 80 && CurrentUser.Id.HasValue)
                    {
                        var notifSvc = LazyServiceProvider.LazyGetRequiredService<Notification.DomainServices.BusinessNotificationService>();
                        await notifSvc.NotifyCreditLimitWarningAsync(
                            CurrentUser.Id.Value, customer.Name, customer.CreditLimit, outstanding, invoice.TenantId);
                    }
                }
            }
            catch (Exception ex) { Logger.LogWarning(ex, "Credit limit notification failed for SI {Id}", invoice.Id); }

            // Selling price validation: selling rate must be >= valuation rate.
            // Per ERPNext validate_selling_price — gated entirely by Selling Settings.validate_selling_price
            // (a plain on/off Check field; ERPNext always hard-Stops when enabled, no Warn mode).
            if (invoice.WarehouseId.HasValue
                && await SettingProvider.IsTrueAsync(MyERP.Settings.MyERPSettings.Selling.ValidateSellingPrice))
            {
                var siItemData = invoice.Items
                    .Select(i => (i.ItemId, i.UnitPrice, i.Description))
                    .ToList().AsReadOnly();
                await SalesInvoiceManager.ValidateSellingPriceAsync(
                    siItemData,
                    async itemId =>
                    {
                        var balance = await _valuationService
                            .GetCurrentBalanceAsync(itemId, invoice.WarehouseId.Value);
                        return balance.ValuationRate;
                    },
                    action: "Stop");
            }

            // Mandatory SO/DN linkage (Selling Settings or Customer flags: "Is SO/DN required for Sales Invoice?")
            var customerEntity = await _customerRepository.FindAsync(invoice.CustomerId);
            var soRequired = (customerEntity?.SoRequired ?? false) || await SettingProvider.IsTrueAsync(MyERP.Settings.MyERPSettings.Selling.SoRequired);
            var dnRequired = (customerEntity?.DnRequired ?? false) || await SettingProvider.IsTrueAsync(MyERP.Settings.MyERPSettings.Selling.DnRequired);
            SalesInvoiceManager.ValidateSoRequired(invoice, soRequired);
            SalesInvoiceManager.ValidateDnRequired(invoice, dnRequired);

            // Project must belong to the invoice's customer (prevents billing/costing
            // against a different customer's project) — same rule as Sales Order.
            if (invoice.ProjectId.HasValue)
            {
                var projectValidation = LazyServiceProvider
                    .LazyGetRequiredService<MyERP.Core.DomainServices.TransactionValidationService>();
                await projectValidation.ValidateProjectCustomerAsync(invoice.ProjectId, invoice.CustomerId);
            }

            // Maintain same rate throughout the sales cycle (Selling Settings)
            if (await SettingProvider.IsTrueAsync(MyERP.Settings.MyERPSettings.Selling.MaintainSameRate))
            {
                var rateCheckSoItemIds = invoice.Items
                    .Where(i => i.SalesOrderItemId.HasValue)
                    .Select(i => i.SalesOrderItemId!.Value)
                    .Distinct()
                    .ToList();

                if (rateCheckSoItemIds.Any())
                {
                    var rateCheckOrders = (await _salesOrderRepository.GetQueryableAsync())
                        .Where(so => so.Items.Any(soi => rateCheckSoItemIds.Contains(soi.Id)))
                        .ToList();
                    var soItemRates = rateCheckOrders.SelectMany(so => so.Items)
                        .Where(soi => rateCheckSoItemIds.Contains(soi.Id))
                        .ToDictionary(soi => soi.Id, soi => soi.UnitPrice);

                    var rateAction = await SettingProvider.GetOrNullAsync(MyERP.Settings.MyERPSettings.Selling.MaintainSameRateAction) ?? "Stop";
                    var overrideRole = await SettingProvider.GetOrNullAsync(MyERP.Settings.MyERPSettings.Selling.RoleToOverrideStopAction);
                    var canOverride = !string.IsNullOrEmpty(overrideRole)
                        && (CurrentUser.Roles ?? Array.Empty<string>()).Contains(overrideRole);

                    var rateLines = invoice.Items
                        .Where(i => i.SalesOrderItemId.HasValue && soItemRates.ContainsKey(i.SalesOrderItemId.Value))
                        .Select(i => (i.Description, i.UnitPrice, soItemRates[i.SalesOrderItemId!.Value], "Sales Order"));

                    var transactionValidation = LazyServiceProvider
                        .LazyGetRequiredService<MyERP.Core.DomainServices.TransactionValidationService>();
                    transactionValidation.ValidateMaintainSameRate(rateLines, rateAction, canOverride);
                }
            }
        }

        // Server-side tax recalculation (delegated to domain service)
        var taxRecalcService = LazyServiceProvider.LazyGetRequiredService<TransactionTaxRecalculationService>();
        var discountAmt = invoice.DiscountAmount;
        if (invoice.AdditionalDiscountPercentage > 0 && discountAmt == 0)
        {
            var netForDiscount = invoice.Items.Sum(i => i.LineTotal);
            discountAmt = Math.Round(netForDiscount * invoice.AdditionalDiscountPercentage / 100m, 2);
        }
        var totals = await taxRecalcService.RecalculateAsync(new TaxRecalculationInput
        {
            DocumentType = "SalesInvoice",
            DocumentId = invoice.Id,
            Items = invoice.Items.Select(i => new TaxItemInput
            {
                ItemId = i.ItemId, Quantity = i.Quantity, UnitPrice = i.UnitPrice
            }).ToList(),
            ExchangeRate = invoice.ExchangeRate,
            DiscountAmount = discountAmt,
        });
        invoice.NetTotal = totals.NetTotal;
        invoice.TaxAmount = totals.TaxAmount;
        invoice.GrandTotal = totals.GrandTotal;
        invoice.BaseNetTotal = totals.BaseNetTotal;
        invoice.BaseTaxAmount = totals.BaseTaxAmount;
        invoice.BaseGrandTotal = totals.BaseGrandTotal;

        // Validate payment schedule integrity before submit
        var scheduleValidator = LazyServiceProvider.LazyGetRequiredService<PaymentScheduleValidationService>();
        var scheduleQuery = await _paymentScheduleRepository.GetQueryableAsync();
        var scheduleEntries = scheduleQuery
            .Where(e => e.ParentType == "SalesInvoice" && e.ParentId == invoice.Id)
            .ToList();
        if (scheduleEntries.Count > 0)
        {
            var scheduleInputs = scheduleEntries.Select(e => new PaymentScheduleInput
            {
                DueDate = e.DueDate,
                InvoicePortion = e.InvoicePortion,
                PaymentAmount = e.PaymentAmount,
            }).ToList();
            var validation = scheduleValidator.Validate(scheduleInputs, invoice.GrandTotal, invoice.IssueDate);
            if (!validation.IsValid)
            {
                throw new BusinessException("MyERP:02004")
                    .WithData("errors", string.Join("; ", validation.Errors));
            }
        }

        invoice.Submit();

        // Credit Note: reduce original invoice outstanding (domain service)
        if (invoice.IsReturn && invoice.ReturnAgainstId.HasValue)
        {
            await _invoiceManager.ApplyCreditNoteAsync(invoice);
        }

        // Update Stock: create SLE entries for direct sales (without DN)
        // Per DO-NOT: opening invoices with update_stock=true are blocked (accounting-only)
        if (invoice.IsOpening && invoice.UpdateStock)
        {
            throw new Volo.Abp.BusinessException("MyERP:01006")
                .WithData("documentType", "Sales Invoice")
                .WithData("invoiceNumber", invoice.InvoiceNumber);
        }

        if (invoice.UpdateStock && invoice.WarehouseId.HasValue && !invoice.IsReturn)
        {
            var itemRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Inventory.Entities.Item, Guid>>();

            // Batch expiry validation — block expired/disabled batches on stock-out
            // Per DO-NOT: must block expired batch consumption in transactions
            // Note: SI items don't have per-item BatchId; batch tracking is via Serial and Batch Bundle.
            // When SABB integration is fully wired, batch validation will fire via the bundle path.
            // For now, skip batch validation on SI (DN handles it in the standard delivery flow).

            // Quality Inspection enforcement — block if items require QI but none submitted+accepted
            var stockItemIds = new List<Guid>();
            foreach (var item in invoice.Items)
            {
                var itemEntity = await itemRepo.FindAsync(item.ItemId);
                if (itemEntity != null && itemEntity.MaintainStock)
                    stockItemIds.Add(item.ItemId);
            }
            if (stockItemIds.Any())
            {
                var qiEnforcement = LazyServiceProvider.LazyGetRequiredService<Inventory.DomainServices.QualityInspectionEnforcementService>();
                await qiEnforcement.ValidateForSalesInvoiceAsync(invoice.Id, stockItemIds.ToArray(), invoice.TenantId);
            }

            foreach (var item in invoice.Items)
            {
                // Skip non-stock items (service items don't create SLE)
                var itemEntity = await itemRepo.FindAsync(item.ItemId);
                if (itemEntity != null && !itemEntity.MaintainStock)
                    continue;

                // Capture valuation rate BEFORE stock-out (actual cost, not selling price)
                // Per ERPNext: stock movements always use valuation rate for value calculation
                var balance = await _valuationService.GetCurrentBalanceAsync(item.ItemId, invoice.WarehouseId.Value);
                var ratePerStockUnit = balance.ValuationRate;
                item.ValuationRate = ratePerStockUnit; // Store for cancel reversal + gross profit

                // Use StockQty for SLE (respects UOM conversion)
                var stockQty = item.StockQty;

                await _valuationService.CreateLedgerEntryAsync(
                    invoice.CompanyId, item.ItemId, invoice.WarehouseId.Value,
                    invoice.IssueDate, -stockQty, ratePerStockUnit,
                    voucherType: "SalesInvoice", voucherId: invoice.Id,
                    tenantId: invoice.TenantId);

                await _binService.ApplyStockMovementAsync(
                    item.ItemId, invoice.WarehouseId.Value,
                    -stockQty, -(stockQty * ratePerStockUnit), invoice.TenantId);

                // Trigger auto-reorder check after stock-out
                var autoReorder = LazyServiceProvider.LazyGetRequiredService<Inventory.DomainServices.AutoReorderService>();
                await autoReorder.CheckSingleItemAsync(
                    item.ItemId, invoice.WarehouseId.Value, invoice.CompanyId, invoice.TenantId);
            }

            // Low-stock alert for procurement staff, distinct from AutoReorderService above (which
            // auto-creates a Material Request) — batched once for all stock items, not per-item,
            // since every SI item shares the same document-level WarehouseId here. Reuses
            // stockItemIds (computed above for QI enforcement) to skip service items the same way
            // the stock-out loop itself does.
            if (stockItemIds.Any())
            {
                var stockAlert = LazyServiceProvider.LazyGetRequiredService<Inventory.DomainServices.StockAlertNotificationService>();
                await stockAlert.CheckMultipleAndNotifyAsync(
                    stockItemIds, invoice.WarehouseId.Value, invoice.CompanyId, invoice.TenantId);
            }

            // Consume Stock Reservation Entries for UpdateStock SI (direct sales without DN)
            // Per ERPNext: update_stock_reservation_entries on SI submit with update_stock=true
            var sreManager = LazyServiceProvider.LazyGetRequiredService<Inventory.DomainServices.StockReservationManager>();
            foreach (var item in invoice.Items)
            {
                var itemEntity = await itemRepo.FindAsync(item.ItemId);
                if (itemEntity == null || !itemEntity.MaintainStock) continue;

                await sreManager.ConsumeOnDeliveryAsync(
                    item.ItemId, invoice.WarehouseId.Value, item.StockQty);
            }
        }

        // Over-billing validation + SO BilledQty update (domain service)
        if (!invoice.IsReturn)
        {
            var billingCompany = await _companyRepository.GetAsync(invoice.CompanyId);
            await _invoiceManager.ValidateOverBillingAsync(invoice, billingCompany.OverBillingAllowance);
        }

        // Per ERPNext PR #56410: update linked Sales Order BilledQty on both SI and Credit Note submit
        await _invoiceManager.UpdateLinkedOrderBillingAsync(invoice);

        // DN BilledQty update: track which DN items have been billed
        // Per ERPNext: update_billed_amount_based_on_dn FIFO billing status
        await _invoiceManager.UpdateLinkedDeliveryNoteBillingAsync(invoice);

        await _repository.UpdateAsync(invoice, autoSave: true);

        // Loyalty Points: redeem points if specified on invoice (deduct from customer balance)
        if (invoice.LoyaltyPointsRedeemed > 0 && invoice.LoyaltyProgramId.HasValue && !invoice.IsReturn)
        {
            var loyaltyService = LazyServiceProvider.LazyGetRequiredService<LoyaltyPointService>();
            await loyaltyService.RedeemPointsAsync(
                invoice.LoyaltyProgramId.Value, invoice.CustomerId,
                invoice.LoyaltyPointsRedeemed, invoice.IssueDate,
                invoice.CompanyId,
                invoiceType: "SalesInvoice", invoiceId: invoice.Id, tenantId: invoice.TenantId);
        }

        // Loyalty Points: earn points on non-return, non-consolidated invoices
        if (!invoice.IsReturn)
        {
            var customer = await _customerRepository.GetAsync(invoice.CustomerId);
            if (customer.LoyaltyProgramId.HasValue)
            {
                var loyaltyService = LazyServiceProvider.LazyGetRequiredService<LoyaltyPointService>();

                // Eligible amount = grand_total - returned_amount (per ERPNext make_loyalty_point_entry):
                // sum of grand_total across all submitted return invoices referencing this one, so a
                // partially-credited sale earns points only on what actually stuck. Return invoices
                // store a negative GrandTotal in this codebase (negative qty × positive rate) — negate
                // the sum to get a positive magnitude to subtract.
                var invoiceQuery = await _repository.GetQueryableAsync();
                var returnedAmount = -invoiceQuery
                    .Where(i => i.ReturnAgainstId == invoice.Id
                        && i.IsReturn
                        && i.Status == Core.DocumentStatus.Posted)
                    .Sum(i => i.GrandTotal);
                var eligibleAmount = Math.Max(0, invoice.GrandTotal - returnedAmount);

                await loyaltyService.EarnPointsAsync(
                    customer.LoyaltyProgramId.Value, customer.Id, invoice.CompanyId,
                    eligibleAmount, 0m, invoice.IssueDate,
                    invoiceType: "SalesInvoice", invoiceId: invoice.Id, tenantId: invoice.TenantId);
            }

            // Inter-Company: create corresponding PI in target company if customer represents another company
            if (customer.RepresentsCompanyId.HasValue)
            {
                var interCompanyService = LazyServiceProvider.LazyGetRequiredService<Core.DomainServices.InterCompanyTransactionService>();
                await interCompanyService.CreatePurchaseInvoiceFromSalesInvoiceAsync(
                    invoice.Id, customer.RepresentsCompanyId.Value, invoice.TenantId);
            }
        }

        // Auto-insert Item Prices from transaction rates
        // Per ERPNext: auto_insert_price_list_rate_if_missing creates date-segmented price history
        try
        {
            var priceAutoInsert = LazyServiceProvider.LazyGetRequiredService<Inventory.DomainServices.ItemPriceAutoInsertService>();
            var defaultPriceListRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Inventory.Entities.PriceList, Guid>>();
            var defaultPriceList = (await defaultPriceListRepo.GetQueryableAsync())
                .FirstOrDefault(p => p.IsSelling && p.IsDefault);
            if (defaultPriceList != null)
            {
                await priceAutoInsert.AutoInsertFromTransactionAsync(new Inventory.DomainServices.AutoInsertPriceContext
                {
                    IsEnabled = true,
                    PriceListId = defaultPriceList.Id,
                    PartyId = invoice.CustomerId,
                    IsSelling = true,
                    TransactionDate = invoice.IssueDate,
                    CurrencyCode = invoice.CurrencyCode,
                    TenantId = invoice.TenantId,
                    Items = invoice.Items.Select(i => new Inventory.DomainServices.AutoInsertPriceItem
                    {
                        ItemId = i.ItemId, Rate = i.UnitPrice, Uom = i.Uom
                    }).ToArray(),
                });
            }
        }
        catch (Exception ex) { Logger.LogWarning(ex, "Item price auto-insert failed for SI {Id}", invoice.Id); }

        // Audit trail
        await _activityLog.LogSubmittedAsync("SalesInvoice", invoice.Id, invoice.CompanyId,
            invoice.InvoiceNumber, invoice.TenantId);

        return ObjectMapper.Map<SalesInvoice, SalesInvoiceDto>(invoice);
    }

    [Authorize(MyERPPermissions.SalesInvoices.Submit)]
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

    [Authorize(MyERPPermissions.SalesInvoices.Submit)]
    public async Task<BulkOperationResultDto> BulkPostAsync(List<Guid> ids)
    {
        var results = new BulkOperationResultDto();
        foreach (var id in ids)
        {
            try
            {
                await PostAsync(id);
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

    [Authorize(MyERPPermissions.SalesInvoices.Submit)]
    public async Task<SalesInvoiceDto> PostAsync(Guid id)
    {
        var invoice = await _repository.GetAsync(id);
        invoice.Post();

        var glService = LazyServiceProvider.LazyGetRequiredService<Accounting.DomainServices.GlRepostService>();
        await glService.RebuildSalesInvoiceGlAsync(invoice);

        await _repository.UpdateAsync(invoice, autoSave: true);

        // Audit trail
        await _activityLog.LogPostedAsync("SalesInvoice", invoice.Id, invoice.CompanyId,
            invoice.InvoiceNumber, invoice.TenantId);

        return ObjectMapper.Map<SalesInvoice, SalesInvoiceDto>(invoice);
    }

    [Authorize(MyERPPermissions.SalesInvoices.Cancel)]
    public async Task<SalesInvoiceDto> CancelAsync(Guid id)
    {
        var invoice = await _repository.GetAsync(id);

        // Guard: cannot cancel invoices with payments applied
        if (invoice.AmountPaid > 0)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.CannotCancelWithPayments)
                .WithData("documentType", "Sales Invoice")
                .WithData("amountPaid", invoice.AmountPaid);
        }

        // Guard: cannot cancel while the e-Invoice is Valid with LHDN — doing so would leave
        // MyERP showing the document cancelled while LHDN still holds a legally valid,
        // unresolved e-Invoice. The user must cancel the LHDN submission first (via the
        // e-Invoice module, which enforces the 72-hour window and calls LHDN's cancel API),
        // which flips EInvoiceStatus away from Valid before this cancel is allowed to proceed.
        if (invoice.EInvoiceStatus == EInvoiceStatus.Valid)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.CannotCancelInvoiceWithValidEInvoice)
                .WithData("invoiceNumber", invoice.InvoiceNumber);
        }

        // Loyalty cancel guard: can't cancel if earned points have been redeemed
        var customer = await _customerRepository.GetAsync(invoice.CustomerId);
        if (customer.LoyaltyProgramId.HasValue)
        {
            var loyaltyService = LazyServiceProvider.LazyGetRequiredService<LoyaltyPointService>();
            if (await loyaltyService.HasPointsBeenRedeemedAsync(invoice.Id, "SalesInvoice"))
            {
                throw new Volo.Abp.BusinessException("MyERP:03014")
                    .WithData("invoiceNumber", invoice.InvoiceNumber);
            }
        }

        invoice.Cancel();

        if (!string.IsNullOrWhiteSpace(invoice.CouponCode))
        {
            var couponServiceCancel = LazyServiceProvider.LazyGetRequiredService<CouponCodeAppService>();
            await couponServiceCancel.ReverseUsageAsync(invoice.CouponCode);
        }

        // Auto-cancel linked system-generated Credit Note Journal Entries (gotcha #3909)
        var jeRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Accounting.Entities.JournalEntry, Guid>>();
        var linkedJes = await jeRepo.GetListAsync(j =>
            j.ReferenceType == "SalesInvoice"
            && j.ReferenceId == invoice.Id
            && j.VoucherType == Accounting.JournalEntryVoucherType.CreditNote
            && j.Status == Core.DocumentStatus.Posted);

        foreach (var je in linkedJes)
        {
            je.Cancel();
            await _postingOrchestrator.ReversePleForDocumentAsync("JournalEntry", je.Id);
            await _postingOrchestrator.ReverseGlForDocumentAsync("JournalEntry", je.Id);
            await jeRepo.UpdateAsync(je, autoSave: true);
        }

        // Reverse PLE entries + reverse the posted GL Journal Entry
        await _postingOrchestrator.ReversePleForDocumentAsync("SalesInvoice", invoice.Id);
        await _postingOrchestrator.ReverseGlForDocumentAsync("SalesInvoice", invoice.Id);

        // Reverse stock if UpdateStock was used (in stock UOM)
        if (invoice.UpdateStock && invoice.WarehouseId.HasValue)
        {
            var itemRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Inventory.Entities.Item, Guid>>();
            foreach (var item in invoice.Items)
            {
                var itemEntity = await itemRepo.FindAsync(item.ItemId);
                if (itemEntity != null && !itemEntity.MaintainStock)
                    continue;

                var stockQty = item.StockQty;
                // Use the valuation rate that was captured during submit (actual cost rate)
                // Fallback to current balance rate if ValuationRate wasn't set (legacy data)
                var ratePerStockUnit = item.ValuationRate > 0
                    ? item.ValuationRate
                    : (await _valuationService.GetCurrentBalanceAsync(item.ItemId, invoice.WarehouseId.Value)).ValuationRate;

                await _valuationService.CreateLedgerEntryAsync(
                    invoice.CompanyId, item.ItemId, invoice.WarehouseId.Value,
                    invoice.IssueDate, stockQty, ratePerStockUnit, // Positive = stock back in
                    voucherType: "SalesInvoice", voucherId: invoice.Id,
                    tenantId: invoice.TenantId);

                await _binService.ApplyStockMovementAsync(
                    item.ItemId, invoice.WarehouseId.Value,
                    stockQty, stockQty * ratePerStockUnit, invoice.TenantId);
            }
        }

        // Reverse linked Sales Order BilledQty (domain service)
        await _invoiceManager.UpdateLinkedOrderBillingAsync(invoice, reverse: true);

        // Reverse DN BilledQty (domain service)
        await _invoiceManager.UpdateLinkedDeliveryNoteBillingAsync(invoice, reverse: true);

        // Unlink billed timesheet entries (gotcha #2247)
        var tsRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Projects.Entities.Timesheet, Guid>>();
        var tsList = await tsRepo.GetListAsync(t => t.Details.Any(d => d.SalesInvoiceId == invoice.Id), includeDetails: true);
        foreach (var ts in tsList)
        {
            var modified = false;
            foreach (var detail in ts.Details.Where(d => d.SalesInvoiceId == invoice.Id))
            {
                detail.SalesInvoiceId = null;
                modified = true;
            }
            if (modified)
            {
                await tsRepo.UpdateAsync(ts);
            }
        }

        await _repository.UpdateAsync(invoice, autoSave: true);

        // Audit trail
        await _activityLog.LogCancelledAsync("SalesInvoice", invoice.Id, invoice.CompanyId,
            invoice.InvoiceNumber, "Posted", invoice.TenantId);

        // Inter-company cancellation cascade: cancelling this SI also cancels the Purchase
        // Invoice it created in the target company. Status-guarded so cascading from either
        // side converges instead of recursing (this SI is already Cancelled by the time the
        // linked PI's own cascade would look back at it). Only the reversal steps that apply
        // to an inter-company-created PI run here (no PO/PR-linked items, no UpdateStock) —
        // matches PurchaseInvoiceAppService.CancelAsync's own steps for that shape of document.
        if (invoice.InterCompanyPurchaseInvoiceId.HasValue)
        {
            var piRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Purchasing.Entities.PurchaseInvoice, Guid>>();
            var linkedPi = await piRepo.FindAsync(invoice.InterCompanyPurchaseInvoiceId.Value);
            if (linkedPi != null && linkedPi.Status == Core.DocumentStatus.Posted && linkedPi.AmountPaid <= 0)
            {
                linkedPi.Cancel();
                await _postingOrchestrator.ReversePleForDocumentAsync("PurchaseInvoice", linkedPi.Id);
                await _postingOrchestrator.ReverseGlForDocumentAsync("PurchaseInvoice", linkedPi.Id);
                await piRepo.UpdateAsync(linkedPi, autoSave: true);
                await _activityLog.LogCancelledAsync("PurchaseInvoice", linkedPi.Id, linkedPi.CompanyId,
                    linkedPi.InvoiceNumber, "Posted", linkedPi.TenantId);
            }
        }

        return ObjectMapper.Map<SalesInvoice, SalesInvoiceDto>(invoice);
    }

    /// <summary>
    /// Write off the outstanding amount on a posted invoice (bad debt).
    /// Sets AmountPaid = GrandTotal (clears outstanding) and creates reversal PLE.
    /// </summary>
    [Authorize(MyERPPermissions.SalesInvoices.Submit)]
    public async Task<SalesInvoiceDto> WriteOffAsync(Guid id)
    {
        var invoice = await _repository.GetAsync(id);

        if (invoice.Status != Core.DocumentStatus.Posted)
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        if (invoice.OutstandingAmount <= 0)
            throw new Volo.Abp.BusinessException("MyERP:02010")
                .WithData("invoiceNumber", invoice.InvoiceNumber);

        // Write off remaining outstanding
        var writeOffAmount = invoice.OutstandingAmount;
        invoice.AmountPaid = invoice.GrandTotal; // Clears outstanding to 0

        // Create write-off Journal Entry (DR Write-Off Expense, CR Receivable)
        var company = await _companyRepository.GetAsync(invoice.CompanyId);
        if (company.DefaultExpenseAccountId.HasValue && company.DefaultReceivableAccountId.HasValue)
        {
            // Resolve fiscal year for the posting date
            var fyRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Accounting.Entities.FiscalYear, Guid>>();
            var fyQuery = await fyRepo.GetQueryableAsync();
            var fy = fyQuery.FirstOrDefault(f => f.CompanyId == invoice.CompanyId
                && f.StartDate <= DateTime.UtcNow && f.EndDate >= DateTime.UtcNow);

            if (fy != null)
            {
                var je = new Accounting.Entities.JournalEntry(
                    GuidGenerator.Create(), invoice.CompanyId, fy.Id, DateTime.UtcNow, invoice.TenantId);

                je.AddLine(company.DefaultExpenseAccountId.Value, writeOffAmount, true, $"Write-off: {invoice.InvoiceNumber}");
                je.AddLine(company.DefaultReceivableAccountId.Value, writeOffAmount, false, $"Write-off: {invoice.InvoiceNumber}");
                je.Validate();
                je.Post();

                var jeRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Accounting.Entities.JournalEntry, Guid>>();
                await jeRepo.InsertAsync(je);
            }
        }

        // Reverse PLE outstanding (creates write-off PLE entry)
        await _postingOrchestrator.ReversePleForDocumentAsync("SalesInvoice", invoice.Id);

        await _repository.UpdateAsync(invoice, autoSave: true);
        return ObjectMapper.Map<SalesInvoice, SalesInvoiceDto>(invoice);
    }

    /// <summary>
    /// Amend a cancelled Sales Invoice — creates a new draft copy with amendment link.
    /// Per DO-NOT: only Cancelled documents can be amended.
    /// </summary>
    [Authorize(MyERPPermissions.SalesInvoices.Create)]
    public async Task<SalesInvoiceDto> AmendAsync(Guid id)
    {
        var original = await _repository.GetAsync(id);
        var amendService = LazyServiceProvider.LazyGetRequiredService<Core.DomainServices.DocumentAmendmentService>();

        amendService.ValidateCanAmend(original.Status);
        var newNumber = amendService.GenerateAmendedNumber(original.InvoiceNumber, original.AmendmentIndex + 1);

        var amended = new Sales.Entities.SalesInvoice(
            GuidGenerator.Create(),
            original.CompanyId,
            original.CustomerId,
            newNumber,
            DateTime.UtcNow.Date);

        amended.AmendedFromId = original.Id;
        amended.AmendmentIndex = original.AmendmentIndex + 1;
        amended.CurrencyCode = original.CurrencyCode;
        amended.ExchangeRate = original.ExchangeRate;
        amended.PriceListId = original.PriceListId;
        amended.PaymentTermsTemplateId = original.PaymentTermsTemplateId;

        foreach (var item in original.Items)
        {
            amended.AddItem(item.ItemId, item.Description, item.Quantity, item.UnitPrice, item.TaxAmount, item.Uom);
        }

        await _repository.InsertAsync(amended, autoSave: true);
        return ObjectMapper.Map<SalesInvoice, SalesInvoiceDto>(amended);
    }

    [Authorize(MyERPPermissions.SalesInvoices.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        var invoice = await _repository.GetAsync(id);
        if (invoice.Status != Core.DocumentStatus.Draft)
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("detail", "Only Draft invoices can be deleted");
        await _repository.DeleteAsync(id);
    }

    /// <summary>
    /// Auto-adjusts advance payments from linked Sales Order.
    /// Per ERPNext: set_advances() queries Payment Entries with
    /// AgainstOrderType="SalesOrder" that are posted and allocated against the SO.
    /// The total advance is set on the invoice to reduce outstanding.
    /// </summary>
    private async Task AdjustAdvanceFromSalesOrderAsync(SalesInvoice invoice)
    {
        try
        {
            // Find SO IDs from invoice items
            var soItemIds = invoice.Items
                .Where(i => i.SalesOrderItemId.HasValue)
                .Select(i => i.SalesOrderItemId!.Value)
                .Distinct()
                .ToList();

            if (!soItemIds.Any()) return;

            // Find the SO via item lookup
            var soRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.SalesOrder, Guid>>();
            var soQuery = await soRepo.GetQueryableAsync();
            var linkedSOs = soQuery
                .Where(so => so.Items.Any(i => soItemIds.Contains(i.Id)))
                .ToList();

            if (!linkedSOs.Any()) return;

            // Sum up advance paid on linked SOs
            var totalAdvance = linkedSOs.Sum(so => so.AdvancePaid);

            if (totalAdvance > 0)
            {
                // Cap at grand total (can't deduct more than invoice amount)
                var applicableAdvance = Math.Min(totalAdvance, invoice.GrandTotal);
                invoice.SetTotalAdvance(applicableAdvance);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to auto-adjust advance for SI {InvoiceId}", invoice.Id);
            // Non-blocking: advance adjustment failure shouldn't prevent invoice creation
        }
    }

    /// <summary>
    /// Returns unbilled Delivery Note items for a customer.
    /// Per ERPNext: SI form "Get Items From Delivery Note" fetches DN items
    /// where billed_qty < qty (partially or fully unbilled).
    /// Used by the frontend to populate SI items from delivered-but-unbilled goods.
    /// </summary>
    [Authorize(MyERPPermissions.SalesInvoices.Create)]
    public async Task<List<UnbilledDeliveryItemDto>> GetUnbilledDeliveryItemsAsync(
        Guid customerId, Guid? companyId = null)
    {
        var dnRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<DeliveryNote, Guid>>();
        var dnQuery = await dnRepo.GetQueryableAsync();

        var query = dnQuery.Where(dn =>
            dn.CustomerId == customerId &&
            dn.Status == Core.DocumentStatus.Posted &&
            !dn.IsReturn);

        if (companyId.HasValue)
            query = query.Where(dn => dn.CompanyId == companyId.Value);

        var deliveryNotes = query.ToList();

        var result = new List<UnbilledDeliveryItemDto>();
        foreach (var dn in deliveryNotes)
        {
            foreach (var item in dn.Items)
            {
                var unbilledQty = item.Quantity - item.BilledQty;
                if (unbilledQty > 0)
                {
                    result.Add(new UnbilledDeliveryItemDto
                    {
                        DeliveryNoteId = dn.Id,
                        DeliveryNoteNumber = dn.DeliveryNumber,
                        DeliveryDate = dn.PostingDate,
                        ItemId = item.ItemId,
                        ItemName = item.Description,
                        Quantity = unbilledQty,
                        Rate = item.UnitPrice,
                        Uom = item.Uom,
                        DeliveryNoteItemId = item.Id,
                    });
                }
            }
        }

        return result.OrderBy(r => r.DeliveryDate).ThenBy(r => r.ItemName).ToList();
    }

    /// <summary>
    /// Returns unbilled Sales Order items for a customer.
    /// Per ERPNext: SI form "Get Items From Sales Order" fetches SO items
    /// where billed_qty < qty (partially or fully unbilled).
    /// Used for direct billing from orders (service companies, advance billing).
    /// </summary>
    [Authorize(MyERPPermissions.SalesInvoices.Create)]
    public async Task<List<UnbilledOrderItemDto>> GetUnbilledOrderItemsAsync(
        Guid customerId, Guid? companyId = null)
    {
        var soRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<SalesOrder, Guid>>();
        var soQuery = await soRepo.GetQueryableAsync();

        var query = soQuery.Where(so =>
            so.CustomerId == customerId &&
            so.Status != Core.DocumentStatus.Draft &&
            so.Status != Core.DocumentStatus.Cancelled &&
            so.Status != Core.DocumentStatus.Closed);

        if (companyId.HasValue)
            query = query.Where(so => so.CompanyId == companyId.Value);

        var orders = query.ToList();

        var result = new List<UnbilledOrderItemDto>();
        foreach (var so in orders)
        {
            foreach (var item in so.Items)
            {
                var unbilledQty = item.PendingBillingQty;
                if (unbilledQty > 0)
                {
                    result.Add(new UnbilledOrderItemDto
                    {
                        SalesOrderId = so.Id,
                        OrderNumber = so.OrderNumber,
                        OrderDate = so.OrderDate,
                        ItemId = item.ItemId,
                        ItemName = item.Description,
                        Quantity = unbilledQty,
                        Rate = item.UnitPrice,
                        Uom = item.Uom,
                        SalesOrderItemId = item.Id,
                    });
                }
            }
        }

        return result.OrderBy(r => r.OrderDate).ThenBy(r => r.ItemName).ToList();
    }

    /// <summary>
    /// Creates a consolidated Sales Invoice from multiple submitted Delivery Notes.
    /// Per ERPNext: primary billing workflow for goods-based businesses (deliver daily, invoice weekly/monthly).
    /// Only includes items with pending billing qty. Links each SI item to its DN item for tracking.
    /// </summary>
    [Authorize(MyERPPermissions.SalesInvoices.Create)]
    public async Task<SalesInvoiceDto> CreateFromDeliveryNotesAsync(CreateInvoiceFromDeliveryNotesDto input)
    {
        if (input.DeliveryNoteIds == null || input.DeliveryNoteIds.Count == 0)
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustHaveItems)
                .WithData("documentType", "SalesInvoice");

        var dnRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<DeliveryNote, Guid>>();
        var deliveryNotes = new List<DeliveryNote>();

        foreach (var dnId in input.DeliveryNoteIds.Distinct())
        {
            var dn = await dnRepo.GetAsync(dnId);
            if (dn.CustomerId != input.CustomerId)
                throw new BusinessException("MyERP:07004")
                    .WithData("deliveryNote", dn.DeliveryNumber)
                    .WithData("reason", "Delivery Note belongs to a different customer");
            if (dn.CompanyId != input.CompanyId)
                throw new BusinessException("MyERP:07004")
                    .WithData("deliveryNote", dn.DeliveryNumber)
                    .WithData("reason", "Delivery Note belongs to a different company");
            if (dn.Status != Core.DocumentStatus.Posted && dn.Status != Core.DocumentStatus.Submitted)
                throw new BusinessException(MyERPDomainErrorCodes.DocumentMustBeSubmittedForConversion)
                    .WithData("documentType", "DeliveryNote")
                    .WithData("documentNumber", dn.DeliveryNumber);
            deliveryNotes.Add(dn);
        }

        // Collect all unbilled items across all selected DNs
        var invoiceItems = new List<CreateSalesInvoiceItemDto>();
        var dnItemLinks = new List<(Guid siItemIndex, Guid dnItemId, Guid dnId)>();

        foreach (var dn in deliveryNotes.OrderBy(d => d.PostingDate))
        {
            foreach (var item in dn.Items)
            {
                var pendingQty = item.Quantity - item.BilledQty;
                if (pendingQty <= 0) continue;

                invoiceItems.Add(new CreateSalesInvoiceItemDto
                {
                    ItemId = item.ItemId,
                    Description = item.Description ?? "",
                    Quantity = pendingQty,
                    UnitPrice = item.UnitPrice,
                    TaxAmount = 0,
                    Uom = item.Uom ?? "Unit",
                    DeliveryNoteItemId = item.Id,
                });
                dnItemLinks.Add((Guid.Empty, item.Id, dn.Id));
            }
        }

        if (invoiceItems.Count == 0)
            throw new BusinessException(MyERPDomainErrorCodes.DocumentAlreadyConverted)
                .WithData("documentType", "DeliveryNote")
                .WithData("documentNumber", string.Join(", ", deliveryNotes.Select(d => d.DeliveryNumber)))
                .WithData("reason", "All items have been fully invoiced");

        // Create the consolidated invoice using existing CreateAsync logic
        var createDto = new CreateSalesInvoiceDto
        {
            CompanyId = input.CompanyId,
            CustomerId = input.CustomerId,
            IssueDate = input.IssueDate ?? DateTime.UtcNow.Date,
            CurrencyCode = input.CurrencyCode,
            PaymentTermsTemplateId = input.PaymentTermsTemplateId,
            Notes = input.Notes ?? $"Consolidated invoice for: {string.Join(", ", deliveryNotes.Select(d => d.DeliveryNumber))}",
            Items = invoiceItems,
        };

        var result = await CreateAsync(createDto);

        // Link SI items back to DN items for billing tracking (DeliveryNoteItemId)
        var invoice = await _repository.GetAsync(result.Id);
        for (int i = 0; i < Math.Min(invoice.Items.Count, dnItemLinks.Count); i++)
        {
            invoice.Items.ElementAt(i).DeliveryNoteItemId = dnItemLinks[i].dnItemId;
        }
        await _repository.UpdateAsync(invoice, autoSave: true);

        // Create activity log
        try
        {
            await _activityLog.LogConvertedAsync("DeliveryNote",
                deliveryNotes.First().Id, invoice.CompanyId,
                "SalesInvoice", invoice.Id, deliveryNotes.First().DeliveryNumber,
                CurrentTenant.Id);
        }
        catch { /* non-blocking */ }

        return result;
    }

    public async Task<List<Purchasing.InvoicePaymentDto>> GetPaymentsAsync(Guid id)
    {
        var peRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Accounting.Entities.PaymentEntry, Guid>>();
        var peQuery = await peRepo.GetQueryableAsync();
        var payments = peQuery
            .Where(pe => pe.AgainstInvoiceId == id
                         || pe.References.Any(r => r.ReferenceType == "SalesInvoice" && r.ReferenceId == id))
            .OrderByDescending(pe => pe.PostingDate)
            .Select(pe => new Purchasing.InvoicePaymentDto
            {
                Id = pe.Id,
                PaymentNumber = pe.PaymentNumber ?? pe.Id.ToString().Substring(0, 8),
                PostingDate = pe.PostingDate,
                Amount = pe.PaidAmount,
                Status = pe.Status.ToString()
            }).ToList();
        return payments;
    }

    private async Task EnsureExchangeRateNotStaleAsync(string fromCurrency, string toCurrency)
    {
        var settingsRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<AccountsSettings, Guid>>();
        var settings = (await settingsRepo.GetQueryableAsync()).FirstOrDefault();
        if (settings == null || settings.AllowStaleExchangeRates) return;

        var (isStale, rateDate, daysSinceRate) = await _exchangeService.CheckStaleRateAsync(
            fromCurrency, toCurrency, settings.StaleDays);
        if (isStale)
        {
            throw new BusinessException(MyERPDomainErrorCodes.StaleExchangeRate)
                .WithData("fromCurrency", fromCurrency)
                .WithData("toCurrency", toCurrency)
                .WithData("daysSinceRate", daysSinceRate == int.MaxValue ? "no rate on record" : daysSinceRate.ToString())
                .WithData("rateDate", rateDate?.ToString("yyyy-MM-dd") ?? "never");
        }
    }
}

public class UnbilledDeliveryItemDto
{
    public Guid DeliveryNoteId { get; set; }
    public string DeliveryNoteNumber { get; set; } = null!;
    public DateTime DeliveryDate { get; set; }
    public Guid ItemId { get; set; }
    public string? ItemName { get; set; }
    public decimal Quantity { get; set; }
    public decimal Rate { get; set; }
    public string? Uom { get; set; }
    public Guid DeliveryNoteItemId { get; set; }
}

public class UnbilledOrderItemDto
{
    public Guid SalesOrderId { get; set; }
    public string OrderNumber { get; set; } = null!;
    public DateTime OrderDate { get; set; }
    public Guid ItemId { get; set; }
    public string? ItemName { get; set; }
    public decimal Quantity { get; set; }
    public decimal Rate { get; set; }
    public string? Uom { get; set; }
    public Guid SalesOrderItemId { get; set; }
}

