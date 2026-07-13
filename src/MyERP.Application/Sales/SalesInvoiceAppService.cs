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
        ItemTransactionValidationService itemValidation)
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
    }

    public async Task<SalesInvoiceDto> GetAsync(Guid id)
    {
        var invoice = await _repository.GetAsync(id);
        return MapToDto(invoice);
    }

    public async Task<List<PaymentScheduleDto>> GetPaymentScheduleAsync(Guid invoiceId)
    {
        var query = await _paymentScheduleRepository.GetQueryableAsync();
        return query
            .Where(e => e.ParentId == invoiceId && e.ParentType == "SalesInvoice")
            .OrderBy(e => e.DueDate)
            .Select(e => new PaymentScheduleDto
            {
                Id = e.Id,
                DueDate = e.DueDate,
                InvoicePortion = e.InvoicePortion,
                PaymentAmount = e.PaymentAmount,
                PaidAmount = e.PaidAmount,
                Outstanding = e.Outstanding,
            }).ToList();
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
            invoices.Select(MapToDto).ToList());
    }

    [Authorize(MyERPPermissions.SalesInvoices.Create)]
    public async Task<SalesInvoiceDto> CreateAsync(CreateSalesInvoiceDto input)
    {
        // Input validation
        Check.NotDefaultOrNull<Guid>(input.CompanyId, nameof(input.CompanyId));
        Check.NotDefaultOrNull<Guid>(input.CustomerId, nameof(input.CustomerId));
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
        invoice.PaymentTermsTemplateId = input.PaymentTermsTemplateId;

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

        return MapToDto(invoice);
    }

    [Authorize(MyERPPermissions.SalesInvoices.Submit)]
    public async Task<SalesInvoiceDto> SubmitAsync(Guid id)
    {
        var invoice = await _repository.GetAsync(id);

        // Return document validation
        if (invoice.IsReturn)
        {
            // Returns must have negative quantities
            foreach (var item in invoice.Items)
            {
                if (item.Quantity > 0)
                {
                    throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ReturnQtyMustBeNegative)
                        .WithData("item", item.Description ?? item.ItemId.ToString());
                }
            }

            // Must reference an original invoice
            if (!invoice.ReturnAgainstId.HasValue)
            {
                throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ReturnMustReferenceOriginal);
            }

            // Exchange rate must match original document
            var original = await _repository.GetAsync(invoice.ReturnAgainstId.Value);
            if (invoice.ExchangeRate != original.ExchangeRate)
            {
                throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ReturnExchangeRateMismatch)
                    .WithData("expected", original.ExchangeRate)
                    .WithData("actual", invoice.ExchangeRate);
            }

            // Return qty cannot exceed original qty (absolute value comparison)
            foreach (var returnItem in invoice.Items)
            {
                var matchingOriginal = original.Items
                    .FirstOrDefault(i => i.ItemId == returnItem.ItemId);
                if (matchingOriginal != null)
                {
                    if (Math.Abs(returnItem.Quantity) > matchingOriginal.Quantity)
                    {
                        throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ReturnQtyExceedsOriginal)
                            .WithData("item", returnItem.Description ?? returnItem.ItemId.ToString())
                            .WithData("maxQty", matchingOriginal.Quantity)
                            .WithData("returnQty", Math.Abs(returnItem.Quantity));
                    }
                }
            }
        }

        // Credit limit validation (enforced at SI submit per ERPNext rules, skip for returns)
        if (!invoice.IsReturn)
        {
            await _creditLimitService.ValidateCreditLimitAsync(invoice.CustomerId, invoice.GrandTotal);
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

        // Credit Note: reduce original invoice outstanding
        if (invoice.IsReturn && invoice.ReturnAgainstId.HasValue)
        {
            var original = await _repository.GetAsync(invoice.ReturnAgainstId.Value);
            var returnAmount = Math.Abs(invoice.GrandTotal);
            original.AmountPaid += returnAmount;
            await _repository.UpdateAsync(original);
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

        // Update linked Sales Order BilledQty + fulfillment status
        if (!invoice.IsReturn)
        {
            var soIds = invoice.Items
                .Where(i => i.SalesOrderItemId.HasValue)
                .Select(i => i.SalesOrderItemId!.Value)
                .Distinct()
                .ToList();

            if (soIds.Any())
            {
                var orderQuery = await _salesOrderRepository.GetQueryableAsync();
                var affectedOrders = orderQuery
                    .Where(so => so.Items.Any(soi => soIds.Contains(soi.Id)))
                    .ToList();

                // Over-billing validation: billed qty cannot exceed ordered qty
                foreach (var so in affectedOrders)
                {
                    foreach (var siItem in invoice.Items.Where(i => i.SalesOrderItemId.HasValue))
                    {
                        var soItem = so.Items.FirstOrDefault(i => i.Id == siItem.SalesOrderItemId!.Value);
                        if (soItem != null && (soItem.BilledQty + siItem.Quantity) > soItem.Quantity)
                        {
                            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.OverBilling)
                                .WithData("item", siItem.Description ?? siItem.ItemId.ToString())
                                .WithData("ordered", soItem.Quantity)
                                .WithData("billed", soItem.BilledQty)
                                .WithData("attempted", siItem.Quantity);
                        }
                    }
                }

                // Update BilledQty
                foreach (var so in affectedOrders)
                {
                    foreach (var siItem in invoice.Items.Where(i => i.SalesOrderItemId.HasValue))
                    {
                        var soItem = so.Items.FirstOrDefault(i => i.Id == siItem.SalesOrderItemId!.Value);
                        if (soItem != null)
                        {
                            soItem.BilledQty += siItem.Quantity;
                        }
                    }
                    so.UpdateFulfillmentStatus();
                    await _salesOrderRepository.UpdateAsync(so);
                }
            }
        }

        await _repository.UpdateAsync(invoice, autoSave: true);

        // Audit trail
        await _activityLog.LogSubmittedAsync("SalesInvoice", invoice.Id, invoice.CompanyId,
            invoice.InvoiceNumber, invoice.TenantId);

        return MapToDto(invoice);
    }

    [Authorize(MyERPPermissions.SalesInvoices.Submit)]
    public async Task<SalesInvoiceDto> PostAsync(Guid id)
    {
        var invoice = await _repository.GetAsync(id);
        invoice.Post();

        // Resolve receivable account from company defaults
        var company = await _companyRepository.GetAsync(invoice.CompanyId);
        var receivableAccountId = company.DefaultReceivableAccountId ?? invoice.CompanyId;

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

        return MapToDto(invoice);
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

        // Reverse linked Sales Order BilledQty
        var soItemIds = invoice.Items
            .Where(i => i.SalesOrderItemId.HasValue)
            .Select(i => i.SalesOrderItemId!.Value)
            .Distinct()
            .ToList();

        if (soItemIds.Any())
        {
            var orderQuery = await _salesOrderRepository.GetQueryableAsync();
            var affectedOrders = orderQuery
                .Where(so => so.Items.Any(soi => soItemIds.Contains(soi.Id)))
                .ToList();

            foreach (var so in affectedOrders)
            {
                foreach (var siItem in invoice.Items.Where(i => i.SalesOrderItemId.HasValue))
                {
                    var soItem = so.Items.FirstOrDefault(i => i.Id == siItem.SalesOrderItemId!.Value);
                    if (soItem != null)
                    {
                        soItem.BilledQty = Math.Max(0, soItem.BilledQty - siItem.Quantity);
                    }
                }
                so.UpdateFulfillmentStatus();
                await _salesOrderRepository.UpdateAsync(so);
            }
        }

        await _repository.UpdateAsync(invoice, autoSave: true);

        // Audit trail
        await _activityLog.LogCancelledAsync("SalesInvoice", invoice.Id, invoice.CompanyId,
            invoice.InvoiceNumber, "Posted", invoice.TenantId);

        return MapToDto(invoice);
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
        return MapToDto(invoice);
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
        return MapToDto(amended);
    }

    private SalesInvoiceDto MapToDto(SalesInvoice invoice)
    {
        return new SalesInvoiceDto
        {
            Id = invoice.Id,
            CompanyId = invoice.CompanyId,
            InvoiceNumber = invoice.InvoiceNumber,
            IssueDate = invoice.IssueDate,
            DueDate = invoice.DueDate,
            CustomerId = invoice.CustomerId,
            CurrencyCode = invoice.CurrencyCode,
            ExchangeRate = invoice.ExchangeRate,
            NetTotal = invoice.NetTotal,
            TaxAmount = invoice.TaxAmount,
            GrandTotal = invoice.GrandTotal,
            AmountPaid = invoice.AmountPaid,
            OutstandingAmount = invoice.OutstandingAmount,
            BaseNetTotal = invoice.BaseNetTotal,
            BaseTaxAmount = invoice.BaseTaxAmount,
            BaseGrandTotal = invoice.BaseGrandTotal,
            BaseOutstandingAmount = invoice.BaseOutstandingAmount,
            Status = invoice.Status.ToString(),
            EInvoiceStatus = invoice.EInvoiceStatus.ToString(),
            LhdnUuid = invoice.LhdnUuid,
            IsReturn = invoice.IsReturn,
            ReturnAgainstId = invoice.ReturnAgainstId,
            AmendedFromId = invoice.AmendedFromId,
            AmendmentIndex = invoice.AmendmentIndex,
            Items = invoice.Items.Select(i => new SalesInvoiceItemDto
            {
                Id = i.Id,
                ItemId = i.ItemId,
                Description = i.Description,
                Uom = i.Uom,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                TaxAmount = i.TaxAmount,
                LineTotal = i.LineTotal
            }).ToList()
        };
    }
}
