using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core.DomainServices;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Permissions;
using MyERP.Projects.Entities;
using MyERP.Purchasing.DTOs;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Purchasing;

[Authorize(MyERPPermissions.MaterialRequests.Default)]
public class MaterialRequestAppService : ApplicationService, IMaterialRequestAppService
{
    private readonly IRepository<MaterialRequest, Guid> _repository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepository;
    private readonly IDocumentNumberGenerator _numberGenerator;
    private readonly BudgetValidationService _budgetValidation;
    private readonly ItemDefaultsResolutionService _itemDefaultsResolution;

    public MaterialRequestAppService(
        IRepository<MaterialRequest, Guid> repository,
        IRepository<Item, Guid> itemRepository,
        IRepository<FiscalYear, Guid> fiscalYearRepository,
        IDocumentNumberGenerator numberGenerator,
        BudgetValidationService budgetValidation,
        ItemDefaultsResolutionService itemDefaultsResolution)
    {
        _repository = repository;
        _itemRepository = itemRepository;
        _fiscalYearRepository = fiscalYearRepository;
        _numberGenerator = numberGenerator;
        _budgetValidation = budgetValidation;
        _itemDefaultsResolution = itemDefaultsResolution;
    }

    public async Task<MaterialRequestDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id, includeDetails: true);
        return ObjectMapper.Map<MaterialRequest, MaterialRequestDto>(entity);
    }

    public async Task<PagedResultDto<MaterialRequestDto>> GetListAsync(GetMaterialRequestListDto input)
    {
        var query = await _repository.GetQueryableAsync();

        if (input.RequestType.HasValue)
            query = query.Where(x => x.RequestType == input.RequestType.Value);
        if (input.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var f = input.Filter;
            query = query.Where(x => x.RequestNumber.Contains(f));
        }
        if (!string.IsNullOrWhiteSpace(input.Status) && Enum.TryParse<Core.DocumentStatus>(input.Status, true, out var status))
            query = query.Where(x => x.Status == status);

        var totalCount = query.Count();
        var sorted = SortingHelper.ApplySorting(query, input.Sorting,
            q => q.OrderByDescending(x => x.CreationTime),
            ("requestNumber", x => (object)(x.RequestNumber ?? string.Empty)),
            ("requestDate", x => x.RequestDate),
            ("requestType", x => x.RequestType),
            ("status", x => x.Status));
        var items = sorted
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<MaterialRequestDto>(totalCount, items.Select(x => ObjectMapper.Map<MaterialRequest, MaterialRequestDto>(x)).ToList());
    }

    [Authorize(MyERPPermissions.MaterialRequests.Create)]
    public async Task<MaterialRequestDto> CreateAsync(CreateMaterialRequestDto input)
    {
        if (input.RequiredByDate.HasValue && input.RequiredByDate.Value.Date < input.RequestDate.Date)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Required By Date cannot be before the Request Date.");
        }

        if (input.Items == null || input.Items.Count == 0)
            throw new Volo.Abp.BusinessException("MyERP:01007")
                .WithData("documentType", "Material Request");

        // Validate all items are active
        var itemIds = input.Items.Select(i => i.ItemId).ToArray();
        var itemValidation = LazyServiceProvider.LazyGetRequiredService<MyERP.Inventory.DomainServices.ItemTransactionValidationService>();
        await itemValidation.ValidateItemsForTransactionAsync(itemIds);

        await ValidateCompanyBoundariesAsync(input, input.CompanyId);

        var number = await _numberGenerator.GenerateAsync("MR", input.CompanyId);
        var entity = new MaterialRequest(
            GuidGenerator.Create(), input.CompanyId, number,
            input.RequestType, input.RequestDate, CurrentTenant.Id)
        {
            ProjectId = input.ProjectId,
            RequiredByDate = input.RequiredByDate,
            WorkOrderId = input.WorkOrderId,
            SourceWarehouseId = input.SourceWarehouseId,
            TargetWarehouseId = input.TargetWarehouseId,
            Notes = input.Notes,
        };

        foreach (var item in input.Items)
        {
            entity.AddItem(
                item.ItemId,
                item.ItemName,
                item.Quantity,
                item.Uom,
                item.WarehouseId,
                item.SalesOrderId,
                item.SalesOrderItemId,
                item.ProjectId ?? input.ProjectId,
                item.ConversionFactor > 0 ? item.ConversionFactor : 1m);
        }

        await ValidateItemsAgainstSalesOrderAsync(entity);

        await _repository.InsertAsync(entity);
        return ObjectMapper.Map<MaterialRequest, MaterialRequestDto>(entity);
    }

    [Authorize(MyERPPermissions.MaterialRequests.Edit)]
    public async Task<MaterialRequestDto> UpdateAsync(Guid id, CreateMaterialRequestDto input)
    {
        var entity = await _repository.GetAsync(id, includeDetails: true);
        if (entity.Status != Core.DocumentStatus.Draft)
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("detail", "Only Draft material requests can be edited");

        if (input.CompanyId != Guid.Empty && input.CompanyId != entity.CompanyId)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.CompanyMismatch)
                .WithData("entityCompany", entity.CompanyId)
                .WithData("inputCompany", input.CompanyId);
        }

        if (input.RequiredByDate.HasValue && input.RequiredByDate.Value.Date < input.RequestDate.Date)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Required By Date cannot be before the Request Date.");
        }

        if (input.Items == null || input.Items.Count == 0)
            throw new Volo.Abp.BusinessException("MyERP:01007")
                .WithData("documentType", "Material Request");

        // Validate all items are active
        var itemIds = input.Items.Select(i => i.ItemId).ToArray();
        var itemValidation = LazyServiceProvider.LazyGetRequiredService<MyERP.Inventory.DomainServices.ItemTransactionValidationService>();
        await itemValidation.ValidateItemsForTransactionAsync(itemIds);

        await ValidateCompanyBoundariesAsync(input, entity.CompanyId);

        entity.RequestType = input.RequestType;
        entity.RequestDate = input.RequestDate;
        entity.RequiredByDate = input.RequiredByDate;
        entity.ProjectId = input.ProjectId;
        entity.WorkOrderId = input.WorkOrderId;
        entity.SourceWarehouseId = input.SourceWarehouseId;
        entity.TargetWarehouseId = input.TargetWarehouseId;
        entity.Notes = input.Notes;

        entity.ClearItems();
        foreach (var item in input.Items)
        {
            entity.AddItem(
                item.ItemId,
                item.ItemName,
                item.Quantity,
                item.Uom,
                item.WarehouseId,
                item.SalesOrderId,
                item.SalesOrderItemId,
                item.ProjectId ?? input.ProjectId,
                item.ConversionFactor > 0 ? item.ConversionFactor : 1m);
        }

        await ValidateItemsAgainstSalesOrderAsync(entity);

        await _repository.UpdateAsync(entity);
        return ObjectMapper.Map<MaterialRequest, MaterialRequestDto>(entity);
    }

    [Authorize(MyERPPermissions.MaterialRequests.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    [Authorize(MyERPPermissions.MaterialRequests.Submit)]
    public async Task<MaterialRequestDto> SubmitAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id, includeDetails: true);

        // Re-checked at submit per ERPNext's before_update_after_submit re-validation — RequiredByDate
        // has a public setter and could have been changed after Create.
        if (entity.RequiredByDate.HasValue && entity.RequiredByDate.Value.Date < entity.RequestDate.Date)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Required By Date cannot be before the Request Date.");
        }

        await ValidateItemsAgainstSalesOrderAsync(entity);

        // Budget validation (Level 1: MR enforcement) — only for Purchase type
        if (entity.RequestType == MaterialRequestType.Purchase)
        {
            var fiscalYear = (await _fiscalYearRepository.GetQueryableAsync())
                .FirstOrDefault(fy => fy.CompanyId == entity.CompanyId
                                   && fy.StartDate <= entity.RequestDate
                                   && fy.EndDate >= entity.RequestDate);

            if (fiscalYear != null)
            {
                // Batch load item data (price only — expense account resolved via fallback chain below)
                var mrItemIds = entity.Items.Select(i => i.ItemId).Distinct().ToArray();
                var itemQuery = await _itemRepository.GetQueryableAsync();
                var itemData = itemQuery
                    .Where(i => mrItemIds.Contains(i.Id))
                    .Select(i => new { i.Id, i.StandardBuyingPrice })
                    .ToDictionary(i => i.Id);

                var budgetItems = new List<BudgetCheckItem>();
                foreach (var mrItem in entity.Items)
                {
                    if (!itemData.TryGetValue(mrItem.ItemId, out var item)) continue;

                    // Falls back to Item Default -> Item Group hierarchy when the item has no expense account of its own
                    var expenseAccountId = await _itemDefaultsResolution.ResolveExpenseAccountAsync(mrItem.ItemId, entity.CompanyId);
                    if (!expenseAccountId.HasValue) continue;

                    // MR items don't have price — use item's standard buying price or qty as estimate
                    var estimatedAmount = mrItem.Quantity * (item.StandardBuyingPrice ?? 1m);
                    budgetItems.Add(new BudgetCheckItem(expenseAccountId.Value, estimatedAmount));
                }

                if (budgetItems.Any())
                {
                    await _budgetValidation.ValidateForMaterialRequestAsync(
                        entity.CompanyId, fiscalYear.Id, entity.RequestDate, budgetItems, entity.TenantId);
                }
            }
        }

        entity.Submit();
        await _repository.UpdateAsync(entity);
        await ApplyIndentedQtyAsync(entity, sign: 1);
        await ApplySalesOrderRequestedQtyAsync(entity, sign: 1);

        var activityRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Core.Entities.DocumentActivityLog, Guid>>();
        await activityRepo.InsertAsync(new MyERP.Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "MaterialRequest", entity.Id, "Submitted",
            entity.CompanyId, entity.RequestNumber, "Draft", "Submitted",
            CurrentUser.Id, tenantId: entity.TenantId));

        return ObjectMapper.Map<MaterialRequest, MaterialRequestDto>(entity);
    }

    [Authorize(MyERPPermissions.MaterialRequests.Cancel)]
    public async Task<MaterialRequestDto> CancelAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id, includeDetails: true);
        entity.Cancel();
        await _repository.UpdateAsync(entity);
        await ApplyIndentedQtyAsync(entity, sign: -1);
        await ApplySalesOrderRequestedQtyAsync(entity, sign: -1);

        var activityRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Core.Entities.DocumentActivityLog, Guid>>();
        await activityRepo.InsertAsync(new MyERP.Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "MaterialRequest", entity.Id, "Cancelled",
            entity.CompanyId, entity.RequestNumber, "Submitted", "Cancelled",
            CurrentUser.Id, tenantId: entity.TenantId));

        return ObjectMapper.Map<MaterialRequest, MaterialRequestDto>(entity);
    }

    [Authorize(MyERPPermissions.MaterialRequests.Edit)]
    public async Task<MaterialRequestDto> StopAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id, includeDetails: true);
        entity.Stop();
        await _repository.UpdateAsync(entity);
        await ApplyIndentedQtyAsync(entity, sign: -1);
        await ApplySalesOrderRequestedQtyAsync(entity, sign: -1);

        var activityRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Core.Entities.DocumentActivityLog, Guid>>();
        await activityRepo.InsertAsync(new MyERP.Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "MaterialRequest", entity.Id, "Stopped",
            entity.CompanyId, entity.RequestNumber, "Submitted", "Closed",
            CurrentUser.Id, tenantId: entity.TenantId));

        return ObjectMapper.Map<MaterialRequest, MaterialRequestDto>(entity);
    }

    [Authorize(MyERPPermissions.MaterialRequests.Edit)]
    public async Task<MaterialRequestDto> ReopenAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id, includeDetails: true);
        entity.Reopen();
        await _repository.UpdateAsync(entity);
        await ApplyIndentedQtyAsync(entity, sign: 1);
        await ApplySalesOrderRequestedQtyAsync(entity, sign: 1);

        var activityRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Core.Entities.DocumentActivityLog, Guid>>();
        await activityRepo.InsertAsync(new MyERP.Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "MaterialRequest", entity.Id, "Reopened",
            entity.CompanyId, entity.RequestNumber, "Closed", "Submitted",
            CurrentUser.Id, tenantId: entity.TenantId));

        return ObjectMapper.Map<MaterialRequest, MaterialRequestDto>(entity);
    }

    /// <summary>
    /// Updates SalesOrderItem.RequestedQty on MR submit (+1) and cancel (-1) per ERPNext PR #52835, #52825.
    /// </summary>
    private async Task ApplySalesOrderRequestedQtyAsync(MaterialRequest entity, int sign)
    {
        var soItems = entity.Items.Where(i => i.SalesOrderId.HasValue && i.SalesOrderItemId.HasValue).ToList();
        if (!soItems.Any()) return;

        var soRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.SalesOrder, Guid>>();
        var soGroups = soItems.GroupBy(i => i.SalesOrderId!.Value);
        foreach (var group in soGroups)
        {
            var so = await soRepo.FindAsync(group.Key);
            if (so == null) continue;

            foreach (var mrItem in group)
            {
                var targetSoItem = so.Items.FirstOrDefault(i => i.Id == mrItem.SalesOrderItemId!.Value);
                if (targetSoItem != null)
                {
                    targetSoItem.RequestedQty = Math.Max(0, targetSoItem.RequestedQty + (sign * mrItem.Quantity));
                }
            }
            await soRepo.UpdateAsync(so);
        }
    }

    /// <summary>
    /// Per material-request-rfq-full.md's Submit/Cancel effects: updates Bin.IndentedQty ("requested
    /// but not yet fulfilled") for every stock item with a warehouse set — sign=+1 on submit, -1 on
    /// cancel (revert). Applies to every MR type per the doc's unconditional "Submit Effects" list;
    /// only the separate ordered_qty/received_qty fulfillment tracking distinguishes Purchase MRs.
    /// </summary>
    private async Task ApplyIndentedQtyAsync(MaterialRequest entity, int sign)
    {
        var candidateItemIds = entity.Items.Where(i => i.WarehouseId.HasValue)
            .Select(i => i.ItemId).Distinct().ToArray();
        if (candidateItemIds.Length == 0) return;

        var itemQuery = await _itemRepository.GetQueryableAsync();
        var stockItemIds = itemQuery
            .Where(i => candidateItemIds.Contains(i.Id) && i.MaintainStock)
            .Select(i => i.Id)
            .ToHashSet();
        if (stockItemIds.Count == 0) return;

        var binService = LazyServiceProvider.LazyGetRequiredService<MyERP.Inventory.DomainServices.BinService>();
        foreach (var item in entity.Items.Where(i => i.WarehouseId.HasValue && stockItemIds.Contains(i.ItemId)))
        {
            await binService.UpdateIndentedQtyAsync(
                item.ItemId, item.WarehouseId!.Value, sign * item.Quantity, entity.TenantId);
        }
    }

    /// <summary>
    /// Gets the fulfillment status of a Material Request.
    /// Per ERPNext: MR is fully fulfilled when all items are ordered/transferred at ≥99.99%.
    /// </summary>
    public async Task<MrFulfillmentStatusDto> GetFulfillmentStatusAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id, includeDetails: true);
        var mrManager = LazyServiceProvider
            .LazyGetRequiredService<MyERP.Purchasing.DomainServices.MaterialRequestManager>();

        var isFullyFulfilled = mrManager.IsFullyFulfilled(entity);

        var items = entity.Items.Select(item => new MrItemFulfillmentDto
        {
            ItemId = item.ItemId,
            RequestedQty = item.Quantity,
            OrderedQty = item.OrderedQuantity,
            PendingQty = MyERP.Purchasing.DomainServices.MaterialRequestManager.GetPendingQty(item),
            PerOrdered = item.Quantity > 0 ? Math.Round(item.OrderedQuantity / item.Quantity * 100, 2) : 0,
        }).ToList();

        return new MrFulfillmentStatusDto
        {
            MaterialRequestId = entity.Id,
            IsFullyFulfilled = isFullyFulfilled,
            Items = items,
        };
    }

    /// <summary>
    /// Validates Material Request items against linked Sales Order lines (gotcha upstream #58443).
    /// If an item line links to a Sales Order, verifies item matches, UOM matches, and conversion factor matches.
    /// </summary>
    private async Task ValidateItemsAgainstSalesOrderAsync(MaterialRequest mr)
    {
        var soRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.SalesOrder, Guid>>();
        var mrManager = LazyServiceProvider.LazyGetRequiredService<MyERP.Purchasing.DomainServices.MaterialRequestManager>();
        await mrManager.ValidateWithSalesOrderAsync(mr, soRepo);
    }

    private async Task ValidateCompanyBoundariesAsync(CreateMaterialRequestDto input, Guid companyId)
    {
        var allProjectIds = input.Items
            .Where(i => i.ProjectId.HasValue)
            .Select(i => i.ProjectId!.Value)
            .ToList();
        if (input.ProjectId.HasValue)
        {
            allProjectIds.Add(input.ProjectId.Value);
        }
        allProjectIds = allProjectIds.Distinct().ToList();

        if (allProjectIds.Count > 0)
        {
            var projRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Projects.Entities.Project, Guid>>();
            var projects = await projRepo.GetListAsync(p => allProjectIds.Contains(p.Id));
            var projMismatch = projects.FirstOrDefault(p => p.CompanyId != companyId);
            if (projMismatch != null)
            {
                throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.CompanyMismatch)
                    .WithData("projectCompany", projMismatch.CompanyId)
                    .WithData("materialRequestCompany", companyId);
            }
        }

        if (input.WorkOrderId.HasValue)
        {
            var woRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Manufacturing.Entities.WorkOrder, Guid>>();
            var wo = await woRepo.FindAsync(input.WorkOrderId.Value);
            if (wo != null && wo.CompanyId != companyId)
            {
                throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.CompanyMismatch)
                    .WithData("workOrderCompany", wo.CompanyId)
                    .WithData("materialRequestCompany", companyId);
            }
        }

        var allWarehouseIds = input.Items
            .Where(i => i.WarehouseId.HasValue)
            .Select(i => i.WarehouseId!.Value)
            .ToList();
        if (input.SourceWarehouseId.HasValue)
            allWarehouseIds.Add(input.SourceWarehouseId.Value);
        if (input.TargetWarehouseId.HasValue)
            allWarehouseIds.Add(input.TargetWarehouseId.Value);
        allWarehouseIds = allWarehouseIds.Distinct().ToList();

        if (allWarehouseIds.Count > 0)
        {
            var whRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Warehouse, Guid>>();
            var warehouses = await whRepo.GetListAsync(w => allWarehouseIds.Contains(w.Id));
            var whMismatch = warehouses.FirstOrDefault(w => w.CompanyId != companyId);
            if (whMismatch != null)
            {
                throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.CompanyMismatch)
                    .WithData("warehouseCompany", whMismatch.CompanyId)
                    .WithData("materialRequestCompany", companyId);
            }
        }

        var linkedSoIds = input.Items
            .Where(i => i.SalesOrderId.HasValue)
            .Select(i => i.SalesOrderId!.Value)
            .Distinct()
            .ToList();
        if (linkedSoIds.Count > 0)
        {
            var soRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Sales.Entities.SalesOrder, Guid>>();
            var salesOrders = await soRepo.GetListAsync(s => linkedSoIds.Contains(s.Id));
            var soMismatch = salesOrders.FirstOrDefault(s => s.CompanyId != companyId);
            if (soMismatch != null)
            {
                throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.CompanyMismatch)
                    .WithData("salesOrderCompany", soMismatch.CompanyId)
                    .WithData("materialRequestCompany", companyId);
            }
        }

        var linkedSoItemIds = input.Items
            .Where(i => i.SalesOrderItemId.HasValue)
            .Select(i => i.SalesOrderItemId!.Value)
            .Distinct()
            .ToList();
        if (linkedSoItemIds.Count > 0)
        {
            var soRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Sales.Entities.SalesOrder, Guid>>();
            var soQuery = await soRepo.GetQueryableAsync();
            var crossCompanySo = soQuery
                .Where(so => so.Items.Any(i => linkedSoItemIds.Contains(i.Id)) && so.CompanyId != companyId)
                .FirstOrDefault();
            if (crossCompanySo != null)
            {
                throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.CompanyMismatch)
                    .WithData("salesOrderCompany", crossCompanySo.CompanyId)
                    .WithData("materialRequestCompany", companyId);
            }
        }

        var itemIds = input.Items.Select(i => i.ItemId).Distinct().ToList();
        var companyRestriction = LazyServiceProvider.LazyGetRequiredService<CompanyRestrictionValidationService>();
        await companyRestriction.ValidateTransactionCompanyAsync(
            "MaterialRequest", companyId,
            itemIds: itemIds,
            warehouseIds: allWarehouseIds.Count > 0 ? allWarehouseIds : null);
    }
}