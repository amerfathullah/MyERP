using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Core;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Manufacturing;

/// <summary>
/// Work Order Summary report — aggregates Work Orders with status, age, actual vs planned quantities,
/// lead times, alternative finished goods, and chart metrics.
/// ERPNext equivalent: manufacturing/report/work_order_summary/work_order_summary.py
/// </summary>
[Authorize(MyERPPermissions.Manufacturing.Default)]
public class WorkOrderSummaryAppService : ApplicationService, IWorkOrderSummaryAppService
{
    private readonly IRepository<WorkOrder, Guid> _workOrderRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<SalesOrder, Guid> _salesOrderRepository;
    private readonly IRepository<ManufacturingSettings, Guid> _manufacturingSettingsRepository;
    private readonly IRepository<StockEntry, Guid> _stockEntryRepository;

    public WorkOrderSummaryAppService(
        IRepository<WorkOrder, Guid> workOrderRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<SalesOrder, Guid> salesOrderRepository,
        IRepository<ManufacturingSettings, Guid> manufacturingSettingsRepository,
        IRepository<StockEntry, Guid> stockEntryRepository)
    {
        _workOrderRepository = workOrderRepository;
        _itemRepository = itemRepository;
        _salesOrderRepository = salesOrderRepository;
        _manufacturingSettingsRepository = manufacturingSettingsRepository;
        _stockEntryRepository = stockEntryRepository;
    }

    public async Task<WorkOrderSummaryDto> GetSummaryAsync(WorkOrderSummaryRequestDto input)
    {
        var fromDate = input.FromDate.Date;
        var toDate = input.ToDate.Date;

        var woQuery = await _workOrderRepository.GetQueryableAsync();
        var query = woQuery
            .Where(w => w.CompanyId == input.CompanyId)
            .Where(w => w.Status != WorkOrderStatus.Cancelled);

        if (input.Status.HasValue)
        {
            query = query.Where(w => w.Status == input.Status.Value);
        }

        if (input.WorkOrderIds != null && input.WorkOrderIds.Count > 0)
        {
            query = query.Where(w => input.WorkOrderIds.Contains(w.Id));
        }

        if (input.ProductionItemIds != null && input.ProductionItemIds.Count > 0)
        {
            query = query.Where(w => input.ProductionItemIds.Contains(w.ItemId));
        }

        if (input.SalesOrderIds != null && input.SalesOrderIds.Count > 0)
        {
            query = query.Where(w => w.SalesOrderId.HasValue && input.SalesOrderIds.Contains(w.SalesOrderId.Value));
        }

        if (string.Equals(input.BasedOn, "Planned Date", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(w =>
                w.PlannedStartDate.HasValue && w.PlannedStartDate.Value.Date >= fromDate &&
                w.PlannedEndDate.HasValue && w.PlannedEndDate.Value.Date <= toDate);
        }
        else if (string.Equals(input.BasedOn, "Actual Date", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(w =>
                w.ActualStartDate.HasValue && w.ActualStartDate.Value.Date >= fromDate &&
                w.ActualEndDate.HasValue && w.ActualEndDate.Value.Date <= toDate);
        }
        else
        {
            query = query.Where(w => w.CreationTime.Date >= fromDate && w.CreationTime.Date <= toDate);
        }

        var workOrders = query.OrderBy(w => w.PlannedStartDate ?? w.CreationTime).ToList();

        // Calculate age and filter by min age
        var filteredWorkOrders = new List<(WorkOrder WorkOrder, int Age)>();
        var today = DateTime.UtcNow.Date;

        foreach (var wo in workOrders)
        {
            var startDate = wo.ActualStartDate ?? wo.PlannedStartDate;
            int age = 0;
            if (wo.Status != WorkOrderStatus.Completed && startDate.HasValue)
            {
                age = Math.Max(0, (today - startDate.Value.Date).Days);
            }

            if (age >= input.Age)
            {
                filteredWorkOrders.Add((wo, age));
            }
        }

        if (filteredWorkOrders.Count == 0)
        {
            var emptyChart = BuildChartData(new List<WorkOrderSummaryRowDto>(), input, fromDate, toDate);
            return new WorkOrderSummaryDto
            {
                CompanyId = input.CompanyId,
                FromDate = input.FromDate,
                ToDate = input.ToDate,
                Rows = new(),
                Chart = emptyChart,
                TotalWorkOrders = 0,
                TotalProduceQty = 0,
                TotalProducedQty = 0
            };
        }

        // Check if Alternative Finished Goods conversion is enabled
        var mfgSettings = await _manufacturingSettingsRepository.FindAsync(s => s.CompanyId == input.CompanyId);
        bool showActualFinishedGoods = mfgSettings?.AllowAlternativeFinishedGoods ?? false;

        // Fetch FG conversions if enabled
        var fgConversionMap = new Dictionary<Guid, string>();
        if (showActualFinishedGoods)
        {
            var candidateWoIds = filteredWorkOrders.Where(x => x.WorkOrder.ProducedQuantity > 0).Select(x => x.WorkOrder.Id).ToList();
            if (candidateWoIds.Count > 0)
            {
                var seQuery = await _stockEntryRepository.WithDetailsAsync(se => se.Items);
                var conversionEntries = seQuery
                    .Where(se => se.CompanyId == input.CompanyId &&
                                 se.Status == DocumentStatus.Submitted &&
                                 se.IsFgConversion &&
                                 se.WorkOrderId.HasValue &&
                                 candidateWoIds.Contains(se.WorkOrderId.Value))
                    .ToList();

                if (conversionEntries.Count > 0)
                {
                    // Map items for lookup
                    var convItemIds = conversionEntries.SelectMany(se => se.Items.Select(i => i.ItemId)).Distinct().ToList();
                    var convItems = await _itemRepository.GetListAsync(i => convItemIds.Contains(i.Id));
                    var convItemLookup = convItems.ToDictionary(i => i.Id, i => i.ItemCode);

                    var groupedEntries = conversionEntries.GroupBy(se => se.WorkOrderId!.Value);
                    foreach (var group in groupedEntries)
                    {
                        var woId = group.Key;
                        var wo = filteredWorkOrders.First(x => x.WorkOrder.Id == woId).WorkOrder;

                        decimal convertedQty = 0;
                        var altFg = new Dictionary<string, decimal>();

                        foreach (var se in group)
                        {
                            foreach (var item in se.Items)
                            {
                                var itemCode = convItemLookup.TryGetValue(item.ItemId, out var c) ? c : item.ItemId.ToString()[..8];
                                if (item.IsFinishedItem)
                                {
                                    altFg[itemCode] = altFg.GetValueOrDefault(itemCode) + item.Quantity;
                                }
                                else if (item.ItemId == wo.ItemId)
                                {
                                    convertedQty += item.Quantity;
                                }
                            }
                        }

                        if (altFg.Count > 0)
                        {
                            var outputs = new List<string>();
                            var remainingOriginalQty = wo.ProducedQuantity - convertedQty;
                            if (remainingOriginalQty > 0)
                            {
                                var origCode = convItemLookup.TryGetValue(wo.ItemId, out var oc) ? oc : wo.ItemId.ToString()[..8];
                                outputs.Add($"{origCode}: {remainingOriginalQty:0.##}");
                            }

                            foreach (var (code, qty) in altFg.OrderBy(kv => kv.Key))
                            {
                                if (qty > 0)
                                {
                                    outputs.Add($"{code}: {qty:0.##}");
                                }
                            }

                            fgConversionMap[woId] = string.Join(", ", outputs);
                        }
                    }
                }
            }
        }

        // Lookup items and sales orders
        var itemIds = filteredWorkOrders.Select(x => x.WorkOrder.ItemId).Distinct().ToList();
        var items = await _itemRepository.GetListAsync(i => itemIds.Contains(i.Id));
        var itemLookup = items.ToDictionary(i => i.Id);

        var soIds = filteredWorkOrders.Where(x => x.WorkOrder.SalesOrderId.HasValue).Select(x => x.WorkOrder.SalesOrderId!.Value).Distinct().ToList();
        var salesOrders = await _salesOrderRepository.GetListAsync(so => soIds.Contains(so.Id));
        var soLookup = salesOrders.ToDictionary(so => so.Id);

        var rows = new List<WorkOrderSummaryRowDto>();
        foreach (var (wo, age) in filteredWorkOrders)
        {
            itemLookup.TryGetValue(wo.ItemId, out var item);
            SalesOrder? so = null;
            if (wo.SalesOrderId.HasValue)
            {
                soLookup.TryGetValue(wo.SalesOrderId.Value, out so);
            }

            decimal? leadTime = wo.LeadTime;
            if (!leadTime.HasValue && wo.Status == WorkOrderStatus.Completed && wo.ActualStartDate.HasValue && wo.ActualEndDate.HasValue)
            {
                leadTime = (decimal)(wo.ActualEndDate.Value - wo.ActualStartDate.Value).TotalMinutes;
            }

            fgConversionMap.TryGetValue(wo.Id, out var actualFg);

            rows.Add(new WorkOrderSummaryRowDto
            {
                Id = wo.Id,
                WorkOrderNumber = wo.WorkOrderNumber,
                Status = wo.Status.ToString(),
                StatusCode = wo.Status,
                ProductionItemId = wo.ItemId,
                ProductionItemCode = item?.ItemCode ?? wo.ItemId.ToString()[..8],
                ProductionItemName = item?.ItemName ?? "Unknown Item",
                Qty = wo.Quantity,
                ProducedQty = wo.ProducedQuantity,
                ActualFinishedGoods = actualFg,
                SalesOrderId = wo.SalesOrderId,
                SalesOrderNumber = so?.OrderNumber,
                CreationTime = wo.CreationTime,
                PlannedStartDate = wo.PlannedStartDate,
                PlannedEndDate = wo.PlannedEndDate,
                ActualStartDate = wo.ActualStartDate,
                ActualEndDate = wo.ActualEndDate,
                Age = age,
                LeadTime = leadTime
            });
        }

        var chart = BuildChartData(rows, input, fromDate, toDate);

        return new WorkOrderSummaryDto
        {
            CompanyId = input.CompanyId,
            FromDate = input.FromDate,
            ToDate = input.ToDate,
            Rows = rows,
            Chart = chart,
            TotalWorkOrders = rows.Count,
            TotalProduceQty = rows.Sum(r => r.Qty),
            TotalProducedQty = rows.Sum(r => r.ProducedQty)
        };
    }

    private static WorkOrderSummaryChartDto BuildChartData(
        List<WorkOrderSummaryRowDto> rows,
        WorkOrderSummaryRequestDto input,
        DateTime fromDate,
        DateTime toDate)
    {
        if (string.Equals(input.ChartsBasedOn, "Status", StringComparison.OrdinalIgnoreCase))
        {
            var statuses = Enum.GetValues<WorkOrderStatus>();
            var labels = statuses.Select(s => s.ToString()).ToList();
            var counts = statuses.Select(s => (decimal)rows.Count(r => r.StatusCode == s)).ToList();

            return new WorkOrderSummaryChartDto
            {
                ChartType = "donut",
                Labels = labels,
                Datasets = new List<WorkOrderChartDatasetDto>
                {
                    new() { Name = "Status", Values = counts }
                }
            };
        }

        if (string.Equals(input.ChartsBasedOn, "Age", StringComparison.OrdinalIgnoreCase))
        {
            var labels = new List<string> { "0-30 Days", "30-60 Days", "60-90 Days", "90 Above" };
            decimal bucket1 = 0, bucket2 = 0, bucket3 = 0, bucket4 = 0;

            foreach (var r in rows)
            {
                if (r.Age > 0 && r.Age <= 30)
                {
                    bucket1++;
                }
                else if (r.Age > 30 && r.Age <= 60)
                {
                    bucket2++;
                }
                else if (r.Age > 60 && r.Age <= 90)
                {
                    bucket3++;
                }
                else
                {
                    bucket4++;
                }
            }

            return new WorkOrderSummaryChartDto
            {
                ChartType = "donut",
                Labels = labels,
                Datasets = new List<WorkOrderChartDatasetDto>
                {
                    new() { Name = "Age", Values = new List<decimal> { bucket1, bucket2, bucket3, bucket4 } }
                }
            };
        }

        // Default: Quantity chart (stacked bar monthly)
        var monthLabels = new List<string>();
        var pendingValues = new List<decimal>();
        var completedValues = new List<decimal>();

        var cur = new DateTime(fromDate.Year, fromDate.Month, 1);
        var endMonth = new DateTime(toDate.Year, toDate.Month, 1);

        while (cur <= endMonth)
        {
            var rangeStart = cur < fromDate ? fromDate : cur;
            var daysInMonth = DateTime.DaysInMonth(cur.Year, cur.Month);
            var monthLastDay = new DateTime(cur.Year, cur.Month, daysInMonth);
            var rangeEnd = monthLastDay > toDate ? toDate : monthLastDay;

            var label = cur.ToString("MMM yyyy");
            monthLabels.Add(label);

            var inPeriodRows = rows.Where(r =>
            {
                var dt = (r.PlannedStartDate ?? r.CreationTime).Date;
                return dt >= rangeStart && dt <= rangeEnd;
            }).ToList();

            decimal pending = inPeriodRows.Sum(r => Math.Max(0, r.Qty - r.ProducedQty));
            decimal completed = inPeriodRows.Sum(r => r.ProducedQty);

            pendingValues.Add(pending);
            completedValues.Add(completed);

            cur = cur.AddMonths(1);
        }

        return new WorkOrderSummaryChartDto
        {
            ChartType = "bar",
            Labels = monthLabels,
            Datasets = new List<WorkOrderChartDatasetDto>
            {
                new() { Name = "Pending", Values = pendingValues },
                new() { Name = "Completed", Values = completedValues }
            }
        };
    }
}
