using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Accounting.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Projects.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

[Authorize(MyERPPermissions.SalesInvoices.Default)]
public class GrossProfitReportAppService : ApplicationService, IGrossProfitReportAppService
{
    private readonly IRepository<SalesInvoice, Guid> _invoiceRepository;
    private readonly GrossProfitService _grossProfitService;
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;
    private readonly IRepository<PurchaseInvoice, Guid>? _purchaseInvoiceRepository;
    private readonly IRepository<PurchaseOrder, Guid>? _purchaseOrderRepository;
    private readonly IRepository<Project, Guid>? _projectRepository;
    private readonly IRepository<SalesPerson, Guid>? _salesPersonRepository;
    private readonly IRepository<SalesTeamEntry, Guid>? _salesTeamRepository;
    private readonly IRepository<PaymentScheduleEntry, Guid>? _paymentScheduleRepository;

    public GrossProfitReportAppService(
        IRepository<SalesInvoice, Guid> invoiceRepository,
        GrossProfitService grossProfitService,
        IRepository<Customer, Guid> customerRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<Warehouse, Guid> warehouseRepository,
        IRepository<PurchaseInvoice, Guid>? purchaseInvoiceRepository = null,
        IRepository<PurchaseOrder, Guid>? purchaseOrderRepository = null,
        IRepository<Project, Guid>? projectRepository = null,
        IRepository<SalesPerson, Guid>? salesPersonRepository = null,
        IRepository<SalesTeamEntry, Guid>? salesTeamRepository = null,
        IRepository<PaymentScheduleEntry, Guid>? paymentScheduleRepository = null)
    {
        _invoiceRepository = invoiceRepository;
        _grossProfitService = grossProfitService;
        _customerRepository = customerRepository;
        _itemRepository = itemRepository;
        _warehouseRepository = warehouseRepository;
        _purchaseInvoiceRepository = purchaseInvoiceRepository;
        _purchaseOrderRepository = purchaseOrderRepository;
        _projectRepository = projectRepository;
        _salesPersonRepository = salesPersonRepository;
        _salesTeamRepository = salesTeamRepository;
        _paymentScheduleRepository = paymentScheduleRepository;
    }

    public async Task<GrossProfitReportDto> GetReportAsync(GrossProfitRequestDto input)
    {
        var from = input.FromDate ?? DateTime.UtcNow.AddMonths(-1).Date;
        var to = input.ToDate ?? DateTime.UtcNow.Date;
        var groupBy = (input.GroupBy ?? "Invoice").Trim();

        var query = await _invoiceRepository.GetQueryableAsync();
        var invoicesQ = query
            .Where(si => si.CompanyId == input.CompanyId
                      && si.Status == Core.DocumentStatus.Posted
                      && si.IssueDate >= from
                      && si.IssueDate <= to);

        if (input.CustomerId.HasValue)
        {
            invoicesQ = invoicesQ.Where(si => si.CustomerId == input.CustomerId.Value);
        }

        var invoices = invoicesQ
            .OrderByDescending(si => si.IssueDate)
            .ToList();

        var invoiceIds = invoices.Select(si => si.Id).ToList();
        var customerIds = invoices.Select(si => si.CustomerId).Distinct().ToList();
        var itemIds = invoices.SelectMany(si => si.Items).Select(i => i.ItemId).Distinct().ToList();
        var warehouseIds = invoices.Where(si => si.WarehouseId.HasValue).Select(si => si.WarehouseId!.Value).Distinct().ToList();
        var projectIds = invoices.Where(si => si.ProjectId.HasValue).Select(si => si.ProjectId!.Value).Distinct().ToList();

        var customers = (await _customerRepository.GetListAsync(c => customerIds.Contains(c.Id)))
            .ToDictionary(c => c.Id, c => c.Name);
        var items = (await _itemRepository.GetListAsync(i => itemIds.Contains(i.Id)))
            .ToDictionary(i => i.Id);
        var warehouses = (await _warehouseRepository.GetListAsync(w => warehouseIds.Contains(w.Id)))
            .ToDictionary(w => w.Id, w => w.Name);

        var projects = _projectRepository != null && projectIds.Any()
            ? (await _projectRepository.GetListAsync(p => projectIds.Contains(p.Id)))
                .ToDictionary(p => p.Id, p => p.ProjectName)
            : new Dictionary<Guid, string>();

        var salesTeamEntries = _salesTeamRepository != null && invoiceIds.Any()
            ? (await _salesTeamRepository.GetListAsync(st => st.ParentType == "SalesInvoice" && invoiceIds.Contains(st.ParentId)))
                .GroupBy(st => st.ParentId)
                .ToDictionary(g => g.Key, g => g.ToList())
            : new Dictionary<Guid, List<SalesTeamEntry>>();

        var salesPersonIds = salesTeamEntries.Values.SelectMany(x => x).Select(x => x.SalesPersonId).Distinct().ToList();
        var salesPersons = _salesPersonRepository != null && salesPersonIds.Any()
            ? (await _salesPersonRepository.GetListAsync(sp => salesPersonIds.Contains(sp.Id)))
                .ToDictionary(sp => sp.Id, sp => sp.Name)
            : new Dictionary<Guid, string>();

        var paymentSchedules = _paymentScheduleRepository != null && invoiceIds.Any()
            ? (await _paymentScheduleRepository.GetListAsync(ps => ps.ParentType == "SalesInvoice" && invoiceIds.Contains(ps.ParentId)))
                .GroupBy(ps => ps.ParentId)
                .ToDictionary(g => g.Key, g => g.ToList())
            : new Dictionary<Guid, List<PaymentScheduleEntry>>();

        // Per ERPNext PR #58226 / #59885 (commit 9f03f19f65):
        // Load drop-ship buying rates from submitted Purchase Invoices against the PO,
        // falling back to Purchase Order net rate if not yet billed by supplier.
        var dropShipBuyingRates = new Dictionary<Guid, decimal>();
        if (_purchaseInvoiceRepository != null)
        {
            var pis = await _purchaseInvoiceRepository.GetListAsync(
                pi => pi.CompanyId == input.CompanyId
                   && pi.Status == Core.DocumentStatus.Posted
                   && pi.IssueDate <= to);

            foreach (var pi in pis)
            {
                var discountMultiplier = (100m - pi.AdditionalDiscountPercentage) / 100m;
                foreach (var piItem in pi.Items.Where(i => i.DeliveredBySupplier))
                {
                    if (piItem.StockQty > 0)
                    {
                        var rate = Math.Round((piItem.LineTotal * discountMultiplier) / piItem.StockQty, 4);
                        dropShipBuyingRates[piItem.ItemId] = rate;
                        if (piItem.PurchaseOrderItemId.HasValue)
                        {
                            dropShipBuyingRates[piItem.PurchaseOrderItemId.Value] = rate;
                        }
                    }
                }
            }
        }

        if (_purchaseOrderRepository != null)
        {
            var pos = await _purchaseOrderRepository.GetListAsync(
                po => po.CompanyId == input.CompanyId
                   && po.Status != Core.DocumentStatus.Draft
                   && po.Status != Core.DocumentStatus.Cancelled
                   && po.OrderDate <= to);

            foreach (var po in pos)
            {
                foreach (var poItem in po.Items.Where(i => i.DeliveredBySupplier))
                {
                    if (poItem.StockQty > 0 && !dropShipBuyingRates.ContainsKey(poItem.ItemId))
                    {
                        var rate = Math.Round(poItem.LineTotal / poItem.StockQty, 4);
                        dropShipBuyingRates[poItem.ItemId] = rate;
                    }
                }
            }
        }

        // Per ERPNext PR #59885 (commit e8496405d4):
        // Price non-stock items at the last net purchase rate (discounted base_net_rate).
        var lastNetPurchaseRates = new Dictionary<Guid, decimal>();
        if (_purchaseInvoiceRepository != null)
        {
            var nonStockItemIds = items.Values
                .Where(i => i.ItemType != ItemType.Goods)
                .Select(i => i.Id)
                .ToHashSet();

            if (nonStockItemIds.Any())
            {
                var pastPis = (await _purchaseInvoiceRepository.GetListAsync(
                    pi => pi.CompanyId == input.CompanyId
                       && pi.Status == Core.DocumentStatus.Posted
                       && pi.IssueDate <= to))
                    .OrderByDescending(pi => pi.IssueDate)
                    .ToList();

                foreach (var pi in pastPis)
                {
                    var discountMultiplier = (100m - pi.AdditionalDiscountPercentage) / 100m;
                    foreach (var piItem in pi.Items)
                    {
                        if (nonStockItemIds.Contains(piItem.ItemId) && !lastNetPurchaseRates.ContainsKey(piItem.ItemId))
                        {
                            var conversion = piItem.ConversionFactor > 0 ? piItem.ConversionFactor : 1m;
                            var netRate = Math.Round((piItem.UnitPrice * discountMultiplier) / conversion, 4);
                            lastNetPurchaseRates[piItem.ItemId] = netRate;
                        }
                    }
                }
            }
        }

        var lineData = new List<GrossProfitLineDto>();

        foreach (var invoice in invoices)
        {
            var customerName = customers.GetValueOrDefault(invoice.CustomerId) ?? string.Empty;
            var warehouseName = invoice.WarehouseId.HasValue ? warehouses.GetValueOrDefault(invoice.WarehouseId.Value) : null;
            var projectName = invoice.ProjectId.HasValue ? projects.GetValueOrDefault(invoice.ProjectId.Value) : null;
            var gp = _grossProfitService.CalculateForInvoice(invoice, dropShipBuyingRates);

            foreach (var itemDetail in gp.ItemDetails)
            {
                if (input.ItemId.HasValue && itemDetail.ItemId != input.ItemId.Value)
                    continue;

                // Per ERPNext PR #59885 (commit 202c50d475):
                // Filter and group by Project reads item/invoice project.
                var effectiveProjectId = invoice.ProjectId;
                if (input.ProjectId.HasValue && effectiveProjectId != input.ProjectId.Value)
                    continue;

                var itemEntity = items.GetValueOrDefault(itemDetail.ItemId);
                var valuationRate = itemDetail.ValuationRate;

                // Commit e8496405d4: fallback to discounted last purchase net rate for non-stock items
                if (valuationRate == 0 && lastNetPurchaseRates.TryGetValue(itemDetail.ItemId, out var lastNetRate))
                {
                    valuationRate = lastNetRate;
                }

                var revenue = Math.Round(itemDetail.Quantity * itemDetail.SellingRate, 2);
                var cost = Math.Round(itemDetail.Quantity * valuationRate, 2);
                var profit = revenue - cost;
                var profitPercent = revenue > 0 ? Math.Round((profit / revenue) * 100m, 2) : 0m;

                lineData.Add(new GrossProfitLineDto
                {
                    InvoiceId = invoice.Id,
                    InvoiceNumber = invoice.InvoiceNumber ?? string.Empty,
                    IssueDate = invoice.IssueDate,
                    CustomerId = invoice.CustomerId,
                    CustomerName = customerName,
                    ItemId = itemDetail.ItemId,
                    ItemCode = itemEntity?.ItemCode ?? string.Empty,
                    ItemName = itemEntity?.ItemName ?? itemDetail.Description,
                    ItemGroup = itemEntity?.ItemGroup ?? string.Empty,
                    WarehouseId = invoice.WarehouseId,
                    WarehouseName = warehouseName,
                    Quantity = itemDetail.Quantity,
                    SellingRate = itemDetail.SellingRate,
                    ValuationRate = valuationRate,
                    Revenue = revenue,
                    Cost = cost,
                    GrossProfit = profit,
                    GrossProfitPercentage = profitPercent,
                    ProjectId = invoice.ProjectId,
                    ProjectName = projectName
                });
            }
        }

        List<GrossProfitLineDto> resultRows;

        if (groupBy.Equals("Item", StringComparison.OrdinalIgnoreCase))
        {
            resultRows = lineData
                .GroupBy(l => l.ItemId)
                .Select(g =>
                {
                    var first = g.First();
                    var totalQty = g.Sum(x => x.Quantity);
                    var totalRev = g.Sum(x => x.Revenue);
                    var totalCost = g.Sum(x => x.Cost);
                    var profit = totalRev - totalCost;
                    var profitPct = totalRev > 0 ? Math.Round((profit / totalRev) * 100m, 2) : 0m;
                    var avgSelling = totalQty > 0 ? Math.Round(totalRev / totalQty, 4) : 0m;
                    var avgValuation = totalQty > 0 ? Math.Round(totalCost / totalQty, 4) : 0m;

                    return new GrossProfitLineDto
                    {
                        ItemId = first.ItemId,
                        ItemCode = first.ItemCode,
                        ItemName = first.ItemName,
                        ItemGroup = first.ItemGroup,
                        Quantity = totalQty,
                        SellingRate = avgSelling,
                        ValuationRate = avgValuation,
                        Revenue = totalRev,
                        Cost = totalCost,
                        GrossProfit = profit,
                        GrossProfitPercentage = profitPct
                    };
                })
                .OrderByDescending(x => x.Revenue)
                .ToList();
        }
        else if (groupBy.Equals("Customer", StringComparison.OrdinalIgnoreCase))
        {
            resultRows = lineData
                .GroupBy(l => l.CustomerId)
                .Select(g =>
                {
                    var first = g.First();
                    var totalQty = g.Sum(x => x.Quantity);
                    var totalRev = g.Sum(x => x.Revenue);
                    var totalCost = g.Sum(x => x.Cost);
                    var profit = totalRev - totalCost;
                    var profitPct = totalRev > 0 ? Math.Round((profit / totalRev) * 100m, 2) : 0m;

                    return new GrossProfitLineDto
                    {
                        CustomerId = first.CustomerId,
                        CustomerName = first.CustomerName,
                        Quantity = totalQty,
                        Revenue = totalRev,
                        Cost = totalCost,
                        GrossProfit = profit,
                        GrossProfitPercentage = profitPct
                    };
                })
                .OrderByDescending(x => x.Revenue)
                .ToList();
        }
        else if (groupBy.Equals("Project", StringComparison.OrdinalIgnoreCase))
        {
            // Per ERPNext PR #59885 (commit 202c50d475): Group by Project
            resultRows = lineData
                .GroupBy(l => l.ProjectId)
                .Select(g =>
                {
                    var first = g.First();
                    var totalQty = g.Sum(x => x.Quantity);
                    var totalRev = g.Sum(x => x.Revenue);
                    var totalCost = g.Sum(x => x.Cost);
                    var profit = totalRev - totalCost;
                    var profitPct = totalRev > 0 ? Math.Round((profit / totalRev) * 100m, 2) : 0m;
                    var projName = first.ProjectId.HasValue
                        ? projects.GetValueOrDefault(first.ProjectId.Value) ?? first.ProjectName ?? "Project"
                        : "No Project";

                    return new GrossProfitLineDto
                    {
                        ProjectId = first.ProjectId,
                        ProjectName = projName,
                        Quantity = totalQty,
                        Revenue = totalRev,
                        Cost = totalCost,
                        GrossProfit = profit,
                        GrossProfitPercentage = profitPct
                    };
                })
                .OrderByDescending(x => x.Revenue)
                .ToList();
        }
        else if (groupBy.Equals("Payment Term", StringComparison.OrdinalIgnoreCase)
              || groupBy.Equals("PaymentTerm", StringComparison.OrdinalIgnoreCase))
        {
            // Per ERPNext PR #59885 (commit cc0d7021b1):
            // Invoices without payment schedule (or returns) count in full (100%) under "No Terms".
            var termRows = new List<GrossProfitLineDto>();

            foreach (var invoice in invoices)
            {
                var invoiceLines = lineData.Where(l => l.InvoiceId == invoice.Id).ToList();
                if (!invoiceLines.Any()) continue;

                var invRev = invoiceLines.Sum(l => l.Revenue);
                var invCost = invoiceLines.Sum(l => l.Cost);
                var invQty = invoiceLines.Sum(l => l.Quantity);

                var schedules = paymentSchedules.GetValueOrDefault(invoice.Id);
                if (schedules == null || schedules.Count == 0 || invoice.IsReturn)
                {
                    termRows.Add(new GrossProfitLineDto
                    {
                        PaymentTerm = "No Terms",
                        Quantity = invQty,
                        Revenue = invRev,
                        Cost = invCost,
                        GrossProfit = invRev - invCost,
                        GrossProfitPercentage = invRev > 0 ? Math.Round(((invRev - invCost) / invRev) * 100m, 2) : 0m
                    });
                }
                else
                {
                    foreach (var sched in schedules)
                    {
                        var portion = sched.InvoicePortion > 0 ? (sched.InvoicePortion / 100m) : 1m;
                        var portionRev = Math.Round(invRev * portion, 2);
                        var portionCost = Math.Round(invCost * portion, 2);
                        var profit = portionRev - portionCost;
                        termRows.Add(new GrossProfitLineDto
                        {
                            PaymentTerm = sched.Description ?? "No Terms",
                            Quantity = Math.Round(invQty * portion, 4),
                            Revenue = portionRev,
                            Cost = portionCost,
                            GrossProfit = profit,
                            GrossProfitPercentage = portionRev > 0 ? Math.Round((profit / portionRev) * 100m, 2) : 0m
                        });
                    }
                }
            }

            resultRows = termRows
                .GroupBy(t => t.PaymentTerm ?? "No Terms")
                .Select(g =>
                {
                    var totalQty = g.Sum(x => x.Quantity);
                    var totalRev = g.Sum(x => x.Revenue);
                    var totalCost = g.Sum(x => x.Cost);
                    var profit = totalRev - totalCost;
                    var profitPct = totalRev > 0 ? Math.Round((profit / totalRev) * 100m, 2) : 0m;
                    return new GrossProfitLineDto
                    {
                        PaymentTerm = g.Key,
                        Quantity = totalQty,
                        Revenue = totalRev,
                        Cost = totalCost,
                        GrossProfit = profit,
                        GrossProfitPercentage = profitPct
                    };
                })
                .OrderByDescending(x => x.Revenue)
                .ToList();
        }
        else if (groupBy.Equals("Sales Person", StringComparison.OrdinalIgnoreCase)
              || groupBy.Equals("SalesPerson", StringComparison.OrdinalIgnoreCase))
        {
            // Per ERPNext PR #59885 (commit 2152a7d848):
            // Shared invoices repeat under each assigned sales person, but Total counts each invoice once.
            var spRows = new List<GrossProfitLineDto>();

            foreach (var invoice in invoices)
            {
                var invoiceLines = lineData.Where(l => l.InvoiceId == invoice.Id).ToList();
                if (!invoiceLines.Any()) continue;

                var invRev = invoiceLines.Sum(l => l.Revenue);
                var invCost = invoiceLines.Sum(l => l.Cost);
                var invQty = invoiceLines.Sum(l => l.Quantity);

                var team = salesTeamEntries.GetValueOrDefault(invoice.Id);
                if (team == null || team.Count == 0)
                {
                    spRows.Add(new GrossProfitLineDto
                    {
                        SalesPerson = "No Sales Person",
                        Quantity = invQty,
                        Revenue = invRev,
                        Cost = invCost,
                        GrossProfit = invRev - invCost,
                        GrossProfitPercentage = invRev > 0 ? Math.Round(((invRev - invCost) / invRev) * 100m, 2) : 0m
                    });
                }
                else
                {
                    foreach (var member in team)
                    {
                        var spName = salesPersons.GetValueOrDefault(member.SalesPersonId) ?? "Unknown";
                        spRows.Add(new GrossProfitLineDto
                        {
                            SalesPersonId = member.SalesPersonId,
                            SalesPerson = spName,
                            Quantity = invQty,
                            Revenue = invRev,
                            Cost = invCost,
                            GrossProfit = invRev - invCost,
                            GrossProfitPercentage = invRev > 0 ? Math.Round(((invRev - invCost) / invRev) * 100m, 2) : 0m
                        });
                    }
                }
            }

            resultRows = spRows
                .GroupBy(s => s.SalesPerson ?? "No Sales Person")
                .Select(g =>
                {
                    var first = g.First();
                    var totalQty = g.Sum(x => x.Quantity);
                    var totalRev = g.Sum(x => x.Revenue);
                    var totalCost = g.Sum(x => x.Cost);
                    var profit = totalRev - totalCost;
                    var profitPct = totalRev > 0 ? Math.Round((profit / totalRev) * 100m, 2) : 0m;
                    return new GrossProfitLineDto
                    {
                        SalesPersonId = first.SalesPersonId,
                        SalesPerson = g.Key,
                        Quantity = totalQty,
                        Revenue = totalRev,
                        Cost = totalCost,
                        GrossProfit = profit,
                        GrossProfitPercentage = profitPct
                    };
                })
                .OrderByDescending(x => x.Revenue)
                .ToList();
        }
        else
        {
            // Default: GroupBy == "Invoice"
            resultRows = lineData
                .OrderByDescending(l => l.IssueDate)
                .ThenBy(l => l.InvoiceNumber)
                .ThenBy(l => l.ItemCode)
                .ToList();
        }

        // Per ERPNext PR #59885 (commit 2152a7d848):
        // Total summary row counts distinct invoices once, even when grouped by Sales Person.
        var totalRevenue = lineData.Sum(l => l.Revenue);
        var totalCost = lineData.Sum(l => l.Cost);
        var grossProfit = totalRevenue - totalCost;
        var grossProfitPercentage = totalRevenue > 0
            ? Math.Round((grossProfit / totalRevenue) * 100m, 2)
            : 0m;

        return new GrossProfitReportDto
        {
            TotalRevenue = totalRevenue,
            TotalCost = totalCost,
            GrossProfit = grossProfit,
            GrossProfitPercentage = grossProfitPercentage,
            Items = resultRows
        };
    }
}
