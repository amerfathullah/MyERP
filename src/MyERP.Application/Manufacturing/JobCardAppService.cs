using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.DomainServices;
using MyERP.Manufacturing.DomainServices;
using MyERP.Manufacturing.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Manufacturing;

public class JobCardDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public Guid WorkOrderId { get; set; }
    public Guid OperationId { get; set; }
    public Guid? BomOperationId { get; set; }
    public Guid? WorkstationId { get; set; }
    public Guid? FinishedGoodItemId { get; set; }
    public Guid? SemiFgBomId { get; set; }
    public bool IsCorrective { get; set; }
    public decimal ForQuantity { get; set; }
    public decimal CompletedQty { get; set; }
    public decimal TotalTimeInMins { get; set; }
    public decimal PlannedTimeInMins { get; set; }
    public int SequenceId { get; set; }
    public int Status { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public JobCardTimeLogDto[] TimeLogs { get; set; } = [];
    public DateTime CreationTime { get; set; }
}

public class JobCardTimeLogDto
{
    public Guid Id { get; set; }
    public DateTime FromTime { get; set; }
    public DateTime ToTime { get; set; }
    public decimal TimeInMins { get; set; }
    public decimal CompletedQty { get; set; }
}

public class CreateJobCardDto
{
    public Guid CompanyId { get; set; }
    public Guid WorkOrderId { get; set; }
    public Guid OperationId { get; set; }
    public Guid? WorkstationId { get; set; }
    public decimal ForQuantity { get; set; }
    public int SequenceId { get; set; }
    public decimal PlannedTimeInMins { get; set; }
}

public class AddTimeLogDto
{
    public DateTime FromTime { get; set; }
    public DateTime ToTime { get; set; }
    public decimal CompletedQty { get; set; }
}

public class GetJobCardListDto : PagedAndSortedResultRequestDto
{
    public Guid? WorkOrderId { get; set; }
    public Guid? CompanyId { get; set; }
    public JobCardStatus? Status { get; set; }
    public string? Filter { get; set; }
}

[Authorize(MyERPPermissions.Manufacturing.Default)]
public class JobCardAppService : ApplicationService
{
    private readonly IRepository<JobCard, Guid> _repository;

    public JobCardAppService(IRepository<JobCard, Guid> repository) => _repository = repository;

    public async Task<PagedResultDto<JobCardDto>> GetListAsync(GetJobCardListDto input)
    {
        var query = (await _repository.WithDetailsAsync()).AsQueryable();
        if (input.WorkOrderId.HasValue)
            query = query.Where(j => j.WorkOrderId == input.WorkOrderId.Value);
        if (input.CompanyId.HasValue)
            query = query.Where(j => j.CompanyId == input.CompanyId.Value);
        if (input.Status.HasValue)
            query = query.Where(j => j.Status == input.Status.Value);
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var f = input.Filter;
            query = query.Where(j => j.WorkstationType != null && j.WorkstationType.Contains(f));
        }

        var totalCount = query.Count();
        var items = query.OrderByDescending(j => j.CreationTime)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<JobCardDto>(totalCount, items.Select(x => ObjectMapper.Map<JobCard, JobCardDto>(x)).ToList());
    }

    public async Task<JobCardDto> GetAsync(Guid id)
    {
        var jc = (await _repository.WithDetailsAsync()).First(j => j.Id == id);
        return ObjectMapper.Map<JobCard, JobCardDto>(jc);
    }

    [Authorize(MyERPPermissions.Manufacturing.Create)]
    public async Task<JobCardDto> CreateAsync(CreateJobCardDto input)
    {
        var jc = new JobCard(GuidGenerator.Create(), input.CompanyId, input.WorkOrderId,
            input.OperationId, input.ForQuantity, input.SequenceId, CurrentTenant.Id)
        {
            WorkstationId = input.WorkstationId,
            PlannedTimeInMins = input.PlannedTimeInMins,
        };
        await _repository.InsertAsync(jc);
        return ObjectMapper.Map<JobCard, JobCardDto>(jc);
    }

    [Authorize(MyERPPermissions.Manufacturing.Edit)]
    public async Task<JobCardDto> StartAsync(Guid id)
    {
        var jc = await _repository.GetAsync(id);
        jc.Start();
        await _repository.UpdateAsync(jc);
        return ObjectMapper.Map<JobCard, JobCardDto>(jc);
    }

    [Authorize(MyERPPermissions.Manufacturing.Edit)]
    public async Task<JobCardDto> AddTimeLogAsync(Guid id, AddTimeLogDto input)
    {
        var jc = (await _repository.WithDetailsAsync()).First(j => j.Id == id);
        jc.AddTimeLog(input.FromTime, input.ToTime, input.CompletedQty);
        await _repository.UpdateAsync(jc);
        return ObjectMapper.Map<JobCard, JobCardDto>(jc);
    }

    [Authorize(MyERPPermissions.Manufacturing.Edit)]
    public async Task<JobCardDto> CompleteAsync(Guid id)
    {
        var jc = await _repository.GetAsync(id);
        jc.Complete();
        await _repository.UpdateAsync(jc);

        // Update Work Order produced qty using bottleneck formula (MIN across operations)
        var jcManager = LazyServiceProvider.LazyGetRequiredService<JobCardManager>();
        var woRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<WorkOrder, Guid>>();
        var wo = await woRepo.GetAsync(jc.WorkOrderId, includeDetails: true);
        var completedQty = await jcManager.GetWorkOrderCompletedQtyAsync(wo.Id);

        // Only process if bottleneck qty exceeds what WO already recorded
        if (completedQty > wo.ProducedQuantity)
        {
            var delta = completedQty - wo.ProducedQuantity;

            // Read overproduction percentage from ManufacturingSettings
            var settingsRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<ManufacturingSettings, Guid>>();
            var settingsQ = await settingsRepo.GetQueryableAsync();
            var settings = settingsQ.FirstOrDefault(s => s.CompanyId == wo.CompanyId);
            var overproductionPct = settings?.OverproductionPercentage ?? 5m;

            wo.RecordProduction(delta, overproductionPercentage: overproductionPct);

            // Create actual stock movements (RM consumption + FG receipt)
            // Per ERPNext: Job Card completion triggers Manufacture Stock Entry
            var valuationService = LazyServiceProvider.LazyGetRequiredService<Inventory.DomainServices.StockValuationService>();
            var binService = LazyServiceProvider.LazyGetRequiredService<Inventory.DomainServices.BinService>();

            decimal totalRmCost = 0;
            var productionRatio = wo.Quantity > 0 ? delta / wo.Quantity : 0m;

            // Consume raw materials proportionally
            foreach (var item in wo.RequiredItems)
            {
                var issueQty = Math.Round(item.RequiredQuantity * productionRatio, 4);
                var warehouseId = item.SourceWarehouseId ?? wo.SourceWarehouseId;
                if (issueQty > 0 && warehouseId.HasValue)
                {
                    var rmBalance = await valuationService.GetCurrentBalanceAsync(item.ItemId, warehouseId.Value);
                    var rmRate = rmBalance.ValuationRate;
                    totalRmCost += issueQty * rmRate;

                    await valuationService.CreateLedgerEntryAsync(
                        wo.CompanyId, item.ItemId, warehouseId.Value,
                        DateTime.UtcNow, -issueQty, rmRate,
                        voucherType: "WorkOrder", voucherId: wo.Id,
                        tenantId: wo.TenantId);

                    await binService.ApplyStockMovementAsync(
                        item.ItemId, warehouseId.Value, -issueQty, -(issueQty * rmRate), wo.TenantId);

                    await binService.UpdateReservedQtyForProductionAsync(
                        item.ItemId, warehouseId.Value, -issueQty, wo.TenantId);
                }
            }

            // Receive finished goods at absorbed cost
            if (wo.FgWarehouseId.HasValue)
            {
                var fgRate = delta > 0 ? totalRmCost / delta : 0;

                await valuationService.CreateLedgerEntryAsync(
                    wo.CompanyId, wo.ItemId, wo.FgWarehouseId.Value,
                    DateTime.UtcNow, delta, fgRate,
                    voucherType: "WorkOrder", voucherId: wo.Id,
                    tenantId: wo.TenantId);

                await binService.ApplyStockMovementAsync(
                    wo.ItemId, wo.FgWarehouseId.Value, delta, totalRmCost, wo.TenantId);

                await binService.UpdatePlannedQtyAsync(
                    wo.ItemId, wo.FgWarehouseId.Value, -delta, wo.TenantId);
            }

            await woRepo.UpdateAsync(wo, autoSave: true);
        }

        return ObjectMapper.Map<JobCard, JobCardDto>(jc);
    }

    [Authorize(MyERPPermissions.Manufacturing.Edit)]
    public async Task<JobCardDto> CancelAsync(Guid id)
    {
        var jc = await _repository.GetAsync(id);
        jc.Cancel();
        await _repository.UpdateAsync(jc);
        return ObjectMapper.Map<JobCard, JobCardDto>(jc);
    }

    [Authorize(MyERPPermissions.Manufacturing.Edit)]
    public async Task<JobCardDto> HoldAsync(Guid id)
    {
        var jc = await _repository.GetAsync(id);
        jc.Hold();
        await _repository.UpdateAsync(jc);
        return ObjectMapper.Map<JobCard, JobCardDto>(jc);
    }

    [Authorize(MyERPPermissions.Manufacturing.Edit)]
    public async Task<JobCardDto> ResumeAsync(Guid id)
    {
        var jc = await _repository.GetAsync(id);
        jc.Resume();
        await _repository.UpdateAsync(jc);
        return ObjectMapper.Map<JobCard, JobCardDto>(jc);
    }
}

