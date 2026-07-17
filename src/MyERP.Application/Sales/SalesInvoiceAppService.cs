using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
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
        return ObjectMapper.Map<SalesInvoice, SalesInvoiceDto>(invoice);
    }

    public async Task<List<PaymentScheduleDto>> GetPaymentScheduleAsync(Guid invoiceId)
    {
        var query = await _paymentScheduleRepository.GetQueryableAsync();
        return query
            .Where(e => e.ParentId == invoiceId && e.ParentType == "SalesInvoice")
            .OrderBy(e => e.DueDate)
            .Select(ObjectMapper.Map<Accounting.Entities.PaymentScheduleEntry, PaymentScheduleDto>).ToList();
    }

    public async Task<PagedResultDto<SalesInvoiceDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();

        if (input.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter.ToLower();
            query = query.Where(x => x.InvoiceNumber.ToLower().Contains(filter));
        }

        if (!string.IsNullOrWhiteSpace(input.Status) && Enum.TryParse<Core.DocumentStatus>(input.Status, true, out var status))
            query = query.Where(x => x.Status == status);

        var totalCount = query.Count();
        var invoices = query
            .OrderByDescending(x => x.IssueDate)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        return new PagedResultDto<SalesInvoiceDto>(
            totalCount,
            invoices.Select(ObjectMapper.Map<SalesInvoice, SalesInvoiceDto>).ToList());
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
        await _itemValidation.ValidateItemsForTransactionAsync(
            input.Items.Select(i => i.ItemId).ToArray());

        var invoiceNumber = await _numberGenerator.GenerateAsync("SalesInvoice", input.CompanyId);

        var invoice = new SalesInvoice(
            GuidGenerator.Create(),
            input.CompanyId,
            input.CustomerId,
            invoiceNumber,
            input.IssueDate);

        invoice.DueDate = input.DueDate;
        invoice.CurrencyCode = input.CurrencyCode;
        invoice.IsReturn = input.IsReturn;
        invoice.ReturnAgainstId = input.ReturnAgainstId;
        invoice.IsOpening = input.IsOpening;

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
            }
        }

        foreach (var item in input.Items)
        {
            invoice.AddItem(item.ItemId, item.Description, item.Quantity, item.UnitPrice, item.TaxAmount, item.Uom);
        }

        // Timesheet-in-SI auto-fetch: when project is set, populate unbilled timesheet entries
        // Per ERPNext Projects Settings.fetch_timesheet_in_sales_invoice
        invoice.ProjectId = input.ProjectId;
        if (input.ProjectId.HasValue && !invoice.IsReturn)
        {
            var tsRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Projects.Entities.Timesheet, Guid>>();
            var tsQuery = await tsRepo.GetQueryableAsync();
            var timesheets = tsQuery
                .Where(t => t.CompanyId == input.CompanyId
                    && t.Status == MyERP.Projects.Entities.TimesheetStatus.Submitted)
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

        await _repository.InsertAsync(invoice, autoSave: true);

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

        // Credit limit validation (enforced at SI submit per ERPNext rules, skip for returns)
        if (!invoice.IsReturn)
        {
            await _creditLimitService.ValidateCreditLimitAsync(invoice.CustomerId, invoice.GrandTotal);

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
            catch { /* Non-critical: don't block invoice submission for notification failure */ }

            // Selling price validation: selling rate must be >= valuation rate
            // Per ERPNext validate_selling_price (Selling Settings configurable: Stop/Warn)
            SalesInvoiceManager.ValidateSellingPrice(
                invoice.Items,
                itemId =>
                {
                    // Resolve valuation rate from latest stock balance
                    var balance = _valuationService
                        .GetCurrentBalanceAsync(itemId, invoice.WarehouseId ?? Guid.Empty)
                        .GetAwaiter().GetResult();
                    return balance.ValuationRate;
                },
                action: "Warn"); // Default to Warn — configurable via Selling Settings
        }

        // Server-side tax recalculation: fetch applicable tax rows and run cascade
        var taxQuery = await _taxRowRepository.GetQueryableAsync();
        var taxRows = taxQuery
            .Where(t => t.ParentType == "SalesInvoice" && t.ParentId == invoice.Id)
            .OrderBy(t => t.RowIndex)
            .ToList();

        if (taxRows.Any())
        {
            var items = invoice.Items.Select(i => new TransactionItem
            {
                ItemId = i.ItemId,
                Qty = i.Quantity,
                Rate = i.UnitPrice,
                NetAmount = i.LineTotal,
            }).ToList();

            // Calculate discount amount from percentage if applicable
            var discountAmt = invoice.DiscountAmount;
            if (invoice.AdditionalDiscountPercentage > 0 && discountAmt == 0)
            {
                var netForDiscount = items.Sum(i => i.NetAmount);
                discountAmt = Math.Round(netForDiscount * invoice.AdditionalDiscountPercentage / 100m, 2);
            }

            var totals = _taxService.Calculate(items, taxRows, invoice.ExchangeRate, discountAmt);
            invoice.NetTotal = totals.NetTotal;
            invoice.TaxAmount = totals.TotalTax;
            invoice.GrandTotal = totals.GrandTotal;
            invoice.BaseNetTotal = totals.BaseNetTotal;
            invoice.BaseTaxAmount = totals.BaseTotalTax;
            invoice.BaseGrandTotal = totals.BaseGrandTotal;
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
            foreach (var item in invoice.Items)
            {
                // Skip non-stock items (service items don't create SLE)
                var itemEntity = await itemRepo.FindAsync(item.ItemId);
                if (itemEntity != null && !itemEntity.MaintainStock)
                    continue;

                await _valuationService.CreateLedgerEntryAsync(
                    invoice.CompanyId, item.ItemId, invoice.WarehouseId.Value,
                    invoice.IssueDate, -item.Quantity, item.UnitPrice,
                    voucherType: "SalesInvoice", voucherId: invoice.Id,
                    tenantId: invoice.TenantId);

                await _binService.ApplyStockMovementAsync(
                    item.ItemId, invoice.WarehouseId.Value,
                    -item.Quantity, -(item.Quantity * item.UnitPrice), invoice.TenantId);

                // Trigger auto-reorder check after stock-out
                var autoReorder = LazyServiceProvider.LazyGetRequiredService<Inventory.DomainServices.AutoReorderService>();
                await autoReorder.CheckSingleItemAsync(
                    item.ItemId, invoice.WarehouseId.Value, invoice.CompanyId, invoice.TenantId);
            }
        }

        // Over-billing validation + SO BilledQty update (domain service)
        if (!invoice.IsReturn)
        {
            await _invoiceManager.ValidateOverBillingAsync(invoice);
            await _invoiceManager.UpdateLinkedOrderBillingAsync(invoice);
        }

        await _repository.UpdateAsync(invoice, autoSave: true);

        // Loyalty Points: earn points on non-return, non-consolidated invoices
        if (!invoice.IsReturn)
        {
            var customer = await _customerRepository.GetAsync(invoice.CustomerId);
            if (customer.LoyaltyProgramId.HasValue)
            {
                var loyaltyService = LazyServiceProvider.LazyGetRequiredService<LoyaltyPointService>();
                // Eligible amount = grand total (per ERPNext: grand_total - loyalty_amount - returned_amount)
                await loyaltyService.EarnPointsAsync(
                    customer.LoyaltyProgramId.Value, customer.Id, invoice.CompanyId,
                    invoice.GrandTotal, 0m, invoice.IssueDate,
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

        // Audit trail
        await _activityLog.LogSubmittedAsync("SalesInvoice", invoice.Id, invoice.CompanyId,
            invoice.InvoiceNumber, invoice.TenantId);

        return ObjectMapper.Map<SalesInvoice, SalesInvoiceDto>(invoice);
    }

    [Authorize(MyERPPermissions.SalesInvoices.Submit)]
    public async Task<SalesInvoiceDto> PostAsync(Guid id)
    {
        var invoice = await _repository.GetAsync(id);
        invoice.Post();

        // Resolve receivable account: invoice-specific → company default → throw
        var company = await _companyRepository.GetAsync(invoice.CompanyId);
        var receivableAccountId = invoice.DebitToAccountId != Guid.Empty
            ? invoice.DebitToAccountId
            : company.DefaultReceivableAccountId ?? Guid.Empty;

        if (receivableAccountId == Guid.Empty)
        {
            throw new Volo.Abp.BusinessException("MyERP:02001")
                .WithData("reason", "No receivable account configured. Set Default Receivable Account in Company settings.");
        }

        // Budget Level 3 validation: validate expense GL amounts against budget
        if (company.DefaultExpenseAccountId.HasValue)
        {
            var expenseItems = invoice.Items
                .Select(i => new Accounting.DomainServices.BudgetCheckItem(
                    company.DefaultExpenseAccountId.Value, i.Quantity * i.UnitPrice))
                .ToList();
            await _postingOrchestrator.ValidateBudgetOnPostingAsync(
                invoice.CompanyId, invoice.IssueDate, expenseItems, invoice.TenantId);
        }

        // GL posting + PLE creation for outstanding tracking
        await _postingOrchestrator.PostSalesInvoiceAsync(
            invoice,
            receivableAccountId: receivableAccountId,
            dueDate: invoice.DueDate);

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

        // Reverse PLE entries (GL reversal handled by cancellation JE)
        await _postingOrchestrator.ReversePleForDocumentAsync("SalesInvoice", invoice.Id);

        // Reverse stock if UpdateStock was used
        if (invoice.UpdateStock && invoice.WarehouseId.HasValue)
        {
            var itemRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Inventory.Entities.Item, Guid>>();
            foreach (var item in invoice.Items)
            {
                var itemEntity = await itemRepo.FindAsync(item.ItemId);
                if (itemEntity != null && !itemEntity.MaintainStock)
                    continue;

                await _valuationService.CreateLedgerEntryAsync(
                    invoice.CompanyId, item.ItemId, invoice.WarehouseId.Value,
                    invoice.IssueDate, item.Quantity, item.UnitPrice, // Positive = stock back in
                    voucherType: "SalesInvoice", voucherId: invoice.Id,
                    tenantId: invoice.TenantId);

                await _binService.ApplyStockMovementAsync(
                    item.ItemId, invoice.WarehouseId.Value,
                    item.Quantity, item.Quantity * item.UnitPrice, invoice.TenantId);
            }
        }

        // Reverse linked Sales Order BilledQty (domain service)
        await _invoiceManager.UpdateLinkedOrderBillingAsync(invoice, reverse: true);

        await _repository.UpdateAsync(invoice, autoSave: true);

        // Audit trail
        await _activityLog.LogCancelledAsync("SalesInvoice", invoice.Id, invoice.CompanyId,
            invoice.InvoiceNumber, "Posted", invoice.TenantId);

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
        amended.PaymentTermsTemplateId = original.PaymentTermsTemplateId;

        foreach (var item in original.Items)
        {
            amended.AddItem(item.ItemId, item.Description, item.Quantity, item.UnitPrice, item.TaxAmount, item.Uom);
        }

        await _repository.InsertAsync(amended, autoSave: true);
        return ObjectMapper.Map<SalesInvoice, SalesInvoiceDto>(amended);
    }
}
