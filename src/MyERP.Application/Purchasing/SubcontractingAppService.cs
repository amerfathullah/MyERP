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
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Purchasing;

[Authorize(MyERPPermissions.PurchaseOrders.Default)]
public class SubcontractingAppService : ApplicationService
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
        return MapOrderToDto(sco);
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

        return new PagedResultDto<SubcontractingOrderDto>(totalCount, items.Select(MapOrderToDto).ToList());
    }

    [Authorize(MyERPPermissions.PurchaseOrders.Create)]
    public async Task<SubcontractingOrderDto> CreateOrderAsync(CreateSubcontractingOrderDto input)
    {
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
        return MapOrderToDto(sco);
    }

    [Authorize(MyERPPermissions.PurchaseOrders.Submit)]
    public async Task<SubcontractingOrderDto> SubmitOrderAsync(Guid id)
    {
        var sco = await _scoRepository.GetAsync(id, includeDetails: true);
        sco.Submit();
        await _scoRepository.UpdateAsync(sco);
        return MapOrderToDto(sco);
    }

    [Authorize(MyERPPermissions.PurchaseOrders.Cancel)]
    public async Task<SubcontractingOrderDto> CancelOrderAsync(Guid id)
    {
        var sco = await _scoRepository.GetAsync(id, includeDetails: true);
        sco.Cancel();
        await _scoRepository.UpdateAsync(sco);
        return MapOrderToDto(sco);
    }

    // === Subcontracting Receipt ===

    [Authorize(MyERPPermissions.PurchaseReceipts.Create)]
    public async Task<SubcontractingReceiptDto> CreateReceiptAsync(CreateSubcontractingReceiptDto input)
    {
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
        return MapReceiptToDto(scr);
    }

    [Authorize(MyERPPermissions.PurchaseReceipts.Submit)]
    public async Task<SubcontractingReceiptDto> SubmitReceiptAsync(Guid id)
    {
        var scr = await _scrRepository.GetAsync(id, includeDetails: true);
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

        // Update linked SCO item received quantities
        var sco = await _scoRepository.GetAsync(scr.SubcontractingOrderId, includeDetails: true);
        foreach (var scrItem in scr.Items)
        {
            var scoItem = sco.Items.FirstOrDefault(i => i.ItemId == scrItem.ItemId);
            if (scoItem != null)
                scoItem.ReceivedQty += scrItem.Qty;
        }

        // Update SCO status based on receipt percentage
        var minPer = sco.Items.Any()
            ? sco.Items.Min(i => i.Qty > 0 ? (i.ReceivedQty / i.Qty) * 100m : 100m)
            : 0m;
        if (minPer >= 100)
            sco.Close();
        else if (minPer > 0 && sco.Status == SubcontractingOrderStatus.Open)
            sco.MarkPartiallyReceived();
        sco.PerReceived = minPer;

        await _scoRepository.UpdateAsync(sco);
        await _scrRepository.UpdateAsync(scr);
        return MapReceiptToDto(scr);
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

        // Reverse SCO received quantities
        var sco = await _scoRepository.GetAsync(scr.SubcontractingOrderId, includeDetails: true);
        foreach (var scrItem in scr.Items)
        {
            var scoItem = sco.Items.FirstOrDefault(i => i.ItemId == scrItem.ItemId);
            if (scoItem != null)
                scoItem.ReceivedQty = Math.Max(0, scoItem.ReceivedQty - scrItem.Qty);
        }
        sco.PerReceived = sco.Items.Any()
            ? sco.Items.Min(i => i.Qty > 0 ? (i.ReceivedQty / i.Qty) * 100m : 100m) : 0m;

        await _scoRepository.UpdateAsync(sco);
        await _scrRepository.UpdateAsync(scr);
        return MapReceiptToDto(scr);
    }

    private static SubcontractingOrderDto MapOrderToDto(SubcontractingOrder s) => new()
    {
        Id = s.Id, OrderNumber = s.OrderNumber, OrderDate = s.OrderDate,
        SupplierId = s.SupplierId, CompanyId = s.CompanyId,
        NetTotal = s.NetTotal, GrandTotal = s.GrandTotal,
        Status = s.Status, PerReceived = s.PerReceived,
        CreationTime = s.CreationTime,
        Items = s.Items.Select(i => new ScoItemDto
        {
            Id = i.Id, ItemId = i.ItemId, ItemName = i.ItemName,
            Qty = i.Qty, Rate = i.Rate, ReceivedQty = i.ReceivedQty,
        }).ToList(),
    };

    private static SubcontractingReceiptDto MapReceiptToDto(SubcontractingReceipt r) => new()
    {
        Id = r.Id, ReceiptNumber = r.ReceiptNumber, PostingDate = r.PostingDate,
        SupplierId = r.SupplierId, SubcontractingOrderId = r.SubcontractingOrderId,
        NetTotal = r.NetTotal, Status = r.Status, CreationTime = r.CreationTime,
    };
}

// DTOs

public class SubcontractingOrderDto : AuditedEntityDto<Guid>
{
    public string OrderNumber { get; set; } = null!;
    public DateTime OrderDate { get; set; }
    public Guid SupplierId { get; set; }
    public Guid CompanyId { get; set; }
    public decimal NetTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public SubcontractingOrderStatus Status { get; set; }
    public decimal PerReceived { get; set; }
    public List<ScoItemDto> Items { get; set; } = new();
}

public class ScoItemDto
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = null!;
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
    public decimal ReceivedQty { get; set; }
}

public class CreateSubcontractingOrderDto
{
    [Required] public Guid CompanyId { get; set; }
    [Required] public Guid SupplierId { get; set; }
    [Required] public DateTime OrderDate { get; set; }
    public Guid? PurchaseOrderId { get; set; }
    public string? Notes { get; set; }
    public List<CreateScoItemDto> Items { get; set; } = new();
}

public class CreateScoItemDto
{
    [Required] public Guid ItemId { get; set; }
    [Required] public string ItemName { get; set; } = null!;
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
    public Guid? BomId { get; set; }
    public Guid? WarehouseId { get; set; }
}

public class GetScoListDto : PagedAndSortedResultRequestDto
{
    public SubcontractingOrderStatus? Status { get; set; }
    public Guid? CompanyId { get; set; }
}

public class SubcontractingReceiptDto : AuditedEntityDto<Guid>
{
    public string ReceiptNumber { get; set; } = null!;
    public DateTime PostingDate { get; set; }
    public Guid SupplierId { get; set; }
    public Guid SubcontractingOrderId { get; set; }
    public decimal NetTotal { get; set; }
    public SubcontractingReceiptStatus Status { get; set; }
}

public class CreateSubcontractingReceiptDto
{
    [Required] public Guid CompanyId { get; set; }
    [Required] public Guid SupplierId { get; set; }
    [Required] public Guid SubcontractingOrderId { get; set; }
    [Required] public DateTime PostingDate { get; set; }
    public Guid? WarehouseId { get; set; }
    public List<CreateScrItemDto> Items { get; set; } = new();
}

public class CreateScrItemDto
{
    [Required] public Guid ItemId { get; set; }
    [Required] public string ItemName { get; set; } = null!;
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
    public Guid? WarehouseId { get; set; }
}
