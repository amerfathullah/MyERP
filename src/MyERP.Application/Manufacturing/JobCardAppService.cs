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
        if (wo.Status is WorkOrderStatus.Draft or WorkOrderStatus.Cancelled or WorkOrderStatus.Completed or WorkOrderStatus.Stopped or WorkOrderStatus.Closed)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("documentType", "WorkOrder")
                .WithData("status", wo.Status.ToString());
        }

        if (input.WorkstationId.HasValue)
        {
            var wsRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Workstation, Guid>>();
            var ws = await wsRepo.GetAsync(input.WorkstationId.Value);
            if (ws.CompanyId != input.CompanyId)
            {
                throw new BusinessException(MyERPDomainErrorCodes.CompanyMismatch);
            }
        }

        var bomRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<BillOfMaterials, Guid>>();
        var bom = await bomRepo.GetAsync(wo.BomId, includeDetails: true);
        if (bom.Operations.Any() || bom.RoutingId.HasValue)
        {
            var operationFound = bom.Operations.Any(o => o.OperationId == input.OperationId);
            if (!operationFound && bom.RoutingId.HasValue)
            {
                var routingRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Routing, Guid>>();
                var routing = await routingRepo.FindAsync(bom.RoutingId.Value, includeDetails: true);
                operationFound = routing?.Operations.Any(o => o.OperationId == input.OperationId) == true;
            }

            if (!operationFound)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Operation {input.OperationId} does not belong to Work Order BOM {bom.BomNumber}.");
            }
        }

        await ValidateJobCardQtyAsync(wo.Id, input.OperationId, input.ForQuantity, wo.Quantity, wo.CompanyId);

        var jc = new JobCard(GuidGenerator.Create(), input.CompanyId, input.WorkOrderId,
            input.OperationId, input.ForQuantity, input.SequenceId, CurrentTenant.Id)
        {
            WorkstationId = input.WorkstationId,
            PlannedTimeInMins = input.PlannedTimeInMins,
            BatchSplit = input.BatchSplit,
            WeightPerPiece = input.WeightPerPiece,
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

        if (input.CompanyId != default && jc.CompanyId != input.CompanyId)
        {
            throw new BusinessException(MyERPDomainErrorCodes.CompanyMismatch)
                .WithData("jobCardCompany", jc.CompanyId)
                .WithData("inputCompany", input.CompanyId);
        }

        if (input.WorkOrderId != default && input.WorkOrderId != jc.WorkOrderId)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "WorkOrderId cannot be changed on existing Job Card.");
        }

        var woRepoForUpdate = LazyServiceProvider.LazyGetRequiredService<IRepository<WorkOrder, Guid>>();
        var woForUpdate = await woRepoForUpdate.GetAsync(jc.WorkOrderId);
        if (woForUpdate.Status is WorkOrderStatus.Cancelled or WorkOrderStatus.Completed or WorkOrderStatus.Stopped or WorkOrderStatus.Closed)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("documentType", "WorkOrder")
                .WithData("status", woForUpdate.Status.ToString());
        }

        if (input.WorkstationId.HasValue && input.WorkstationId != jc.WorkstationId)
        {
            var wsRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Workstation, Guid>>();
            var ws = await wsRepo.GetAsync(input.WorkstationId.Value);
            if (ws.CompanyId != jc.CompanyId)
            {
                throw new BusinessException(MyERPDomainErrorCodes.CompanyMismatch);
            }
        }

        if (input.ForQuantity != jc.ForQuantity)
        {
            await ValidateJobCardQtyAsync(jc.WorkOrderId, jc.OperationId, input.ForQuantity, woForUpdate.Quantity, jc.CompanyId, excludeJobCardId: jc.Id);
        }

        jc.WorkstationId = input.WorkstationId;
        jc.PlannedTimeInMins = input.PlannedTimeInMins;
        jc.ForQuantity = input.ForQuantity;
        jc.SequenceId = input.SequenceId;
        jc.BatchSplit = input.BatchSplit;
        jc.WeightPerPiece = input.WeightPerPiece;

        await _repository.UpdateAsync(jc);
        return ObjectMapper.Map<JobCard, JobCardDto>(jc);
    }

    /// <summary>
    /// Per ERPNext Job Card validate_job_card_qty/get_allowed_wo_qty: the sum of ForQuantity
    /// across every non-cancelled Job Card for a given Work Order + operation must not exceed
    /// the Work Order's quantity plus the configured overproduction percentage. Without this,
    /// Job Cards could be planned (not just completed) far beyond what the Work Order calls for,
    /// with no guard until production was actually recorded at completion time.
    /// </summary>
    private async Task ValidateJobCardQtyAsync(Guid workOrderId, Guid operationId, decimal forQuantity, decimal woQuantity, Guid companyId, Guid? excludeJobCardId = null)
    {
        var settingsRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<ManufacturingSettings, Guid>>();
        var settingsQ = await settingsRepo.GetQueryableAsync();
        var settings = settingsQ.FirstOrDefault(s => s.CompanyId == companyId);
        var overproductionPct = settings?.OverproductionPercentage ?? 5m;
        var allowedQty = woQuantity + (woQuantity * overproductionPct / 100m);

        var jcQuery = await _repository.GetQueryableAsync();
        var existingQty = jcQuery
            .Where(j => j.WorkOrderId == workOrderId && j.OperationId == operationId && j.Status != JobCardStatus.Cancelled)
            .Where(j => !excludeJobCardId.HasValue || j.Id != excludeJobCardId.Value)
            .Sum(j => (decimal?)j.ForQuantity) ?? 0m;

        if (existingQty + forQuantity > allowedQty)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Job Card quantity ({existingQty + forQuantity}) exceeds the allowed quantity ({allowedQty}) for this Work Order operation. Increase the overproduction percentage in Manufacturing Settings or reduce the Job Card quantity.");
        }
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

        // Validate From Time and To Time are present on all time logs (ERPNext PR #47325 / commit 7499c25a3c)
        foreach (var row in jc.TimeLogs)
        {
            if (row.FromTime == default || row.ToTime == default || row.ToTime <= row.FromTime)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", "From Time and To Time fields are required and To Time must be after From Time.");
            }
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
                JobCardId = jc.Id,
                WeightPerPiece = jc.BatchSplit && jc.WeightPerPiece.HasValue ? jc.WeightPerPiece.Value : 0m,
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

            var seRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Inventory.Entities.StockEntry, Guid>>();
            await seRepo.InsertAsync(entry, autoSave: true);

            if (entry.WeightPerPiece > 0)
            {
                var batchSplitManager = LazyServiceProvider.LazyGetRequiredService<Inventory.DomainServices.BatchSplitManager>();
                await batchSplitManager.ProcessBatchSplitAsync(entry, jc);
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

            await seRepo.UpdateAsync(entry, autoSave: true);

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

    /// <summary>
    /// Gets raw materials required for a Job Card from the linked Work Order.
    /// Per ERPNext workstation.py / PR #58927: throws when Job Card has no raw materials to transfer.
    /// </summary>
    public async Task<System.Collections.Generic.List<JobCardRawMaterialDto>> GetRawMaterialsAsync(Guid id)
    {
        var jc = await _repository.GetAsync(id);
        var jobCardManager = LazyServiceProvider.LazyGetRequiredService<JobCardManager>();
        var woRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<WorkOrder, Guid>>();
        var matchingItems = await jobCardManager.GetRawMaterialsAsync(jc, woRepo);

        var wo = await woRepo.GetAsync(jc.WorkOrderId);
        var itemRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Inventory.Entities.Item, Guid>>();
        var whRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Inventory.Entities.Warehouse, Guid>>();
        var binService = LazyServiceProvider.LazyGetRequiredService<BinService>();

        var itemIds = matchingItems.Select(i => i.ItemId).Distinct().ToList();
        var items = await itemRepo.GetListAsync(i => itemIds.Contains(i.Id));
        var itemDict = items.ToDictionary(i => i.Id);

        var warehouseIds = matchingItems
            .Select(i => i.SourceWarehouseId ?? wo.SourceWarehouseId)
            .Where(w => w.HasValue)
            .Select(w => w!.Value)
            .Distinct()
            .ToList();
        var warehouses = await whRepo.GetListAsync(w => warehouseIds.Contains(w.Id));
        var whDict = warehouses.ToDictionary(w => w.Id);

        var result = new System.Collections.Generic.List<JobCardRawMaterialDto>();
        foreach (var reqItem in matchingItems)
        {
            var srcWhId = reqItem.SourceWarehouseId ?? wo.SourceWarehouseId;
            itemDict.TryGetValue(reqItem.ItemId, out var itemObj);
            Inventory.Entities.Warehouse? whObj = null;
            if (srcWhId.HasValue)
            {
                whDict.TryGetValue(srcWhId.Value, out whObj);
            }

            decimal stockQty = 0;
            if (srcWhId.HasValue)
            {
                var bin = await binService.GetOrCreateAsync(reqItem.ItemId, srcWhId.Value, wo.TenantId);
                stockQty = bin.ActualQty;
            }

            result.Add(new JobCardRawMaterialDto
            {
                ItemId = reqItem.ItemId,
                ItemCode = itemObj?.ItemCode ?? string.Empty,
                ItemName = itemObj?.ItemName ?? reqItem.ItemName,
                Description = itemObj?.Description ?? reqItem.ItemName,
                Uom = reqItem.StockUom,
                SourceWarehouseId = srcWhId,
                SourceWarehouseName = whObj?.Name,
                WipWarehouseId = jc.WipWarehouseId ?? wo.WipWarehouseId,
                RequiredQty = reqItem.RequiredQuantity,
                TransferredQty = reqItem.TransferredQuantity,
                StockQty = stockQty,
                IsAvailable = stockQty >= reqItem.RequiredQuantity
            });
        }

        return result;
    }

    /// <summary>
    /// Creates a Material Transfer for Manufacture Stock Entry from a Job Card.
    /// Maps to ERPNext job_card/mapper.py make_stock_entry and PR #58927.
    /// </summary>
    [Authorize(MyERPPermissions.StockEntries.Create)]
    public async Task<Inventory.StockEntryDto> CreateMaterialTransferAsync(Guid id)
    {
        var jc = await _repository.GetAsync(id);
        if (jc.Status is JobCardStatus.Cancelled or JobCardStatus.Completed)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("documentType", "JobCard")
                .WithData("status", jc.Status.ToString());
        }

        var woRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<WorkOrder, Guid>>();
        var wo = await woRepo.GetAsync(jc.WorkOrderId, includeDetails: true);
        if (wo.Status is WorkOrderStatus.Draft or WorkOrderStatus.Cancelled or WorkOrderStatus.Stopped or WorkOrderStatus.Closed)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("documentType", "WorkOrder")
                .WithData("status", wo.Status.ToString());
        }

        var jobCardManager = LazyServiceProvider.LazyGetRequiredService<JobCardManager>();
        var matchingItems = await jobCardManager.GetRawMaterialsAsync(jc, woRepo);

        if (jc.FinishedGoodItemId.HasValue && !jc.WipWarehouseId.HasValue && !wo.WipWarehouseId.HasValue)
        {
            throw new BusinessException(MyERPDomainErrorCodes.MissingWarehouse)
                .WithData("detail", "Please set the Target Warehouse in the Job Card.");
        }

        var wipWarehouseId = jc.WipWarehouseId ?? wo.WipWarehouseId;
        if (!wipWarehouseId.HasValue)
        {
            throw new BusinessException(MyERPDomainErrorCodes.MissingWarehouse)
                .WithData("field", "WIPWarehouse");
        }

        var numberGenerator = LazyServiceProvider.LazyGetRequiredService<IDocumentNumberGenerator>();
        var bomRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<BillOfMaterials, Guid>>();
        var bom = await bomRepo.FindAsync(wo.BomId, includeDetails: true);

        var entry = new Inventory.Entities.StockEntry(
            GuidGenerator.Create(), wo.CompanyId,
            StockEntryType.MaterialTransferForManufacture,
            DateTime.UtcNow.Date, CurrentTenant.Id)
        {
            WorkOrderId = wo.Id,
            JobCardId = jc.Id,
            EntryNumber = await numberGenerator.GenerateAsync("SE", wo.CompanyId),
            Notes = $"Material Transfer for Job Card {jc.Id} (WO {wo.WorkOrderNumber ?? wo.Id.ToString()})"
        };

        foreach (var reqItem in matchingItems)
        {
            var pendingQty = reqItem.RequiredQuantity - reqItem.TransferredQuantity;
            if (pendingQty <= 0) continue;

            var sourceWhId = reqItem.SourceWarehouseId ?? wo.SourceWarehouseId ?? bom?.SourceWarehouseId;
            if (!sourceWhId.HasValue)
            {
                throw new BusinessException(MyERPDomainErrorCodes.MissingWarehouse)
                    .WithData("field", "SourceWarehouse");
            }

            var bomItem = bom?.Items.FirstOrDefault(b => b.ItemId == reqItem.ItemId);
            var rate = bomItem?.Rate ?? 0m;

            entry.AddItem(
                itemId: reqItem.ItemId,
                quantity: pendingQty,
                sourceWarehouseId: sourceWhId.Value,
                targetWarehouseId: wipWarehouseId.Value,
                valuationRate: rate);
        }

        if (!entry.Items.Any())
        {
            throw new BusinessException("MyERP:10013")
                .WithData("reason", "All materials have already been transferred for this Job Card.");
        }

        var seRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Inventory.Entities.StockEntry, Guid>>();
        await seRepo.InsertAsync(entry, autoSave: true);

        var allTransferred = matchingItems.All(i => i.TransferredQuantity >= i.RequiredQuantity);
        var anyTransferred = matchingItems.Any(i => i.TransferredQuantity > 0);
        jc.UpdateTransferStatus(allTransferred, anyTransferred);
        await _repository.UpdateAsync(jc);

        return ObjectMapper.Map<Inventory.Entities.StockEntry, Inventory.StockEntryDto>(entry);
    }
}

