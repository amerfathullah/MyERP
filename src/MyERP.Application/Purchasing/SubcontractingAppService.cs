using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.DomainServices;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using MyERP.Settings;

namespace MyERP.Purchasing;

[Authorize(MyERPPermissions.PurchaseOrders.Default)]
public class SubcontractingAppService : ApplicationService, ISubcontractingAppService
{
    private readonly IRepository<SubcontractingOrder, Guid> _scoRepository;
    private readonly IRepository<SubcontractingReceipt, Guid> _scrRepository;
    private readonly IDocumentNumberGenerator _numberGenerator;
    private readonly StockValuationService _stockValuationService;
    private readonly BinService _binService;

    public SubcontractingAppService(
        IRepository<SubcontractingOrder, Guid> scoRepository,
        IRepository<SubcontractingReceipt, Guid> scrRepository,
        IDocumentNumberGenerator numberGenerator,
        StockValuationService stockValuationService,
        BinService binService)
    {
        _scoRepository = scoRepository;
        _scrRepository = scrRepository;
        _numberGenerator = numberGenerator;
        _stockValuationService = stockValuationService;
        _binService = binService;
    }

    // === Subcontracting Order ===

    public async Task<SubcontractingOrderDto> GetOrderAsync(Guid id)
    {
        var sco = await _scoRepository.GetAsync(id, includeDetails: true);
        return ObjectMapper.Map<SubcontractingOrder, SubcontractingOrderDto>(sco);
    }

    public async Task<PagedResultDto<SubcontractingOrderDto>> GetOrderListAsync(GetScoListDto input)
    {
        var query = await _scoRepository.GetQueryableAsync();
        if (input.Status.HasValue)
            query = query.Where(s => s.Status == input.Status.Value);
        if (input.CompanyId.HasValue)
            query = query.Where(s => s.CompanyId == input.CompanyId.Value);

        var totalCount = query.Count();
        var items = query.OrderByDescending(s => s.OrderDate)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<SubcontractingOrderDto>(totalCount, items.Select(ObjectMapper.Map<SubcontractingOrder, SubcontractingOrderDto>).ToList());
    }

    [Authorize(MyERPPermissions.PurchaseOrders.Create)]
    public async Task<SubcontractingOrderDto> CreateOrderAsync(CreateSubcontractingOrderDto input)
    {
        if (input.Items == null || !input.Items.Any())
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustHaveItems);

        var poManager = LazyServiceProvider.LazyGetRequiredService<Purchasing.DomainServices.PurchaseOrderManager>();
        await poManager.ValidateSupplierEligibilityAsync(input.SupplierId);

        var supplierRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Supplier, Guid>>();
        var supplier = await supplierRepo.GetAsync(input.SupplierId);
        if (supplier.RepresentsCompanyId.HasValue && supplier.RepresentsCompanyId.Value == input.CompanyId)
        {
            throw new BusinessException(MyERPDomainErrorCodes.PartyCannotRepresentOwnCompany);
        }

        var itemValidation = LazyServiceProvider.LazyGetRequiredService<MyERP.Inventory.DomainServices.ItemTransactionValidationService>();
        await itemValidation.ValidateItemsForTransactionAsync(input.Items.Select(i => i.ItemId).ToArray());

        var number = await _numberGenerator.GenerateAsync("SCO", input.CompanyId);
        var sco = new SubcontractingOrder(GuidGenerator.Create(), input.CompanyId, number,
            input.OrderDate, input.SupplierId, CurrentTenant.Id)
        { PurchaseOrderId = input.PurchaseOrderId, Notes = input.Notes };

        foreach (var item in input.Items)
        {
            sco.AddItem(new SubcontractingOrderItem(
                GuidGenerator.Create(), sco.Id, item.ItemId, item.ItemName, item.Qty, item.Rate)
            { BomId = item.BomId, WarehouseId = item.WarehouseId });
        }

        await _scoRepository.InsertAsync(sco);
        return ObjectMapper.Map<SubcontractingOrder, SubcontractingOrderDto>(sco);
    }

    [Authorize(MyERPPermissions.PurchaseOrders.Submit)]
    public async Task<SubcontractingOrderDto> SubmitOrderAsync(Guid id)
    {
        var sco = await _scoRepository.GetAsync(id, includeDetails: true);

        // Populate RM requirements (SuppliedItems) from BOM explosion on first submit.
        // Without this, SuppliedItems stays empty forever: ValidateTransferQuantityAsync
        // (called from StockEntryAppService.SubmitAsync for every "Send to Subcontractor"
        // entry) sums an empty list to a required qty of 0, so ANY transfer qty > 0 exceeds
        // "remaining" and hard-blocks with OverTransfer — every RM transfer against every
        // SCO fails unconditionally. Also reserves the required qty in Bin so
        // CloseOrderAsync's existing release-on-close logic has something real to release.
        if (!sco.SuppliedItems.Any())
        {
            var rmService = LazyServiceProvider.LazyGetRequiredService<DomainServices.SubcontractingRmTransferService>();
            var requirements = await rmService.CalculateRmRequirementsAsync(id);
            foreach (var req in requirements)
            {
                var reserveWarehouseId = req.SourceWarehouseId ?? req.WarehouseId;
                sco.AddSuppliedItem(new SubcontractingOrderSuppliedItem(
                    GuidGenerator.Create(), sco.Id, req.ItemId, req.ItemName ?? string.Empty, req.RequiredQty)
                { ReserveWarehouseId = reserveWarehouseId });

                if (reserveWarehouseId.HasValue)
                {
                    await _binService.UpdateReservedQtyForSubContractAsync(
                        req.ItemId, reserveWarehouseId.Value, req.RequiredQty);
                }
            }
        }

        sco.Submit();
        await _scoRepository.UpdateAsync(sco);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "SubcontractingOrder", sco.Id,
            "Submitted", sco.CompanyId,
            sco.OrderNumber, "Draft", "Submitted", CurrentUser.Id,
            $"Subcontracting Order {sco.OrderNumber} submitted", CurrentTenant.Id));

        return ObjectMapper.Map<SubcontractingOrder, SubcontractingOrderDto>(sco);
    }

    [Authorize(MyERPPermissions.PurchaseOrders.Cancel)]
    public async Task<SubcontractingOrderDto> CancelOrderAsync(Guid id)
    {
        var sco = await _scoRepository.GetAsync(id, includeDetails: true);
        sco.Cancel();
        await _scoRepository.UpdateAsync(sco);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "SubcontractingOrder", sco.Id,
            "Cancelled", sco.CompanyId,
            sco.OrderNumber, sco.Status.ToString(), "Cancelled", CurrentUser.Id,
            $"Subcontracting Order {sco.OrderNumber} cancelled", CurrentTenant.Id));

        return ObjectMapper.Map<SubcontractingOrder, SubcontractingOrderDto>(sco);
    }

    /// <summary>
    /// Close an open/partially-received SCO, releasing RM reservation for unreceived supplied items.
    /// Per upstream PR #57463: pending RM reservation must be released on close.
    /// </summary>
    [Authorize(MyERPPermissions.PurchaseOrders.Edit)]
    public async Task<SubcontractingOrderDto> CloseOrderAsync(Guid id)
    {
        var sco = await _scoRepository.GetAsync(id, includeDetails: true);
        sco.Close();

        // Release RM reservation for unreceived supplied items
        foreach (var item in sco.SuppliedItems)
        {
            var pendingQty = Math.Max(0, item.RequiredQty - item.ConsumedQty);
            if (pendingQty > 0 && item.ReserveWarehouseId.HasValue)
            {
                await _binService.UpdateReservedQtyForSubContractAsync(
                    item.ItemId, item.ReserveWarehouseId.Value, -pendingQty);
            }
        }

        await _scoRepository.UpdateAsync(sco);
        return ObjectMapper.Map<SubcontractingOrder, SubcontractingOrderDto>(sco);
    }

    /// <summary>
    /// Reopens a closed Subcontracting Order and re-reserves RM in Bin for pending supplied items (Gotcha #5993).
    /// </summary>
    [Authorize(MyERPPermissions.PurchaseOrders.Edit)]
    public async Task<SubcontractingOrderDto> ReopenOrderAsync(Guid id)
    {
        var sco = await _scoRepository.GetAsync(id, includeDetails: true);
        sco.Reopen();

        // Re-apply RM reservation for unconsumed supplied items
        foreach (var item in sco.SuppliedItems)
        {
            var pendingQty = Math.Max(0, item.RequiredQty - item.ConsumedQty);
            if (pendingQty > 0 && item.ReserveWarehouseId.HasValue)
            {
                await _binService.UpdateReservedQtyForSubContractAsync(
                    item.ItemId, item.ReserveWarehouseId.Value, pendingQty);
            }
        }

        await _scoRepository.UpdateAsync(sco);

        var activityLogRepo = LazyServiceProvider?.LazyGetService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        if (activityLogRepo != null)
        {
            await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
                GuidGenerator?.Create() ?? Guid.NewGuid(), "SubcontractingOrder", sco.Id,
                "Reopened", sco.CompanyId,
                sco.OrderNumber, "Closed", sco.Status.ToString(), CurrentUser?.Id,
                $"Subcontracting Order {sco.OrderNumber} reopened", CurrentTenant?.Id));
        }

        return new SubcontractingOrderDto
        {
            Id = sco.Id,
            OrderNumber = sco.OrderNumber,
            OrderDate = sco.OrderDate,
            SupplierId = sco.SupplierId,
            CompanyId = sco.CompanyId,
            NetTotal = sco.NetTotal,
            GrandTotal = sco.GrandTotal,
            Status = sco.Status,
            PerReceived = sco.PerReceived,
            SupplierWarehouseId = sco.SupplierWarehouseId,
            Items = sco.Items.Select(i => new ScoItemDto
            {
                Id = i.Id,
                ItemId = i.ItemId,
                ItemName = i.ItemName,
                Qty = i.Qty,
                Rate = i.Rate,
                ReceivedQty = i.ReceivedQty
            }).ToList()
        };
    }

    /// <summary>
    /// Computes summary metrics for a Subcontracting Order (Gotcha #5993).
    /// </summary>
    [Authorize(MyERPPermissions.PurchaseOrders.Default)]
    public async Task<SubcontractingOrderSummaryDto> GetOrderSummaryAsync(Guid id)
    {
        var sco = await _scoRepository.GetAsync(id, includeDetails: true);
        return new SubcontractingOrderSummaryDto
        {
            Id = sco.Id,
            OrderNumber = sco.OrderNumber,
            Status = (int)sco.Status,
            NetTotal = sco.NetTotal,
            PerReceived = sco.PerReceived,
            TotalItemsCount = sco.Items.Count,
            TotalSuppliedItemsCount = sco.SuppliedItems.Count,
            TotalOrderedQty = sco.Items.Sum(i => i.Qty),
            TotalReceivedQty = sco.Items.Sum(i => i.ReceivedQty),
            CanReopen = sco.Status == SubcontractingOrderStatus.Closed,
            CanClose = sco.Status is SubcontractingOrderStatus.Open or SubcontractingOrderStatus.PartiallyReceived,
            CanCancel = sco.Status is not (SubcontractingOrderStatus.Cancelled or SubcontractingOrderStatus.Completed)
        };
    }

    /// <summary>
    /// Creates a Draft Stock Entry (SendToSubcontractor) with pending RM items from the SCO's BOM.
    /// Per ERPNext make_rm_stock_entry: resolves RM requirements, caps at pending qty, creates SE.
    /// </summary>
    [Authorize(MyERPPermissions.StockEntries.Create)]
    public async Task<RmTransferResultDto> CreateRmTransferStockEntryAsync(Guid scoId)
    {
        var sco = await _scoRepository.GetAsync(scoId, includeDetails: true);
        if (sco.Status == SubcontractingOrderStatus.Draft || sco.Status == SubcontractingOrderStatus.Cancelled)
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("status", sco.Status.ToString());

        var rmService = LazyServiceProvider.LazyGetRequiredService<DomainServices.SubcontractingRmTransferService>();
        var requirements = await rmService.CalculateRmRequirementsAsync(scoId);

        var pendingItems = requirements.Where(r => r.PendingQty > 0).ToList();
        if (!pendingItems.Any())
            throw new Volo.Abp.BusinessException("MyERP:10013");

        var seRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<StockEntry, Guid>>();
        var number = await _numberGenerator.GenerateAsync("SE", sco.CompanyId);
        var se = new StockEntry(GuidGenerator.Create(), sco.CompanyId,
            Inventory.StockEntryType.SendToSubcontractor, DateTime.UtcNow.Date, CurrentTenant.Id)
        { EntryNumber = number, SubcontractingOrderId = sco.Id };

        foreach (var rm in pendingItems)
        {
            se.AddItem(rm.ItemId, rm.PendingQty,
                sourceWarehouseId: rm.SourceWarehouseId ?? rm.WarehouseId,
                targetWarehouseId: sco.SupplierWarehouseId,
                valuationRate: null);
        }

        await seRepo.InsertAsync(se);

        return new RmTransferResultDto
        {
            StockEntryId = se.Id,
            EntryNumber = number,
            ItemCount = pendingItems.Count,
            TotalQty = pendingItems.Sum(i => i.PendingQty),
        };
    }

    // === Subcontracting Receipt ===

    [Authorize(MyERPPermissions.PurchaseReceipts.Create)]
    public async Task<SubcontractingReceiptDto> CreateReceiptAsync(CreateSubcontractingReceiptDto input)
    {
        if (input.Items == null || !input.Items.Any())
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustHaveItems);

        var supplierRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Supplier, Guid>>();
        var supplier = await supplierRepo.GetAsync(input.SupplierId);
        if (supplier.RepresentsCompanyId.HasValue && supplier.RepresentsCompanyId.Value == input.CompanyId)
        {
            throw new BusinessException(MyERPDomainErrorCodes.PartyCannotRepresentOwnCompany);
        }

        var itemValidation = LazyServiceProvider.LazyGetRequiredService<MyERP.Inventory.DomainServices.ItemTransactionValidationService>();
        await itemValidation.ValidateItemsForTransactionAsync(input.Items.Select(i => i.ItemId).ToArray());

        var number = await _numberGenerator.GenerateAsync("SCR", input.CompanyId);
        var scr = new SubcontractingReceipt(GuidGenerator.Create(), input.CompanyId, number,
            input.PostingDate, input.SupplierId, input.SubcontractingOrderId, CurrentTenant.Id)
        { WarehouseId = input.WarehouseId };

        foreach (var item in input.Items)
        {
            scr.AddItem(new SubcontractingReceiptItem(
                GuidGenerator.Create(), scr.Id, item.ItemId, item.ItemName, item.Qty, item.Rate)
            {
                WarehouseId = item.WarehouseId,
                CostCenterId = item.CostCenterId,
                ExpenseAccountId = item.ExpenseAccountId,
                ServiceExpenseAccountId = item.ServiceExpenseAccountId
            });
        }

        await _scrRepository.InsertAsync(scr);
        return ObjectMapper.Map<SubcontractingReceipt, SubcontractingReceiptDto>(scr);
    }

    [Authorize(MyERPPermissions.PurchaseReceipts.Submit)]
    public async Task<SubcontractingReceiptDto> SubmitReceiptAsync(Guid id)
    {
        var scr = await _scrRepository.GetAsync(id, includeDetails: true);

        // Validate receipt against SCO (qty caps, status guard) via domain service
        var scManager = LazyServiceProvider.LazyGetRequiredService<MyERP.Purchasing.DomainServices.SubcontractingManager>();
        await scManager.ValidateReceiptAgainstOrderAsync(scr);

        scr.Submit();

        // Stock-in for received finished goods
        foreach (var item in scr.Items)
        {
            var warehouseId = item.WarehouseId ?? scr.WarehouseId
                ?? throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.MissingWarehouse);

            await _stockValuationService.CreateLedgerEntryAsync(
                scr.CompanyId, item.ItemId, warehouseId, scr.PostingDate,
                item.Qty, item.Rate, "SubcontractingReceipt", scr.Id, scr.TenantId);

            await _binService.ApplyStockMovementAsync(item.ItemId, warehouseId, item.Qty, item.Qty * item.Rate);
        }

        // Update linked SCO fulfillment via domain service (replaces inline logic)
        await scManager.UpdateOrderOnReceiptAsync(scr, reverse: false);

        // RM consumption: calculate and track consumed quantities
        // Per ERPNext subcontracting_controller: RM consumed proportional to received FG qty
        // Per ERPNext PR #46892 / commit 7479e1ec32: backflush setting is ignored on returns
        var totalReceivedFgQty = scr.Items.Sum(i => i.Qty);
        var sco = await _scoRepository.GetAsync(scr.SubcontractingOrderId);
        var backflushSetting = await SettingProvider.GetOrNullAsync(
            MyERPSettings.Buying.BackflushSubcontractBasedOn) ?? "BOM";
        var rmConsumptions = scManager.CalculateRmConsumption(sco, totalReceivedFgQty, backflushSetting, scr.IsReturn);

        foreach (var rm in rmConsumptions)
        {
            if (rm.ConsumedQty <= 0 || !rm.WarehouseId.HasValue) continue;

            // Update SCO supplied item consumed qty
            var suppliedItem = sco.SuppliedItems.FirstOrDefault(si => si.ItemId == rm.ItemId);
            if (suppliedItem != null)
            {
                suppliedItem.ConsumedQty += rm.ConsumedQty;
            }

            // Create SLE for RM consumption (stock-out from supplier warehouse)
            await _stockValuationService.CreateLedgerEntryAsync(
                scr.CompanyId, rm.ItemId, rm.WarehouseId.Value, scr.PostingDate,
                -rm.ConsumedQty, 0, "SubcontractingReceipt", scr.Id, scr.TenantId);

            await _binService.ApplyStockMovementAsync(rm.ItemId, rm.WarehouseId.Value, -rm.ConsumedQty, 0);
        }

        await _scoRepository.UpdateAsync(sco);

        // GL posting: DR Stock, CR Stock Received But Not Billed (perpetual inventory)
        var postingOrchestrator = LazyServiceProvider.LazyGetRequiredService<MyERP.Accounting.DomainServices.DocumentPostingOrchestrator>();
        await postingOrchestrator.PostPurchaseReceiptAsync(scr);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "SubcontractingReceipt", scr.Id,
            "Submitted", scr.CompanyId,
            scr.ReceiptNumber, "Draft", "Submitted", CurrentUser.Id,
            $"Subcontracting Receipt {scr.ReceiptNumber} submitted", CurrentTenant.Id));

        await _scrRepository.UpdateAsync(scr);
        return ObjectMapper.Map<SubcontractingReceipt, SubcontractingReceiptDto>(scr);
    }

    [Authorize(MyERPPermissions.PurchaseReceipts.Cancel)]
    public async Task<SubcontractingReceiptDto> CancelReceiptAsync(Guid id)
    {
        var scr = await _scrRepository.GetAsync(id, includeDetails: true);
        scr.Cancel();

        // Reverse stock-in for FG items
        foreach (var item in scr.Items)
        {
            var warehouseId = item.WarehouseId ?? scr.WarehouseId;
            if (warehouseId.HasValue)
            {
                await _stockValuationService.CreateLedgerEntryAsync(
                    scr.CompanyId, item.ItemId, warehouseId.Value, scr.PostingDate,
                    -item.Qty, item.Rate, "SubcontractingReceipt", scr.Id, scr.TenantId);

                await _binService.ApplyStockMovementAsync(item.ItemId, warehouseId.Value, -item.Qty, -(item.Qty * item.Rate));
            }
        }

        // Reverse SCO fulfillment via domain service
        var scManager = LazyServiceProvider.LazyGetRequiredService<MyERP.Purchasing.DomainServices.SubcontractingManager>();
        await scManager.UpdateOrderOnReceiptAsync(scr, reverse: true);

        // Reverse RM consumption
        var totalReceivedFgQty = scr.Items.Sum(i => i.Qty);
        var sco = await _scoRepository.GetAsync(scr.SubcontractingOrderId);
        var rmConsumptions = scManager.CalculateRmConsumption(sco, totalReceivedFgQty);

        foreach (var rm in rmConsumptions)
        {
            if (rm.ConsumedQty <= 0 || !rm.WarehouseId.HasValue) continue;

            var suppliedItem = sco.SuppliedItems.FirstOrDefault(si => si.ItemId == rm.ItemId);
            if (suppliedItem != null)
            {
                suppliedItem.ConsumedQty = Math.Max(0, suppliedItem.ConsumedQty - rm.ConsumedQty);
            }

            // Reverse SLE for RM consumption (stock back in)
            await _stockValuationService.CreateLedgerEntryAsync(
                scr.CompanyId, rm.ItemId, rm.WarehouseId.Value, scr.PostingDate,
                rm.ConsumedQty, 0, "SubcontractingReceipt", scr.Id, scr.TenantId);

            await _binService.ApplyStockMovementAsync(rm.ItemId, rm.WarehouseId.Value, rm.ConsumedQty, 0);
        }

        await _scoRepository.UpdateAsync(sco);

        // Reverse GL entries for the receipt
        var postingOrchestrator = LazyServiceProvider.LazyGetRequiredService<MyERP.Accounting.DomainServices.DocumentPostingOrchestrator>();
        await postingOrchestrator.ReversePleForDocumentAsync("SubcontractingReceipt", scr.Id);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "SubcontractingReceipt", scr.Id,
            "Cancelled", scr.CompanyId,
            scr.ReceiptNumber, "Submitted", "Cancelled", CurrentUser.Id,
            $"Subcontracting Receipt {scr.ReceiptNumber} cancelled", CurrentTenant.Id));

        await _scrRepository.UpdateAsync(scr);
        return ObjectMapper.Map<SubcontractingReceipt, SubcontractingReceiptDto>(scr);
    }

    /// <summary>
    /// Creates a return Subcontracting Receipt against an original submitted receipt (Gotcha #5997).
    /// </summary>
    [Authorize(MyERPPermissions.PurchaseReceipts.Create)]
    public async Task<SubcontractingReceiptDto> CreateReceiptReturnAsync(CreateSubcontractingReceiptReturnDto input)
    {
        var original = await _scrRepository.GetAsync(input.ReturnAgainstReceiptId, includeDetails: true);
        if (original.Status != SubcontractingReceiptStatus.Submitted)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("detail", "Cannot create a return against a non-submitted Subcontracting Receipt.");
        }

        if (original.IsReturn)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("detail", "Cannot create a return against another return Subcontracting Receipt.");
        }

        if (input.Items == null || !input.Items.Any())
        {
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustHaveItems);
        }

        var allReceiptsQuery = await _scrRepository.GetQueryableAsync();
        var priorReturns = allReceiptsQuery
            .Where(r => r.ReturnAgainstReceiptId == original.Id && r.Status != SubcontractingReceiptStatus.Cancelled)
            .ToList();

        var priorReturnedByItem = priorReturns
            .SelectMany(r => r.Items)
            .GroupBy(i => i.ItemId)
            .ToDictionary(g => g.Key, g => g.Sum(i => Math.Abs(i.Qty)));

        var originalItemsByItem = original.Items.ToDictionary(i => i.ItemId);

        foreach (var retItem in input.Items)
        {
            if (!originalItemsByItem.TryGetValue(retItem.ItemId, out var origItem))
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Item {retItem.ItemId} does not exist in original receipt.");
            }

            var priorReturned = priorReturnedByItem.GetValueOrDefault(retItem.ItemId, 0m);
            var maxReturnable = origItem.Qty - priorReturned;

            if (retItem.Qty > maxReturnable)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ReturnQtyExceedsOriginal)
                    .WithData("itemId", retItem.ItemId)
                    .WithData("returnQty", retItem.Qty)
                    .WithData("maxReturnable", maxReturnable);
            }
        }

        var number = await _numberGenerator.GenerateAsync("SCR-RET", original.CompanyId);
        var returnReceipt = new SubcontractingReceipt(
            Guid.NewGuid(),
            original.CompanyId,
            number,
            input.PostingDate,
            original.SupplierId,
            original.SubcontractingOrderId,
            original.TenantId)
        {
            IsReturn = true,
            ReturnAgainstReceiptId = original.Id,
            WarehouseId = original.WarehouseId
        };

        foreach (var retItem in input.Items)
        {
            var origItem = originalItemsByItem[retItem.ItemId];
            var negativeQty = -Math.Abs(retItem.Qty);
            returnReceipt.AddItem(new SubcontractingReceiptItem(
                Guid.NewGuid(),
                returnReceipt.Id,
                retItem.ItemId,
                retItem.ItemName,
                negativeQty,
                retItem.Rate > 0 ? retItem.Rate : origItem.Rate)
            {
                WarehouseId = retItem.WarehouseId ?? origItem.WarehouseId ?? original.WarehouseId,
                CostCenterId = origItem.CostCenterId,
                ExpenseAccountId = origItem.ExpenseAccountId,
                ServiceExpenseAccountId = origItem.ServiceExpenseAccountId
            });
        }

        await _scrRepository.InsertAsync(returnReceipt, autoSave: true);
        return new SubcontractingReceiptDto
        {
            Id = returnReceipt.Id,
            ReceiptNumber = returnReceipt.ReceiptNumber,
            PostingDate = returnReceipt.PostingDate,
            SupplierId = returnReceipt.SupplierId,
            SubcontractingOrderId = returnReceipt.SubcontractingOrderId,
            NetTotal = returnReceipt.NetTotal,
            Status = returnReceipt.Status,
            IsReturn = returnReceipt.IsReturn,
            ReturnAgainstReceiptId = returnReceipt.ReturnAgainstReceiptId,
            Items = returnReceipt.Items.Select(i => new SubcontractingReceiptItemDto
            {
                Id = i.Id,
                ItemId = i.ItemId,
                ItemName = i.ItemName,
                Qty = i.Qty,
                Rate = i.Rate,
                Amount = i.Amount,
                WarehouseId = i.WarehouseId,
                ExpenseAccountId = i.ExpenseAccountId,
                ServiceExpenseAccountId = i.ServiceExpenseAccountId,
                CostCenterId = i.CostCenterId,
            }).ToList()
        };
    }

    /// <summary>
    /// Lists Subcontracting Receipts (including returns) created against a given Subcontracting Order,
    /// newest first — needed by the UI to offer a return against a specific submitted receipt.
    /// </summary>
    [Authorize(MyERPPermissions.PurchaseReceipts.Default)]
    public async Task<List<SubcontractingReceiptDto>> GetReceiptsForOrderAsync(Guid subcontractingOrderId)
    {
        var query = await _scrRepository.WithDetailsAsync(r => r.Items);
        var receipts = query
            .Where(r => r.SubcontractingOrderId == subcontractingOrderId)
            .OrderByDescending(r => r.CreationTime)
            .ToList();

        return receipts.Select(scr => new SubcontractingReceiptDto
        {
            Id = scr.Id,
            ReceiptNumber = scr.ReceiptNumber,
            PostingDate = scr.PostingDate,
            SupplierId = scr.SupplierId,
            SubcontractingOrderId = scr.SubcontractingOrderId,
            NetTotal = scr.NetTotal,
            Status = scr.Status,
            IsReturn = scr.IsReturn,
            ReturnAgainstReceiptId = scr.ReturnAgainstReceiptId,
            Items = scr.Items.Select(i => new SubcontractingReceiptItemDto
            {
                Id = i.Id,
                ItemId = i.ItemId,
                ItemName = i.ItemName,
                Qty = i.Qty,
                Rate = i.Rate,
                Amount = i.Amount,
                WarehouseId = i.WarehouseId,
                ExpenseAccountId = i.ExpenseAccountId,
                ServiceExpenseAccountId = i.ServiceExpenseAccountId,
                CostCenterId = i.CostCenterId,
            }).ToList()
        }).ToList();
    }

    /// <summary>
    /// Computes summary metrics for a Subcontracting Receipt (Gotcha #5997).
    /// </summary>
    [Authorize(MyERPPermissions.PurchaseReceipts.Default)]
    public async Task<SubcontractingReceiptSummaryDto> GetReceiptSummaryAsync(Guid id)
    {
        var scr = await _scrRepository.GetAsync(id, includeDetails: true);
        string? returnAgainstNumber = null;
        if (scr.ReturnAgainstReceiptId.HasValue)
        {
            var orig = await _scrRepository.FindAsync(scr.ReturnAgainstReceiptId.Value);
            returnAgainstNumber = orig?.ReceiptNumber;
        }

        return new SubcontractingReceiptSummaryDto
        {
            Id = scr.Id,
            ReceiptNumber = scr.ReceiptNumber,
            Status = (int)scr.Status,
            NetTotal = scr.NetTotal,
            TotalReceivedQty = scr.Items.Sum(i => i.Qty),
            TotalItemsCount = scr.Items.Count,
            IsReturn = scr.IsReturn,
            ReturnAgainstReceiptId = scr.ReturnAgainstReceiptId,
            ReturnAgainstReceiptNumber = returnAgainstNumber
        };
    }
}
