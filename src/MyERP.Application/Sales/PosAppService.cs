using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.DomainServices;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

/// <summary>
/// Point of Sale application service.
/// Creates a Sales Invoice in Posted status directly (like ERPNext POS Invoice).
/// Deducts stock from the specified warehouse.
/// </summary>
[Authorize(MyERPPermissions.SalesInvoices.Create)]
public class PosAppService : ApplicationService, IPosAppService
{
    private readonly IRepository<SalesInvoice, Guid> _invoiceRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IDocumentNumberGenerator _numberGenerator;
    private readonly StockValuationService _stockValuationService;
    private readonly BinService _binService;

    public PosAppService(
        IRepository<SalesInvoice, Guid> invoiceRepository,
        IRepository<Item, Guid> itemRepository,
        IDocumentNumberGenerator numberGenerator,
        StockValuationService stockValuationService,
        BinService binService)
    {
        _invoiceRepository = invoiceRepository;
        _itemRepository = itemRepository;
        _numberGenerator = numberGenerator;
        _stockValuationService = stockValuationService;
        _binService = binService;
    }

    public async Task<PosInvoiceDto> CompleteSaleAsync(CreatePosInvoiceDto input)
    {
        // Validate posting period is not frozen/closed
        var postingOrchestrator = LazyServiceProvider
            .LazyGetRequiredService<Accounting.DomainServices.DocumentPostingOrchestrator>();
        await postingOrchestrator.ValidatePostingPeriodAsync(input.CompanyId, DateTime.UtcNow, "POS Invoice");

        // Validate an active POS Opening Entry exists for this POS Profile / company
        // Per ERPNext PR #46907 / commit 3de1b22480: validate if pos is opened before pos invoice creation
        var posOpeningRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PosOpeningEntry, Guid>>();
        var openingQuery = await posOpeningRepo.GetQueryableAsync();

        PosProfile? profile = null;
        if (input.PosProfileId.HasValue)
        {
            var profileRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PosProfile, Guid>>();
            profile = await profileRepo.FindAsync(input.PosProfileId.Value);

            var hasActiveSession = openingQuery.Any(
                e => e.CompanyId == input.CompanyId
                    && e.PosProfileId == input.PosProfileId.Value
                    && e.Status == PosOpeningStatus.Open);
            if (!hasActiveSession)
            {
                var profileName = profile?.ProfileName ?? input.PosProfileId.Value.ToString();
                throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.NoPosOpeningEntry)
                    .WithData("posProfile", profileName);
            }
        }
        else
        {
            var hasActiveSession = openingQuery.Any(
                e => e.CompanyId == input.CompanyId
                    && e.Status == PosOpeningStatus.Open);
            if (!hasActiveSession)
            {
                throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.NoPosOpeningEntry)
                    .WithData("posProfile", "Default");
            }
        }

        var invoiceNumber = await _numberGenerator.GenerateAsync("POS", input.CompanyId);

        var invoice = new SalesInvoice(
            GuidGenerator.Create(),
            input.CompanyId,
            input.CustomerId
                ?? throw new Volo.Abp.BusinessException("MyERP:01007")
                    .WithData("documentType", "POS Invoice — CustomerId is required"),
            invoiceNumber,
            DateTime.UtcNow,
            CurrentTenant.Id);

        // POS always deducts stock
        invoice.UpdateStock = true;
        invoice.WarehouseId = input.WarehouseId ?? profile?.WarehouseId;
        invoice.IsPos = true;
        invoice.PosProfileId = input.PosProfileId;
        if (profile?.ProjectId.HasValue == true)
        {
            invoice.ProjectId = profile.ProjectId;
        }

        // Multi-currency handling per ERPNext PR #58599 / commit f16f249a38
        var currency = !string.IsNullOrWhiteSpace(input.CurrencyCode)
            ? input.CurrencyCode
            : profile?.CurrencyCode ?? "MYR";
        invoice.CurrencyCode = currency;

        if (input.ExchangeRate.HasValue && input.ExchangeRate.Value > 0)
        {
            invoice.ExchangeRate = input.ExchangeRate.Value;
        }
        else
        {
            var companyRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Core.Entities.Company, Guid>>();
            var company = await companyRepo.GetAsync(input.CompanyId);
            if (currency != company.CurrencyCode)
            {
                var exchangeService = LazyServiceProvider.LazyGetRequiredService<Accounting.DomainServices.CurrencyExchangeService>();
                invoice.ExchangeRate = await exchangeService.GetExchangeRateAsync(currency, company.CurrencyCode, invoice.IssueDate);
            }
            else
            {
                invoice.ExchangeRate = 1m;
            }
        }

        // POS invoice due date defaults to issue date (ERPNext PR #49232 / commit 77478303fe)
        invoice.DueDate ??= invoice.IssueDate;

        foreach (var item in input.Items)
        {
            invoice.AddItem(item.ItemId, item.Description, item.Quantity, item.UnitPrice, item.TaxAmount);
        }

        // POS invoices go straight to Posted status; record customer payment received at counter
        invoice.AmountPaid = Math.Min(input.AmountReceived, invoice.GrandTotal);
        invoice.Submit();
        await _invoiceRepository.InsertAsync(invoice, autoSave: true);

        // Post() only flips status — the actual GL/PLE journal entry (AR debit, income/tax
        // credit) is built here. Without this call every POS sale reached Posted status with
        // zero ledger impact: no receivable, no revenue, no tax booked.
        invoice.Post();
        var glService = LazyServiceProvider.LazyGetRequiredService<Accounting.DomainServices.GlRepostService>();
        await glService.RebuildSalesInvoiceGlAsync(invoice);
        await _invoiceRepository.UpdateAsync(invoice, autoSave: true);

        // Deduct stock for stock items
        if (input.WarehouseId.HasValue)
        {
            // Batch load MaintainStock flags to avoid N+1
            var posItemIds = input.Items.Select(i => i.ItemId).Distinct().ToArray();
            var itemQuery = await _itemRepository.GetQueryableAsync();
            var stockItemIds = itemQuery
                .Where(i => posItemIds.Contains(i.Id) && i.MaintainStock)
                .Select(i => i.Id)
                .ToHashSet();

            foreach (var item in input.Items)
            {
                if (!stockItemIds.Contains(item.ItemId)) continue;

                await _stockValuationService.CreateLedgerEntryAsync(
                    input.CompanyId, item.ItemId, input.WarehouseId.Value,
                    DateTime.UtcNow, -item.Quantity, item.UnitPrice,
                    "SalesInvoice", invoice.Id, CurrentTenant.Id);

                await _binService.ApplyStockMovementAsync(
                    item.ItemId, input.WarehouseId.Value, -item.Quantity, -(item.Quantity * item.UnitPrice));
            }
        }

        var change = input.AmountReceived - invoice.GrandTotal;
        var actualChange = change > 0 ? change : 0;
        var baseChange = Math.Round(actualChange * invoice.ExchangeRate, 2);

        return new PosInvoiceDto
        {
            Id = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            IssueDate = invoice.IssueDate,
            NetTotal = invoice.NetTotal,
            TaxAmount = invoice.TaxAmount,
            GrandTotal = invoice.GrandTotal,
            AmountReceived = input.AmountReceived,
            Change = actualChange,
            BaseChange = baseChange,
            Status = invoice.Status.ToString(),
        };
    }

    public async Task<PagedResultDto<PosItemDto>> SearchItemsAsync(PosItemSearchDto input)
    {
        var query = await _itemRepository.GetQueryableAsync();

        var hideUnavailable = input.HideUnavailableItems;
        Guid? warehouseId = input.WarehouseId;

        if (input.PosProfileId.HasValue)
        {
            var posProfileRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PosProfile, Guid>>();
            var profile = await posProfileRepo.FindAsync(input.PosProfileId.Value);
            if (profile != null)
            {
                hideUnavailable = hideUnavailable || profile.HideUnavailableItems;
                warehouseId ??= profile.WarehouseId;
            }
        }

        if (hideUnavailable && warehouseId.HasValue)
        {
            // Per ERPNext PR #47493 / commit 57f3489dfa:
            // Non-stock items (service/digital) are NOT hidden even when HideUnavailableItems is active.
            // Stock items must have actual_qty > 0 in the specified warehouse.
            var binRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Bin, Guid>>();
            var binQuery = await binRepo.GetQueryableAsync();
            var availableStockItemIds = binQuery
                .Where(b => b.WarehouseId == warehouseId.Value && b.ActualQty > 0)
                .Select(b => b.ItemId);

            query = query.Where(i => !i.MaintainStock || availableStockItemIds.Contains(i.Id));
        }

        if (!string.IsNullOrWhiteSpace(input.Search))
        {
            var search = input.Search;
            query = query.Where(i => i.IsActive &&
                (i.ItemName.Contains(search) ||
                 i.ItemCode.Contains(search) ||
                 (i.Barcode != null && i.Barcode.Contains(search)) ||
                 i.Barcodes.Any(b => b.Barcode.Contains(search))));
        }
        else
        {
            query = query.Where(i => i.IsActive);
        }

        var items = query.Take(input.MaxResultCount).ToList();

        return new PagedResultDto<PosItemDto>(
            items.Count,
            items.Select(i => new PosItemDto
            {
                Id = i.Id,
                ItemCode = i.ItemCode,
                ItemName = i.ItemName,
                SellingPrice = i.StandardSellingPrice ?? 0,
                Uom = i.Uom,
                Barcode = i.Barcode,
            }).ToList());
    }

    public async Task<BarcodeScanResultDto> ScanBarcodeAsync(ScanBarcodeInput input)
    {
        var query = await _itemRepository.GetQueryableAsync();
        var barcode = input.Barcode?.Trim();
        if (string.IsNullOrEmpty(barcode))
            return new BarcodeScanResultDto { Found = false };

        // Per ERPNext barcode_scanner.js: search barcode → item code → serial no
        // Also checks the multi-barcode child table (case/carton codes, alternate symbologies)
        // for items carrying more than one scannable code.
        var item = query.FirstOrDefault(i =>
            i.IsActive && (i.Barcode == barcode || i.ItemCode == barcode
                || i.Barcodes.Any(b => b.Barcode == barcode)));

        if (item == null)
            return new BarcodeScanResultDto { Found = false };

        return new BarcodeScanResultDto
        {
            Found = true,
            ItemId = item.Id,
            ItemCode = item.ItemCode,
            ItemName = item.ItemName,
            Rate = item.StandardSellingPrice ?? 0,
            Uom = item.Uom,
            Barcode = item.Barcode,
        };
    }
}

