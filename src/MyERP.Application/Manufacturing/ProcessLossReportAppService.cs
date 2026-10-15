using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Core;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Manufacturing;

/// <summary>
/// Process Loss Report — tracks process loss quantities and valuation across Work Orders and Stock Entries.
/// ERPNext equivalent: manufacturing/report/process_loss_report/process_loss_report.py
/// 
/// Key calculations per ERPNext PR #60680 / commits ff394e3ab4d, 14d424e4bc1:
/// - Only includes Work Orders where total process_loss_qty > 0.
/// - Processed Qty = Output Qty (all finished items in entries) + Process Loss Qty.
/// - Process Loss Value = Process Loss Qty × Total FG Value / Processed Qty.
/// - Manufactured Qty counts the primary WO production item; Output Qty counts all finished item rows.
/// </summary>
[Authorize(MyERPPermissions.Manufacturing.Default)]
public class ProcessLossReportAppService : ApplicationService, IProcessLossReportAppService
{
    private readonly IRepository<StockEntry, Guid> _stockEntryRepository;
    private readonly IRepository<WorkOrder, Guid> _workOrderRepository;
    private readonly IRepository<Item, Guid> _itemRepository;

    public ProcessLossReportAppService(
        IRepository<StockEntry, Guid> stockEntryRepository,
        IRepository<WorkOrder, Guid> workOrderRepository,
        IRepository<Item, Guid> itemRepository)
    {
        _stockEntryRepository = stockEntryRepository;
        _workOrderRepository = workOrderRepository;
        _itemRepository = itemRepository;
    }

    public async Task<ProcessLossReportDto> GetReportAsync(ProcessLossReportRequestDto input)
    {
        var seQuery = await _stockEntryRepository.WithDetailsAsync(se => se.Items);
        var entries = seQuery
            .Where(se => se.CompanyId == input.CompanyId
                && se.EntryType == StockEntryType.Manufacture
                && (se.Status == DocumentStatus.Submitted || se.Status == DocumentStatus.Posted)
                && se.PostingDate >= input.FromDate
                && se.PostingDate <= input.ToDate
                && se.WorkOrderId.HasValue)
            .ToList();

        if (input.WorkOrderId.HasValue)
        {
            entries = entries.Where(se => se.WorkOrderId == input.WorkOrderId.Value).ToList();
        }

        if (!entries.Any())
        {
            return new ProcessLossReportDto
            {
                CompanyId = input.CompanyId,
                FromDate = input.FromDate,
                ToDate = input.ToDate,
                Rows = new()
            };
        }

        // Group entries by Work Order
        var entriesByWorkOrder = entries
            .GroupBy(se => se.WorkOrderId!.Value)
            .Where(g => g.Sum(se => se.ProcessLossQty) > 0) // having sum(process_loss_qty) > 0
            .ToList();

        if (!entriesByWorkOrder.Any())
        {
            return new ProcessLossReportDto
            {
                CompanyId = input.CompanyId,
                FromDate = input.FromDate,
                ToDate = input.ToDate,
                Rows = new()
            };
        }

        var workOrderIds = entriesByWorkOrder.Select(g => g.Key).ToList();
        var workOrders = await _workOrderRepository.GetListAsync(wo => workOrderIds.Contains(wo.Id));
        var workOrderLookup = workOrders.ToDictionary(wo => wo.Id);

        var itemIds = workOrders.Select(wo => wo.ItemId).Distinct().ToList();
        var items = await _itemRepository.GetListAsync(i => itemIds.Contains(i.Id));
        var itemLookup = items.ToDictionary(i => i.Id);

        var rows = new List<ProcessLossReportRowDto>();

        foreach (var group in entriesByWorkOrder)
        {
            if (!workOrderLookup.TryGetValue(group.Key, out var wo))
                continue;

            if (input.ItemId.HasValue && wo.ItemId != input.ItemId.Value)
                continue;

            itemLookup.TryGetValue(wo.ItemId, out var item);

            var totalProcessLossQty = group.Sum(se => se.ProcessLossQty);
            var totalFgValue = group.Sum(se => se.TotalIncomingValue);
            var totalRmValue = group.Sum(se => se.TotalOutgoingValue);

            // Produced Qty: quantity of primary production item finished goods
            var producedQty = group
                .SelectMany(se => se.Items)
                .Where(i => i.IsFinishedItem && i.ItemId == wo.ItemId)
                .Sum(i => i.Quantity);

            // Output Qty: quantity of all finished items across entries (PR #60680)
            var outputQty = group
                .SelectMany(se => se.Items)
                .Where(i => i.IsFinishedItem)
                .Sum(i => i.Quantity);

            // Processed Qty denominator = outputQty + processLossQty
            var processedQty = outputQty + totalProcessLossQty;

            // Total Process Loss Value per ERPNext PR #60680
            var totalPlValue = processedQty > 0
                ? Math.Round(totalProcessLossQty * totalFgValue / processedQty, 4)
                : 0m;

            rows.Add(new ProcessLossReportRowDto
            {
                WorkOrderId = wo.Id,
                WorkOrderNumber = wo.WorkOrderNumber,
                ProductionItemId = wo.ItemId,
                ItemCode = item?.ItemCode ?? wo.ItemId.ToString()[..8],
                ItemName = item?.ItemName ?? "Unknown Item",
                Status = wo.Status.ToString(),
                QtyToManufacture = wo.Quantity,
                ProducedQty = producedQty,
                OutputQty = outputQty,
                ProcessLossQty = totalProcessLossQty,
                TotalProcessLossValue = totalPlValue,
                TotalFgValue = totalFgValue,
                TotalRmValue = totalRmValue
            });
        }

        return new ProcessLossReportDto
        {
            CompanyId = input.CompanyId,
            FromDate = input.FromDate,
            ToDate = input.ToDate,
            Rows = rows,
            TotalProducedQty = rows.Sum(r => r.ProducedQty),
            TotalProcessLossQty = rows.Sum(r => r.ProcessLossQty),
            TotalProcessLossValue = rows.Sum(r => r.TotalProcessLossValue),
            TotalFgValue = rows.Sum(r => r.TotalFgValue),
            TotalRmValue = rows.Sum(r => r.TotalRmValue)
        };
    }
}
