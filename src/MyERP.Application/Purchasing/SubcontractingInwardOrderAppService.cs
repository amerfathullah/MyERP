using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Inventory.DomainServices;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using MyERP.Shared;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Purchasing;

/// <summary>
/// Application service for Subcontracting Inward Order management.
/// Per DO-NOT: "Allow SO item updates when Subcontracting Inward Order exists (must cancel SCIO first)"
/// Per DO-NOT: "Close Sales Order without cascading status to linked Subcontracting Inward Orders"
/// </summary>
[Authorize(MyERPPermissions.PurchaseOrders.Default)]
public class SubcontractingInwardOrderAppService : ApplicationService, ISubcontractingInwardOrderAppService
{
    private readonly IRepository<SubcontractingInwardOrder, Guid> _repository;
    private readonly IRepository<Core.Entities.DocumentSeries, Guid> _seriesRepository;
    private readonly StockValuationService _stockValuationService;
    private readonly BinService _binService;

    public SubcontractingInwardOrderAppService(
        IRepository<SubcontractingInwardOrder, Guid> repository,
        IRepository<Core.Entities.DocumentSeries, Guid> seriesRepository,
        StockValuationService stockValuationService,
        BinService binService)
    {
        _repository = repository;
        _seriesRepository = seriesRepository;
        _stockValuationService = stockValuationService;
        _binService = binService;
    }

    public async Task<PagedResultDto<SubcontractingInwardOrderDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        if (input.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);
        if (!string.IsNullOrWhiteSpace(input.Filter))
            query = query.Where(x => x.OrderNumber.Contains(input.Filter));
        if (!string.IsNullOrWhiteSpace(input.Status) &&
            Enum.TryParse<SubcontractingInwardOrderStatus>(input.Status, true, out var status))
            query = query.Where(x => x.Status == status);

        var count = query.Count();
        var items = query.OrderByDescending(x => x.OrderDate)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<SubcontractingInwardOrderDto>(count, items.Select(ObjectMapper.Map<SubcontractingInwardOrder, SubcontractingInwardOrderDto>).ToList());
    }

    public async Task<SubcontractingInwardOrderDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return ObjectMapper.Map<SubcontractingInwardOrder, SubcontractingInwardOrderDto>(entity);
    }

    [Authorize(MyERPPermissions.PurchaseOrders.Create)]
    public async Task<SubcontractingInwardOrderDto> CreateAsync(CreateSubcontractingInwardOrderDto input)
    {
        Check.NotDefaultOrNull<Guid>(input.CompanyId, nameof(input.CompanyId));
        Check.NotDefaultOrNull<Guid>(input.SupplierId, nameof(input.SupplierId));
        if (input.Items == null || input.Items.Count == 0)
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustHaveItems);

        var supplierRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Supplier, Guid>>();
        var supplier = await supplierRepo.GetAsync(input.SupplierId);
        if (supplier.RepresentsCompanyId == input.CompanyId)
        {
            throw new BusinessException(MyERPDomainErrorCodes.PartyCannotRepresentOwnCompany);
        }

        var itemValidation = LazyServiceProvider.LazyGetRequiredService<Inventory.DomainServices.ItemTransactionValidationService>();
        foreach (var item in input.Items)
        {
            await itemValidation.ValidateItemAsync(item.ItemId);
        }

        var orderNumber = $"SCIO-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..4].ToUpper()}";
        var entity = new SubcontractingInwardOrder(GuidGenerator.Create(), input.CompanyId,
            orderNumber, input.OrderDate, input.SupplierId, CurrentTenant.Id);
        entity.SalesOrderId = input.SalesOrderId;
        entity.SubcontractingOrderId = input.SubcontractingOrderId;
        entity.CurrencyCode = input.CurrencyCode;

        foreach (var item in input.Items)
        {
            entity.AddItem(new SubcontractingInwardOrderItem(GuidGenerator.Create(),
                entity.Id, item.ItemId, item.Quantity, item.Rate, CurrentTenant.Id)
            {
                BomId = item.BomId,
                WarehouseId = item.WarehouseId,
                ServiceCostPerQty = item.ServiceCostPerQty
            });
        }

        await _repository.InsertAsync(entity);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "SubcontractingInwardOrder", entity.Id,
            "Created", entity.CompanyId,
            entity.OrderNumber, "Draft", "Draft", CurrentUser.Id,
            $"Subcontracting inward order '{entity.OrderNumber}' created with {entity.Items.Count} items", CurrentTenant.Id));

        return ObjectMapper.Map<SubcontractingInwardOrder, SubcontractingInwardOrderDto>(entity);
    }

    [Authorize(MyERPPermissions.PurchaseOrders.Submit)]
    public async Task<SubcontractingInwardOrderDto> SubmitAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        entity.Submit();
        await _repository.UpdateAsync(entity);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "SubcontractingInwardOrder", entity.Id,
            "Submitted", entity.CompanyId,
            entity.OrderNumber, "Draft", "Submitted", CurrentUser.Id,
            $"Subcontracting inward order '{entity.OrderNumber}' submitted", CurrentTenant.Id));

        return ObjectMapper.Map<SubcontractingInwardOrder, SubcontractingInwardOrderDto>(entity);
    }

    [Authorize(MyERPPermissions.PurchaseOrders.Cancel)]
    public async Task<SubcontractingInwardOrderDto> CancelAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        entity.Cancel();
        await _repository.UpdateAsync(entity);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "SubcontractingInwardOrder", entity.Id,
            "Cancelled", entity.CompanyId,
            entity.OrderNumber, "Submitted", "Cancelled", CurrentUser.Id,
            $"Subcontracting inward order '{entity.OrderNumber}' cancelled", CurrentTenant.Id));

        return MapToDto(entity);
    }

    [Authorize(MyERPPermissions.PurchaseOrders.Edit)]
    public async Task<SubcontractingInwardOrderDto> CloseAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        entity.Close();
        await _repository.UpdateAsync(entity);

        var activityLogRepo = LazyServiceProvider?.LazyGetService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        if (activityLogRepo != null)
        {
            await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
                GuidGenerator.Create(), "SubcontractingInwardOrder", entity.Id,
                "Closed", entity.CompanyId,
                entity.OrderNumber, "Submitted", "Closed", CurrentUser?.Id,
                $"Subcontracting inward order '{entity.OrderNumber}' closed", CurrentTenant?.Id));
        }

        return MapToDto(entity);
    }

    [Authorize(MyERPPermissions.PurchaseOrders.Edit)]
    public async Task<SubcontractingInwardOrderDto> ReopenAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        entity.Reopen();
        await _repository.UpdateAsync(entity);

        var activityLogRepo = LazyServiceProvider?.LazyGetService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        if (activityLogRepo != null)
        {
            await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
                GuidGenerator.Create(), "SubcontractingInwardOrder", entity.Id,
                "Reopened", entity.CompanyId,
                entity.OrderNumber, "Closed", entity.Status.ToString(), CurrentUser?.Id,
                $"Subcontracting inward order '{entity.OrderNumber}' reopened", CurrentTenant?.Id));
        }

        return MapToDto(entity);
    }

    /// <summary>
    /// Creates a draft Subcontracting Inward Order DTO pre-populated with items and supplier from a submitted Sales Order (Gotcha #5994).
    /// </summary>
    public async Task<CreateSubcontractingInwardOrderDto> MapFromSalesOrderAsync(MapSubcontractingInwardOrderFromSalesOrderDto input)
    {
        Check.NotDefaultOrNull<Guid>(input.SalesOrderId, nameof(input.SalesOrderId));
        Check.NotDefaultOrNull<Guid>(input.SupplierId, nameof(input.SupplierId));

        var salesOrderRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.SalesOrder, Guid>>();
        var soQuery = await salesOrderRepo.WithDetailsAsync(so => so.Items);
        var so = soQuery.FirstOrDefault(x => x.Id == input.SalesOrderId);
        if (so == null)
        {
            throw new BusinessException(MyERPDomainErrorCodes.EntityNotFound);
        }

        if (so.Status != DocumentStatus.Submitted)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Sales Order '{so.OrderNumber}' must be submitted to create Subcontracting Inward Order.");
        }

        var result = new CreateSubcontractingInwardOrderDto
        {
            CompanyId = so.CompanyId,
            SupplierId = input.SupplierId,
            SalesOrderId = so.Id,
            OrderDate = DateTime.UtcNow,
            CurrencyCode = so.CurrencyCode ?? "MYR",
            Items = new System.Collections.Generic.List<CreateScioItemDto>()
        };

        foreach (var soItem in so.Items)
        {
            result.Items.Add(new CreateScioItemDto
            {
                ItemId = soItem.ItemId,
                Quantity = soItem.Quantity,
                Rate = soItem.UnitPrice,
                ServiceCostPerQty = 0m
            });
        }

        return result;
    }

    /// <summary>
    /// Returns action availability summary for the given Subcontracting Inward Order (Gotcha #5994).
    /// </summary>
    public async Task<SubcontractingInwardOrderActionSummaryDto> GetActionSummaryAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        var pendingItems = entity.Items.Count(i => i.PendingReceiptQty > 0);

        return new SubcontractingInwardOrderActionSummaryDto
        {
            OrderId = entity.Id,
            Status = entity.Status,
            PerReceived = entity.PerReceived,
            PerBilled = entity.PerBilled,
            CanReopen = entity.Status == SubcontractingInwardOrderStatus.Closed,
            CanClose = entity.Status == SubcontractingInwardOrderStatus.Open || entity.Status == SubcontractingInwardOrderStatus.PartiallyReceived,
            CanCancel = entity.Status == SubcontractingInwardOrderStatus.Open || entity.Status == SubcontractingInwardOrderStatus.PartiallyReceived,
            PendingItemCount = pendingItems
        };
    }

    /// <summary>
    /// Receives finished goods against a Subcontracting Inward Order: updates each item's
    /// ReceivedQty, stocks the received qty into its warehouse (valued at the item's
    /// ServiceCostPerQty — our conversion cost basis, not the customer billing Rate, since the
    /// underlying goods remain the customer's property), and recalculates order fulfillment.
    /// Previously the Angular UI called the unrelated SubcontractingOrder/Receipt API here, which
    /// created an orphaned Draft SubcontractingReceipt with no route to submit it and zero
    /// real-world stock/GL effect (Gap: SCIO "Create Receipt" dead-end).
    /// </summary>
    [Authorize(MyERPPermissions.PurchaseReceipts.Create)]
    public async Task<SubcontractingInwardOrderDto> ReceiveItemsAsync(Guid id, ScioReceiveItemsDto input)
    {
        if (input.Items == null || input.Items.Count == 0)
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustHaveItems);

        var entity = await _repository.GetAsync(id);

        if (entity.Status != SubcontractingInwardOrderStatus.Open &&
            entity.Status != SubcontractingInwardOrderStatus.PartiallyReceived)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("detail", "Items can only be received against an Open or Partially Received Subcontracting Inward Order.");
        }

        var receivedAny = false;
        foreach (var receiveItem in input.Items)
        {
            if (receiveItem.Qty <= 0) continue;

            var item = entity.Items.FirstOrDefault(i => i.ItemId == receiveItem.ItemId)
                ?? throw new BusinessException(MyERPDomainErrorCodes.EntityNotFound)
                    .WithData("detail", "Item is not part of this Subcontracting Inward Order.");

            if (receiveItem.Qty > item.PendingReceiptQty)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Receive quantity ({receiveItem.Qty}) exceeds pending quantity ({item.PendingReceiptQty}) for the item.");
            }

            var warehouseId = item.WarehouseId
                ?? throw new BusinessException(MyERPDomainErrorCodes.MissingWarehouse);

            await _stockValuationService.CreateLedgerEntryAsync(
                entity.CompanyId, item.ItemId, warehouseId, input.PostingDate,
                receiveItem.Qty, item.ServiceCostPerQty, "SubcontractingInwardOrder", entity.Id, entity.TenantId);

            await _binService.ApplyStockMovementAsync(
                item.ItemId, warehouseId, receiveItem.Qty, receiveItem.Qty * item.ServiceCostPerQty);

            item.ReceivedQty += receiveItem.Qty;
            receivedAny = true;
        }

        if (!receivedAny)
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustHaveItems);

        entity.UpdateReceivedStatus();
        await _repository.UpdateAsync(entity);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "SubcontractingInwardOrder", entity.Id,
            "ItemsReceived", entity.CompanyId,
            entity.OrderNumber, entity.Status.ToString(), entity.Status.ToString(), CurrentUser.Id,
            $"Received items against Subcontracting Inward Order '{entity.OrderNumber}'", CurrentTenant.Id));

        return MapToDto(entity);
    }

    private static SubcontractingInwardOrderDto MapToDto(SubcontractingInwardOrder entity) => new()
    {
        Id = entity.Id,
        CompanyId = entity.CompanyId,
        OrderNumber = entity.OrderNumber,
        OrderDate = entity.OrderDate,
        SupplierId = entity.SupplierId,
        SalesOrderId = entity.SalesOrderId,
        SubcontractingOrderId = entity.SubcontractingOrderId,
        CurrencyCode = entity.CurrencyCode,
        NetTotal = entity.NetTotal,
        GrandTotal = entity.GrandTotal,
        Status = entity.Status,
        PerReceived = entity.PerReceived,
        PerBilled = entity.PerBilled,
        Items = entity.Items.Select(i => new SubcontractingInwardOrderItemDto
        {
            Id = i.Id,
            ItemId = i.ItemId,
            BomId = i.BomId,
            Quantity = i.Quantity,
            Rate = i.Rate,
            Amount = i.Amount,
            ReceivedQty = i.ReceivedQty,
            BilledQty = i.BilledQty,
            PendingReceiptQty = i.PendingReceiptQty,
            WarehouseId = i.WarehouseId,
            ServiceCostPerQty = i.ServiceCostPerQty
        }).ToList()
    };
}
