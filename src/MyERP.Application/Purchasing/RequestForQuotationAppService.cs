using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.DomainServices;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using MyERP.Shared;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Purchasing;

[Authorize(MyERPPermissions.PurchaseOrders.Default)]
public class RequestForQuotationAppService : ApplicationService, IRequestForQuotationAppService
{
    private readonly IRepository<RequestForQuotation, Guid> _repository;
    private readonly IRepository<Supplier, Guid> _supplierRepository;
    private readonly IDocumentNumberGenerator _numberGenerator;

    public RequestForQuotationAppService(
        IRepository<RequestForQuotation, Guid> repository,
        IRepository<Supplier, Guid> supplierRepository,
        IDocumentNumberGenerator numberGenerator)
    {
        _repository = repository;
        _supplierRepository = supplierRepository;
        _numberGenerator = numberGenerator;
    }

    public async Task<RfqDto> GetAsync(Guid id)
    {
        var rfq = await _repository.GetAsync(id);
        return ObjectMapper.Map<RequestForQuotation, RfqDto>(rfq);
    }

    public async Task<PagedResultDto<RfqDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();

        if (input.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var f = input.Filter;
            query = query.Where(x => x.RfqNumber.Contains(f));
        }

        if (!string.IsNullOrWhiteSpace(input.Status) && Enum.TryParse<DocumentStatus>(input.Status, true, out var status))
            query = query.Where(x => x.Status == status);

        var count = query.Count();
        var list = query.OrderByDescending(x => x.TransactionDate)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<RfqDto>(count, list.Select(x => ObjectMapper.Map<RequestForQuotation, RfqDto>(x)).ToList());
    }

    [Authorize(MyERPPermissions.PurchaseOrders.Create)]
    public async Task<RfqDto> CreateAsync(CreateRfqDto input)
    {
        var rfqNumber = await _numberGenerator.GenerateAsync("RFQ", input.CompanyId);
        var rfq = new RequestForQuotation(GuidGenerator.Create(), input.CompanyId, rfqNumber, input.TransactionDate, CurrentTenant.Id);
        rfq.CurrencyCode = input.CurrencyCode ?? "MYR";
        rfq.MessageForSupplier = input.MessageForSupplier;

        // Validate all items are active
        var itemValidation = LazyServiceProvider.LazyGetRequiredService<MyERP.Inventory.DomainServices.ItemTransactionValidationService>();
        await itemValidation.ValidateItemsForTransactionAsync(input.Items.Select(i => i.ItemId).ToArray());

        foreach (var item in input.Items)
            rfq.AddItem(item.ItemId, item.Description, item.Qty, item.Uom, item.WarehouseId, item.MaterialRequestItemId);

        // Validate no duplicate suppliers
        if (input.Suppliers.Select(s => s.SupplierId).Distinct().Count() != input.Suppliers.Count)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.DuplicateRfqSupplier);
        }

        foreach (var supplier in input.Suppliers)
        {
            var supplierEntity = await _supplierRepository.GetAsync(supplier.SupplierId);

            // Per ERPNext PR #57983 / commit 4bf65ffc1d: block disabled or on-hold suppliers
            if (!supplierEntity.IsActive)
            {
                throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.PartyDisabled)
                    .WithData("partyType", "Supplier")
                    .WithData("partyName", supplierEntity.Name);
            }

            if (supplierEntity.IsOnHold)
            {
                throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.SupplierOnHold)
                    .WithData("supplierName", supplierEntity.Name);
            }

            // Validate supplier scorecard: prevent_rfqs blocks
            if (supplierEntity.PreventRfqs)
                throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ScorecardBlockedRFQ)
                    .WithData("supplierName", supplierEntity.Name);

            rfq.AddSupplier(supplier.SupplierId, supplierEntity.Name, supplier.Email);
        }

        await _repository.InsertAsync(rfq, autoSave: true);
        return ObjectMapper.Map<RequestForQuotation, RfqDto>(rfq);
    }

    [Authorize(MyERPPermissions.PurchaseOrders.Edit)]
    public async Task<RfqDto> SubmitAsync(Guid id)
    {
        var rfq = await _repository.GetAsync(id);
        rfq.Submit();
        await _repository.UpdateAsync(rfq, autoSave: true);
        return ObjectMapper.Map<RequestForQuotation, RfqDto>(rfq);
    }

    [Authorize(MyERPPermissions.PurchaseOrders.Edit)]
    public async Task<RfqDto> CancelAsync(Guid id)
    {
        var rfq = await _repository.GetAsync(id);
        rfq.Cancel();
        await _repository.UpdateAsync(rfq, autoSave: true);
        return ObjectMapper.Map<RequestForQuotation, RfqDto>(rfq);
    }

    /// <summary>
    /// Gets pending Material Request items (Purchase type) that have not been fully ordered or received.
    /// Deducts draft RFQ items (per ERPNext PR #58617 / commit d8432d92c8) and filters fully ordered items (PR #58534 / commit c93815b4ae).
    /// Used by RFQ form "Get Items from Material Request" button.
    /// </summary>
    public async Task<List<PendingMaterialRequestItemDto>> GetPendingMaterialRequestItemsAsync(Guid? companyId = null)
    {
        var mrRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MaterialRequest, Guid>>();
        var mrQuery = await mrRepo.GetQueryableAsync();

        var query = mrQuery.Where(mr =>
            (mr.RequestType == MaterialRequestType.Purchase || mr.RequestType == MaterialRequestType.Subcontracting) &&
            mr.Status == Core.DocumentStatus.Submitted);

        if (companyId.HasValue)
            query = query.Where(mr => mr.CompanyId == companyId.Value);

        var requests = query.ToList();
        if (!requests.Any()) return new List<PendingMaterialRequestItemDto>();

        // Deduct quantities already mapped in draft RFQs (PR #58617 parity)
        var rfqQuery = await _repository.GetQueryableAsync();
        var draftRfqs = rfqQuery
            .Where(r => r.Status == Core.DocumentStatus.Draft)
            .SelectMany(r => r.Items)
            .Where(i => i.MaterialRequestItemId.HasValue)
            .GroupBy(i => i.MaterialRequestItemId!.Value)
            .Select(g => new { MrItemId = g.Key, Qty = g.Sum(i => i.Qty) })
            .ToList();
        var draftRfqQtyByItem = draftRfqs.ToDictionary(x => x.MrItemId, x => x.Qty);

        var allItemIds = requests.SelectMany(mr => mr.Items).Select(i => i.ItemId).Distinct().ToList();
        var itemRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Inventory.Entities.Item, Guid>>();
        var itemQuery = await itemRepo.GetQueryableAsync();
        var itemNames = itemQuery
            .Where(i => allItemIds.Contains(i.Id))
            .Select(i => new { i.Id, i.ItemCode, i.ItemName })
            .ToList()
            .ToDictionary(i => i.Id, i => $"{i.ItemCode} - {i.ItemName}");

        var result = new List<PendingMaterialRequestItemDto>();
        foreach (var mr in requests)
        {
            foreach (var item in mr.Items)
            {
                var fulfilledQty = Math.Max(item.OrderedQuantity, item.ReceivedQuantity);
                var draftQty = draftRfqQtyByItem.GetValueOrDefault(item.Id, 0m);
                var pendingQty = item.Quantity - fulfilledQty - draftQty;
                if (pendingQty > 0)
                {
                    result.Add(new PendingMaterialRequestItemDto
                    {
                        MaterialRequestId = mr.Id,
                        MaterialRequestNumber = mr.RequestNumber,
                        RequestDate = mr.RequestDate,
                        RequiredByDate = mr.RequiredByDate,
                        MaterialRequestItemId = item.Id,
                        ItemId = item.ItemId,
                        ItemName = itemNames.GetValueOrDefault(item.ItemId) ?? item.ItemName,
                        PendingQty = pendingQty,
                        Uom = item.Uom,
                        WarehouseId = item.WarehouseId,
                    });
                }
            }
        }

        return result.OrderBy(r => r.RequestDate).ThenBy(r => r.ItemName).ToList();
    }
}

