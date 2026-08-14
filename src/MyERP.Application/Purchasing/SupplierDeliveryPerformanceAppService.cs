using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using MyERP.Sales;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Purchasing;

[Authorize(MyERPPermissions.PurchaseOrders.Default)]
public class SupplierDeliveryPerformanceAppService : ApplicationService, ISupplierDeliveryPerformanceAppService
{
    private readonly IRepository<PurchaseOrder, Guid> _poRepository;
    private readonly IRepository<Supplier, Guid> _supplierRepository;

    public SupplierDeliveryPerformanceAppService(
        IRepository<PurchaseOrder, Guid> poRepository,
        IRepository<Supplier, Guid> supplierRepository)
    {
        _poRepository = poRepository;
        _supplierRepository = supplierRepository;
    }

    public async Task<DeliveryPerformanceReportDto> GetReportAsync(RegisterFilterDto input)
    {
        var from = input.FromDate ?? DateTime.UtcNow.AddMonths(-6).Date;
        var to = input.ToDate ?? DateTime.UtcNow.Date;
        var today = DateTime.UtcNow.Date;

        var query = await _poRepository.GetQueryableAsync();
        var orders = query
            .Where(po => po.CompanyId == input.CompanyId
                      && po.OrderDate >= from
                      && po.OrderDate <= to
                      && po.Status != DocumentStatus.Draft
                      && po.Status != DocumentStatus.Cancelled)
            .ToList();

        var supplierIds = orders.Select(po => po.SupplierId).Distinct().ToList();
        var supplierQuery = await _supplierRepository.GetQueryableAsync();
        var supplierNames = supplierQuery
            .Where(s => supplierIds.Contains(s.Id))
            .ToDictionary(s => s.Id, s => s.Name);

        var supplierGroups = orders
            .GroupBy(po => po.SupplierId)
            .Select(g =>
            {
                var items = g.ToList();
                var withExpectedDate = items.Where(po => po.ExpectedDeliveryDate.HasValue).ToList();

                int onTime = 0, late = 0, pending = 0;
                var totalDelayDays = 0m;

                foreach (var po in withExpectedDate)
                {
                    var expected = po.ExpectedDeliveryDate!.Value;
                    var isFullyReceived = po.PerReceived >= 100;

                    if (isFullyReceived)
                    {
                        var completionDate = po.LastModificationTime?.Date ?? today;
                        if (completionDate <= expected)
                            onTime++;
                        else
                        {
                            late++;
                            totalDelayDays += (decimal)(completionDate - expected).TotalDays;
                        }
                    }
                    else if (today > expected)
                    {
                        late++;
                        totalDelayDays += (decimal)(today - expected).TotalDays;
                    }
                    else
                    {
                        pending++;
                    }
                }

                pending += items.Count - withExpectedDate.Count;

                var deliveredCount = onTime + late;
                return new SupplierDeliveryPerformanceDto
                {
                    SupplierId = g.Key,
                    SupplierName = supplierNames.GetValueOrDefault(g.Key, "—"),
                    TotalOrders = items.Count,
                    OnTimeDeliveries = onTime,
                    LateDeliveries = late,
                    PendingDeliveries = pending,
                    AvgDelayDays = deliveredCount > 0 ? Math.Round(totalDelayDays / deliveredCount, 1) : 0,
                    TotalOrderValue = items.Sum(po => po.GrandTotal)
                };
            })
            .OrderByDescending(s => s.TotalOrders)
            .ToList();

        var allDelivered = supplierGroups.Sum(s => s.OnTimeDeliveries + s.LateDeliveries);
        var allDelayDays = supplierGroups.Sum(s => s.AvgDelayDays * (s.OnTimeDeliveries + s.LateDeliveries));

        return new DeliveryPerformanceReportDto
        {
            Suppliers = supplierGroups,
            TotalOrders = orders.Count,
            TotalOnTime = supplierGroups.Sum(s => s.OnTimeDeliveries),
            TotalLate = supplierGroups.Sum(s => s.LateDeliveries),
            TotalPending = supplierGroups.Sum(s => s.PendingDeliveries),
            OverallAvgDelayDays = allDelivered > 0 ? Math.Round(allDelayDays / allDelivered, 1) : 0
        };
    }
}
