using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Purchasing.DTOs;
using MyERP.Purchasing.Entities;
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

    public MaterialRequestAppService(
        IRepository<MaterialRequest, Guid> repository,
        IRepository<Item, Guid> itemRepository,
        IRepository<FiscalYear, Guid> fiscalYearRepository,
        IDocumentNumberGenerator numberGenerator,
        BudgetValidationService budgetValidation)
    {
        _repository = repository;
        _itemRepository = itemRepository;
        _fiscalYearRepository = fiscalYearRepository;
        _numberGenerator = numberGenerator;
        _budgetValidation = budgetValidation;
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
            var f = input.Filter.ToLower();
            query = query.Where(x => x.RequestNumber.ToLower().Contains(f));
        }
        if (!string.IsNullOrWhiteSpace(input.Status) && Enum.TryParse<Core.DocumentStatus>(input.Status, true, out var status))
            query = query.Where(x => x.Status == status);

        var totalCount = query.Count();
        var items = query.OrderByDescending(x => x.CreationTime)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<MaterialRequestDto>(totalCount, items.Select(x => ObjectMapper.Map<MaterialRequest, MaterialRequestDto>(x)).ToList());
    }

    [Authorize(MyERPPermissions.MaterialRequests.Create)]
    public async Task<MaterialRequestDto> CreateAsync(CreateMaterialRequestDto input)
    {
        var number = await _numberGenerator.GenerateAsync("MR", input.CompanyId);
        var entity = new MaterialRequest(
            GuidGenerator.Create(), input.CompanyId, number,
            input.RequestType, input.RequestDate, CurrentTenant.Id)
        {
            RequiredByDate = input.RequiredByDate,
            WorkOrderId = input.WorkOrderId,
            SourceWarehouseId = input.SourceWarehouseId,
            TargetWarehouseId = input.TargetWarehouseId,
            Notes = input.Notes,
        };

        // Validate all items are active
        var itemValidation = LazyServiceProvider.LazyGetRequiredService<MyERP.Inventory.DomainServices.ItemTransactionValidationService>();
        await itemValidation.ValidateItemsForTransactionAsync(input.Items.Select(i => i.ItemId).ToArray());

        foreach (var item in input.Items)
        {
            entity.AddItem(item.ItemId, item.ItemName, item.Quantity, item.Uom, item.WarehouseId);
        }

        await _repository.InsertAsync(entity);
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

        // Budget validation (Level 1: MR enforcement) — only for Purchase type
        if (entity.RequestType == MaterialRequestType.Purchase)
        {
            var fiscalYear = (await _fiscalYearRepository.GetQueryableAsync())
                .FirstOrDefault(fy => fy.CompanyId == entity.CompanyId
                                   && fy.StartDate <= entity.RequestDate
                                   && fy.EndDate >= entity.RequestDate);

            if (fiscalYear != null)
            {
                var budgetItems = new List<BudgetCheckItem>();
                foreach (var mrItem in entity.Items)
                {
                    var item = await _itemRepository.FindAsync(mrItem.ItemId);
                    if (item?.DefaultExpenseAccountId != null)
                    {
                        // MR items don't have price — use item's standard buying price or qty as estimate
                        var estimatedAmount = mrItem.Quantity * (item.StandardBuyingPrice ?? 1m);
                        budgetItems.Add(new BudgetCheckItem(
                            item.DefaultExpenseAccountId.Value,
                            estimatedAmount));
                    }
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

        var activityRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Core.Entities.DocumentActivityLog, Guid>>();
        await activityRepo.InsertAsync(new MyERP.Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "MaterialRequest", entity.Id, "Cancelled",
            entity.CompanyId, entity.RequestNumber, "Submitted", "Cancelled",
            CurrentUser.Id, tenantId: entity.TenantId));

        return ObjectMapper.Map<MaterialRequest, MaterialRequestDto>(entity);
    }
}
