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
using MyERP.Purchasing.Entities;
using MyERP.Sales;
using MyERP.Shared;
using MyERP.Tax.DomainServices;
using MyERP.Tax.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Purchasing;

[Authorize(MyERPPermissions.PurchaseInvoices.Default)]
public class PurchaseInvoiceAppService : ApplicationService, IPurchaseInvoiceAppService
{
    private readonly IRepository<PurchaseInvoice, Guid> _repository;
    private readonly IRepository<PurchaseOrder, Guid> _purchaseOrderRepository;
    private readonly IRepository<Supplier, Guid> _supplierRepository;
    private readonly IRepository<TransactionTaxRow, Guid> _taxRowRepository;
    private readonly IRepository<PaymentScheduleEntry, Guid> _paymentScheduleRepository;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IDocumentNumberGenerator _numberGenerator;
    private readonly DocumentPostingOrchestrator _postingOrchestrator;
    private readonly TaxesAndTotalsService _taxService;
    private readonly Inventory.DomainServices.StockValuationService _valuationService;
    private readonly Inventory.DomainServices.BinService _binService;
    private readonly DocumentActivityLogService _activityLog;
    private readonly ItemTransactionValidationService _itemValidation;
    private readonly TaxWithholdingService _taxWithholdingService;
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepository;

    public PurchaseInvoiceAppService(
        IRepository<PurchaseInvoice, Guid> repository,
        IRepository<PurchaseOrder, Guid> purchaseOrderRepository,
        IRepository<Supplier, Guid> supplierRepository,
        IRepository<TransactionTaxRow, Guid> taxRowRepository,
        IRepository<PaymentScheduleEntry, Guid> paymentScheduleRepository,
        IRepository<Company, Guid> companyRepository,
        IRepository<FiscalYear, Guid> fiscalYearRepository,
        IDocumentNumberGenerator numberGenerator,
        DocumentPostingOrchestrator postingOrchestrator,
        TaxesAndTotalsService taxService,
        Inventory.DomainServices.StockValuationService valuationService,
        Inventory.DomainServices.BinService binService,
        DocumentActivityLogService activityLog,
        ItemTransactionValidationService itemValidation,
        TaxWithholdingService taxWithholdingService)
    {
        _repository = repository;
        _purchaseOrderRepository = purchaseOrderRepository;
        _supplierRepository = supplierRepository;
        _taxRowRepository = taxRowRepository;
        _paymentScheduleRepository = paymentScheduleRepository;
        _companyRepository = companyRepository;
        _fiscalYearRepository = fiscalYearRepository;
        _numberGenerator = numberGenerator;
        _postingOrchestrator = postingOrchestrator;
        _taxService = taxService;
        _valuationService = valuationService;
        _binService = binService;
        _activityLog = activityLog;
        _itemValidation = itemValidation;
        _taxWithholdingService = taxWithholdingService;
    }

    public async Task<PurchaseInvoiceDto> GetAsync(Guid id)
    {
        var invoice = await _repository.GetAsync(id);
        return ObjectMapper.Map<PurchaseInvoice, PurchaseInvoiceDto>(invoice);
    }

    public async Task<List<PaymentScheduleDto>> GetPaymentScheduleAsync(Guid invoiceId)
    {
        var query = await _paymentScheduleRepository.GetQueryableAsync();
        return query
            .Where(e => e.ParentId == invoiceId && e.ParentType == "PurchaseInvoice")
            .OrderBy(e => e.DueDate)
            .Select(ObjectMapper.Map<Accounting.Entities.PaymentScheduleEntry, Sales.PaymentScheduleDto>).ToList();
    }

    public async Task<PagedResultDto<PurchaseInvoiceDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
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

        return new PagedResultDto<PurchaseInvoiceDto>(
            totalCount,
            invoices.Select(x => ObjectMapper.Map<PurchaseInvoice, PurchaseInvoiceDto>(x)).ToList());
    }

    [Authorize(MyERPPermissions.PurchaseInvoices.Create)]
    public async Task<PurchaseInvoiceDto> CreateAsync(CreatePurchaseInvoiceDto input)
    {
        // Input validation
        Check.NotDefaultOrNull<Guid>(input.CompanyId, nameof(input.CompanyId));
        Check.NotDefaultOrNull<Guid>(input.SupplierId, nameof(input.SupplierId));
        if (input.Items == null || input.Items.Count == 0)
            throw new Volo.Abp.BusinessException("MyERP:01007")
                .WithData("documentType", "Purchase Invoice");

        // Validate all items are active
        await _itemValidation.ValidateItemsForTransactionAsync(
            input.Items.Select(i => i.ItemId).ToArray());

        var invoiceNumber = await _numberGenerator.GenerateAsync("PurchaseInvoice", input.CompanyId);

        var invoice = new PurchaseInvoice(
            GuidGenerator.Create(),
            input.CompanyId,
            input.SupplierId,
            invoiceNumber,
            input.IssueDate);

        invoice.DueDate = input.DueDate;
        invoice.CurrencyCode = input.CurrencyCode;
        invoice.SupplierInvoiceNumber = input.SupplierInvoiceNumber;
        invoice.Notes = input.Notes;
        invoice.IsOpening = input.IsOpening;
        invoice.IsReturn = input.IsReturn;
        invoice.ReturnAgainstId = input.ReturnAgainstId;

        // Set party account (credit_to):
        // Returns: inherit from original invoice (ensures account match validation works)
        // Normal: company default payable account
        var companyForAcct = await _companyRepository.GetAsync(input.CompanyId);
        if (input.IsReturn && input.ReturnAgainstId.HasValue)
        {
            var originalInvoice = await _repository.GetAsync(input.ReturnAgainstId.Value);
            invoice.CreditToAccountId = originalInvoice.CreditToAccountId;
        }
        else if (companyForAcct.DefaultPayableAccountId.HasValue)
        {
            invoice.CreditToAccountId = companyForAcct.DefaultPayableAccountId.Value;
        }

        // Opening invoices: clear payment terms (accounting-only, no schedule)
        if (invoice.IsOpening)
        {
            invoice.PaymentTermsTemplateId = null;
        }

        // Payment terms resolution: explicit → supplier default → null (skip for opening)
        if (!invoice.IsOpening && input.PaymentTermsTemplateId.HasValue)
        {
            invoice.PaymentTermsTemplateId = input.PaymentTermsTemplateId;
        }
        else if (!invoice.IsOpening)
        {
            var supplierRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Purchasing.Entities.Supplier, Guid>>();
            var supplier = await supplierRepo.FindAsync(input.SupplierId);
            if (supplier?.DefaultPaymentTermsTemplateId.HasValue == true)
            {
                invoice.PaymentTermsTemplateId = supplier.DefaultPaymentTermsTemplateId;
            }
        }

        // Auto-fill billing address from supplier
        var partyDefaults = LazyServiceProvider.LazyGetRequiredService<Core.DomainServices.PartyDefaultsService>();
        var billingAddr = await partyDefaults.GetPrimaryAddressAsync("Supplier", input.SupplierId);
        if (billingAddr != null) invoice.BillingAddressId = billingAddr.Id;

        foreach (var item in input.Items)
        {
            invoice.AddItem(item.ItemId, item.Description, item.Quantity, item.UnitPrice, item.TaxAmount, item.Uom);
        }

        await _repository.InsertAsync(invoice, autoSave: true);

        // Auto-generate payment schedule from Payment Terms Template
        if (input.PaymentTermsTemplateId.HasValue && !input.DueDate.HasValue && !invoice.IsOpening)
        {
            var templateRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Accounting.Entities.PaymentTermsTemplate, Guid>>();
            var template = await templateRepo.GetAsync(input.PaymentTermsTemplateId.Value);
            var schedule = template.GenerateSchedule(invoice.IssueDate, invoice.GrandTotal);

            var scheduleRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Accounting.Entities.PaymentScheduleEntry, Guid>>();
            foreach (var entry in schedule)
            {
                await scheduleRepo.InsertAsync(new MyERP.Accounting.Entities.PaymentScheduleEntry(
                    GuidGenerator.Create(), "PurchaseInvoice", invoice.Id,
                    entry.DueDate, entry.InvoicePortion, entry.PaymentAmount));
            }

            // Set due date to the last scheduled due date
            invoice.DueDate = schedule.Max(s => s.DueDate);
            await _repository.UpdateAsync(invoice);
        }

        return ObjectMapper.Map<PurchaseInvoice, PurchaseInvoiceDto>(invoice);
    }

    [Authorize(MyERPPermissions.PurchaseInvoices.Submit)]
    public async Task<PurchaseInvoiceDto> SubmitAsync(Guid id)
    {
        var invoice = await _repository.GetAsync(id);

        // Buying controller validations via domain manager
        var piManager = LazyServiceProvider
            .LazyGetRequiredService<MyERP.Purchasing.DomainServices.PurchaseInvoiceManager>();

        // Temporal ordering: PI date must not precede linked PO dates
        await piManager.ValidatePostingDateWithPOAsync(invoice);

        // Asset return blocking (submitted assets on original doc)
        var assetRepo = LazyServiceProvider
            .LazyGetRequiredService<IRepository<MyERP.Assets.Entities.Asset, Guid>>();
        await piManager.ValidateAssetReturnAsync(invoice, assetRepo);

        // Return (Debit Note) validation — delegates to domain service (single source of truth)
        if (invoice.IsReturn)
        {
            await piManager.ValidateReturnAsync(invoice);
            // Block zero-qty items on stock-affecting returns (corrupts FIFO queue)
            MyERP.Purchasing.DomainServices.PurchaseInvoiceManager.ValidateReturnWithStockNoZeroQty(invoice);
        }

        // Supplier hold check — block PI if supplier is on hold for Invoices or All (skip for returns)
        if (!invoice.IsReturn)
        {
            var supplier = await _supplierRepository.GetAsync(invoice.SupplierId);
            if (supplier.HoldType == SupplierHoldType.All || supplier.HoldType == SupplierHoldType.Invoices)
            {
                throw new Volo.Abp.BusinessException("MyERP:04004")
                    .WithData("supplierName", supplier.Name)
                    .WithData("holdType", supplier.HoldType.ToString());
            }
        }

        // Server-side tax recalculation (mirrors Sales Invoice pattern)
        var taxQuery = await _taxRowRepository.GetQueryableAsync();
        var taxRows = taxQuery
            .Where(t => t.ParentType == "PurchaseInvoice" && t.ParentId == invoice.Id)
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

        // Tax Withholding — apply TDS if supplier has a tax withholding category
        if (!invoice.IsReturn)
        {
            var supplier = invoice.SupplierId != Guid.Empty
                ? await _supplierRepository.FindAsync(invoice.SupplierId)
                : null;
            if (supplier?.TaxWithholdingCategory != null)
            {
                // Resolve fiscal year for cumulative threshold
                var fyQuery = await _fiscalYearRepository.GetQueryableAsync();
                var fy = fyQuery
                    .Where(f => f.CompanyId == invoice.CompanyId
                        && f.StartDate <= invoice.IssueDate && f.EndDate >= invoice.IssueDate)
                    .FirstOrDefault();

                if (fy != null)
                {
                    var cumulative = await _taxWithholdingService.GetCumulativeInvoicedAsync(
                        invoice.SupplierId, fy.StartDate, fy.EndDate);
                    var previouslyDeducted = await _taxWithholdingService.GetPreviouslyDeductedAsync(
                        invoice.SupplierId, fy.StartDate, fy.EndDate);
                    var historicalExists = await _taxWithholdingService.HasHistoricalWithholdingAsync(
                        invoice.SupplierId, supplier.TaxWithholdingCategory, fy.StartDate, fy.EndDate);

                    // Use standard 10% rate as default (configurable via TaxWithholdingCategory in future)
                    var result = _taxWithholdingService.CalculateWithholding(
                        currentInvoiceNetTotal: invoice.NetTotal,
                        cumulativeInvoicedInFY: cumulative,
                        standardRate: 10m,
                        singleThreshold: 0m,
                        cumulativeThreshold: 0m,
                        taxOnExcessAmount: false,
                        previouslyDeductedTDS: previouslyDeducted);

                    // "Once deducted, always deducted" — force threshold crossed if historical exists
                    if (!result.ThresholdCrossed && historicalExists)
                    {
                        result = _taxWithholdingService.CalculateWithholding(
                            currentInvoiceNetTotal: invoice.NetTotal,
                            cumulativeInvoicedInFY: 0m,
                            standardRate: 10m,
                            singleThreshold: 0m,
                            cumulativeThreshold: 0m,
                            taxOnExcessAmount: false,
                            previouslyDeductedTDS: previouslyDeducted);
                    }

                    if (result.ThresholdCrossed && result.WithheldAmount > 0)
                    {
                        // Create withholding entry
                        var company = await _companyRepository.GetAsync(invoice.CompanyId);
                        var taxAccountId = company.DefaultExpenseAccountId ?? Guid.Empty;
                        await _taxWithholdingService.CreateEntryAsync(
                            invoice.CompanyId, invoice.SupplierId,
                            "PurchaseInvoice", invoice.Id, taxAccountId,
                            result, invoice.IssueDate, supplier.TaxWithholdingCategory,
                            invoice.TenantId);
                    }
                }
            }
        }

        invoice.Submit();

        // Debit Note: reduce original invoice outstanding (mirrors credit note behavior)
        if (invoice.IsReturn && invoice.ReturnAgainstId.HasValue)
        {
            var original = await _repository.GetAsync(invoice.ReturnAgainstId.Value);
            var returnAmount = Math.Abs(invoice.GrandTotal);
            original.AmountPaid += returnAmount;
            await _repository.UpdateAsync(original);
        }

        // Update Stock: create SLE entries for direct purchase (without PR)
        // Per DO-NOT: opening invoices with update_stock=true are blocked (accounting-only)
        if (invoice.IsOpening && invoice.UpdateStock)
        {
            throw new Volo.Abp.BusinessException("MyERP:01006")
                .WithData("documentType", "Purchase Invoice")
                .WithData("invoiceNumber", invoice.InvoiceNumber);
        }

        if (invoice.UpdateStock && invoice.WarehouseId.HasValue && !invoice.IsReturn)
        {
            var itemRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Inventory.Entities.Item, Guid>>();
            foreach (var item in invoice.Items)
            {
                // Skip non-stock items
                var itemEntity = await itemRepo.FindAsync(item.ItemId);
                if (itemEntity != null && !itemEntity.MaintainStock)
                    continue;

                await _valuationService.CreateLedgerEntryAsync(
                    invoice.CompanyId, item.ItemId, invoice.WarehouseId.Value,
                    invoice.IssueDate, item.Quantity, item.UnitPrice,
                    voucherType: "PurchaseInvoice", voucherId: invoice.Id,
                    tenantId: invoice.TenantId);

                await _binService.ApplyStockMovementAsync(
                    item.ItemId, invoice.WarehouseId.Value,
                    item.Quantity, item.Quantity * item.UnitPrice, invoice.TenantId);
            }
        }

        // Update linked Purchase Order BilledQty + fulfillment status
        if (!invoice.IsReturn)
        {
            var poItemIds = invoice.Items
                .Where(i => i.PurchaseOrderItemId.HasValue)
                .Select(i => i.PurchaseOrderItemId!.Value)
                .Distinct()
                .ToList();

            if (poItemIds.Any())
            {
                var orderQuery = await _purchaseOrderRepository.GetQueryableAsync();
                var affectedOrders = orderQuery
                    .Where(po => po.Items.Any(poi => poItemIds.Contains(poi.Id)))
                    .ToList();

                // Over-billing validation: billed qty cannot exceed ordered qty
                foreach (var po in affectedOrders)
                {
                    foreach (var piItem in invoice.Items.Where(i => i.PurchaseOrderItemId.HasValue))
                    {
                        var poItem = po.Items.FirstOrDefault(i => i.Id == piItem.PurchaseOrderItemId!.Value);
                        if (poItem != null && (poItem.BilledQty + piItem.Quantity) > poItem.Quantity)
                        {
                            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.OverBilling)
                                .WithData("item", piItem.Description ?? piItem.ItemId.ToString())
                                .WithData("ordered", poItem.Quantity)
                                .WithData("billed", poItem.BilledQty)
                                .WithData("attempted", piItem.Quantity);
                        }
                    }
                }

                // Update BilledQty
                foreach (var po in affectedOrders)
                {
                    foreach (var piItem in invoice.Items.Where(i => i.PurchaseOrderItemId.HasValue))
                    {
                        var poItem = po.Items.FirstOrDefault(i => i.Id == piItem.PurchaseOrderItemId!.Value);
                        if (poItem != null)
                        {
                            poItem.BilledQty += piItem.Quantity;
                        }
                    }
                    po.UpdateFulfillmentStatus();
                    await _purchaseOrderRepository.UpdateAsync(po);
                }
            }
        }

        await _repository.UpdateAsync(invoice, autoSave: true);

        // Audit trail
        await _activityLog.LogSubmittedAsync("PurchaseInvoice", invoice.Id, invoice.CompanyId,
            invoice.InvoiceNumber, invoice.TenantId);

        return ObjectMapper.Map<PurchaseInvoice, PurchaseInvoiceDto>(invoice);
    }

    [Authorize(MyERPPermissions.PurchaseInvoices.Submit)]
    public async Task<PurchaseInvoiceDto> PostAsync(Guid id)
    {
        var invoice = await _repository.GetAsync(id);
        invoice.Post();

        // Resolve payable account: invoice-specific → company default → throw
        var company = await _companyRepository.GetAsync(invoice.CompanyId);
        var payableAccountId = invoice.CreditToAccountId != Guid.Empty
            ? invoice.CreditToAccountId
            : company.DefaultPayableAccountId ?? Guid.Empty;

        if (payableAccountId == Guid.Empty)
        {
            throw new Volo.Abp.BusinessException("MyERP:02001")
                .WithData("reason", "No payable account configured. Set Default Payable Account in Company settings.");
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
        await _postingOrchestrator.PostPurchaseInvoiceAsync(
            invoice,
            payableAccountId: payableAccountId,
            dueDate: invoice.DueDate);

        await _repository.UpdateAsync(invoice, autoSave: true);

        // Audit trail
        await _activityLog.LogPostedAsync("PurchaseInvoice", invoice.Id, invoice.CompanyId,
            invoice.InvoiceNumber, invoice.TenantId);

        return ObjectMapper.Map<PurchaseInvoice, PurchaseInvoiceDto>(invoice);
    }

    [Authorize(MyERPPermissions.PurchaseInvoices.Cancel)]
    public async Task<PurchaseInvoiceDto> CancelAsync(Guid id)
    {
        var invoice = await _repository.GetAsync(id);

        // Guard: cannot cancel invoices with payments applied
        if (invoice.AmountPaid > 0)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.CannotCancelWithPayments)
                .WithData("documentType", "Purchase Invoice")
                .WithData("amountPaid", invoice.AmountPaid);
        }

        invoice.Cancel();

        // Reverse PLE entries
        await _postingOrchestrator.ReversePleForDocumentAsync("PurchaseInvoice", invoice.Id);

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
                    invoice.IssueDate, -item.Quantity, item.UnitPrice, // Negative = stock out (reversal)
                    voucherType: "PurchaseInvoice", voucherId: invoice.Id,
                    tenantId: invoice.TenantId);

                await _binService.ApplyStockMovementAsync(
                    item.ItemId, invoice.WarehouseId.Value,
                    -item.Quantity, -(item.Quantity * item.UnitPrice), invoice.TenantId);
            }
        }

        // Reverse linked Purchase Order BilledQty
        var poItemIds = invoice.Items
            .Where(i => i.PurchaseOrderItemId.HasValue)
            .Select(i => i.PurchaseOrderItemId!.Value)
            .Distinct()
            .ToList();

        if (poItemIds.Any())
        {
            var orderQuery = await _purchaseOrderRepository.GetQueryableAsync();
            var affectedOrders = orderQuery
                .Where(po => po.Items.Any(poi => poItemIds.Contains(poi.Id)))
                .ToList();

            foreach (var po in affectedOrders)
            {
                foreach (var piItem in invoice.Items.Where(i => i.PurchaseOrderItemId.HasValue))
                {
                    var poItem = po.Items.FirstOrDefault(i => i.Id == piItem.PurchaseOrderItemId!.Value);
                    if (poItem != null)
                    {
                        poItem.BilledQty = Math.Max(0, poItem.BilledQty - piItem.Quantity);
                    }
                }
                po.UpdateFulfillmentStatus();
                await _purchaseOrderRepository.UpdateAsync(po);
            }
        }

        await _repository.UpdateAsync(invoice, autoSave: true);

        // Audit trail
        await _activityLog.LogCancelledAsync("PurchaseInvoice", invoice.Id, invoice.CompanyId,
            invoice.InvoiceNumber, "Posted", invoice.TenantId);

        return ObjectMapper.Map<PurchaseInvoice, PurchaseInvoiceDto>(invoice);
    }

    /// <summary>
    /// Write off the outstanding amount on a posted purchase invoice.
    /// Used for small differences (e.g., supplier won't collect RM 0.50 rounding).
    /// </summary>
    [Authorize(MyERPPermissions.PurchaseInvoices.Submit)]
    public async Task<PurchaseInvoiceDto> WriteOffAsync(Guid id)
    {
        var invoice = await _repository.GetAsync(id);

        if (invoice.Status != Core.DocumentStatus.Posted)
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        if (invoice.OutstandingAmount <= 0)
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.InvoiceAlreadySettled);

        invoice.AmountPaid = invoice.GrandTotal;
        await _postingOrchestrator.ReversePleForDocumentAsync("PurchaseInvoice", invoice.Id);
        await _repository.UpdateAsync(invoice, autoSave: true);
        return ObjectMapper.Map<PurchaseInvoice, PurchaseInvoiceDto>(invoice);
    }

    /// <summary>
    /// Amend a cancelled Purchase Invoice — creates a new draft copy with amendment link.
    /// Per DO-NOT: only Cancelled documents can be amended.
    /// </summary>
    [Authorize(MyERPPermissions.PurchaseInvoices.Create)]
    public async Task<PurchaseInvoiceDto> AmendAsync(Guid id)
    {
        var original = await _repository.GetAsync(id);
        var amendService = LazyServiceProvider.LazyGetRequiredService<Core.DomainServices.DocumentAmendmentService>();

        amendService.ValidateCanAmend(original.Status);
        var newNumber = amendService.GenerateAmendedNumber(original.InvoiceNumber, original.AmendmentIndex + 1);

        var amended = new PurchaseInvoice(
            GuidGenerator.Create(),
            original.CompanyId,
            original.SupplierId,
            newNumber,
            DateTime.UtcNow.Date);

        amended.AmendedFromId = original.Id;
        amended.AmendmentIndex = original.AmendmentIndex + 1;
        amended.CurrencyCode = original.CurrencyCode;
        amended.SupplierInvoiceNumber = original.SupplierInvoiceNumber;
        amended.PaymentTermsTemplateId = original.PaymentTermsTemplateId;
        amended.Notes = original.Notes;

        foreach (var item in original.Items)
        {
            amended.AddItem(item.ItemId, item.Description, item.Quantity, item.UnitPrice, item.TaxAmount, item.Uom);
        }

        await _repository.InsertAsync(amended, autoSave: true);
        return ObjectMapper.Map<PurchaseInvoice, PurchaseInvoiceDto>(amended);
    }
}
