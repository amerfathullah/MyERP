using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Core.DomainServices;

/// <summary>
/// Handles inter-company transactions — creating paired documents between companies.
/// E.g., SI in Company A → PI in Company B (linked via party validation).
/// Per DO-NOT: requires bidirectional party validation before creating.
/// Per DO-NOT: must NOT copy party-specific fields (12 CROSS_PARTY_FIELD_NO_MAP fields).
/// </summary>
public class InterCompanyTransactionService : DomainService
{
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepository;
    private readonly IRepository<PurchaseInvoice, Guid> _purchaseInvoiceRepository;
    private readonly IRepository<PurchaseOrder, Guid> _purchaseOrderRepository;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IRepository<Supplier, Guid> _supplierRepository;
    private readonly IRepository<PriceList, Guid>? _priceListRepository;
    private readonly IRepository<Address, Guid>? _addressRepository;
    private readonly IRepository<Accounting.Entities.AccountsSettings, Guid>? _accountsSettingsRepository;
    private readonly TransactionValidationService? _transactionValidationService;

    public InterCompanyTransactionService(
        IRepository<SalesInvoice, Guid> salesInvoiceRepository,
        IRepository<PurchaseInvoice, Guid> purchaseInvoiceRepository,
        IRepository<PurchaseOrder, Guid> purchaseOrderRepository,
        IRepository<Company, Guid> companyRepository,
        IRepository<Customer, Guid> customerRepository,
        IRepository<Supplier, Guid> supplierRepository,
        IRepository<PriceList, Guid>? priceListRepository = null,
        IRepository<Address, Guid>? addressRepository = null,
        IRepository<Accounting.Entities.AccountsSettings, Guid>? accountsSettingsRepository = null,
        TransactionValidationService? transactionValidationService = null)
    {
        _salesInvoiceRepository = salesInvoiceRepository;
        _purchaseInvoiceRepository = purchaseInvoiceRepository;
        _purchaseOrderRepository = purchaseOrderRepository;
        _companyRepository = companyRepository;
        _customerRepository = customerRepository;
        _supplierRepository = supplierRepository;
        _priceListRepository = priceListRepository;
        _addressRepository = addressRepository;
        _accountsSettingsRepository = accountsSettingsRepository;
        _transactionValidationService = transactionValidationService;
    }

    /// <summary>
    /// Price list validation: for inter-company transactions, the selected price list must have
    /// both IsBuying and IsSelling checked (ERPNext accounts/doctype/sales_invoice/mapper.py validate_inter_company_transaction).
    /// </summary>
    private async Task ValidatePriceListAsync(Guid? priceListId)
    {
        if (!priceListId.HasValue) return;

        var priceListRepo = _priceListRepository ?? LazyServiceProvider?.LazyGetService<IRepository<PriceList, Guid>>();
        if (priceListRepo == null) return;

        var priceList = await priceListRepo.FindAsync(priceListId.Value);
        if (priceList != null && (!priceList.IsBuying || !priceList.IsSelling))
        {
            throw new BusinessException(MyERPDomainErrorCodes.InterCompanyPriceListMustBeBuyingAndSelling)
                .WithData("priceList", priceList.Name);
        }
    }

    /// <summary>
    /// Creates a Purchase Invoice in the target company from a Sales Invoice in the source company.
    /// Validates bidirectional party linkage (Customer in source ↔ Supplier in target).
    /// </summary>
    public async Task<Guid> CreatePurchaseInvoiceFromSalesInvoiceAsync(
        Guid salesInvoiceId, Guid targetCompanyId, Guid? tenantId = null)
    {
        var si = await _salesInvoiceRepository.GetAsync(salesInvoiceId);
        var sourceCompany = await _companyRepository.GetAsync(si.CompanyId);
        var targetCompany = await _companyRepository.GetAsync(targetCompanyId);

        // Guard: check counterpart creation and pending uninvoiced qty (ERPNext PR #55639 / #66916 / #66937)
        var (isMadeFromCounterpart, invoicedByItem, existingLinkedPi) = await AnalyzeSalesInvoiceInterCompanyStateAsync(si);
        var pendingItems = new List<(SalesInvoiceItem Item, decimal PendingQty, decimal ConversionFactor)>();
        foreach (var item in si.Items)
        {
            var cf = item.ConversionFactor > 0 ? item.ConversionFactor : 1m;
            var invoicedStock = invoicedByItem.GetValueOrDefault(item.Id);
            var pendingQty = Math.Round((item.StockQty - invoicedStock) / cf, 4);
            if (pendingQty > 0m)
            {
                pendingItems.Add((item, pendingQty, cf));
            }
        }

        if (isMadeFromCounterpart || (existingLinkedPi != null && pendingItems.Count == 0))
        {
            throw new BusinessException(MyERPDomainErrorCodes.InterCompanyInvoiceAlreadyCreated)
                .WithData("salesInvoiceNumber", si.InvoiceNumber)
                .WithData("purchaseInvoiceNumber", existingLinkedPi?.InvoiceNumber ?? si.InterCompanyPurchaseInvoiceId?.ToString() ?? string.Empty);
        }

        // Validate: the Customer in source company must represent target company
        var customer = await _customerRepository.GetAsync(si.CustomerId);
        if (customer.RepresentsCompanyId != targetCompanyId)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InterCompanyPartyMismatch)
                .WithData("customer", customer.Name)
                .WithData("targetCompany", targetCompany.Name);
        }

        // Find the matching Supplier in target company that represents source company
        var supplierQuery = await _supplierRepository.GetQueryableAsync();
        var matchingSupplier = supplierQuery
            .FirstOrDefault(s => s.CompanyId == targetCompanyId
                              && s.RepresentsCompanyId == si.CompanyId
                              && s.IsActive);

        if (matchingSupplier == null)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InterCompanyPartyMismatch)
                .WithData("sourceCompany", sourceCompany.Name)
                .WithData("targetCompany", targetCompany.Name);
        }

        // Validate price list if present
        await ValidatePriceListAsync(si.PriceListId);

        var piNumber = await GenerateUniquePurchaseInvoiceNumberAsync($"IC-{si.InvoiceNumber}");

        // Create the PI in target company — do NOT copy party-specific fields
        var pi = new PurchaseInvoice(
            LazyServiceProvider?.LazyGetService<Volo.Abp.Guids.IGuidGenerator>()?.Create() ?? Guid.NewGuid(),
            targetCompanyId,
            matchingSupplier.Id,
            piNumber,
            si.IssueDate,
            tenantId);

        pi.DueDate = si.DueDate;
        pi.CurrencyCode = sourceCompany.CurrencyCode; // Must be source company currency
        pi.InterCompanyInvoiceId = si.Id;
        pi.PriceListId = si.PriceListId;

        // Per ERPNext commit 145a6c5e2a: fetch exchange rate while creating inter-company order and invoice
        if (!string.Equals(pi.CurrencyCode, targetCompany.CurrencyCode, StringComparison.OrdinalIgnoreCase))
        {
            var exchangeRateService = LazyServiceProvider?.LazyGetService<Accounting.DomainServices.CurrencyExchangeService>();
            if (exchangeRateService != null)
            {
                var rate = await exchangeRateService.GetExchangeRateAsync(pi.CurrencyCode, targetCompany.CurrencyCode, pi.IssueDate);
                if (rate > 0)
                {
                    pi.ExchangeRate = rate;
                }
            }
        }

        // Address link validation (ERPNext PR #47463 / commit aed46ad5b9):
        // Only map address if it is actually linked to the target party
        if (si.BillingAddressId.HasValue && await ValidateAddressLinkAsync(si.BillingAddressId, "Supplier", matchingSupplier.Id))
        {
            pi.BillingAddressId = si.BillingAddressId;
        }

        // Copy remaining pending items (amounts only — no party-specific fields; resolve target company's Item by ItemCode when available)
        var targetItemMap = await ResolveTargetCompanyItemMapAsync(pendingItems.Select(x => x.Item.ItemId), targetCompanyId);
        foreach (var (item, pendingQty, cf) in pendingItems)
        {
            var targetItemId = targetItemMap.GetValueOrDefault(item.ItemId, item.ItemId);
            var tax = item.Quantity > 0 && pendingQty < item.Quantity
                ? Math.Round(item.TaxAmount * (pendingQty / item.Quantity), 4)
                : item.TaxAmount;
            pi.AddItem(targetItemId, item.Description, pendingQty, item.UnitPrice, tax, item.Uom);
            var piItem = pi.Items.Last();
            piItem.StockUom = item.StockUom;
            piItem.ConversionFactor = cf;
            piItem.SalesInvoiceItemId = item.Id;
        }

        await _purchaseInvoiceRepository.InsertAsync(pi, autoSave: true);

        // Reciprocal link — without this, cancelling the SI has no way to find the PI it created.
        si.InterCompanyPurchaseInvoiceId = pi.Id;
        await _salesInvoiceRepository.UpdateAsync(si, autoSave: true);

        return pi.Id;
    }

    /// <summary>
    /// Creates a Sales Invoice in the target company from a Purchase Invoice in the source company.
    /// Validates bidirectional party linkage (Supplier in source ↔ Customer in target).
    /// </summary>
    public async Task<Guid> CreateSalesInvoiceFromPurchaseInvoiceAsync(
        Guid purchaseInvoiceId, Guid targetCompanyId, Guid? tenantId = null)
    {
        var pi = await _purchaseInvoiceRepository.GetAsync(purchaseInvoiceId);
        var sourceCompany = await _companyRepository.GetAsync(pi.CompanyId);
        var targetCompany = await _companyRepository.GetAsync(targetCompanyId);

        // Guard: check counterpart creation and pending uninvoiced qty (ERPNext PR #55639 / #66916 / #66937)
        var (isMadeFromCounterpart, invoicedByItem, existingLinkedSi) = await AnalyzePurchaseInvoiceInterCompanyStateAsync(pi);
        var pendingItems = new List<(PurchaseInvoiceItem Item, decimal PendingQty, decimal ConversionFactor)>();
        foreach (var item in pi.Items)
        {
            var cf = item.ConversionFactor > 0 ? item.ConversionFactor : 1m;
            var invoicedStock = invoicedByItem.GetValueOrDefault(item.Id);
            var pendingQty = Math.Round((item.StockQty - invoicedStock) / cf, 4);
            if (pendingQty > 0m)
            {
                pendingItems.Add((item, pendingQty, cf));
            }
        }

        if (isMadeFromCounterpart || (existingLinkedSi != null && pendingItems.Count == 0))
        {
            throw new BusinessException(MyERPDomainErrorCodes.InterCompanyInvoiceAlreadyCreated)
                .WithData("purchaseInvoiceNumber", pi.InvoiceNumber)
                .WithData("salesInvoiceNumber", existingLinkedSi?.InvoiceNumber ?? pi.InterCompanyInvoiceId?.ToString() ?? string.Empty);
        }

        // Validate: the Supplier in source company must represent target company
        var supplier = await _supplierRepository.GetAsync(pi.SupplierId);
        if (supplier.RepresentsCompanyId != targetCompanyId)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InterCompanyPartyMismatch)
                .WithData("supplier", supplier.Name)
                .WithData("targetCompany", targetCompany.Name);
        }

        // Find matching Customer in target company that represents source company
        var customerQuery = await _customerRepository.GetQueryableAsync();
        var matchingCustomer = customerQuery
            .FirstOrDefault(c => c.CompanyId == targetCompanyId
                              && c.RepresentsCompanyId == pi.CompanyId
                              && c.IsActive);

        if (matchingCustomer == null)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InterCompanyPartyMismatch)
                .WithData("sourceCompany", sourceCompany.Name)
                .WithData("targetCompany", targetCompany.Name);
        }

        // Validate price list if present
        await ValidatePriceListAsync(pi.PriceListId);

        var siNumber = await GenerateUniqueSalesInvoiceNumberAsync($"IC-{pi.InvoiceNumber}");

        // Create the SI in target company
        var si = new SalesInvoice(
            LazyServiceProvider?.LazyGetService<Volo.Abp.Guids.IGuidGenerator>()?.Create() ?? Guid.NewGuid(),
            targetCompanyId,
            matchingCustomer.Id,
            siNumber,
            pi.IssueDate);

        si.DueDate = pi.DueDate;
        si.CurrencyCode = sourceCompany.CurrencyCode;
        si.InterCompanyPurchaseInvoiceId = pi.Id;
        si.PriceListId = pi.PriceListId;

        // Per ERPNext commit 145a6c5e2a: fetch exchange rate while creating inter-company order and invoice
        if (!string.Equals(si.CurrencyCode, targetCompany.CurrencyCode, StringComparison.OrdinalIgnoreCase))
        {
            var exchangeRateService = LazyServiceProvider?.LazyGetService<Accounting.DomainServices.CurrencyExchangeService>();
            if (exchangeRateService != null)
            {
                var rate = await exchangeRateService.GetExchangeRateAsync(si.CurrencyCode, targetCompany.CurrencyCode, si.IssueDate);
                if (rate > 0)
                {
                    si.ExchangeRate = rate;
                }
            }
        }

        // Address link validation (ERPNext PR #47463 / commit aed46ad5b9):
        // Only map address if it is actually linked to the target party
        if (pi.BillingAddressId.HasValue && await ValidateAddressLinkAsync(pi.BillingAddressId, "Customer", matchingCustomer.Id))
        {
            si.BillingAddressId = pi.BillingAddressId;
        }

        var targetItemMap = await ResolveTargetCompanyItemMapAsync(pendingItems.Select(x => x.Item.ItemId), targetCompanyId);
        foreach (var (item, pendingQty, cf) in pendingItems)
        {
            var targetItemId = targetItemMap.GetValueOrDefault(item.ItemId, item.ItemId);
            var tax = item.Quantity > 0 && pendingQty < item.Quantity
                ? Math.Round(item.TaxAmount * (pendingQty / item.Quantity), 4)
                : item.TaxAmount;
            si.AddItem(targetItemId, item.Description, pendingQty, item.UnitPrice, tax, item.Uom);
            var siItem = si.Items.Last();
            siItem.StockUom = item.StockUom;
            siItem.ConversionFactor = cf;
            siItem.PurchaseInvoiceItemId = item.Id;
        }

        await _salesInvoiceRepository.InsertAsync(si, autoSave: true);

        // Reciprocal link — without this, cancelling the PI has no way to find the SI it created.
        pi.InterCompanyInvoiceId = si.Id;
        await _purchaseInvoiceRepository.UpdateAsync(pi, autoSave: true);

        return si.Id;
    }

    /// <summary>
    /// Computes inter-company PO link metadata and already-ordered stock quantities per SO item
    /// for a Sales Order (ERPNext PR #66964: an order made from its counterpart links without item
    /// references; otherwise compute pending qty per non-closed item).
    /// </summary>
    public async Task<(bool IsMadeFromCounterpart, Dictionary<Guid, decimal> OrderedStockQtyByItem, PurchaseOrder? LatestLinkedPo)>
        AnalyzeSalesOrderInterCompanyStateAsync(SalesOrder salesOrder)
    {
        var orderedByItem = new Dictionary<Guid, decimal>();
        if (_purchaseOrderRepository == null)
        {
            return (false, orderedByItem, null);
        }

        var poQuery = await _purchaseOrderRepository.WithDetailsAsync(p => p.Items);
        var linkedPos = poQuery
            .Where(po => po.InterCompanySalesOrderId == salesOrder.Id && po.Status != DocumentStatus.Cancelled)
            .OrderByDescending(po => po.CreationTime)
            .ToList();

        if (linkedPos.Count == 0)
        {
            return (false, orderedByItem, null);
        }

        var itemRefs = linkedPos
            .SelectMany(po => po.Items)
            .Where(i => i.SalesOrderItemId.HasValue)
            .ToList();

        if (itemRefs.Count == 0)
        {
            // Counterpart SO created from PO links without item references — hide IC PO button to avoid loop.
            return (true, orderedByItem, linkedPos[0]);
        }

        foreach (var poItem in itemRefs)
        {
            var key = poItem.SalesOrderItemId!.Value;
            orderedByItem[key] = orderedByItem.GetValueOrDefault(key) + poItem.StockQty;
        }

        return (false, orderedByItem, linkedPos[0]);
    }

    private async Task<IQueryable<PurchaseInvoice>?> GetPurchaseInvoiceQueryAsync()
    {
        return await _purchaseInvoiceRepository.WithDetailsAsync(p => p.Items)
            ?? await _purchaseInvoiceRepository.GetQueryableAsync();
    }

    private async Task<IQueryable<SalesInvoice>?> GetSalesInvoiceQueryAsync()
    {
        return await _salesInvoiceRepository.WithDetailsAsync(s => s.Items)
            ?? await _salesInvoiceRepository.GetQueryableAsync();
    }

    private async Task<string> GenerateUniquePurchaseInvoiceNumberAsync(string baseNumber)
    {
        var query = await _purchaseInvoiceRepository.GetQueryableAsync();
        if (query == null || !query.Any(pi => pi.InvoiceNumber == baseNumber))
            return baseNumber;

        for (var i = 2; i < 1000; i++)
        {
            var candidate = $"{baseNumber}-{i}";
            if (!query.Any(pi => pi.InvoiceNumber == candidate))
                return candidate;
        }
        return $"{baseNumber}-{Guid.NewGuid().ToString("N")[..6]}";
    }

    private async Task<string> GenerateUniqueSalesInvoiceNumberAsync(string baseNumber)
    {
        var query = await _salesInvoiceRepository.GetQueryableAsync();
        if (query == null || !query.Any(si => si.InvoiceNumber == baseNumber))
            return baseNumber;

        for (var i = 2; i < 1000; i++)
        {
            var candidate = $"{baseNumber}-{i}";
            if (!query.Any(si => si.InvoiceNumber == candidate))
                return candidate;
        }
        return $"{baseNumber}-{Guid.NewGuid().ToString("N")[..6]}";
    }

    /// <summary>
    /// Computes inter-company PI link metadata and net invoiced stock quantities per SI item,
    /// subtracting submitted/posted return PIs against those linked internal PIs (ERPNext PR #66916 / #66937).
    /// </summary>
    public async Task<(bool IsMadeFromCounterpart, Dictionary<Guid, decimal> InvoicedStockQtyByItem, PurchaseInvoice? LatestLinkedPi)>
        AnalyzeSalesInvoiceInterCompanyStateAsync(SalesInvoice salesInvoice, Guid? excludePurchaseInvoiceId = null)
    {
        var invoicedByItem = new Dictionary<Guid, decimal>();
        var piQuery = await GetPurchaseInvoiceQueryAsync();
        var linkedPis = piQuery != null
            ? piQuery
                .Where(pi => pi.InterCompanyInvoiceId == salesInvoice.Id
                    && pi.Status != DocumentStatus.Cancelled
                    && pi.Id != excludePurchaseInvoiceId)
                .OrderByDescending(pi => pi.CreationTime)
                .ToList()
            : new List<PurchaseInvoice>();

        if (salesInvoice.InterCompanyPurchaseInvoiceId.HasValue
            && salesInvoice.InterCompanyPurchaseInvoiceId.Value != excludePurchaseInvoiceId
            && !linkedPis.Any(p => p.Id == salesInvoice.InterCompanyPurchaseInvoiceId.Value))
        {
            var fallbackPi = await _purchaseInvoiceRepository.FindAsync(salesInvoice.InterCompanyPurchaseInvoiceId.Value);
            if (fallbackPi != null && fallbackPi.Status != DocumentStatus.Cancelled)
            {
                linkedPis.Insert(0, fallbackPi);
            }
        }

        if (linkedPis.Count == 0)
        {
            return (false, invoicedByItem, null);
        }

        var hasSiRefsOnPis = linkedPis.SelectMany(pi => pi.Items).Any(i => i.SalesInvoiceItemId.HasValue);
        var isMadeFromCounterpart = salesInvoice.Items.Any(i => i.PurchaseInvoiceItemId.HasValue)
            || (!hasSiRefsOnPis && salesInvoice.InterCompanyPurchaseInvoiceId.HasValue);
        if (isMadeFromCounterpart)
        {
            return (true, invoicedByItem, linkedPis[0]);
        }

        var piById = linkedPis.ToDictionary(pi => pi.Id);
        foreach (var piItem in linkedPis.SelectMany(pi => pi.Items))
        {
            if (piItem.SalesInvoiceItemId.HasValue)
            {
                invoicedByItem[piItem.SalesInvoiceItemId.Value] =
                    invoicedByItem.GetValueOrDefault(piItem.SalesInvoiceItemId.Value) + piItem.StockQty;
            }
        }

        // ERPNext PR #66916: returns against internal Purchase Invoices make returned qty uninvoiced again.
        if (piQuery != null)
        {
            var linkedPiIds = piById.Keys.ToList();
            var returnPis = piQuery
                .Where(pi => pi.IsReturn
                    && pi.ReturnAgainstId.HasValue
                    && linkedPiIds.Contains(pi.ReturnAgainstId.Value)
                    && pi.Status != DocumentStatus.Draft
                    && pi.Status != DocumentStatus.Cancelled
                    && pi.Id != excludePurchaseInvoiceId)
                .ToList();

            foreach (var retPi in returnPis)
            {
                piById.TryGetValue(retPi.ReturnAgainstId!.Value, out var origPi);
                foreach (var retItem in retPi.Items)
                {
                    var siItemId = retItem.SalesInvoiceItemId
                        ?? origPi?.Items.FirstOrDefault(i => i.ItemId == retItem.ItemId && i.SalesInvoiceItemId.HasValue)?.SalesInvoiceItemId;
                    if (siItemId.HasValue)
                    {
                        invoicedByItem[siItemId.Value] =
                            invoicedByItem.GetValueOrDefault(siItemId.Value) + retItem.StockQty;
                    }
                }
            }
        }

        return (false, invoicedByItem, linkedPis[0]);
    }

    /// <summary>
    /// Computes inter-company SI link metadata and net invoiced stock quantities per PI item,
    /// subtracting submitted/posted return SIs (ERPNext PR #66916 / #66937 symmetry).
    /// </summary>
    public async Task<(bool IsMadeFromCounterpart, Dictionary<Guid, decimal> InvoicedStockQtyByItem, SalesInvoice? LatestLinkedSi)>
        AnalyzePurchaseInvoiceInterCompanyStateAsync(PurchaseInvoice purchaseInvoice, Guid? excludeSalesInvoiceId = null)
    {
        var invoicedByItem = new Dictionary<Guid, decimal>();
        var siQuery = await GetSalesInvoiceQueryAsync();
        var linkedSis = siQuery != null
            ? siQuery
                .Where(si => si.InterCompanyPurchaseInvoiceId == purchaseInvoice.Id
                    && si.Status != DocumentStatus.Cancelled
                    && si.Id != excludeSalesInvoiceId)
                .OrderByDescending(si => si.CreationTime)
                .ToList()
            : new List<SalesInvoice>();

        if (purchaseInvoice.InterCompanyInvoiceId.HasValue
            && purchaseInvoice.InterCompanyInvoiceId.Value != excludeSalesInvoiceId
            && !linkedSis.Any(s => s.Id == purchaseInvoice.InterCompanyInvoiceId.Value))
        {
            var fallbackSi = await _salesInvoiceRepository.FindAsync(purchaseInvoice.InterCompanyInvoiceId.Value);
            if (fallbackSi != null && fallbackSi.Status != DocumentStatus.Cancelled)
            {
                linkedSis.Insert(0, fallbackSi);
            }
        }

        if (linkedSis.Count == 0)
        {
            return (false, invoicedByItem, null);
        }

        var hasPiRefsOnSis = linkedSis.SelectMany(si => si.Items).Any(i => i.PurchaseInvoiceItemId.HasValue);
        var isMadeFromCounterpart = purchaseInvoice.Items.Any(i => i.SalesInvoiceItemId.HasValue)
            || (!hasPiRefsOnSis && purchaseInvoice.InterCompanyInvoiceId.HasValue);
        if (isMadeFromCounterpart)
        {
            return (true, invoicedByItem, linkedSis[0]);
        }

        var siById = linkedSis.ToDictionary(si => si.Id);
        foreach (var siItem in linkedSis.SelectMany(si => si.Items))
        {
            if (siItem.PurchaseInvoiceItemId.HasValue)
            {
                invoicedByItem[siItem.PurchaseInvoiceItemId.Value] =
                    invoicedByItem.GetValueOrDefault(siItem.PurchaseInvoiceItemId.Value) + siItem.StockQty;
            }
        }

        if (siQuery != null)
        {
            var linkedSiIds = siById.Keys.ToList();
            var returnSis = siQuery
                .Where(si => si.IsReturn
                    && si.ReturnAgainstId.HasValue
                    && linkedSiIds.Contains(si.ReturnAgainstId.Value)
                    && si.Status != DocumentStatus.Draft
                    && si.Status != DocumentStatus.Cancelled
                    && si.Id != excludeSalesInvoiceId)
                .ToList();

            foreach (var retSi in returnSis)
            {
                siById.TryGetValue(retSi.ReturnAgainstId!.Value, out var origSi);
                foreach (var retItem in retSi.Items)
                {
                    var piItemId = retItem.PurchaseInvoiceItemId
                        ?? origSi?.Items.FirstOrDefault(i => i.ItemId == retItem.ItemId && i.PurchaseInvoiceItemId.HasValue)?.PurchaseInvoiceItemId;
                    if (piItemId.HasValue)
                    {
                        invoicedByItem[piItemId.Value] =
                            invoicedByItem.GetValueOrDefault(piItemId.Value) + retItem.StockQty;
                    }
                }
            }
        }

        return (false, invoicedByItem, linkedSis[0]);
    }

    /// <summary>
    /// Validates that submitting an internal Purchase Invoice or cancelling a return against one
    /// does not push net received/invoiced quantity past the linked Sales Invoice (+ over-receipt allowance).
    /// Per ERPNext PR #66916 / internal_transfer.validate_internal_transfer_qty().
    /// </summary>
    public async Task ValidateInternalPurchaseInvoiceQtyAsync(
        PurchaseInvoice invoice,
        decimal overReceiptAllowancePercent = 0m,
        Guid? excludePurchaseInvoiceId = null)
    {
        var linkedSiId = invoice.InterCompanyInvoiceId;
        if (!linkedSiId.HasValue && invoice.IsReturn && invoice.ReturnAgainstId.HasValue)
        {
            var returnAgainstPi = await _purchaseInvoiceRepository.FindAsync(invoice.ReturnAgainstId.Value);
            linkedSiId = returnAgainstPi?.InterCompanyInvoiceId;
        }

        if (!linkedSiId.HasValue) return;

        var siQuery = await GetSalesInvoiceQueryAsync();
        var si = siQuery?.FirstOrDefault(s => s.Id == linkedSiId.Value)
            ?? await _salesInvoiceRepository.FindAsync(linkedSiId.Value);
        if (si == null || si.Status == DocumentStatus.Cancelled) return;

        var (isMadeFromCounterpart, invoicedByItem, _) =
            await AnalyzeSalesInvoiceInterCompanyStateAsync(si, excludePurchaseInvoiceId);
        if (isMadeFromCounterpart || invoicedByItem.Count == 0) return;

        var allowanceMultiplier = 1m + (Math.Max(0m, overReceiptAllowancePercent) / 100m);
        foreach (var siItem in si.Items)
        {
            var maxAllowedStock = Math.Round(siItem.StockQty * allowanceMultiplier, 4);
            var netInvoicedStock = Math.Round(invoicedByItem.GetValueOrDefault(siItem.Id), 4);
            if (netInvoicedStock > maxAllowedStock + 0.0001m)
            {
                throw new BusinessException(MyERPDomainErrorCodes.OverBilling)
                    .WithData("detail",
                        $"Total inter-company Purchase Invoice quantity ({netInvoicedStock}) for item {siItem.Description} exceeds Sales Invoice {si.InvoiceNumber} quantity ({siItem.StockQty}).");
            }
        }
    }

    /// <summary>
    /// Validates that submitting an internal Sales Invoice (made from a Purchase Invoice) or
    /// cancelling a return against one does not push net invoiced quantity past the linked
    /// Purchase Invoice (+ over-receipt allowance). Mirrors <see cref="ValidateInternalPurchaseInvoiceQtyAsync"/>.
    /// </summary>
    public async Task ValidateInternalSalesInvoiceQtyAsync(
        SalesInvoice invoice,
        decimal overReceiptAllowancePercent = 0m,
        Guid? excludeSalesInvoiceId = null)
    {
        var linkedPiId = invoice.InterCompanyPurchaseInvoiceId;
        if (!linkedPiId.HasValue && invoice.IsReturn && invoice.ReturnAgainstId.HasValue)
        {
            var returnAgainstSi = await _salesInvoiceRepository.FindAsync(invoice.ReturnAgainstId.Value);
            linkedPiId = returnAgainstSi?.InterCompanyPurchaseInvoiceId;
        }

        if (!linkedPiId.HasValue) return;

        var piQuery = await GetPurchaseInvoiceQueryAsync();
        var pi = piQuery?.FirstOrDefault(p => p.Id == linkedPiId.Value)
            ?? await _purchaseInvoiceRepository.FindAsync(linkedPiId.Value);
        if (pi == null || pi.Status == DocumentStatus.Cancelled) return;

        var (isMadeFromCounterpart, invoicedByItem, _) =
            await AnalyzePurchaseInvoiceInterCompanyStateAsync(pi, excludeSalesInvoiceId);
        if (isMadeFromCounterpart || invoicedByItem.Count == 0) return;

        var allowanceMultiplier = 1m + (Math.Max(0m, overReceiptAllowancePercent) / 100m);
        foreach (var piItem in pi.Items)
        {
            var maxAllowedStock = Math.Round(piItem.StockQty * allowanceMultiplier, 4);
            var netInvoicedStock = Math.Round(invoicedByItem.GetValueOrDefault(piItem.Id), 4);
            if (netInvoicedStock > maxAllowedStock + 0.0001m)
            {
                throw new BusinessException(MyERPDomainErrorCodes.OverBilling)
                    .WithData("detail",
                        $"Total inter-company Sales Invoice quantity ({netInvoicedStock}) for item {piItem.Description} exceeds Purchase Invoice {pi.InvoiceNumber} quantity ({piItem.StockQty}).");
            }
        }
    }

    /// <summary>
    /// Creates a Purchase Order in the target company from a Sales Order in the source company.
    /// Per ERPNext make_inter_company_purchase_order / PR #66964: only maps non-closed items
    /// with pending qty > 0, and skips if the SO was already created from a counterpart PO.
    /// </summary>
    public async Task<Guid?> CreatePurchaseOrderFromSalesOrderAsync(
        SalesOrder salesOrder, Func<string, Guid, Task<string>> numberGenerator, Guid? tenantId = null)
    {
        var customer = await _customerRepository.GetAsync(salesOrder.CustomerId);
        if (!customer.RepresentsCompanyId.HasValue)
            return null; // Not an inter-company customer

        var (isMadeFromCounterpart, orderedByItem, _) = await AnalyzeSalesOrderInterCompanyStateAsync(salesOrder);
        if (isMadeFromCounterpart)
            return null;

        var pendingItems = new List<(SalesOrderItem Item, decimal PendingQty, decimal ConversionFactor)>();
        foreach (var item in salesOrder.Items)
        {
            if (item.IsClosed) continue;
            var cf = item.ConversionFactor > 0 ? item.ConversionFactor : 1m;
            var orderedStock = orderedByItem.GetValueOrDefault(item.Id);
            var pendingQty = Math.Round((item.StockQty - orderedStock) / cf, 4);
            if (pendingQty > 0)
            {
                pendingItems.Add((item, pendingQty, cf));
            }
        }
        if (pendingItems.Count == 0)
            return null;

        var targetCompanyId = customer.RepresentsCompanyId.Value;
        var targetCompany = await _companyRepository.GetAsync(targetCompanyId);

        // Find the supplier in target company that represents the source company
        var supplierQuery = await _supplierRepository.GetQueryableAsync();
        var matchingSupplier = supplierQuery
            .FirstOrDefault(s => s.CompanyId == targetCompanyId
                              && s.RepresentsCompanyId == salesOrder.CompanyId
                              && s.IsActive);

        if (matchingSupplier == null)
            return null; // No bidirectional link configured — skip silently

        // Validate price list if present
        await ValidatePriceListAsync(salesOrder.PriceListId);

        var poNumber = await numberGenerator("PurchaseOrder", targetCompanyId);

        var po = new PurchaseOrder(
            LazyServiceProvider?.LazyGetService<Volo.Abp.Guids.IGuidGenerator>()?.Create() ?? Guid.NewGuid(),
            targetCompanyId,
            matchingSupplier.Id,
            poNumber,
            salesOrder.OrderDate,
            tenantId);

        var today = DateTime.UtcNow.Date;
        po.ExpectedDeliveryDate = salesOrder.DeliveryDate.HasValue && salesOrder.DeliveryDate.Value.Date >= today
            ? salesOrder.DeliveryDate
            : today;
        po.CurrencyCode = salesOrder.CurrencyCode ?? "MYR";
        po.InterCompanySalesOrderId = salesOrder.Id;
        po.PriceListId = salesOrder.PriceListId;
        po.Notes = $"Auto-created from inter-company Sales Order {salesOrder.OrderNumber}";

        // Per ERPNext commit 145a6c5e2a: fetch exchange rate while creating inter-company order and invoice
        if (!string.Equals(po.CurrencyCode, targetCompany.CurrencyCode, StringComparison.OrdinalIgnoreCase))
        {
            var exchangeRateService = LazyServiceProvider?.LazyGetService<Accounting.DomainServices.CurrencyExchangeService>();
            if (exchangeRateService != null)
            {
                var rate = await exchangeRateService.GetExchangeRateAsync(po.CurrencyCode, targetCompany.CurrencyCode, po.OrderDate);
                if (rate > 0)
                {
                    po.ExchangeRate = rate;
                }
            }
        }

        // Address link validation (ERPNext PR #47463 / commit aed46ad5b9):
        // Only map address if it is actually linked to the target party
        if (salesOrder.BillingAddressId.HasValue && await ValidateAddressLinkAsync(salesOrder.BillingAddressId, "Supplier", matchingSupplier.Id))
        {
            po.BillingAddressId = salesOrder.BillingAddressId;
        }

        var targetItemMap = await ResolveTargetCompanyItemMapAsync(pendingItems.Select(x => x.Item.ItemId), targetCompanyId);
        foreach (var (item, pendingQty, cf) in pendingItems)
        {
            var targetItemId = targetItemMap.GetValueOrDefault(item.ItemId, item.ItemId);
            var poItem = po.AddItem(targetItemId, item.Description, pendingQty, item.UnitPrice, 0m, item.Uom);
            poItem.StockUom = item.StockUom;
            poItem.ConversionFactor = cf;
            poItem.SalesOrderId = salesOrder.Id;
            poItem.SalesOrderItemId = item.Id;
        }

        await _purchaseOrderRepository.InsertAsync(po, autoSave: true);
        return po.Id;
    }

    /// <summary>
    /// Creates a Sales Order in the target company from a Purchase Order in the source company.
    /// Mirrors ERPNext make_inter_company_sales_order / PR #66964.
    /// </summary>
    public async Task<Guid?> CreateSalesOrderFromPurchaseOrderAsync(
        PurchaseOrder purchaseOrder, Func<string, Guid, Task<string>> numberGenerator, Guid? tenantId = null)
    {
        var soRepo = LazyServiceProvider?.LazyGetService<IRepository<SalesOrder, Guid>>();
        if (soRepo == null) return null;

        if (purchaseOrder.InterCompanySalesOrderId.HasValue)
        {
            var existingSo = await soRepo.FindAsync(purchaseOrder.InterCompanySalesOrderId.Value);
            if (existingSo != null && existingSo.Status != DocumentStatus.Cancelled)
            {
                return null;
            }
        }

        var supplier = await _supplierRepository.GetAsync(purchaseOrder.SupplierId);
        if (!supplier.RepresentsCompanyId.HasValue)
            return null;

        var targetCompanyId = supplier.RepresentsCompanyId.Value;
        var targetCompany = await _companyRepository.GetAsync(targetCompanyId);

        var customerQuery = await _customerRepository.GetQueryableAsync();
        var matchingCustomer = customerQuery
            .FirstOrDefault(c => c.CompanyId == targetCompanyId
                              && c.RepresentsCompanyId == purchaseOrder.CompanyId
                              && c.IsActive);

        if (matchingCustomer == null)
            return null;

        var openItems = purchaseOrder.Items.Where(i => !i.IsClosed && i.Quantity > 0).ToList();
        if (openItems.Count == 0)
            return null;

        await ValidatePriceListAsync(purchaseOrder.PriceListId);

        var soNumber = await numberGenerator("SalesOrder", targetCompanyId);
        var so = new SalesOrder(
            LazyServiceProvider?.LazyGetService<Volo.Abp.Guids.IGuidGenerator>()?.Create() ?? Guid.NewGuid(),
            targetCompanyId,
            matchingCustomer.Id,
            soNumber,
            purchaseOrder.OrderDate,
            tenantId);

        so.DeliveryDate = purchaseOrder.ExpectedDeliveryDate;
        so.CurrencyCode = purchaseOrder.CurrencyCode ?? "MYR";
        so.PriceListId = purchaseOrder.PriceListId;
        so.Notes = $"Auto-created from inter-company Purchase Order {purchaseOrder.OrderNumber}";

        if (!string.Equals(so.CurrencyCode, targetCompany.CurrencyCode, StringComparison.OrdinalIgnoreCase))
        {
            var exchangeRateService = LazyServiceProvider?.LazyGetService<Accounting.DomainServices.CurrencyExchangeService>();
            if (exchangeRateService != null)
            {
                var rate = await exchangeRateService.GetExchangeRateAsync(so.CurrencyCode, targetCompany.CurrencyCode, so.OrderDate);
                if (rate > 0)
                {
                    so.ExchangeRate = rate;
                }
            }
        }

        if (purchaseOrder.BillingAddressId.HasValue && await ValidateAddressLinkAsync(purchaseOrder.BillingAddressId, "Customer", matchingCustomer.Id))
        {
            so.BillingAddressId = purchaseOrder.BillingAddressId;
        }

        var targetItemMap = await ResolveTargetCompanyItemMapAsync(openItems.Select(i => i.ItemId), targetCompanyId);
        foreach (var item in openItems)
        {
            var targetItemId = targetItemMap.GetValueOrDefault(item.ItemId, item.ItemId);
            so.AddItem(targetItemId, item.Description, item.Quantity, item.UnitPrice, 0m, item.Uom, item.ExpectedDeliveryDate);
            var addedItem = so.Items.Last();
            addedItem.StockUom = item.StockUom;
            addedItem.ConversionFactor = item.ConversionFactor > 0 ? item.ConversionFactor : 1m;
        }

        await soRepo.InsertAsync(so, autoSave: true);

        purchaseOrder.InterCompanySalesOrderId = so.Id;
        await _purchaseOrderRepository.UpdateAsync(purchaseOrder, autoSave: true);

        return so.Id;
    }

    /// <summary>
    /// Resolves source-company ItemIds to target-company ItemIds matching by ItemCode.
    /// In MyERP items are scoped by CompanyId, so inter-company documents must use the counterparty
    /// company's Item master to satisfy CompanyRestrictionValidationService.
    /// </summary>
    private async Task<Dictionary<Guid, Guid>> ResolveTargetCompanyItemMapAsync(
        IEnumerable<Guid> sourceItemIds, Guid targetCompanyId)
    {
        var map = new Dictionary<Guid, Guid>();
        var itemRepo = LazyServiceProvider?.LazyGetService<IRepository<Inventory.Entities.Item, Guid>>();
        if (itemRepo == null) return map;

        var ids = sourceItemIds.Distinct().ToList();
        if (ids.Count == 0) return map;

        var itemQuery = await itemRepo.GetQueryableAsync();
        var sourceItems = itemQuery
            .Where(i => ids.Contains(i.Id))
            .Select(i => new { i.Id, i.CompanyId, i.ItemCode })
            .ToList();
        var codes = sourceItems
            .Where(i => i.CompanyId != targetCompanyId)
            .Select(i => i.ItemCode)
            .Distinct()
            .ToList();
        if (codes.Count == 0) return map;

        var targetItemsByCode = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var targetItem in itemQuery
            .Where(i => i.CompanyId == targetCompanyId && codes.Contains(i.ItemCode))
            .Select(i => new { i.Id, i.ItemCode })
            .ToList())
        {
            targetItemsByCode.TryAdd(targetItem.ItemCode, targetItem.Id);
        }

        foreach (var src in sourceItems)
        {
            if (src.CompanyId != targetCompanyId && targetItemsByCode.TryGetValue(src.ItemCode, out var targetItemId))
            {
                map[src.Id] = targetItemId;
            }
        }

        return map;
    }

    /// <summary>
    /// Validates that line items in an inter-company transaction have rates matching the linked transaction.
    /// Per ERPNext PR #47780 / commit 3a2b863e7f.
    /// </summary>
    public async Task ValidateInterCompanyRatesAsync(
        Guid companyId,
        Guid? linkedSalesInvoiceId,
        Guid? linkedPurchaseInvoiceId,
        IEnumerable<(Guid ItemId, decimal UnitPrice, string? Description)> items,
        IEnumerable<string>? userRoles = null)
    {
        var settingsRepo = _accountsSettingsRepository ?? LazyServiceProvider?.LazyGetService<IRepository<Accounting.Entities.AccountsSettings, Guid>>();
        if (settingsRepo == null) return;
        var settings = (await settingsRepo.GetQueryableAsync()).FirstOrDefault();
        if (settings == null || !settings.MaintainSameInternalTransactionRate)
            return;

        var action = settings.MaintainSameRateAction ?? "Stop";
        var overrideRole = settings.RoleToOverrideStopAction;
        var canOverride = !string.IsNullOrEmpty(overrideRole)
            && userRoles != null && userRoles.Contains(overrideRole);

        var rateLines = new List<(string ItemDescription, decimal Rate, decimal ReferenceRate, string ReferenceDocType)>();

        if (linkedSalesInvoiceId.HasValue)
        {
            var si = await _salesInvoiceRepository.FindAsync(linkedSalesInvoiceId.Value);
            if (si != null)
            {
                var siRates = si.Items.GroupBy(i => i.ItemId).ToDictionary(g => g.Key, g => g.First().UnitPrice);
                foreach (var item in items)
                {
                    if (siRates.TryGetValue(item.ItemId, out var refRate))
                    {
                        rateLines.Add((item.Description ?? item.ItemId.ToString(), item.UnitPrice, refRate, $"Sales Invoice {si.InvoiceNumber}"));
                    }
                }
            }
        }
        else if (linkedPurchaseInvoiceId.HasValue)
        {
            var pi = await _purchaseInvoiceRepository.FindAsync(linkedPurchaseInvoiceId.Value);
            if (pi != null)
            {
                var piRates = pi.Items.GroupBy(i => i.ItemId).ToDictionary(g => g.Key, g => g.First().UnitPrice);
                foreach (var item in items)
                {
                    if (piRates.TryGetValue(item.ItemId, out var refRate))
                    {
                        rateLines.Add((item.Description ?? item.ItemId.ToString(), item.UnitPrice, refRate, $"Purchase Invoice {pi.InvoiceNumber}"));
                    }
                }
            }
        }

        if (rateLines.Count > 0)
        {
            var validationService = _transactionValidationService ?? LazyServiceProvider?.LazyGetService<TransactionValidationService>();
            validationService?.ValidateMaintainSameRate(rateLines, action, canOverride);
        }
    }

    /// <summary>
    /// Validates that an address is linked to the specified party.
    /// Per ERPNext PR #47463 / commit aed46ad5b9: Prevents cross-company address link leakage.
    /// </summary>
    private async Task<bool> ValidateAddressLinkAsync(Guid? addressId, string partyType, Guid partyId)
    {
        if (!addressId.HasValue) return false;
        var addressRepo = _addressRepository ?? LazyServiceProvider?.LazyGetService<IRepository<Address, Guid>>();
        if (addressRepo == null) return false;
        var address = await addressRepo.FindAsync(addressId.Value);
        return address != null && string.Equals(address.PartyType, partyType, StringComparison.OrdinalIgnoreCase) && address.PartyId == partyId;
    }
}
