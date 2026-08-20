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
    /// Creates a Draft Stock Entry (SendToSubcontractor) with pending RM items from the SCO's BOM.
    /// Per ERPNext make_rm_stock_entry: resolves RM requirements, caps at pending qty, creates SE.
    /// </summary>
    [Authorize(MyERPPermissions.StockEntries.Create)]
    public async Task<RmTransferResultDto> CreateRmTransferStockEntryAsync(Guid scoId)
    {
        var sco = await _scoRepository.GetAsync(scoId, includeDetails: true);
        if (sco.Status == SubcontractingOrderStatus.Draft || sco.Status == SubcontractingOrderStatus.Cancelled)
            throw new Volo.Abp.BusinessException("MyERP:10020")
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
            { WarehouseId = item.WarehouseId });
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
        var totalReceivedFgQty = scr.Items.Sum(i => i.Qty);
        var sco = await _scoRepository.GetAsync(scr.SubcontractingOrderId);
        var rmConsumptions = scManager.CalculateRmConsumption(sco, totalReceivedFgQty);

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
}
