using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.DomainServices;
using MyERP.Manufacturing.DomainServices;
using MyERP.Manufacturing.Entities;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Manufacturing;

[Authorize(MyERPPermissions.MasterProductionSchedules.Default)]
public class MasterProductionScheduleAppService : ApplicationService, IMasterProductionScheduleAppService
{
    private readonly IRepository<MasterProductionSchedule, Guid> _repository;
    private readonly IRepository<SalesOrder, Guid> _salesOrderRepository;
    private readonly IRepository<MaterialRequest, Guid> _materialRequestRepository;
    private readonly IDocumentNumberGenerator _numberGenerator;
    private readonly MasterProductionScheduleService _leadTimeService;

    public MasterProductionScheduleAppService(
        IRepository<MasterProductionSchedule, Guid> repository,
        IRepository<SalesOrder, Guid> salesOrderRepository,
        IRepository<MaterialRequest, Guid> materialRequestRepository,
        IDocumentNumberGenerator numberGenerator,
        MasterProductionScheduleService leadTimeService)
    {
        _repository = repository;
        _salesOrderRepository = salesOrderRepository;
        _materialRequestRepository = materialRequestRepository;
        _numberGenerator = numberGenerator;
        _leadTimeService = leadTimeService;
    }

    private static readonly DocumentStatus[] OpenSalesOrderStatuses =
        { DocumentStatus.ToDeliverAndBill, DocumentStatus.ToDeliver, DocumentStatus.ToBill };

    public async Task<MasterProductionScheduleDto> GetAsync(Guid id)
    {
        var schedule = await _repository.GetAsync(id, includeDetails: true);
        return ObjectMapper.Map<MasterProductionSchedule, MasterProductionScheduleDto>(schedule);
    }

    public async Task<PagedResultDto<MasterProductionScheduleDto>> GetListAsync(MyERP.Shared.CompanyFilteredPagedRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        if (input.CompanyId.HasValue)
            query = query.Where(s => s.CompanyId == input.CompanyId.Value);

        var totalCount = query.Count();
        var items = query.OrderByDescending(s => s.CreationTime).Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<MasterProductionScheduleDto>(totalCount, items.Select(ObjectMapper.Map<MasterProductionSchedule, MasterProductionScheduleDto>).ToList());
    }

    [Authorize(MyERPPermissions.MasterProductionSchedules.Create)]
    public async Task<MasterProductionScheduleDto> CreateAsync(CreateMasterProductionScheduleDto input)
    {
        var number = await _numberGenerator.GenerateAsync("MPS", input.CompanyId);
        var schedule = new MasterProductionSchedule(GuidGenerator.Create(), input.CompanyId, number, input.PostingDate, input.FromDate, CurrentTenant.Id)
        {
            ParentWarehouseId = input.ParentWarehouseId,
        };
        await _repository.InsertAsync(schedule);
        return ObjectMapper.Map<MasterProductionSchedule, MasterProductionScheduleDto>(schedule);
    }

    [Authorize(MyERPPermissions.MasterProductionSchedules.Edit)]
    public async Task<MasterProductionScheduleDto> UpdateAsync(Guid id, UpdateMasterProductionScheduleDto input)
    {
        var schedule = await _repository.GetAsync(id, includeDetails: true);
        schedule.PostingDate = input.PostingDate;
        schedule.FromDate = input.FromDate;
        schedule.ParentWarehouseId = input.ParentWarehouseId;

        if (input.Items.Count > 0)
        {
            var byId = schedule.Items.ToDictionary(i => i.Id);
            foreach (var itemInput in input.Items)
            {
                if (byId.TryGetValue(itemInput.Id, out var item))
                    item.PlannedQty = itemInput.PlannedQty;
            }
        }

        await _repository.UpdateAsync(schedule);
        return ObjectMapper.Map<MasterProductionSchedule, MasterProductionScheduleDto>(schedule);
    }

    [Authorize(MyERPPermissions.MasterProductionSchedules.Delete)]
    public async Task DeleteAsync(Guid id) => await _repository.DeleteAsync(id);

    [Authorize(MyERPPermissions.MasterProductionSchedules.Edit)]
    public async Task<MasterProductionScheduleDto> FetchSalesOrdersAsync(Guid id, FetchSalesOrdersDto input)
    {
        var schedule = await _repository.GetAsync(id, includeDetails: true);

        var query = await _salesOrderRepository.GetQueryableAsync();
        query = query.Where(so => so.CompanyId == schedule.CompanyId && OpenSalesOrderStatuses.Contains(so.Status));
        if (input.CustomerId.HasValue)
            query = query.Where(so => so.CustomerId == input.CustomerId.Value);
        if (input.FromDate.HasValue)
            query = query.Where(so => so.OrderDate >= input.FromDate.Value);
        if (input.ToDate.HasValue)
            query = query.Where(so => so.OrderDate <= input.ToDate.Value);

        var orders = query.OrderBy(so => so.DeliveryDate).ToList();
        schedule.SetSalesOrders(orders.Select(so => new MpsSalesOrderRef(
            GuidGenerator.Create(), schedule.Id, so.Id, so.OrderDate, so.CustomerId, so.GrandTotal, so.Status.ToString())));

        await _repository.UpdateAsync(schedule);
        return ObjectMapper.Map<MasterProductionSchedule, MasterProductionScheduleDto>(schedule);
    }

    [Authorize(MyERPPermissions.MasterProductionSchedules.Edit)]
    public async Task<MasterProductionScheduleDto> FetchMaterialRequestsAsync(Guid id, FetchMaterialRequestsDto input)
    {
        var schedule = await _repository.GetAsync(id, includeDetails: true);

        var query = await _materialRequestRepository.GetQueryableAsync();
        query = query.Where(mr => mr.CompanyId == schedule.CompanyId && mr.Status == DocumentStatus.Submitted);
        if (input.FromDate.HasValue)
            query = query.Where(mr => mr.RequestDate >= input.FromDate.Value);
        if (input.ToDate.HasValue)
            query = query.Where(mr => mr.RequestDate <= input.ToDate.Value);

        var requests = query.OrderBy(mr => mr.RequestDate).ToList();
        schedule.SetMaterialRequests(requests.Select(mr => new MpsMaterialRequestRef(
            GuidGenerator.Create(), schedule.Id, mr.Id, mr.RequestDate)));

        await _repository.UpdateAsync(schedule);
        return ObjectMapper.Map<MasterProductionSchedule, MasterProductionScheduleDto>(schedule);
    }

    /// <summary>
    /// Aggregates staged (or, when none staged, all open) Sales Order and Material Request
    /// demand into the Items table: qty summed per (item, delivery date), with a
    /// lead-time-backed order release date. Per ERPNext get_actual_demand().
    /// </summary>
    [Authorize(MyERPPermissions.MasterProductionSchedules.Edit)]
    public async Task<MasterProductionScheduleDto> ComputeActualDemandAsync(Guid id)
    {
        var schedule = await _repository.GetAsync(id, includeDetails: true);

        var soIds = schedule.SalesOrders.Select(s => s.SalesOrderId).ToHashSet();
        var soQuery = (await _salesOrderRepository.WithDetailsAsync())
            .Where(so => so.CompanyId == schedule.CompanyId && OpenSalesOrderStatuses.Contains(so.Status));
        if (soIds.Count > 0)
            soQuery = soQuery.Where(so => soIds.Contains(so.Id));

        var demand = new Dictionary<(Guid ItemId, DateTime DeliveryDate), (string ItemName, decimal Qty, Guid? WarehouseId)>();

        foreach (var so in soQuery.ToList())
        {
            foreach (var line in so.Items)
            {
                var deliveryDate = (line.DeliveryDate ?? so.DeliveryDate ?? so.OrderDate).Date;
                Accumulate(demand, line.ItemId, deliveryDate, line.StockQty, line.WarehouseId ?? schedule.ParentWarehouseId);
            }
        }

        var mrIds = schedule.MaterialRequests.Select(m => m.MaterialRequestId).ToHashSet();
        var mrQuery = (await _materialRequestRepository.WithDetailsAsync())
            .Where(mr => mr.CompanyId == schedule.CompanyId && mr.Status == DocumentStatus.Submitted);
        if (mrIds.Count > 0)
            mrQuery = mrQuery.Where(mr => mrIds.Contains(mr.Id));

        foreach (var mr in mrQuery.ToList())
        {
            var deliveryDate = (mr.RequiredByDate ?? mr.RequestDate).Date;
            foreach (var line in mr.Items)
                Accumulate(demand, line.ItemId, deliveryDate, line.Quantity, line.WarehouseId ?? schedule.ParentWarehouseId);
        }

        var itemNames = await ResolveItemNamesAsync(demand.Keys.Select(k => k.ItemId).Distinct());

        var scheduleItems = new List<MasterProductionScheduleItem>();
        foreach (var (key, value) in demand)
        {
            var leadTimeDays = await _leadTimeService.GetCumulativeLeadTimeDaysAsync(key.ItemId);
            scheduleItems.Add(new MasterProductionScheduleItem(
                GuidGenerator.Create(), schedule.Id, key.ItemId,
                itemNames.GetValueOrDefault(key.ItemId, value.ItemName), key.DeliveryDate, value.Qty, leadTimeDays)
            {
                WarehouseId = value.WarehouseId,
            });
        }

        schedule.SetItems(scheduleItems.OrderBy(i => i.DeliveryDate));

        await _repository.UpdateAsync(schedule);
        return ObjectMapper.Map<MasterProductionSchedule, MasterProductionScheduleDto>(schedule);
    }

    [Authorize(MyERPPermissions.MasterProductionSchedules.Submit)]
    public async Task<MasterProductionScheduleDto> SubmitAsync(Guid id)
    {
        var schedule = await _repository.GetAsync(id, includeDetails: true);
        schedule.Submit();
        await _repository.UpdateAsync(schedule);
        return ObjectMapper.Map<MasterProductionSchedule, MasterProductionScheduleDto>(schedule);
    }

    [Authorize(MyERPPermissions.MasterProductionSchedules.Cancel)]
    public async Task<MasterProductionScheduleDto> CancelAsync(Guid id)
    {
        var schedule = await _repository.GetAsync(id, includeDetails: true);
        schedule.Cancel();
        await _repository.UpdateAsync(schedule);
        return ObjectMapper.Map<MasterProductionSchedule, MasterProductionScheduleDto>(schedule);
    }

    private static void Accumulate(
        Dictionary<(Guid, DateTime), (string, decimal, Guid?)> demand,
        Guid itemId, DateTime deliveryDate, decimal qty, Guid? warehouseId)
    {
        var key = (itemId, deliveryDate);
        if (demand.TryGetValue(key, out var existing))
            demand[key] = (existing.Item1, existing.Item2 + qty, existing.Item3 ?? warehouseId);
        else
            demand[key] = (string.Empty, qty, warehouseId);
    }

    private async Task<Dictionary<Guid, string>> ResolveItemNamesAsync(IEnumerable<Guid> itemIds)
    {
        var ids = itemIds.ToList();
        if (ids.Count == 0) return new Dictionary<Guid, string>();

        var itemRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Inventory.Entities.Item, Guid>>();
        var query = await itemRepo.GetQueryableAsync();
        return query.Where(i => ids.Contains(i.Id)).ToDictionary(i => i.Id, i => i.ItemName);
    }
}
