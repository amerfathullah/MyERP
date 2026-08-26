using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using MyERP.Purchasing.DomainServices;
using MyERP.Sales;
using MyERP.Shared;
using MyERP.Tax.DomainServices;
using MyERP.Tax.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Settings;

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
    private readonly IRepository<Item, Guid> _itemRepository;

    public PurchaseInvoiceAppService(
        IRepository<PurchaseInvoice, Guid> repository,
        IRepository<PurchaseOrder, Guid> purchaseOrderRepository,
        IRepository<Supplier, Guid> supplierRepository,
        IRepository<TransactionTaxRow, Guid> taxRowRepository,
        IRepository<PaymentScheduleEntry, Guid> paymentScheduleRepository,
        IRepository<Company, Guid> companyRepository,
        IRepository<FiscalYear, Guid> fiscalYearRepository,
        IRepository<Item, Guid> itemRepository,
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
        _itemRepository = itemRepository;
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
        var dto = ObjectMapper.Map<PurchaseInvoice, PurchaseInvoiceDto>(invoice);

        if (invoice.InterCompanyInvoiceId.HasValue)
        {
            var siRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.SalesInvoice, Guid>>();
            var si = await siRepo.FindAsync(invoice.InterCompanyInvoiceId.Value);
            if (si != null)
            {
                dto.InterCompanyInvoiceNumber = si.InvoiceNumber;
                var company = await _companyRepository.FindAsync(si.CompanyId);
                dto.InterCompanyCompanyName = company?.Name;
            }
        }

        return dto;
    }

    public async Task<List<PaymentScheduleDto>> GetPaymentScheduleAsync(Guid invoiceId)
    {
        var query = await _paymentScheduleRepository.GetQueryableAsync();
        return query
            .Where(e => e.ParentId == invoiceId && e.ParentType == "PurchaseInvoice")
            .OrderBy(e => e.DueDate)
            .Select(ObjectMapper.Map<Accounting.Entities.PaymentScheduleEntry, Sales.PaymentScheduleDto>).ToList();
    }

    /// <summary>
    /// Returns tax withholding (TDS/WHT) entries for a purchase invoice.
    /// Per Malaysia Section 107A: withholding tax on payments to non-resident suppliers.
    /// Per ERPNext: PI detail shows withholding entries for audit + compliance.
    /// </summary>
    public async Task<List<TaxWithholdingEntryDto>> GetTaxWithholdingEntriesAsync(Guid invoiceId)
    {
        var tweRepo = LazyServiceProvider
            .LazyGetRequiredService<IRepository<Tax.Entities.TaxWithholdingEntry, Guid>>();
        var query = await tweRepo.GetQueryableAsync();
        return query
            .Where(e => e.VoucherId == invoiceId && e.VoucherType == "PurchaseInvoice")
            .OrderByDescending(e => e.PostingDate)
            .Select(e => new TaxWithholdingEntryDto
            {
                Id = e.Id,
                TaxCategory = e.TaxCategory,
                WithholdingRate = e.WithholdingRate,
                TaxableAmount = e.TaxableAmount,
                WithheldAmount = e.WithheldAmount,
                PostingDate = e.PostingDate,
                HasLDC = e.HasLDC,
                LdcRate = e.LdcRate,
                CertificateNumber = e.CertificateNumber,
                Status = e.Status.ToString()
            }).ToList();
    }

    /// <summary>
    /// Real-time duplicate supplier invoice check (advisory, non-blocking).
    /// Called on keyup as user types supplier invoice number — warns before submit.
    /// Per ERPNext: FY-scoped uniqueness per (supplier, company, invoice_number).
    /// </summary>
    public async Task<DuplicateInvoiceCheckResultDto> CheckDuplicateSupplierInvoiceAsync(
        Guid supplierId, Guid companyId, string supplierInvoiceNumber, Guid? excludeId = null)
    {
        if (string.IsNullOrWhiteSpace(supplierInvoiceNumber))
            return new DuplicateInvoiceCheckResultDto { IsDuplicate = false };

        var query = await _repository.GetQueryableAsync();
        var duplicate = query
            .Where(pi =>
                pi.SupplierId == supplierId
                && pi.CompanyId == companyId
                && pi.SupplierInvoiceNumber == supplierInvoiceNumber
                && pi.Status != Core.DocumentStatus.Cancelled
                && (!excludeId.HasValue || pi.Id != excludeId.Value))
            .Select(pi => new { pi.Id, pi.InvoiceNumber, pi.IssueDate, pi.GrandTotal })
            .FirstOrDefault();

        if (duplicate == null)
            return new DuplicateInvoiceCheckResultDto { IsDuplicate = false };

        return new DuplicateInvoiceCheckResultDto
        {
            IsDuplicate = true,
            ExistingInvoiceId = duplicate.Id,
            ExistingInvoiceNumber = duplicate.InvoiceNumber,
            ExistingInvoiceDate = duplicate.IssueDate,
            ExistingInvoiceAmount = duplicate.GrandTotal,
        };
    }

    public async Task<List<ThreeWayMatchingItemDto>> GetThreeWayMatchingAsync(Guid invoiceId)
    {
        var pi = await _repository.GetAsync(invoiceId);
        var result = new List<ThreeWayMatchingItemDto>();

        // Collect all linked PO item IDs to batch-query PO + PR data
        var poItemIds = pi.Items
            .Where(i => i.PurchaseOrderItemId.HasValue)
            .Select(i => i.PurchaseOrderItemId!.Value)
            .Distinct()
            .ToList();

        // Batch-resolve PO items (ordered qty + rate)
        Dictionary<Guid, (decimal OrderedQty, decimal Rate)> poItemData = new();
        if (poItemIds.Count > 0)
        {
            var poQuery = await _purchaseOrderRepository.GetQueryableAsync();
            var poItems = poQuery
                .SelectMany(po => po.Items)
                .Where(i => poItemIds.Contains(i.Id))
                .Select(i => new { i.Id, i.Quantity, i.UnitPrice })
                .ToList();
            poItemData = poItems.ToDictionary(i => i.Id, i => (i.Quantity, i.UnitPrice));
        }

        // Batch-resolve PR items (received qty) via LazyServiceProvider
        var prItemIds = pi.Items
            .Where(i => i.PurchaseReceiptItemId.HasValue)
            .Select(i => i.PurchaseReceiptItemId!.Value)
            .Distinct()
            .ToList();

        Dictionary<Guid, decimal> prReceivedQty = new();
        if (prItemIds.Count > 0)
        {
            var prRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PurchaseReceipt, Guid>>();
            var prQuery = await prRepo.GetQueryableAsync();
            var prItems = prQuery
                .SelectMany(pr => pr.Items)
                .Where(i => prItemIds.Contains(i.Id))
                .Select(i => new { i.Id, i.Quantity })
                .ToList();
            prReceivedQty = prItems.ToDictionary(i => i.Id, i => i.Quantity);
        }

        foreach (var item in pi.Items)
        {
            var dto = new ThreeWayMatchingItemDto
            {
                PiItemId = item.Id,
                ItemDescription = item.Description,
                BilledQty = item.Quantity,
                BilledRate = item.UnitPrice,
            };

            if (item.PurchaseOrderItemId.HasValue && poItemData.TryGetValue(item.PurchaseOrderItemId.Value, out var poData))
            {
                dto.OrderedQty = poData.OrderedQty;
                dto.OrderedRate = poData.Rate;
                dto.RateVariance = poData.Rate - item.UnitPrice;
                dto.HasRateDiscrepancy = Math.Abs(dto.RateVariance.Value) > 0.01m;
            }

            if (item.PurchaseReceiptItemId.HasValue && prReceivedQty.TryGetValue(item.PurchaseReceiptItemId.Value, out var receivedQty))
            {
                dto.ReceivedQty = receivedQty;
                dto.QtyVariance = receivedQty - item.Quantity;
                dto.HasQtyDiscrepancy = Math.Abs(dto.QtyVariance.Value) > 0.01m;
            }

            // Determine match level
            if (item.PurchaseOrderItemId.HasValue && item.PurchaseReceiptItemId.HasValue)
                dto.MatchLevel = "3-Way";
            else if (item.PurchaseOrderItemId.HasValue)
                dto.MatchLevel = "2-Way";
            else
                dto.MatchLevel = "Direct";

            result.Add(dto);
        }

        return result;
    }

    public async Task<PagedResultDto<PurchaseInvoiceDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
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

        var dtos = invoices.Select(x => ObjectMapper.Map<PurchaseInvoice, PurchaseInvoiceDto>(x)).ToList();

        // Batch-resolve supplier names (avoid N+1)
        var supplierIds = invoices.Select(i => i.SupplierId).Distinct().ToList();
        if (supplierIds.Count > 0)
        {
            var supplierQuery = await _supplierRepository.GetQueryableAsync();
            var supplierNames = supplierQuery
                .Where(s => supplierIds.Contains(s.Id))
                .Select(s => new { s.Id, s.Name })
                .ToDictionary(s => s.Id, s => s.Name);

            foreach (var dto in dtos)
            {
                if (supplierNames.TryGetValue(dto.SupplierId, out var name))
                    dto.SupplierName = name;
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

        // Calculate 3-way matching status + ready-for-payment per invoice
        foreach (var (invoice, dto) in invoices.Zip(dtos))
        {
            var matchStatus = DetermineMatchingStatus(invoice);
            dto.MatchingStatus = matchStatus;
            dto.IsReadyForPayment = dto.Status == "Posted"
                && dto.OutstandingAmount > 0.01m
                && !dto.IsReturn
                && !invoice.IsBlocked
                && matchStatus is "FullyMatched" or "DirectPurchase";
        }

        return new PagedResultDto<PurchaseInvoiceDto>(totalCount, dtos);
    }

    /// <summary>
    /// Determines 3-way matching status: FullyMatched (PO+PR+PI all linked), PartiallyMatched (PO linked but not all PR),
    /// Unmatched (PO linked but no PR), DirectPurchase (no PO reference).
    /// </summary>
    private static string DetermineMatchingStatus(PurchaseInvoice invoice)
    {
        if (invoice.Items == null || invoice.Items.Count == 0) return "DirectPurchase";

        var hasAnyPoLink = invoice.Items.Any(i => i.PurchaseOrderItemId.HasValue);
        if (!hasAnyPoLink) return "DirectPurchase";

        var hasAnyPrLink = invoice.Items.Any(i => i.PurchaseReceiptItemId.HasValue);
        if (!hasAnyPrLink) return "Unmatched";

        var allHavePrLink = invoice.Items.All(i =>
            !i.PurchaseOrderItemId.HasValue || i.PurchaseReceiptItemId.HasValue);

        return allHavePrLink ? "FullyMatched" : "PartiallyMatched";
    }

    /// <summary>
    /// Returns aggregate KPI summary: total payable, overdue, monthly spend.
    /// </summary>
    public async Task<PurchaseInvoiceListSummaryDto> GetListSummaryAsync(Guid? companyId)
    {
        var queryable = await _repository.GetQueryableAsync();
        var posted = queryable.Where(i => i.Status == Core.DocumentStatus.Posted && !i.IsReturn);

        if (companyId.HasValue)
            posted = posted.Where(i => i.CompanyId == companyId.Value);

        var today = DateTime.UtcNow.Date;
        var monthStart = new DateTime(today.Year, today.Month, 1);

        var totalPayable = posted
            .Select(i => i.GrandTotal - i.AmountPaid - i.WriteOffAmount - i.TotalAdvance)
            .Where(o => o > 0)
            .Sum();

        var overdueInvoices = posted
            .Where(i => i.DueDate != null && i.DueDate < today)
            .Where(i => (i.GrandTotal - i.AmountPaid - i.WriteOffAmount - i.TotalAdvance) > 0.01m);
        var overdueCount = overdueInvoices.Count();
        var overdueAmount = overdueInvoices
            .Select(i => i.GrandTotal - i.AmountPaid - i.WriteOffAmount - i.TotalAdvance)
            .Sum();

        var monthlyInvoices = posted.Where(i => i.IssueDate >= monthStart);
        var monthlySpend = monthlyInvoices.Sum(i => i.GrandTotal);
        var monthlyCount = monthlyInvoices.Count();

        var postedCount = posted.Count();

        return new PurchaseInvoiceListSummaryDto
        {
            TotalPayable = Math.Max(0, totalPayable),
            OverdueCount = overdueCount,
            OverdueAmount = Math.Max(0, overdueAmount),
            MonthlySpend = monthlySpend,
            MonthlyInvoiceCount = monthlyCount,
            PostedInvoiceCount = postedCount,
        };
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
        var piItemIds = input.Items.Select(i => i.ItemId).ToArray();
        await _itemValidation.ValidateItemsForTransactionAsync(piItemIds);

        var companyRestriction = LazyServiceProvider.LazyGetRequiredService<Core.DomainServices.CompanyRestrictionValidationService>();
        await companyRestriction.ValidateTransactionCompanyAsync("PurchaseInvoice", input.CompanyId, piItemIds, supplierIds: new[] { input.SupplierId });

        var supplierForStatus = await _supplierRepository.GetAsync(input.SupplierId);
        LazyServiceProvider.LazyGetRequiredService<Core.DomainServices.PartyValidationService>()
            .ValidatePartyStatus("Supplier", isFrozen: false, isDisabled: !supplierForStatus.IsActive, supplierForStatus.Name);

        var invoiceNumber = await _numberGenerator.GenerateAsync("PurchaseInvoice", input.CompanyId);

        var invoice = new PurchaseInvoice(
            GuidGenerator.Create(),
            input.CompanyId,
            input.SupplierId,
            invoiceNumber,
            input.IssueDate);

        invoice.DueDate = input.DueDate;
        invoice.CurrencyCode = input.CurrencyCode;

        // Per ERPNext: Price List defaults from the supplier's own default when not given explicitly.
        invoice.PriceListId = input.PriceListId
            ?? (await _supplierRepository.FindAsync(input.SupplierId))?.DefaultPriceListId;

        invoice.SupplierInvoiceNumber = input.SupplierInvoiceNumber;
        invoice.Notes = input.Notes;
        invoice.IsOpening = input.IsOpening;
        invoice.IsReturn = input.IsReturn;
        invoice.IsSubcontracted = input.IsSubcontracted;
        invoice.ReturnAgainstId = input.ReturnAgainstId;
        invoice.UpdateStock = input.UpdateStock;
        invoice.WarehouseId = input.WarehouseId;
        invoice.CostCenterId = input.CostCenterId;
        invoice.ProjectId = input.ProjectId;

        // Duplicate supplier invoice detection (early — block before DB insert)
        if (!invoice.IsReturn && !string.IsNullOrWhiteSpace(input.SupplierInvoiceNumber))
        {
            var piMgr = LazyServiceProvider
                .LazyGetRequiredService<MyERP.Purchasing.DomainServices.PurchaseInvoiceManager>();
            await piMgr.ValidateNoDuplicateSupplierInvoiceAsync(
                input.SupplierId, input.CompanyId, input.SupplierInvoiceNumber);
        }

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

        // Opening invoices & returns: clear payment terms (gotcha #380)
        if (invoice.IsOpening || invoice.IsReturn)
        {
            invoice.PaymentTermsTemplateId = null;
        }

        // Payment terms resolution: explicit → supplier default → null (skip for opening & returns)
        if (!invoice.IsOpening && !invoice.IsReturn && input.PaymentTermsTemplateId.HasValue)
        {
            invoice.PaymentTermsTemplateId = input.PaymentTermsTemplateId;
        }
        else if (!invoice.IsOpening && !invoice.IsReturn)
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

        // Per gotcha #1238 / #1508: PI issue date cannot be before linked PO date
        var poItemIds = input.Items
            .Where(i => i.PurchaseOrderItemId.HasValue)
            .Select(i => i.PurchaseOrderItemId!.Value)
            .Distinct()
            .ToList();

        if (poItemIds.Count > 0)
        {
            var poQuery = await _purchaseOrderRepository.GetQueryableAsync();
            var linkedPos = poQuery
                .Where(po => po.Items.Any(item => poItemIds.Contains(item.Id)))
                .Select(po => new { po.OrderNumber, po.OrderDate })
                .ToList();

            foreach (var po in linkedPos)
            {
                if (input.IssueDate.Date < po.OrderDate.Date)
                {
                    throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                        .WithData("detail", $"Issue Date cannot be before Purchase Order {po.OrderNumber} date ({po.OrderDate:yyyy-MM-dd}).");
                }
            }
        }

        foreach (var item in input.Items)
        {
            invoice.AddItem(item.ItemId, item.Description, item.Quantity, item.UnitPrice, item.TaxAmount, item.Uom);
            var added = invoice.Items.Last();
            added.PurchaseOrderItemId = item.PurchaseOrderItemId;
            added.PurchaseReceiptItemId = item.PurchaseReceiptItemId;

            if (item.EnableDeferredExpense)
            {
                added.EnableDeferredExpense = true;
                added.DeferredExpenseAccountId = item.DeferredExpenseAccountId;
                added.ServiceStartDate = item.ServiceStartDate;
                added.ServiceEndDate = item.ServiceEndDate;
                added.ServiceStopDate = item.ServiceStopDate;
            }
        }

        // Resolve UOM conversion factors for direct PI creation (when UpdateStock=true, stock needs StockQty)
        var uomSvc = LazyServiceProvider.LazyGetRequiredService<Inventory.DomainServices.UomConversionService>();
        var itemRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Inventory.Entities.Item, Guid>>();
        foreach (var piItem in invoice.Items)
        {
            var itemEntity = await itemRepo.FindAsync(piItem.ItemId);
            if (itemEntity != null)
            {
                piItem.StockUom = itemEntity.Uom ?? "Unit";
                if (!string.IsNullOrEmpty(piItem.Uom) && piItem.Uom != piItem.StockUom)
                {
                    piItem.ConversionFactor = await uomSvc.GetConversionFactorAsync(
                        piItem.ItemId, piItem.Uom, piItem.StockUom);
                }
            }
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

    [Authorize(MyERPPermissions.PurchaseInvoices.Edit)]
    public async Task<PurchaseInvoiceDto> UpdateAsync(Guid id, CreatePurchaseInvoiceDto input)
    {
        var invoice = await _repository.GetAsync(id);
        if (invoice.Status != Core.DocumentStatus.Draft)
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("detail", "Only Draft purchase invoices can be edited");

        var updateItemIds = input.Items.Select(i => i.ItemId).ToArray();
        var updateCompanyRestriction = LazyServiceProvider.LazyGetRequiredService<Core.DomainServices.CompanyRestrictionValidationService>();
        await updateCompanyRestriction.ValidateTransactionCompanyAsync("PurchaseInvoice", invoice.CompanyId, updateItemIds, supplierIds: new[] { invoice.SupplierId });

        invoice.IssueDate = input.IssueDate;
        invoice.DueDate = input.DueDate;
        invoice.CurrencyCode = input.CurrencyCode;
        invoice.PriceListId = input.PriceListId;
        invoice.SupplierInvoiceNumber = input.SupplierInvoiceNumber;
        invoice.Notes = input.Notes;
        invoice.IsSubcontracted = input.IsSubcontracted;

        invoice.ClearItems();
        foreach (var item in input.Items)
        {
            invoice.AddItem(item.ItemId, item.Description, item.Quantity, item.UnitPrice, item.TaxAmount, item.Uom);
        }

        await _repository.UpdateAsync(invoice, autoSave: true);
        return ObjectMapper.Map<PurchaseInvoice, PurchaseInvoiceDto>(invoice);
    }

    [Authorize(MyERPPermissions.PurchaseInvoices.Submit)]
    public async Task<PurchaseInvoiceDto> SubmitAsync(Guid id)
    {
        var invoice = await _repository.GetAsync(id);

        // Authorization control: high-value transaction approval check
        var authControl = LazyServiceProvider.LazyGetRequiredService<MyERP.Core.DomainServices.AuthorizationControlService>();
        var userRoles = (CurrentUser.Roles ?? Array.Empty<string>()).ToArray();
        await authControl.ValidateApprovingAuthorityAsync(
            "PurchaseInvoice", invoice.CompanyId,
            CurrentUser.Id ?? Guid.Empty, userRoles, invoice.GrandTotal);

        // Buying controller validations via domain manager
        var piManager = LazyServiceProvider
            .LazyGetRequiredService<MyERP.Purchasing.DomainServices.PurchaseInvoiceManager>();

        // Duplicate supplier invoice detection (FY-scoped per ERPNext)
        if (!invoice.IsReturn)
        {
            await piManager.ValidateNoDuplicateSupplierInvoiceAsync(
                invoice.SupplierId, invoice.CompanyId, invoice.SupplierInvoiceNumber, invoice.Id);
        }

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
            DocumentType = "PurchaseInvoice",
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
            .Where(e => e.ParentType == "PurchaseInvoice" && e.ParentId == invoice.Id)
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

        // Tax Withholding — apply TDS if supplier has a tax withholding category
        if (!invoice.IsReturn)
        {
            var supplier = invoice.SupplierId != Guid.Empty
                ? await _supplierRepository.FindAsync(invoice.SupplierId)
                : null;
            if (!string.IsNullOrWhiteSpace(supplier?.TaxWithholdingCategory))
            {
                // Resolve fiscal year for cumulative threshold
                var fyQuery = await _fiscalYearRepository.GetQueryableAsync();
                var fy = fyQuery
                    .Where(f => f.CompanyId == invoice.CompanyId
                        && f.StartDate <= invoice.IssueDate && f.EndDate >= invoice.IssueDate)
                    .FirstOrDefault();

                if (fy != null)
                {
                    // Resolve the real category master by name — supplier.TaxWithholdingCategory
                    // is a free-text label matched against TaxWithholdingCategory.CategoryName.
                    var categoryRepo = LazyServiceProvider
                        .LazyGetRequiredService<IRepository<Tax.Entities.TaxWithholdingCategory, Guid>>();
                    var categoryQuery = await categoryRepo.GetQueryableAsync();
                    var category = categoryQuery
                        .FirstOrDefault(c => c.CategoryName == supplier.TaxWithholdingCategory);
                    if (category == null)
                        throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.TaxWithholdingCategoryNotFound)
                            .WithData("category", supplier.TaxWithholdingCategory!);

                    var applicableRate = category.GetApplicableRate(invoice.IssueDate);

                    var cumulative = await _taxWithholdingService.GetCumulativeInvoicedAsync(
                        invoice.SupplierId, fy.StartDate, fy.EndDate);
                    var previouslyDeducted = await _taxWithholdingService.GetPreviouslyDeductedAsync(
                        invoice.SupplierId, fy.StartDate, fy.EndDate);
                    var historicalExists = await _taxWithholdingService.HasHistoricalWithholdingAsync(
                        invoice.SupplierId, supplier.TaxWithholdingCategory, fy.StartDate, fy.EndDate);

                    var singleThreshold = category.DisableTransactionThreshold ? 0m : (applicableRate.SingleThreshold ?? 0m);
                    var cumulativeThreshold = category.DisableCumulativeThreshold ? 0m : (applicableRate.CumulativeThreshold ?? 0m);

                    var ldc = await _taxWithholdingService.GetLdcDetailsAsync(
                        invoice.CompanyId, invoice.SupplierId, category.Id, invoice.IssueDate);

                    var result = _taxWithholdingService.CalculateWithholding(
                        currentInvoiceNetTotal: invoice.NetTotal,
                        cumulativeInvoicedInFY: cumulative,
                        standardRate: applicableRate.Rate,
                        singleThreshold: singleThreshold,
                        cumulativeThreshold: cumulativeThreshold,
                        taxOnExcessAmount: category.TaxOnExcessAmount,
                        previouslyDeductedTDS: previouslyDeducted,
                        ldc: ldc);

                    // "Once deducted, always deducted" — force threshold crossed if historical exists
                    if (!result.ThresholdCrossed && historicalExists)
                    {
                        result = _taxWithholdingService.CalculateWithholding(
                            currentInvoiceNetTotal: invoice.NetTotal,
                            cumulativeInvoicedInFY: 0m,
                            standardRate: applicableRate.Rate,
                            singleThreshold: 0m,
                            cumulativeThreshold: 0m,
                            taxOnExcessAmount: category.TaxOnExcessAmount,
                            previouslyDeductedTDS: previouslyDeducted,
                            ldc: ldc);
                    }

                    if (result.ThresholdCrossed && result.WithheldAmount > 0)
                    {
                        if (category.RoundOffTaxAmount)
                            result.WithheldAmount = Math.Round(result.WithheldAmount, 0);

                        // Real per-company withholding payable account, not a fallback.
                        var taxAccountId = category.GetCompanyAccount(invoice.CompanyId);
                        await _taxWithholdingService.CreateEntryAsync(
                            invoice.CompanyId, invoice.SupplierId,
                            "PurchaseInvoice", invoice.Id, taxAccountId,
                            result, invoice.IssueDate, supplier.TaxWithholdingCategory,
                            invoice.TenantId);
                    }
                }
            }
        }

        // Mandatory PO/PR linkage (Buying Settings: "Is PO/PR required for Purchase Invoice?")
        var poRequired = await SettingProvider.IsTrueAsync(MyERP.Settings.MyERPSettings.Buying.PoRequired);
        var prRequired = await SettingProvider.IsTrueAsync(MyERP.Settings.MyERPSettings.Buying.PrRequired);
        MyERP.Purchasing.DomainServices.PurchaseInvoiceManager.ValidatePoRequired(invoice, poRequired);

        if (prRequired && !invoice.IsReturn)
        {
            var invoiceItemIds = invoice.Items.Select(i => i.ItemId).Distinct().ToList();
            var stockItemIds = (await _itemRepository.GetListAsync(i => invoiceItemIds.Contains(i.Id) && i.MaintainStock))
                .Select(i => i.Id).ToHashSet();
            MyERP.Purchasing.DomainServices.PurchaseInvoiceManager.ValidatePrRequiredLinkage(
                invoice, prRequired, itemId => stockItemIds.Contains(itemId));
        }

        // 3-Way Matching: block billing more than received (PO↔PR↔PI fraud prevention)
        if (!invoice.IsReturn)
        {
            var prItemRepo = LazyServiceProvider
                .LazyGetRequiredService<IRepository<PurchaseReceiptItem, Guid>>();
            var prItemQueryable = await prItemRepo.GetQueryableAsync();

            // Build a dictionary of PO item → total received qty (single DB query)
            var poItemIds = invoice.Items
                .Where(i => i.PurchaseOrderItemId.HasValue)
                .Select(i => i.PurchaseOrderItemId!.Value)
                .Distinct()
                .ToList();

            var receivedQtyMap = prItemQueryable
                .Where(pri => pri.PurchaseOrderItemId.HasValue && poItemIds.Contains(pri.PurchaseOrderItemId.Value))
                .GroupBy(pri => pri.PurchaseOrderItemId!.Value)
                .ToDictionary(g => g.Key, g => g.Sum(pri => pri.Quantity));

            Func<Guid, decimal> getReceivedQty = (poItemId) =>
                receivedQtyMap.TryGetValue(poItemId, out var qty) ? qty : 0m;

            MyERP.Purchasing.DomainServices.PurchaseInvoiceManager.ValidateThreeWayMatching(invoice, getReceivedQty, prRequired);
        }

        // Maintain same rate throughout the purchase cycle (Buying Settings)
        if (!invoice.IsReturn && await SettingProvider.IsTrueAsync(MyERP.Settings.MyERPSettings.Buying.MaintainSameRate))
        {
            var rateCheckPoItemIds = invoice.Items
                .Where(i => i.PurchaseOrderItemId.HasValue)
                .Select(i => i.PurchaseOrderItemId!.Value)
                .Distinct()
                .ToList();

            if (rateCheckPoItemIds.Any())
            {
                var rateCheckOrders = (await _purchaseOrderRepository.GetQueryableAsync())
                    .Where(po => po.Items.Any(poi => rateCheckPoItemIds.Contains(poi.Id)))
                    .ToList();
                var poItemRates = rateCheckOrders.SelectMany(po => po.Items)
                    .Where(poi => rateCheckPoItemIds.Contains(poi.Id))
                    .ToDictionary(poi => poi.Id, poi => poi.UnitPrice);

                var rateAction = await SettingProvider.GetOrNullAsync(MyERP.Settings.MyERPSettings.Buying.MaintainSameRateAction) ?? "Stop";
                var overrideRole = await SettingProvider.GetOrNullAsync(MyERP.Settings.MyERPSettings.Buying.RoleToOverrideStopAction);
                var canOverride = !string.IsNullOrEmpty(overrideRole)
                    && (CurrentUser.Roles ?? Array.Empty<string>()).Contains(overrideRole);

                var rateLines = invoice.Items
                    .Where(i => i.PurchaseOrderItemId.HasValue && poItemRates.ContainsKey(i.PurchaseOrderItemId.Value))
                    .Select(i => (i.Description, i.UnitPrice, poItemRates[i.PurchaseOrderItemId!.Value], "Purchase Order"));

                var transactionValidation = LazyServiceProvider
                    .LazyGetRequiredService<MyERP.Core.DomainServices.TransactionValidationService>();
                transactionValidation.ValidateMaintainSameRate(rateLines, rateAction, canOverride);
            }
        }

        invoice.Submit();

        // Inter-Company: create corresponding SI in source company if supplier represents another company.
        // Mirrors SalesInvoiceAppService.SubmitAsync's PI auto-creation — that direction was wired,
        // this one (PI submit -> auto-create SI) was not, even though the domain service method
        // to do it (CreateSalesInvoiceFromPurchaseInvoiceAsync) already existed and was never called.
        if (!invoice.IsReturn)
        {
            var icSupplier = await _supplierRepository.GetAsync(invoice.SupplierId);
            if (icSupplier.RepresentsCompanyId.HasValue)
            {
                var interCompanyService = LazyServiceProvider
                    .LazyGetRequiredService<MyERP.Core.DomainServices.InterCompanyTransactionService>();
                await interCompanyService.CreateSalesInvoiceFromPurchaseInvoiceAsync(
                    invoice.Id, icSupplier.RepresentsCompanyId.Value, invoice.TenantId);
            }
        }

        // Debit Note: reduce original invoice outstanding (with concurrency retry)
        if (invoice.IsReturn && invoice.ReturnAgainstId.HasValue)
        {
            var returnAmount = Math.Abs(invoice.GrandTotal);
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    var original = await _repository.GetAsync(invoice.ReturnAgainstId.Value);
                    original.AmountPaid += returnAmount;
                    await _repository.UpdateAsync(original, autoSave: true);
                    break;
                }
                catch (Volo.Abp.Data.AbpDbConcurrencyException) when (attempt < 3)
                {
                    Logger.LogWarning("Concurrency conflict on PI debit note AmountPaid (attempt {Attempt})", attempt);
                    await Task.Delay(attempt * 10);
                }
            }
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

                // Use StockQty for SLE (respects UOM conversion)
                var stockQty = item.StockQty;
                var ratePerStockUnit = item.ConversionFactor != 0
                    ? item.UnitPrice / item.ConversionFactor
                    : item.UnitPrice;

                await _valuationService.CreateLedgerEntryAsync(
                    invoice.CompanyId, item.ItemId, invoice.WarehouseId.Value,
                    invoice.IssueDate, stockQty, ratePerStockUnit,
                    voucherType: "PurchaseInvoice", voucherId: invoice.Id,
                    tenantId: invoice.TenantId);

                await _binService.ApplyStockMovementAsync(
                    item.ItemId, invoice.WarehouseId.Value,
                    stockQty, stockQty * ratePerStockUnit, invoice.TenantId);
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

                // Over-billing tolerance: max allowed = ordered × (1 + allowance% / 100).
                // Per ERPNext StatusUpdater; allowance comes from Company.OverBillingAllowance.
                var billingCompany = await _companyRepository.GetAsync(invoice.CompanyId);
                var billingAllowancePct = billingCompany.OverBillingAllowance;

                foreach (var po in affectedOrders)
                {
                    foreach (var piItem in invoice.Items.Where(i => i.PurchaseOrderItemId.HasValue))
                    {
                        var poItem = po.Items.FirstOrDefault(i => i.Id == piItem.PurchaseOrderItemId!.Value);
                        if (poItem == null) continue;

                        var maxAllowedTotal = poItem.Quantity * (1m + billingAllowancePct / 100m);
                        if (poItem.BilledQty + piItem.Quantity > maxAllowedTotal)
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
                    await _purchaseOrderRepository.UpdateAsync(po, autoSave: true);
                }
            }
        }

        // PR BilledQty update: track which PR items have been billed
        // Per ERPNext: update_billed_amount_in_pr FIFO billing status
        var piMgr = LazyServiceProvider.LazyGetRequiredService<PurchaseInvoiceManager>();
        await piMgr.UpdateLinkedPurchaseReceiptBillingAsync(invoice);

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

        var glService = LazyServiceProvider.LazyGetRequiredService<Accounting.DomainServices.GlRepostService>();
        await glService.RebuildPurchaseInvoiceGlAsync(invoice);

        // Auto-insert item prices (per ERPNext: auto_insert_price_list_rate_if_missing)
        try
        {
            var priceAutoInsert = LazyServiceProvider
                .LazyGetRequiredService<Inventory.DomainServices.ItemPriceAutoInsertService>();

            {
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
                        PartyId = invoice.SupplierId,
                        IsSelling = false,
                        TransactionDate = invoice.IssueDate,
                        CurrencyCode = invoice.CurrencyCode,
                        TenantId = invoice.TenantId,
                        Items = invoice.Items.Select(i => new Inventory.DomainServices.AutoInsertPriceItem
                        {
                            ItemId = i.ItemId, Rate = i.UnitPrice, Uom = i.Uom,
                        }).ToArray(),
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Item price auto-insert failed for PI {PiId}", invoice.Id);
        }

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

        // Auto-cancel linked system-generated Debit Note Journal Entries (gotcha #3909)
        var jeRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Accounting.Entities.JournalEntry, Guid>>();
        var linkedJes = await jeRepo.GetListAsync(j =>
            j.ReferenceType == "PurchaseInvoice"
            && j.ReferenceId == invoice.Id
            && j.VoucherType == Accounting.JournalEntryVoucherType.DebitNote
            && j.Status == Core.DocumentStatus.Posted);

        foreach (var je in linkedJes)
        {
            je.Cancel();
            await _postingOrchestrator.ReversePleForDocumentAsync("JournalEntry", je.Id);
            await _postingOrchestrator.ReverseGlForDocumentAsync("JournalEntry", je.Id);
            await jeRepo.UpdateAsync(je, autoSave: true);
        }

        // Reverse PLE entries
        await _postingOrchestrator.ReversePleForDocumentAsync("PurchaseInvoice", invoice.Id);
        await _postingOrchestrator.ReverseGlForDocumentAsync("PurchaseInvoice", invoice.Id);

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
                var ratePerStockUnit = item.ConversionFactor != 0
                    ? item.UnitPrice / item.ConversionFactor
                    : item.UnitPrice;

                await _valuationService.CreateLedgerEntryAsync(
                    invoice.CompanyId, item.ItemId, invoice.WarehouseId.Value,
                    invoice.IssueDate, -stockQty, ratePerStockUnit, // Negative = stock out (reversal)
                    voucherType: "PurchaseInvoice", voucherId: invoice.Id,
                    tenantId: invoice.TenantId);

                await _binService.ApplyStockMovementAsync(
                    item.ItemId, invoice.WarehouseId.Value,
                    -stockQty, -(stockQty * ratePerStockUnit), invoice.TenantId);
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
                await _purchaseOrderRepository.UpdateAsync(po, autoSave: true);
            }
        }

        // Reverse PR BilledQty (domain service)
        var piMgrCancel = LazyServiceProvider.LazyGetRequiredService<PurchaseInvoiceManager>();
        await piMgrCancel.UpdateLinkedPurchaseReceiptBillingAsync(invoice, reverse: true);

        await _repository.UpdateAsync(invoice, autoSave: true);

        // Audit trail
        await _activityLog.LogCancelledAsync("PurchaseInvoice", invoice.Id, invoice.CompanyId,
            invoice.InvoiceNumber, "Posted", invoice.TenantId);

        // Inter-company cancellation cascade: cancelling this PI also cancels the Sales
        // Invoice it was created from (or that was created from it). Status-guarded so
        // cascading from either side converges. Only the reversal steps that apply to an
        // inter-company-created SI run here (no SO/DN-linked items, no UpdateStock, no
        // loyalty program) — matches SalesInvoiceAppService.CancelAsync's own steps for
        // that shape of document.
        if (invoice.InterCompanyInvoiceId.HasValue)
        {
            var siRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.SalesInvoice, Guid>>();
            var linkedSi = await siRepo.FindAsync(invoice.InterCompanyInvoiceId.Value);
            if (linkedSi != null && linkedSi.Status == Core.DocumentStatus.Posted && linkedSi.AmountPaid <= 0)
            {
                linkedSi.Cancel();
                await _postingOrchestrator.ReversePleForDocumentAsync("SalesInvoice", linkedSi.Id);
                await _postingOrchestrator.ReverseGlForDocumentAsync("SalesInvoice", linkedSi.Id);
                await siRepo.UpdateAsync(linkedSi, autoSave: true);
                await _activityLog.LogCancelledAsync("SalesInvoice", linkedSi.Id, linkedSi.CompanyId,
                    linkedSi.InvoiceNumber, "Posted", linkedSi.TenantId);
            }
        }

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
    /// Blocks this invoice from payment, independent of any Supplier-level hold.
    /// Per purchase-invoice skill: release_date (if set) must be a future date.
    /// </summary>
    [Authorize(MyERPPermissions.PurchaseInvoices.Edit)]
    public async Task<PurchaseInvoiceDto> BlockAsync(Guid id, string? holdComment, DateTime? releaseDate)
    {
        var invoice = await _repository.GetAsync(id);
        invoice.SetHold(true, holdComment, releaseDate);
        await _repository.UpdateAsync(invoice, autoSave: true);
        return ObjectMapper.Map<PurchaseInvoice, PurchaseInvoiceDto>(invoice);
    }

    /// <summary>Unblocks this invoice, clearing OnHold/HoldComment/ReleaseDate.</summary>
    [Authorize(MyERPPermissions.PurchaseInvoices.Edit)]
    public async Task<PurchaseInvoiceDto> UnblockAsync(Guid id)
    {
        var invoice = await _repository.GetAsync(id);
        invoice.SetHold(false, null, null);
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
        amended.PriceListId = original.PriceListId;
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

    [Authorize(MyERPPermissions.PurchaseInvoices.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        var invoice = await _repository.GetAsync(id);
        if (invoice.Status != Core.DocumentStatus.Draft)
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("detail", "Only Draft invoices can be deleted");
        await _repository.DeleteAsync(id);
    }

    /// <summary>
    /// Returns unbilled Purchase Receipt items for a supplier.
    /// Per ERPNext PI form "Get Items From Purchase Receipt": fetches PR items
    /// where billed_qty &lt; qty (partially or fully unbilled).
    /// Most common PI creation workflow: bill against verified goods receipts.
    /// </summary>
    [Authorize(MyERPPermissions.PurchaseInvoices.Create)]
    public async Task<List<UnbilledReceiptItemDto>> GetUnbilledReceiptItemsAsync(
        Guid supplierId, Guid? companyId = null)
    {
        var prRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PurchaseReceipt, Guid>>();
        var prQuery = await prRepo.GetQueryableAsync();

        var query = prQuery.Where(pr =>
            pr.SupplierId == supplierId &&
            pr.Status == Core.DocumentStatus.Posted &&
            !pr.IsReturn);

        if (companyId.HasValue)
            query = query.Where(pr => pr.CompanyId == companyId.Value);

        var receipts = query.ToList();

        var result = new List<UnbilledReceiptItemDto>();
        foreach (var pr in receipts)
        {
            foreach (var item in pr.Items)
            {
                var unbilledQty = item.PendingBillingQty;
                if (unbilledQty > 0)
                {
                    result.Add(new UnbilledReceiptItemDto
                    {
                        PurchaseReceiptId = pr.Id,
                        ReceiptNumber = pr.ReceiptNumber,
                        ReceiptDate = pr.PostingDate,
                        ItemId = item.ItemId,
                        ItemName = item.Description,
                        Quantity = unbilledQty,
                        Rate = item.UnitPrice,
                        Uom = item.Uom,
                        PurchaseReceiptItemId = item.Id,
                        PurchaseOrderItemId = item.PurchaseOrderItemId,
                    });
                }
            }
        }

        return result.OrderBy(r => r.ReceiptDate).ThenBy(r => r.ItemName).ToList();
    }

    /// <summary>
    /// Returns unbilled Purchase Order items for a supplier.
    /// Per ERPNext PI form "Get Items From Purchase Order": fetches PO items
    /// where billed_qty &lt; qty (partially or fully unbilled).
    /// Used for direct billing from orders (service purchases without PR).
    /// </summary>
    [Authorize(MyERPPermissions.PurchaseInvoices.Create)]
    public async Task<List<UnbilledPurchaseOrderItemDto>> GetUnbilledPurchaseOrderItemsAsync(
        Guid supplierId, Guid? companyId = null)
    {
        var poQuery = await _purchaseOrderRepository.GetQueryableAsync();

        var query = poQuery.Where(po =>
            po.SupplierId == supplierId &&
            po.Status != Core.DocumentStatus.Draft &&
            po.Status != Core.DocumentStatus.Cancelled &&
            po.Status != Core.DocumentStatus.Closed);

        if (companyId.HasValue)
            query = query.Where(po => po.CompanyId == companyId.Value);

        var orders = query.ToList();

        var result = new List<UnbilledPurchaseOrderItemDto>();
        foreach (var po in orders)
        {
            foreach (var item in po.Items)
            {
                var unbilledQty = item.PendingBillingQty;
                if (unbilledQty > 0)
                {
                    result.Add(new UnbilledPurchaseOrderItemDto
                    {
                        PurchaseOrderId = po.Id,
                        OrderNumber = po.OrderNumber,
                        OrderDate = po.OrderDate,
                        ItemId = item.ItemId,
                        ItemName = item.Description,
                        Quantity = unbilledQty,
                        Rate = item.UnitPrice,
                        Uom = item.Uom,
                        PurchaseOrderItemId = item.Id,
                    });
                }
            }
        }

        return result.OrderBy(r => r.OrderDate).ThenBy(r => r.ItemName).ToList();
    }

    /// <summary>
    /// Gets unbilled items from submitted Purchase Receipts for a supplier.
    /// Per ERPNext PI form: "Get Items from Purchase Receipt" button populates items
    /// from received goods that haven't been billed yet.
    /// Formula: pending = qty - billed_qty (per PurchaseReceiptItem.PendingBillingQty)
    /// Enables: 3-way matching (PO→PR→PI) and bill-on-receipt workflows.
    /// </summary>
    [Authorize(MyERPPermissions.PurchaseInvoices.Create)]
    public async Task<List<UnbilledPurchaseReceiptItemDto>> GetUnbilledPurchaseReceiptItemsAsync(
        Guid supplierId, Guid? companyId = null)
    {
        var prRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PurchaseReceipt, Guid>>();
        var prQuery = (await prRepo.WithDetailsAsync()).AsQueryable();

        var query = prQuery.Where(pr =>
            pr.SupplierId == supplierId &&
            pr.Status != Core.DocumentStatus.Draft &&
            pr.Status != Core.DocumentStatus.Cancelled);

        if (companyId.HasValue)
            query = query.Where(pr => pr.CompanyId == companyId.Value);

        var receipts = query.ToList();

        var result = new List<UnbilledPurchaseReceiptItemDto>();
        foreach (var pr in receipts)
        {
            foreach (var item in pr.Items)
            {
                var unbilledQty = item.PendingBillingQty;
                if (unbilledQty > 0)
                {
                    result.Add(new UnbilledPurchaseReceiptItemDto
                    {
                        PurchaseReceiptId = pr.Id,
                        ReceiptNumber = pr.ReceiptNumber,
                        ReceiptDate = pr.PostingDate,
                        ItemId = item.ItemId,
                        ItemName = item.Description,
                        Quantity = unbilledQty,
                        Rate = item.UnitPrice,
                        Uom = item.Uom,
                        PurchaseReceiptItemId = item.Id,
                        PurchaseOrderItemId = item.PurchaseOrderItemId,
                        WarehouseId = item.WarehouseId ?? pr.WarehouseId,
                    });
                }
            }
        }

        return result.OrderBy(r => r.ReceiptDate).ThenBy(r => r.ItemName).ToList();
    }

    /// <summary>
    /// Get payment entries that have been made against this invoice.
    /// </summary>
    public async Task<List<InvoicePaymentDto>> GetPaymentsAsync(Guid id)
    {
        var peRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PaymentEntry, Guid>>();
        var peQuery = await peRepo.GetQueryableAsync();
        var payments = peQuery
            .Where(pe => pe.AgainstInvoiceId == id
                         || pe.References.Any(r => r.ReferenceType == "PurchaseInvoice" && r.ReferenceId == id))
            .OrderByDescending(pe => pe.PostingDate)
            .Select(pe => new InvoicePaymentDto
            {
                Id = pe.Id,
                PaymentNumber = pe.PaymentNumber ?? pe.Id.ToString().Substring(0, 8),
                PostingDate = pe.PostingDate,
                Amount = pe.PaidAmount,
                Status = pe.Status.ToString()
            }).ToList();
        return payments;
    }
}
