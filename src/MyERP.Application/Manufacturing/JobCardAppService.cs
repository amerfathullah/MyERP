using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.Core.DomainServices;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Manufacturing.DomainServices;
using MyERP.Manufacturing.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Manufacturing;

[Authorize(MyERPPermissions.Manufacturing.Default)]
public class JobCardAppService : ApplicationService, IJobCardAppService
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
        if (input.ForQuantity <= 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", "ForQuantity");
        }

        var woRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<WorkOrder, Guid>>();
        var wo = await woRepo.GetAsync(input.WorkOrderId);
        if (wo.CompanyId != input.CompanyId)
        {
            throw new BusinessException(MyERPDomainErrorCodes.CompanyMismatch);
        }
        if (wo.Status is WorkOrderStatus.Draft or WorkOrderStatus.Cancelled or WorkOrderStatus.Completed)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("documentType", "WorkOrder")
                .WithData("status", wo.Status.ToString());
        }

        var jc = new JobCard(GuidGenerator.Create(), input.CompanyId, input.WorkOrderId,
            input.OperationId, input.ForQuantity, input.SequenceId, CurrentTenant.Id)
        {
            WorkstationId = input.WorkstationId,
            PlannedTimeInMins = input.PlannedTimeInMins,
        };
        await _repository.InsertAsync(jc);

        // Workstation scheduling: compute time slot for capacity planning
        // Per DO-NOT: "Skip workstation holiday enforcement on Job Card scheduling"
        if (jc.WorkstationId.HasValue && jc.PlannedTimeInMins > 0)
        {
            var schedulingService = LazyServiceProvider
                .LazyGetRequiredService<WorkstationSchedulingService>();
            var slot = await schedulingService.ScheduleJobCardAsync(
                jc.WorkstationId.Value, jc.CompanyId,
                jc.PlannedTimeInMins, DateTime.UtcNow);

            if (slot.Status == ScheduleStatus.NoCapacity)
            {
                Logger.LogWarning(
                    "No workstation capacity for JobCard {JobCardId} within planning window",
                    jc.Id);
            }
        }

        return ObjectMapper.Map<JobCard, JobCardDto>(jc);
    }

    [Authorize(MyERPPermissions.Manufacturing.Edit)]
    public async Task<JobCardDto> UpdateAsync(Guid id, CreateJobCardDto input)
    {
        var jc = await _repository.GetAsync(id);
        if (jc.Status != JobCardStatus.Open)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("documentType", "JobCard")
                .WithData("status", jc.Status.ToString());

        jc.WorkstationId = input.WorkstationId;
        jc.PlannedTimeInMins = input.PlannedTimeInMins;
        jc.ForQuantity = input.ForQuantity;
        jc.SequenceId = input.SequenceId;

        await _repository.UpdateAsync(jc);
        return ObjectMapper.Map<JobCard, JobCardDto>(jc);
    }

    [Authorize(MyERPPermissions.Manufacturing.Edit)]
    public async Task<JobCardDto> StartAsync(Guid id)
    {
        var jc = await _repository.GetAsync(id);

        var jobCardManager = LazyServiceProvider.LazyGetRequiredService<JobCardManager>();
        var woRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<WorkOrder, Guid>>();
        await jobCardManager.ValidateWorkOrderNotClosedAsync(jc, woRepo);
        await jobCardManager.ValidateMaterialTransferAsync(jc, woRepo);
        await jobCardManager.ValidatePreviousOperationManufacturedAsync(jc);
        await jobCardManager.ValidateCapacityAsync(jc);

        jc.Start();
        await _repository.UpdateAsync(jc);
        return ObjectMapper.Map<JobCard, JobCardDto>(jc);
    }

    [Authorize(MyERPPermissions.Manufacturing.Edit)]
    public async Task<JobCardDto> AddTimeLogAsync(Guid id, AddTimeLogDto input)
    {
        if (input.ToTime < input.FromTime)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidDateRange);
        }

        var jc = (await _repository.WithDetailsAsync()).First(j => j.Id == id);
        var jobCardManager = LazyServiceProvider.LazyGetRequiredService<JobCardManager>();
        var woRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<WorkOrder, Guid>>();
        await jobCardManager.ValidateWorkOrderNotClosedAsync(jc, woRepo);

        jc.AddTimeLog(input.FromTime, input.ToTime, input.CompletedQty);
        await _repository.UpdateAsync(jc);
        return ObjectMapper.Map<JobCard, JobCardDto>(jc);
    }

    [Authorize(MyERPPermissions.Manufacturing.Edit)]
    public async Task<JobCardDto> CompleteAsync(Guid id)
    {
        var jc = await _repository.GetAsync(id);

        var jobCardManager = LazyServiceProvider.LazyGetRequiredService<JobCardManager>();
        var woRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<WorkOrder, Guid>>();
        await jobCardManager.ValidateWorkOrderNotClosedAsync(jc, woRepo);
        await jobCardManager.ValidateMaterialTransferAsync(jc, woRepo);

        var settingsRepoForTimeLogs = LazyServiceProvider.LazyGetRequiredService<IRepository<ManufacturingSettings, Guid>>();
        var mfgSettings = await settingsRepoForTimeLogs.FindAsync(s => s.CompanyId == jc.CompanyId);
        if (mfgSettings?.EnforceTimeLogs == true && !jc.TimeLogs.Any())
        {
            throw new BusinessException(MyERPDomainErrorCodes.JobCardTimeLogRequired);
        }

        jc.Complete();
        await _repository.UpdateAsync(jc);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "JobCard", jc.Id,
            "Completed", jc.CompanyId,
            jc.Id.ToString(), "InProcess", "Completed", CurrentUser.Id,
            $"Job Card for sequence {jc.SequenceId} completed ({jc.CompletedQty} qty)", CurrentTenant.Id));

        // Update Work Order produced qty using bottleneck formula (MIN across operations)
        var jcManager = LazyServiceProvider.LazyGetRequiredService<JobCardManager>();
        var wo = await woRepo.GetAsync(jc.WorkOrderId, includeDetails: true);

        // Roll up process loss to Work Order for semi-finished goods tracking (ERPNext PR #57895 / commit 0eb61c9fac)
        if (wo.TrackSemiFinishedGoods)
        {
            var jcQ = await _repository.GetQueryableAsync();
            var totalProcessLoss = jcQ
                .Where(c => c.WorkOrderId == wo.Id && (c.Status == JobCardStatus.Completed || c.Id == jc.Id))
                .Sum(c => c.ProcessLossQty);
            wo.SetProcessLossQty(totalProcessLoss);
            await woRepo.UpdateAsync(wo, autoSave: true);
        }

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

            // Build a real Manufacture Stock Entry instead of moving stock directly — mirrors
            // the round-58f fix already applied to ManufacturingAppService.RecordProductionAsync.
            // Moving stock via valuationService/binService straight through here (as this method
            // used to) creates SLE/Bin movement with NO backing Stock Entry document and NO GL
            // posting at all, silently skipping both the audit trail and the accounting impact
            // for every unit produced through Job Card completion (round-78 fix).
            var postingOrchestrator = LazyServiceProvider
                .LazyGetRequiredService<Accounting.DomainServices.DocumentPostingOrchestrator>();
            await postingOrchestrator.ValidatePostingPeriodAsync(wo.CompanyId, DateTime.UtcNow, "WorkOrder");

            var valuationService = LazyServiceProvider.LazyGetRequiredService<Inventory.DomainServices.StockValuationService>();
            var binService = LazyServiceProvider.LazyGetRequiredService<Inventory.DomainServices.BinService>();
            var numberGen = LazyServiceProvider.LazyGetRequiredService<IDocumentNumberGenerator>();

            var entry = new Inventory.Entities.StockEntry(
                GuidGenerator.Create(), wo.CompanyId, StockEntryType.Manufacture,
                DateTime.UtcNow.Date, CurrentTenant.Id)
            {
                WorkOrderId = wo.Id,
                EntryNumber = await numberGen.GenerateAsync("SE", wo.CompanyId),
                FgCompletedQty = delta,
                Notes = $"Production recorded — WO {wo.WorkOrderNumber} (Job Card {jc.Id} completion)",
            };

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

                    entry.AddItem(
                        itemId: item.ItemId, quantity: issueQty,
                        sourceWarehouseId: warehouseId.Value, targetWarehouseId: null,
                        valuationRate: rmRate);

                    await binService.UpdateReservedQtyForProductionAsync(
                        item.ItemId, warehouseId.Value, -issueQty, wo.TenantId);
                }
            }

            // Receive finished goods at absorbed cost
            if (wo.FgWarehouseId.HasValue && delta > 0)
            {
                var fgRate = totalRmCost / delta;

                entry.AddItem(
                    itemId: wo.ItemId, quantity: delta,
                    sourceWarehouseId: null, targetWarehouseId: wo.FgWarehouseId.Value,
                    valuationRate: fgRate);

                await binService.UpdatePlannedQtyAsync(
                    wo.ItemId, wo.FgWarehouseId.Value, -delta, wo.TenantId);
            }

            // Submit + post the entry: StockPostingService creates the SLE/Bin movement,
            // DocumentPostingOrchestrator posts the GL Journal Entry, then the entry itself is
            // persisted so this production event is auditable via the normal Stock Entry list.
            entry.Submit();
            entry.Post();
            var stockPostingService = LazyServiceProvider
                .LazyGetRequiredService<Inventory.DomainServices.StockPostingService>();
            await stockPostingService.PostStockEntryAsync(entry);
            await FlushPendingChangesAsync();
            await postingOrchestrator.PostStockEntryAsync(entry);

            var seRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Inventory.Entities.StockEntry, Guid>>();
            await seRepo.InsertAsync(entry, autoSave: true);

            await woRepo.UpdateAsync(wo, autoSave: true);
        }

        return ObjectMapper.Map<JobCard, JobCardDto>(jc);
    }

    /// <summary>
    /// Flushes pending changes (e.g. StockPostingService's just-inserted SLEs) to the DB without
    /// completing/committing the ambient UnitOfWork — needed before
    /// DocumentPostingOrchestrator.PostStockEntryAsync's own SLE query, which otherwise sees zero
    /// rows and silently skips building the GL Journal Entry. Same helper as
    /// ManufacturingAppService.FlushPendingChangesAsync.
    /// </summary>
    private async Task FlushPendingChangesAsync()
    {
        var uowManager = LazyServiceProvider.LazyGetRequiredService<Volo.Abp.Uow.IUnitOfWorkManager>();
        if (uowManager.Current != null)
        {
            await uowManager.Current.SaveChangesAsync();
        }
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
        var jobCardManager = LazyServiceProvider.LazyGetRequiredService<JobCardManager>();
        var woRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<WorkOrder, Guid>>();
        await jobCardManager.ValidateWorkOrderNotClosedAsync(jc, woRepo);

        jc.Hold();
        await _repository.UpdateAsync(jc);
        return ObjectMapper.Map<JobCard, JobCardDto>(jc);
    }

    [Authorize(MyERPPermissions.Manufacturing.Edit)]
    public async Task<JobCardDto> ResumeAsync(Guid id)
    {
        var jc = await _repository.GetAsync(id);
        var jobCardManager = LazyServiceProvider.LazyGetRequiredService<JobCardManager>();
        var woRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<WorkOrder, Guid>>();
        await jobCardManager.ValidateWorkOrderNotClosedAsync(jc, woRepo);

        jc.Resume();
        await _repository.UpdateAsync(jc);
        return ObjectMapper.Map<JobCard, JobCardDto>(jc);
    }

    [Authorize(MyERPPermissions.Manufacturing.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        var jc = await _repository.GetAsync(id);
        if (jc.Status != JobCardStatus.Open)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("documentType", "JobCard")
                .WithData("status", jc.Status.ToString());
        await _repository.DeleteAsync(id);
    }
}

