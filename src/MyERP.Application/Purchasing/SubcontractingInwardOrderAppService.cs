using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
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

    public SubcontractingInwardOrderAppService(
        IRepository<SubcontractingInwardOrder, Guid> repository,
        IRepository<Core.Entities.DocumentSeries, Guid> seriesRepository)
    {
        _repository = repository;
        _seriesRepository = seriesRepository;
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

        return ObjectMapper.Map<SubcontractingInwardOrder, SubcontractingInwardOrderDto>(entity);
    }

    [Authorize(MyERPPermissions.PurchaseOrders.Edit)]
    public async Task<SubcontractingInwardOrderDto> CloseAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        entity.Close();
        await _repository.UpdateAsync(entity);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "SubcontractingInwardOrder", entity.Id,
            "Closed", entity.CompanyId,
            entity.OrderNumber, "Submitted", "Closed", CurrentUser.Id,
            $"Subcontracting inward order '{entity.OrderNumber}' closed", CurrentTenant.Id));

        return ObjectMapper.Map<SubcontractingInwardOrder, SubcontractingInwardOrderDto>(entity);
    }
}
