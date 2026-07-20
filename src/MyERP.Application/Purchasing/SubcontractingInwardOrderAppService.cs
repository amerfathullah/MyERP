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

#region DTOs

public class SubcontractingInwardOrderDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string OrderNumber { get; set; } = null!;
    public DateTime OrderDate { get; set; }
    public Guid SupplierId { get; set; }
    public Guid? SalesOrderId { get; set; }
    public Guid? SubcontractingOrderId { get; set; }
    public string CurrencyCode { get; set; } = "MYR";
    public decimal NetTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public int Status { get; set; }
    public decimal PerReceived { get; set; }
    public decimal PerBilled { get; set; }
    public SubcontractingInwardOrderItemDto[] Items { get; set; } = [];
}

public class SubcontractingInwardOrderItemDto : EntityDto<Guid>
{
    public Guid ItemId { get; set; }
    public Guid? BomId { get; set; }
    public decimal Quantity { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
    public decimal ReceivedQty { get; set; }
    public decimal BilledQty { get; set; }
    public decimal PendingReceiptQty { get; set; }
    public Guid? WarehouseId { get; set; }
    public decimal ServiceCostPerQty { get; set; }
}

public class CreateSubcontractingInwardOrderDto
{
    public Guid CompanyId { get; set; }
    public Guid SupplierId { get; set; }
    public DateTime OrderDate { get; set; }
    public Guid? SalesOrderId { get; set; }
    public Guid? SubcontractingOrderId { get; set; }
    public string CurrencyCode { get; set; } = "MYR";
    public CreateScioItemDto[] Items { get; set; } = [];
}

public class CreateScioItemDto
{
    public Guid ItemId { get; set; }
    public Guid? BomId { get; set; }
    public decimal Quantity { get; set; }
    public decimal Rate { get; set; }
    public Guid? WarehouseId { get; set; }
    public decimal ServiceCostPerQty { get; set; }
}

#endregion

/// <summary>
/// Application service for Subcontracting Inward Order management.
/// Per DO-NOT: "Allow SO item updates when Subcontracting Inward Order exists (must cancel SCIO first)"
/// Per DO-NOT: "Close Sales Order without cascading status to linked Subcontracting Inward Orders"
/// </summary>
[Authorize(MyERPPermissions.PurchaseOrders.Default)]
public class SubcontractingInwardOrderAppService : ApplicationService
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
        if (input.Items == null || input.Items.Length == 0)
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustHaveItems);

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
        return ObjectMapper.Map<SubcontractingInwardOrder, SubcontractingInwardOrderDto>(entity);
    }

    [Authorize(MyERPPermissions.PurchaseOrders.Submit)]
    public async Task<SubcontractingInwardOrderDto> SubmitAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        entity.Submit();
        await _repository.UpdateAsync(entity);
        return ObjectMapper.Map<SubcontractingInwardOrder, SubcontractingInwardOrderDto>(entity);
    }

    [Authorize(MyERPPermissions.PurchaseOrders.Cancel)]
    public async Task<SubcontractingInwardOrderDto> CancelAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        entity.Cancel();
        await _repository.UpdateAsync(entity);
        return ObjectMapper.Map<SubcontractingInwardOrder, SubcontractingInwardOrderDto>(entity);
    }

    [Authorize(MyERPPermissions.PurchaseOrders.Edit)]
    public async Task<SubcontractingInwardOrderDto> CloseAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        entity.Close();
        await _repository.UpdateAsync(entity);
        return ObjectMapper.Map<SubcontractingInwardOrder, SubcontractingInwardOrderDto>(entity);
    }
}
