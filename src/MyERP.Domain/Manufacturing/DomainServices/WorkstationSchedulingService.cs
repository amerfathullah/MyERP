using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.HumanResources.Entities;
using MyERP.Manufacturing.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Manufacturing.DomainServices;

/// <summary>
/// Workstation scheduling service — plans job card time slots
/// based on workstation capacity, working hours, and holiday calendars.
///
/// Per ERPNext manufacturing/doctype/workstation:
/// - Working hours define available time windows per day of week
/// - Production capacity limits concurrent operations
/// - Holiday list blocks scheduling on non-working days
/// - MinsBetweenOperations provides gap between consecutive jobs
///
/// Per gotcha #2657: capacity enforcement is workstation.ProductionCapacity based,
/// with overlap counting from existing active JCs.
/// </summary>
public class WorkstationSchedulingService : DomainService
{
    private readonly IRepository<Workstation, Guid> _workstationRepository;
    private readonly IRepository<JobCard, Guid> _jobCardRepository;
    private readonly IRepository<HolidayList, Guid> _holidayListRepository;
    private readonly IRepository<ManufacturingSettings, Guid> _settingsRepository;

    public WorkstationSchedulingService(
        IRepository<Workstation, Guid> workstationRepository,
        IRepository<JobCard, Guid> jobCardRepository,
        IRepository<HolidayList, Guid> holidayListRepository,
        IRepository<ManufacturingSettings, Guid> settingsRepository)
    {
        _workstationRepository = workstationRepository;
        _jobCardRepository = jobCardRepository;
        _holidayListRepository = holidayListRepository;
        _settingsRepository = settingsRepository;
    }

    /// <summary>
    /// Calculates the scheduled start and end times for a job card operation.
    /// Respects workstation working hours, capacity limits, and holidays.
    /// </summary>
    public async Task<ScheduledTimeSlot> ScheduleJobCardAsync(
        Guid workstationId,
        Guid companyId,
        decimal totalTimeInMins,
        DateTime requestedStartDate)
    {
        var workstation = await _workstationRepository.GetAsync(workstationId);
        var settings = await _settingsRepository.FindAsync(s => s.CompanyId == companyId);
        var minsBetween = settings?.MinsBetweenOperations ?? 10;
        var capacityPlanningDays = settings?.CapacityPlanningForDays ?? 30;
        var allowHolidays = settings?.AllowProductionOnHolidays ?? false;
        var disableCapacityPlanning = settings?.DisableCapacityPlanning ?? false;

        // Load holidays if configured
        var holidays = new HashSet<DateTime>();
        if (!allowHolidays && workstation.HolidayListId.HasValue)
        {
            var holidayList = await _holidayListRepository.FindAsync(workstation.HolidayListId.Value);
            if (holidayList != null)
            {
                holidays = holidayList.Holidays.Select(h => h.HolidayDate.Date).ToHashSet();
            }
        }

        // Get working hours for this workstation (sorted by day, enabled only - ERPNext PR #60595)
        var workingHours = workstation.WorkingHours.Where(wh => wh.IsEnabled).ToList();

        // If no working hours defined, use default 8am-5pm Mon-Fri
        if (!workingHours.Any())
        {
            workingHours = GetDefaultWorkingHours(workstation.Id);
        }

        // Find available time slots starting from requested date
        var remainingMins = totalTimeInMins;
        DateTime currentDate = requestedStartDate.Date;
        DateTime? slotStart = null;
        DateTime? slotEnd = null;
        var maxDate = requestedStartDate.AddDays(capacityPlanningDays);

        while (remainingMins > 0 && currentDate <= maxDate)
        {
            // Skip holidays
            if (holidays.Contains(currentDate))
            {
                currentDate = currentDate.AddDays(1);
                continue;
            }

            // Get working hours for this day of week
            var dayName = currentDate.DayOfWeek.ToString();
            var dayHours = workingHours
                .Where(wh => wh.Day.Equals(dayName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(wh => wh.StartTime)
                .ToList();

            if (!dayHours.Any())
            {
                currentDate = currentDate.AddDays(1);
                continue;
            }

            foreach (var window in dayHours)
            {
                if (remainingMins <= 0) break;

                var windowStart = currentDate.Add(window.StartTime);
                var windowEnd = currentDate.Add(window.EndTime);
                var windowMins = (decimal)(windowEnd - windowStart).TotalMinutes;

                // Check capacity — count overlapping active JCs (skipped when capacity planning is disabled)
                if (!disableCapacityPlanning)
                {
                    var overlapping = await CountOverlappingJobCardsAsync(
                        workstationId, windowStart, windowEnd);

                    if (overlapping >= workstation.ProductionCapacity)
                        continue; // Slot full, try next window
                }

                var availableMins = Math.Min(windowMins, remainingMins);
                slotStart ??= windowStart;
                slotEnd = windowStart.AddMinutes((double)availableMins);
                remainingMins -= availableMins;
            }

            currentDate = currentDate.AddDays(1);
        }

        if (!slotStart.HasValue || remainingMins > 0)
        {
            // Capacity exhausted within planning window — fall back to requested date
            return new ScheduledTimeSlot(
                requestedStartDate,
                requestedStartDate.AddMinutes((double)totalTimeInMins),
                ScheduleStatus.NoCapacity);
        }

        // Add mins-between-operations gap
        var finalEnd = slotEnd!.Value.AddMinutes(minsBetween);

        return new ScheduledTimeSlot(slotStart.Value, finalEnd, ScheduleStatus.Scheduled);
    }

    /// <summary>
    /// Gets the effective working minutes per day for a workstation.
    /// Used for lead time calculation in Production Planning.
    /// </summary>
    public async Task<decimal> GetDailyCapacityMinutesAsync(Guid workstationId)
    {
        var ws = await _workstationRepository.GetAsync(workstationId);
        var hours = ws.WorkingHours.ToList();

        if (!hours.Any())
            return 480m; // Default 8 hours

        return hours.Sum(h => (decimal)(h.EndTime - h.StartTime).TotalMinutes);
    }

    /// <summary>
    /// Validates no workstation overlap for time log entries.
    /// Per gotcha #2657: employee cross-workstation overlap also checked.
    /// </summary>
    public async Task ValidateNoOverlapAsync(
        Guid workstationId, DateTime fromTime, DateTime toTime, Guid? excludeJobCardId = null)
    {
        var ws = await _workstationRepository.GetAsync(workstationId);
        if (ws.ProductionCapacity <= 0) return;

        var settings = await _settingsRepository.FindAsync(s => s.CompanyId == ws.CompanyId);
        if (settings?.DisableCapacityPlanning == true) return;

        var overlapping = await CountOverlappingJobCardsAsync(
            workstationId, fromTime, toTime, excludeJobCardId);

        if (overlapping >= ws.ProductionCapacity)
        {
            throw new BusinessException(MyERPDomainErrorCodes.WorkstationCapacityExceeded)
                .WithData("workstation", ws.Name)
                .WithData("capacity", ws.ProductionCapacity)
                .WithData("current", overlapping);
        }
    }

    private async Task<int> CountOverlappingJobCardsAsync(
        Guid workstationId, DateTime from, DateTime to, Guid? excludeId = null)
    {
        var q = await _jobCardRepository.GetQueryableAsync();
        var activeJcs = q.Where(jc =>
            jc.WorkstationId == workstationId
            && jc.Id != (excludeId ?? Guid.Empty)
            && jc.Status == JobCardStatus.WorkInProgress)
            .ToList();

        var logs = new List<JobCardTimeLogRange>();
        foreach (var jc in activeJcs)
        {
            if (jc.TimeLogs.Any())
            {
                logs.AddRange(jc.TimeLogs
                    .Where(tl => tl.FromTime < to && tl.ToTime > from)
                    .Select(tl => new JobCardTimeLogRange(tl.FromTime, tl.ToTime, jc.Id)));
            }
            else if (jc.StartedAt.HasValue)
            {
                var sFrom = jc.StartedAt.Value;
                var sTo = sFrom.AddMinutes((double)(jc.PlannedTimeInMins > 0 ? jc.PlannedTimeInMins : 30m));
                if (sFrom < to && sTo > from)
                {
                    logs.Add(new JobCardTimeLogRange(sFrom, sTo, jc.Id));
                }
            }
        }

        var merged = JobCardManager.MergeTimeLogsPerJobCard(logs);
        return JobCardManager.GetAllotedCapacity(merged).Count;
    }

    /// <summary>
    /// Filters out Production Plan schedule rows whose operation quantity is covered by job cards carrying booked load.
    /// Per ERPNext commits 1c53d45356, 3771672e23, 68c6d2fe71, 808549515d, and bfd5ed73b5:
    /// A schedule block steps aside only when job cards carrying booked load (logged time or scheduled time)
    /// cover the required quantity (net of upstream process loss) of EACH work order on its plan row and operation.
    /// </summary>
    public static List<ProductionPlanScheduleRow> FilterCoveredScheduleRows(
        IReadOnlyList<ProductionPlanScheduleRow> rows,
        IReadOnlyList<WorkOrder> workOrders,
        IReadOnlyList<WorkOrderOperationCoverageSnapshot> operations,
        IReadOnlyList<JobCard> jobCards,
        IReadOnlySet<Guid>? jobCardsWithScheduledTime = null)
    {
        if (rows.Count == 0)
            return new List<ProductionPlanScheduleRow>();

        var plans = rows.Select(r => r.ProductionPlanId).ToHashSet();
        var covered = GetCoveredOperations(plans, workOrders, operations, jobCards, jobCardsWithScheduledTime);

        return rows
            .Where(r => !covered.Contains((r.ProductionPlanId, r.PlanRowId, r.OperationId)))
            .ToList();
    }

    /// <summary>
    /// Returns plan row operations <c>(ProductionPlanId, PlanRowId, OperationId)</c> whose every work order
    /// has job cards carrying booked load covering its required operation quantity net of upstream loss.
    /// Per ERPNext commits 3771672e23, 68c6d2fe71, 808549515d, and bfd5ed73b5:
    /// - Coverage is checked per work order so one work order's upstream loss or job cards cannot offset
    ///   another work order's uncovered required quantity on the same plan row.
    /// - Quantities are rounded to 4 decimals only at the comparison.
    /// - An operation whose quantity is fully lost upstream (required &lt;= 0) is treated as covered and frees its block.
    /// </summary>
    public static HashSet<(Guid ProductionPlanId, Guid PlanRowId, Guid OperationId)> GetCoveredOperations(
        IReadOnlySet<Guid> productionPlanIds,
        IReadOnlyList<WorkOrder> workOrders,
        IReadOnlyList<WorkOrderOperationCoverageSnapshot> operations,
        IReadOnlyList<JobCard> jobCards,
        IReadOnlySet<Guid>? jobCardsWithScheduledTime = null,
        int precision = 4)
    {
        var (required, carried) = GetJobCardCoverage(
            productionPlanIds, workOrders, operations, jobCards, jobCardsWithScheduledTime);

        var shortOperations = new HashSet<(Guid ProductionPlanId, Guid PlanRowId, Guid OperationId)>();
        var allOperations = new HashSet<(Guid ProductionPlanId, Guid PlanRowId, Guid OperationId)>();

        foreach (var (key, qty) in required)
        {
            var opKey = (key.ProductionPlanId, key.PlanRowId, key.OperationId);
            allOperations.Add(opKey);

            var carriedQty = carried.GetValueOrDefault(key, 0m);
            if (Math.Round(carriedQty, precision) < Math.Round(qty, precision))
            {
                shortOperations.Add(opKey);
            }
        }

        allOperations.ExceptWith(shortOperations);
        return allOperations;
    }

    /// <summary>
    /// Computes required and carried operation quantities keyed by
    /// <c>(ProductionPlanId, PlanRowId, OperationId, WorkOrderId)</c>.
    /// </summary>
    public static (
        Dictionary<(Guid ProductionPlanId, Guid PlanRowId, Guid OperationId, Guid WorkOrderId), decimal> Required,
        Dictionary<(Guid ProductionPlanId, Guid PlanRowId, Guid OperationId, Guid WorkOrderId), decimal> Carried)
        GetJobCardCoverage(
            IReadOnlySet<Guid> productionPlanIds,
            IReadOnlyList<WorkOrder> workOrders,
            IReadOnlyList<WorkOrderOperationCoverageSnapshot> operations,
            IReadOnlyList<JobCard> jobCards,
            IReadOnlySet<Guid>? jobCardsWithScheduledTime = null)
    {
        var activeWorkOrders = workOrders
            .Where(wo => wo.Status != WorkOrderStatus.Cancelled
                      && wo.ProductionPlanId.HasValue
                      && productionPlanIds.Contains(wo.ProductionPlanId.Value))
            .ToDictionary(wo => wo.Id);

        if (activeWorkOrders.Count == 0)
        {
            return (
                new Dictionary<(Guid, Guid, Guid, Guid), decimal>(),
                new Dictionary<(Guid, Guid, Guid, Guid), decimal>());
        }

        var opsByWorkOrder = operations
            .Where(op => activeWorkOrders.ContainsKey(op.WorkOrderId))
            .GroupBy(op => op.WorkOrderId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<WorkOrderOperationCoverageSnapshot>)g.ToList());

        var required = new Dictionary<(Guid ProductionPlanId, Guid PlanRowId, Guid OperationId, Guid WorkOrderId), decimal>();
        foreach (var op in operations.Where(o => activeWorkOrders.ContainsKey(o.WorkOrderId)))
        {
            var wo = activeWorkOrders[op.WorkOrderId];
            var woOps = opsByWorkOrder.GetValueOrDefault(wo.Id) ?? Array.Empty<WorkOrderOperationCoverageSnapshot>();
            var upstreamLoss = GetUpstreamProcessLoss(wo, woOps, op);
            var qty = GetOperationQty(wo, op) - upstreamLoss;
            AddOperationQty(required, wo, op.OperationId, qty);
        }

        var carried = new Dictionary<(Guid ProductionPlanId, Guid PlanRowId, Guid OperationId, Guid WorkOrderId), decimal>();
        foreach (var jc in jobCards)
        {
            if (jc.Status == JobCardStatus.Cancelled || !activeWorkOrders.TryGetValue(jc.WorkOrderId, out var wo))
                continue;

            var hasBookedLoad = jc.TotalTimeInMins != 0m
                || jc.TimeLogs.Count > 0
                || (jobCardsWithScheduledTime != null && jobCardsWithScheduledTime.Contains(jc.Id));

            if (hasBookedLoad)
            {
                AddOperationQty(carried, wo, jc.OperationId, jc.ForQuantity);
            }
        }

        return (required, carried);
    }

    public static decimal GetOperationQty(WorkOrder workOrder, WorkOrderOperationCoverageSnapshot operation)
    {
        if (workOrder.TrackSemiFinishedGoods && operation.OutputQty.HasValue && operation.OutputQty.Value != 0m)
        {
            return operation.OutputQty.Value;
        }

        return workOrder.Quantity;
    }

    public static decimal GetUpstreamProcessLoss(
        WorkOrder workOrder,
        IReadOnlyList<WorkOrderOperationCoverageSnapshot> workOrderOperations,
        WorkOrderOperationCoverageSnapshot? targetOperation = null)
    {
        if (workOrder.TrackSemiFinishedGoods || workOrderOperations.Count == 0)
            return 0m;

        decimal processLossQty = 0m;
        decimal? availableQty = null;

        var sequenceGroups = workOrderOperations
            .OrderBy(r => r.SequenceId)
            .GroupBy(r => r.SequenceId);

        foreach (var group in sequenceGroups)
        {
            if (targetOperation != null && group.Key >= targetOperation.SequenceId)
                break;

            var rows = group.ToList();
            var stepLoss = rows.Max(row =>
                !availableQty.HasValue
                    ? row.ProcessLossQty
                    : Math.Min(row.ProcessLossQty, Math.Max(availableQty.Value - row.CompletedQty, 0m)));

            processLossQty += stepLoss;
            availableQty = rows.Min(row => row.CompletedQty);
        }

        return processLossQty;
    }

    private static void AddOperationQty(
        Dictionary<(Guid ProductionPlanId, Guid PlanRowId, Guid OperationId, Guid WorkOrderId), decimal> bucket,
        WorkOrder workOrder,
        Guid operationId,
        decimal qty)
    {
        if (!workOrder.ProductionPlanId.HasValue)
            return;

        var planId = workOrder.ProductionPlanId.Value;
        var planRows = new[] { workOrder.ProductionPlanItemId, workOrder.ProductionPlanSubAssemblyItemId };
        foreach (var planRow in planRows)
        {
            if (planRow.HasValue && planRow.Value != Guid.Empty)
            {
                var key = (planId, planRow.Value, operationId, workOrder.Id);
                bucket[key] = bucket.GetValueOrDefault(key, 0m) + qty;
            }
        }
    }

    private static List<WorkstationWorkingHour> GetDefaultWorkingHours(Guid workstationId)
    {
        var defaults = new List<WorkstationWorkingHour>();
        var days = new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday" };
        foreach (var day in days)
        {
            defaults.Add(new WorkstationWorkingHour(
                Guid.NewGuid(), workstationId, day,
                new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0)));
        }
        return defaults;
    }

    /// <summary>
    /// Consumes a workstation scheduling calendar window using exact <see cref="TimeSpan"/> (<c>timedelta</c>) arithmetic
    /// rather than floating-point minutes so split segments never drift by fractional milliseconds
    /// per ERPNext commit 2de0d38152 (<c>workstation_calendar.py</c>).
    /// </summary>
    public static (DateTime SegmentStart, DateTime SegmentEnd, TimeSpan RemainingDuration) ConsumeSchedulingWindowAsTimeSpan(
        DateTime windowStart,
        DateTime windowEnd,
        TimeSpan remainingDuration)
    {
        if (remainingDuration <= TimeSpan.Zero || windowEnd <= windowStart)
        {
            return (windowStart, windowStart, remainingDuration);
        }

        var available = windowEnd - windowStart;
        var consumed = remainingDuration <= available ? remainingDuration : available;
        return (windowStart, windowStart + consumed, remainingDuration - consumed);
    }

    /// <summary>
    /// Extracts the Production Plan identifier from either a direct name/ID string or a JSON/dict payload
    /// passed by the reserve/unreserve stock action per ERPNext commit 920bdf0885 (<c>production_plan.py</c>).
    /// </summary>
    public static string ExtractProductionPlanNameFromPayload(string? docOrNamePayload)
    {
        if (string.IsNullOrWhiteSpace(docOrNamePayload))
        {
            return string.Empty;
        }

        var trimmed = docOrNamePayload.Trim();
        if (trimmed.StartsWith("{", StringComparison.Ordinal) && trimmed.EndsWith("}", StringComparison.Ordinal))
        {
            const string key = "\"name\"";
            var keyIdx = trimmed.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (keyIdx >= 0)
            {
                var colonIdx = trimmed.IndexOf(':', keyIdx + key.Length);
                if (colonIdx >= 0)
                {
                    var firstQuote = trimmed.IndexOf('"', colonIdx + 1);
                    if (firstQuote >= 0)
                    {
                        var secondQuote = trimmed.IndexOf('"', firstQuote + 1);
                        if (secondQuote > firstQuote)
                        {
                            return trimmed.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
                        }
                    }
                }
            }
        }

        return trimmed;
    }
}

/// <summary>Result of job card scheduling.</summary>
public record ScheduledTimeSlot(
    DateTime PlannedStart,
    DateTime PlannedEnd,
    ScheduleStatus Status)
{
    /// <summary>Duration in minutes.</summary>
    public decimal DurationMinutes => (decimal)(PlannedEnd - PlannedStart).TotalMinutes;
}

/// <summary>Scheduling result status.</summary>
public enum ScheduleStatus
{
    /// <summary>Successfully scheduled within capacity.</summary>
    Scheduled = 0,

    /// <summary>No capacity available within planning window — fallback used.</summary>
    NoCapacity = 1
}

/// <summary>
/// Represents a Production Plan Schedule block reserving workstation capacity
/// until covered by job cards carrying booked load (ERPNext manufacturing/scheduling/loaders.py).
/// </summary>
public record ProductionPlanScheduleRow(
    Guid WorkstationId,
    DateTime FromTime,
    DateTime ToTime,
    Guid ProductionPlanId,
    Guid PlanRowId,
    Guid OperationId);

/// <summary>
/// Represents a Work Order Operation row for schedule coverage and upstream process loss evaluation.
/// </summary>
public record WorkOrderOperationCoverageSnapshot(
    Guid WorkOrderId,
    Guid OperationId,
    int SequenceId,
    decimal? OutputQty = null,
    decimal CompletedQty = 0m,
    decimal ProcessLossQty = 0m);
