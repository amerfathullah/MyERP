using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core.DomainServices;
using MyERP.Dtos;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

[Authorize(MyERPPermissions.LandedCostVouchers.Default)]
public class LandedCostVoucherAppService : ApplicationService, ILandedCostVoucherAppService
{
    private readonly IRepository<LandedCostVoucher, Guid> _repository;
    private readonly IRepository<SerialNo, Guid> _serialNoRepository;
    private readonly IRepository<PurchaseReceipt, Guid> _purchaseReceiptRepository;
    private readonly IRepository<PurchaseInvoice, Guid> _purchaseInvoiceRepository;
    private readonly IRepository<JournalEntry, Guid> _journalEntryRepository;
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepository;
    private readonly StockValuationService _valuationService;
    private readonly BinService _binService;
    private readonly WarehouseAccountService _warehouseAccountService;
    private readonly IDocumentNumberGenerator _numberGenerator;

    public LandedCostVoucherAppService(
        IRepository<LandedCostVoucher, Guid> repository,
        IRepository<SerialNo, Guid> serialNoRepository,
        IRepository<PurchaseReceipt, Guid> purchaseReceiptRepository,
        IRepository<PurchaseInvoice, Guid> purchaseInvoiceRepository,
        IRepository<JournalEntry, Guid> journalEntryRepository,
        IRepository<FiscalYear, Guid> fiscalYearRepository,
        StockValuationService valuationService,
        BinService binService,
        WarehouseAccountService warehouseAccountService,
        IDocumentNumberGenerator numberGenerator)
    {
        _repository = repository;
        _serialNoRepository = serialNoRepository;
        _purchaseReceiptRepository = purchaseReceiptRepository;
        _purchaseInvoiceRepository = purchaseInvoiceRepository;
        _journalEntryRepository = journalEntryRepository;
        _fiscalYearRepository = fiscalYearRepository;
        _valuationService = valuationService;
        _binService = binService;
        _warehouseAccountService = warehouseAccountService;
        _numberGenerator = numberGenerator;
    }

    /// <summary>
    /// Resolves the real Warehouse for a landed-cost item's source receipt.
    /// LandedCostItem.ReceiptId is the source document's ID (a PurchaseReceipt or PurchaseInvoice),
    /// NOT a warehouse ID — both document types carry a single header-level WarehouseId.
    /// </summary>
    private async Task<Guid?> ResolveWarehouseIdAsync(string receiptType, Guid receiptId)
    {
        if (receiptType == "PurchaseReceipt")
        {
            var pr = await _purchaseReceiptRepository.FindAsync(receiptId);
            return pr?.WarehouseId;
        }
        if (receiptType == "PurchaseInvoice")
        {
            var pi = await _purchaseInvoiceRepository.FindAsync(receiptId);
            return pi?.WarehouseId;
        }
        return null;
    }

    public async Task<PagedResultDto<LandedCostVoucherDto>> GetListAsync(GetLandedCostVoucherListDto input)
    {
        var query = (await _repository.WithDetailsAsync()).AsQueryable();
        if (input.CompanyId.HasValue)
            query = query.Where(l => l.CompanyId == input.CompanyId.Value);
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var f = input.Filter;
            query = query.Where(l => (l.VoucherNumber ?? "").Contains(f));
        }

        var totalCount = query.Count();
        var items = query.OrderByDescending(l => l.PostingDate)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<LandedCostVoucherDto>(totalCount, items.Select(x => ObjectMapper.Map<LandedCostVoucher, LandedCostVoucherDto>(x)).ToList());
    }

    public async Task<LandedCostVoucherDto> GetAsync(Guid id)
    {
        var lcv = (await _repository.WithDetailsAsync()).First(l => l.Id == id);
        return ObjectMapper.Map<LandedCostVoucher, LandedCostVoucherDto>(lcv);
    }

    [Authorize(MyERPPermissions.LandedCostVouchers.Create)]
    public async Task<LandedCostVoucherDto> CreateAsync(CreateLandedCostVoucherDto input)
    {
        if (input.Items == null || !input.Items.Any())
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.LandedCostHasNoItems);
        if (input.Charges == null || !input.Charges.Any())
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.LandedCostHasNoCharges);

        // Validate all items are active
        var itemValidation = LazyServiceProvider.LazyGetRequiredService<MyERP.Inventory.DomainServices.ItemTransactionValidationService>();
        await itemValidation.ValidateItemsForTransactionAsync(input.Items.Select(i => i.ItemId).ToArray());

        var lcv = new LandedCostVoucher(GuidGenerator.Create(), input.CompanyId,
            input.PostingDate, CurrentTenant.Id)
        {
            VoucherNumber = await _numberGenerator.GenerateAsync("LCV", input.CompanyId),
            DistributionMethod = input.DistributionMethod,
            Notes = input.Notes,
        };

        foreach (var item in input.Items)
        {
            // Per gotcha #280: LCV requires Purchase Invoice to have UpdateStock=true
            if (string.Equals(item.ReceiptType, "PurchaseInvoice", StringComparison.OrdinalIgnoreCase))
            {
                var pi = await _purchaseInvoiceRepository.FindAsync(item.ReceiptId);
                if (pi != null && !pi.UpdateStock)
                {
                    throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                        .WithData("detail", $"Purchase Invoice {pi.InvoiceNumber} does not have Update Stock enabled. Landed Cost Voucher can only apply to Purchase Invoices with Update Stock.");
                }
            }

            lcv.AddItem(item.ReceiptId, item.ReceiptType, item.ItemId,
                item.Quantity, item.Amount, item.Description);
        }

        foreach (var charge in input.Charges)
            lcv.AddCharge(charge.Description, charge.ExpenseAccountId, charge.Amount,
                charge.CostCenterId, charge.ProjectId);

        await _repository.InsertAsync(lcv);
        return ObjectMapper.Map<LandedCostVoucher, LandedCostVoucherDto>(lcv);
    }

    [Authorize(MyERPPermissions.LandedCostVouchers.Submit)]
    public async Task<LandedCostVoucherDto> SubmitAsync(Guid id)
    {
        var lcv = (await _repository.WithDetailsAsync()).First(l => l.Id == id);

        // Validate posting period is not frozen/closed before creating SLE entries
        var postingOrchestrator = LazyServiceProvider.LazyGetRequiredService<Accounting.DomainServices.DocumentPostingOrchestrator>();
        await postingOrchestrator.ValidatePostingPeriodAsync(lcv.CompanyId, lcv.PostingDate, "LandedCostVoucher");

        lcv.Submit();

        // Resolve the real Warehouse for each item's receipt once (ReceiptId is a document ID, not a
        // warehouse ID — see ResolveWarehouseIdAsync). Cached per (ReceiptType, ReceiptId) pair since
        // multiple LCV items commonly share the same receipt.
        var warehouseCache = new Dictionary<(string, Guid), Guid?>();
        async Task<Guid?> GetWarehouseAsync(string receiptType, Guid receiptId)
        {
            var key = (receiptType, receiptId);
            if (!warehouseCache.TryGetValue(key, out var wid))
            {
                wid = await ResolveWarehouseIdAsync(receiptType, receiptId);
                warehouseCache[key] = wid;
            }
            return wid;
        }

        // Update stock valuation: create SLE entries for the additional cost per item
        // Each item gets a zero-qty entry with value = ApplicableCharges (rate adjustment)
        foreach (var item in lcv.Items)
        {
            if (item.ApplicableCharges <= 0) continue;

            var warehouseId = await GetWarehouseAsync(item.ReceiptType, item.ReceiptId);
            if (!warehouseId.HasValue) continue;

            // Create a rate-adjustment SLE: qty=0, value=ApplicableCharges
            await _valuationService.CreateLedgerEntryAsync(
                lcv.CompanyId,
                item.ItemId,
                warehouseId.Value,
                lcv.PostingDate,
                quantityChange: 0, // Zero qty — pure valuation adjustment
                incomingRate: item.ApplicableCharges, // The additional cost amount
                voucherType: "LandedCostVoucher",
                voucherId: lcv.Id,
                tenantId: lcv.TenantId);

            // Update Bin stock value (no qty change, only value change)
            await _binService.ApplyStockMovementAsync(
                item.ItemId, warehouseId.Value,
                0, item.ApplicableCharges, lcv.TenantId);
        }

        // Update serial number valuation rates (per DO-NOT: serials must reflect true landed cost)
        foreach (var item in lcv.Items.Where(i => i.ApplicableCharges > 0))
        {
            var warehouseId = await GetWarehouseAsync(item.ReceiptType, item.ReceiptId);
            if (!warehouseId.HasValue) continue;

            var serialQuery = await _serialNoRepository.GetQueryableAsync();
            var serials = serialQuery
                .Where(s => s.ItemId == item.ItemId && s.WarehouseId == warehouseId.Value)
                .ToList();

            if (serials.Count > 0)
            {
                // Distribute landed cost equally across serial numbers for this item
                var perSerialCharge = item.ApplicableCharges / serials.Count;
                foreach (var serial in serials)
                {
                    serial.PurchaseRate += perSerialCharge;
                    await _serialNoRepository.UpdateAsync(serial);
                }
            }
        }

        // Post GL: DR each affected warehouse's Stock account (sum of ApplicableCharges routed there),
        // CR each Charge's own ExpenseAccountId (its own Amount) — built manually rather than via
        // AccountingRuleEngine because each Charge's expense account is chosen per-transaction on the
        // document itself, not resolvable from a per-company AccountSource the generic engine supports.
        var chargeWarehouseTotals = new Dictionary<Guid, decimal>();
        foreach (var item in lcv.Items.Where(i => i.ApplicableCharges > 0))
        {
            var warehouseId = await GetWarehouseAsync(item.ReceiptType, item.ReceiptId);
            if (!warehouseId.HasValue) continue;
            chargeWarehouseTotals[warehouseId.Value] = chargeWarehouseTotals.GetValueOrDefault(warehouseId.Value) + item.ApplicableCharges;
        }

        var companyRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Core.Entities.Company, Guid>>();
        var company = await companyRepo.FindAsync(lcv.CompanyId);
        var isPerpetualInventory = company?.EnablePerpetualInventory ?? true;

        if (isPerpetualInventory && chargeWarehouseTotals.Count > 0 && lcv.Charges.Any())
        {
            var fyQuery = await _fiscalYearRepository.GetQueryableAsync();
            var fiscalYear = fyQuery.FirstOrDefault(fy =>
                fy.CompanyId == lcv.CompanyId && fy.StartDate <= lcv.PostingDate && fy.EndDate >= lcv.PostingDate);
            if (fiscalYear == null)
                throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.FiscalYearClosed)
                    .WithData("postingDate", lcv.PostingDate.ToString("yyyy-MM-dd"));

            var jeNumber = await _numberGenerator.GenerateAsync("JE", lcv.CompanyId);
            var je = new JournalEntry(GuidGenerator.Create(), lcv.CompanyId, fiscalYear.Id, lcv.PostingDate, lcv.TenantId)
            {
                EntryNumber = jeNumber,
                ReferenceType = "LandedCostVoucher",
                ReferenceId = lcv.Id,
                Narration = $"Landed cost allocation ({lcv.VoucherNumber})",
            };

            foreach (var (warehouseId, amount) in chargeWarehouseTotals)
            {
                var stockAccountId = await _warehouseAccountService.ResolveStockAccountAsync(warehouseId, lcv.CompanyId);
                je.AddLine(stockAccountId, amount, isDebit: true, description: "Landed cost — stock valuation increase");
            }

            foreach (var charge in lcv.Charges)
            {
                je.AddLineWithDimensions(charge.ExpenseAccountId, charge.Amount, isDebit: false,
                    costCenterId: charge.CostCenterId, projectId: charge.ProjectId, description: charge.Description);
            }

            je.Validate();
            je.Post();
            await _journalEntryRepository.InsertAsync(je);
        }

        // Update PurchaseReceiptItem.LandedCostVoucherAmount for each item
        // Per ERPNext PR #57475: tracks LCV allocation per receipt item for purchase expense GL deduction
        var prItemRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Purchasing.Entities.PurchaseReceiptItem, Guid>>();
        foreach (var lcvItem in lcv.Items.Where(i => i.ApplicableCharges > 0 && i.ReceiptType == "PurchaseReceipt"))
        {
            // Find PR items matching this LCV item's receipt + item
            var prItems = (await prItemRepo.GetQueryableAsync())
                .Where(pri => pri.PurchaseReceiptId == lcvItem.ReceiptId && pri.ItemId == lcvItem.ItemId)
                .ToList();

            if (prItems.Count > 0)
            {
                // Distribute applicable charges equally across matching PR items
                var perItemCharge = lcvItem.ApplicableCharges / prItems.Count;
                foreach (var prItem in prItems)
                {
                    prItem.LandedCostVoucherAmount += perItemCharge;
                    await prItemRepo.UpdateAsync(prItem);
                }
            }
        }

        await _repository.UpdateAsync(lcv);

        var activityRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Core.Entities.DocumentActivityLog, Guid>>();
        await activityRepo.InsertAsync(new MyERP.Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "LandedCostVoucher", lcv.Id, "Submitted",
            lcv.CompanyId, lcv.VoucherNumber, "Draft", "Submitted",
            CurrentUser.Id, tenantId: lcv.TenantId));

        return ObjectMapper.Map<LandedCostVoucher, LandedCostVoucherDto>(lcv);
    }

    [Authorize(MyERPPermissions.LandedCostVouchers.Cancel)]
    public async Task<LandedCostVoucherDto> CancelAsync(Guid id)
    {
        var lcv = (await _repository.WithDetailsAsync()).First(l => l.Id == id);
        lcv.Cancel();

        // Reverse the valuation adjustments (same warehouse resolution as SubmitAsync — see
        // ResolveWarehouseIdAsync — ReceiptId is a document ID, not a warehouse ID)
        foreach (var item in lcv.Items)
        {
            if (item.ApplicableCharges <= 0) continue;

            var warehouseId = await ResolveWarehouseIdAsync(item.ReceiptType, item.ReceiptId);
            if (!warehouseId.HasValue) continue;

            await _valuationService.CreateLedgerEntryAsync(
                lcv.CompanyId, item.ItemId, warehouseId.Value,
                lcv.PostingDate,
                quantityChange: 0,
                incomingRate: -item.ApplicableCharges, // Negative = reversal
                voucherType: "LandedCostVoucher",
                voucherId: lcv.Id,
                tenantId: lcv.TenantId);

            await _binService.ApplyStockMovementAsync(
                item.ItemId, warehouseId.Value,
                0, -item.ApplicableCharges, lcv.TenantId);
        }

        // Reverse the GL entry posted on Submit (swap debit↔credit per line, per the same pattern as
        // JournalEntryAppService.CreateReversalAsync — no shared helper exists for reversing a
        // programmatically-posted JE, so this mirrors that logic directly).
        var jeQuery = await _journalEntryRepository.GetQueryableAsync();
        var sourceJe = jeQuery.FirstOrDefault(j =>
            j.ReferenceType == "LandedCostVoucher" && j.ReferenceId == lcv.Id && !j.ReversalOfId.HasValue);
        if (sourceJe != null)
        {
            var reversalNumber = await _numberGenerator.GenerateAsync("JE", lcv.CompanyId);
            var reversal = new JournalEntry(GuidGenerator.Create(), sourceJe.CompanyId, sourceJe.FiscalYearId, DateTime.UtcNow, sourceJe.TenantId)
            {
                EntryNumber = reversalNumber,
                VoucherType = Accounting.JournalEntryVoucherType.Reversal,
                ReversalOfId = sourceJe.Id,
                ReferenceType = "LandedCostVoucher",
                ReferenceId = lcv.Id,
                Narration = $"Reversal of landed cost allocation ({lcv.VoucherNumber})",
            };
            foreach (var line in sourceJe.Lines)
            {
                reversal.AddLine(line.AccountId, line.Amount, !line.IsDebit,
                    line.Description != null ? $"Reversal: {line.Description}" : "Reversal entry");
            }
            reversal.Post();
            await _journalEntryRepository.InsertAsync(reversal);
        }

        // Reverse PurchaseReceiptItem.LandedCostVoucherAmount per PR #57475
        var prItemRepo2 = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Purchasing.Entities.PurchaseReceiptItem, Guid>>();
        foreach (var lcvItem in lcv.Items.Where(i => i.ApplicableCharges > 0 && i.ReceiptType == "PurchaseReceipt"))
        {
            var prItems = (await prItemRepo2.GetQueryableAsync())
                .Where(pri => pri.PurchaseReceiptId == lcvItem.ReceiptId && pri.ItemId == lcvItem.ItemId)
                .ToList();

            if (prItems.Count > 0)
            {
                var perItemCharge = lcvItem.ApplicableCharges / prItems.Count;
                foreach (var prItem in prItems)
                {
                    prItem.LandedCostVoucherAmount = Math.Max(0, prItem.LandedCostVoucherAmount - perItemCharge);
                    await prItemRepo2.UpdateAsync(prItem);
                }
            }
        }

        await _repository.UpdateAsync(lcv);

        var activityRepo2 = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Core.Entities.DocumentActivityLog, Guid>>();
        await activityRepo2.InsertAsync(new MyERP.Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "LandedCostVoucher", lcv.Id, "Cancelled",
            lcv.CompanyId, lcv.VoucherNumber, "Submitted", "Cancelled",
            CurrentUser.Id, tenantId: lcv.TenantId));

        return ObjectMapper.Map<LandedCostVoucher, LandedCostVoucherDto>(lcv);
    }

    /// <summary>
    /// Fetches eligible items from submitted Purchase Receipts, Purchase Invoices (with update_stock),
    /// or Stock Entries (Material Receipt) for Landed Cost Voucher allocation (Gotcha #5996).
    /// </summary>
    public async Task<List<LandedCostItemDto>> GetReceiptItemsAsync(GetLandedCostReceiptItemsInput input)
    {
        Check.NotDefaultOrNull<Guid>(input.CompanyId, nameof(input.CompanyId));
        if (input.ReceiptIds == null || input.ReceiptIds.Count == 0)
        {
            return new List<LandedCostItemDto>();
        }

        var result = new List<LandedCostItemDto>();

        if (string.Equals(input.ReceiptType, "PurchaseReceipt", StringComparison.OrdinalIgnoreCase))
        {
            var prQuery = await _purchaseReceiptRepository.WithDetailsAsync(pr => pr.Items);
            var receipts = prQuery
                .Where(pr => pr.CompanyId == input.CompanyId && input.ReceiptIds.Contains(pr.Id))
                .ToList();

            foreach (var receipt in receipts)
            {
                if (receipt.Status != Core.DocumentStatus.Submitted)
                {
                    throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                        .WithData("detail", $"Purchase Receipt '{receipt.ReceiptNumber}' must be submitted.");
                }

                foreach (var item in receipt.Items)
                {
                    result.Add(new LandedCostItemDto
                    {
                        Id = Guid.NewGuid(),
                        ReceiptId = receipt.Id,
                        ReceiptType = "PurchaseReceipt",
                        ItemId = item.ItemId,
                        Description = item.Description,
                        Quantity = item.Quantity,
                        Amount = item.LineTotal - item.TaxAmount,
                        ApplicableCharges = 0m
                    });
                }
            }
        }
        else if (string.Equals(input.ReceiptType, "PurchaseInvoice", StringComparison.OrdinalIgnoreCase))
        {
            var piQuery = await _purchaseInvoiceRepository.WithDetailsAsync(pi => pi.Items);
            var invoices = piQuery
                .Where(pi => pi.CompanyId == input.CompanyId && input.ReceiptIds.Contains(pi.Id))
                .ToList();

            foreach (var invoice in invoices)
            {
                if (invoice.Status != Core.DocumentStatus.Submitted)
                {
                    throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                        .WithData("detail", $"Purchase Invoice '{invoice.InvoiceNumber}' must be submitted.");
                }

                if (!invoice.UpdateStock)
                {
                    throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                        .WithData("detail", $"Purchase Invoice '{invoice.InvoiceNumber}' must have Update Stock enabled for Landed Cost Voucher.");
                }

                foreach (var item in invoice.Items)
                {
                    result.Add(new LandedCostItemDto
                    {
                        Id = Guid.NewGuid(),
                        ReceiptId = invoice.Id,
                        ReceiptType = "PurchaseInvoice",
                        ItemId = item.ItemId,
                        Description = item.Description,
                        Quantity = item.Quantity,
                        Amount = item.LineTotal - item.TaxAmount,
                        ApplicableCharges = 0m
                    });
                }
            }
        }
        else if (string.Equals(input.ReceiptType, "StockEntry", StringComparison.OrdinalIgnoreCase))
        {
            var stockEntryRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<StockEntry, Guid>>();
            var seQuery = await stockEntryRepo.WithDetailsAsync(se => se.Items);
            var stockEntries = seQuery
                .Where(se => se.CompanyId == input.CompanyId && input.ReceiptIds.Contains(se.Id))
                .ToList();

            foreach (var se in stockEntries)
            {
                if (se.Status != Core.DocumentStatus.Submitted)
                {
                    throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                        .WithData("detail", $"Stock Entry '{se.EntryNumber}' must be submitted.");
                }

                if (se.EntryType != StockEntryType.MaterialReceipt)
                {
                    throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                        .WithData("detail", $"Stock Entry '{se.EntryNumber}' must be of entry type Material Receipt.");
                }

                foreach (var item in se.Items)
                {
                    result.Add(new LandedCostItemDto
                    {
                        Id = Guid.NewGuid(),
                        ReceiptId = se.Id,
                        ReceiptType = "StockEntry",
                        ItemId = item.ItemId,
                        Description = item.ItemId.ToString(),
                        Quantity = item.Quantity,
                        Amount = (item.ValuationRate ?? 0m) * item.Quantity,
                        ApplicableCharges = 0m
                    });
                }
            }
        }
        else
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Unsupported receipt type: {input.ReceiptType}");
        }

        return result;
    }

    /// <summary>
    /// Distributes total charges proportionally across item rows based on amount, quantity, or manual distribution (Gotcha #5996).
    /// </summary>
    public Task<LandedCostDistributionResultDto> CalculateDistributionAsync(CalculateLandedCostDistributionDto input)
    {
        var totalCharges = input.TotalCharges > 0 
            ? input.TotalCharges 
            : input.Charges.Sum(c => c.Amount);

        if (input.Items == null || input.Items.Count == 0 || totalCharges <= 0)
        {
            return Task.FromResult(new LandedCostDistributionResultDto
            {
                TotalCharges = totalCharges,
                TotalDistributedAmount = 0m,
                DistributedItems = input.Items?.ToList() ?? new List<LandedCostItemDto>()
            });
        }

        var items = input.Items.ToList();

        if (input.DistributionMethod == LandedCostDistributionMethod.Manual)
        {
            var sumManual = items.Sum(i => i.ApplicableCharges);
            return Task.FromResult(new LandedCostDistributionResultDto
            {
                TotalCharges = totalCharges,
                TotalDistributedAmount = sumManual,
                DistributedItems = items
            });
        }

        if (input.DistributionMethod == LandedCostDistributionMethod.BasedOnQuantity)
        {
            var totalQty = items.Sum(i => i.Quantity);
            if (totalQty <= 0)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", "Total item quantity must be greater than zero for quantity-based distribution.");
            }

            decimal allocatedSoFar = 0m;
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (i == items.Count - 1)
                {
                    // Last item absorbs rounding remainder
                    item.ApplicableCharges = Math.Round(totalCharges - allocatedSoFar, 2);
                }
                else
                {
                    var share = Math.Round((item.Quantity / totalQty) * totalCharges, 2);
                    item.ApplicableCharges = share;
                    allocatedSoFar += share;
                }
            }
        }
        else // BasedOnAmount (default)
        {
            var totalAmount = items.Sum(i => i.Amount);
            if (totalAmount <= 0)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", "Total item amount must be greater than zero for amount-based distribution.");
            }

            decimal allocatedSoFar = 0m;
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (i == items.Count - 1)
                {
                    // Last item absorbs rounding remainder
                    item.ApplicableCharges = Math.Round(totalCharges - allocatedSoFar, 2);
                }
                else
                {
                    var share = Math.Round((item.Amount / totalAmount) * totalCharges, 2);
                    item.ApplicableCharges = share;
                    allocatedSoFar += share;
                }
            }
        }

        var totalAllocated = items.Sum(i => i.ApplicableCharges);

        return Task.FromResult(new LandedCostDistributionResultDto
        {
            TotalCharges = totalCharges,
            TotalDistributedAmount = totalAllocated,
            DistributedItems = items
        });
    }
}

