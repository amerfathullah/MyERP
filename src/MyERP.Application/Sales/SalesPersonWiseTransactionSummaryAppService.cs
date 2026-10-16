using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

[Authorize(MyERPPermissions.SalesOrders.Default)]
public class SalesPersonWiseTransactionSummaryAppService : ApplicationService, ISalesPersonWiseTransactionSummaryAppService
{
    private readonly IRepository<SalesTeamEntry, Guid> _teamEntryRepo;
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepo;
    private readonly IRepository<SalesOrder, Guid> _salesOrderRepo;
    private readonly IRepository<DeliveryNote, Guid> _deliveryNoteRepo;
    private readonly IRepository<Item, Guid> _itemRepo;
    private readonly IRepository<Customer, Guid> _customerRepo;
    private readonly IRepository<Territory, Guid> _territoryRepo;
    private readonly IRepository<Warehouse, Guid> _warehouseRepo;
    private readonly IRepository<SalesPerson, Guid> _salesPersonRepo;
    private readonly IRepository<Company, Guid> _companyRepo;

    public SalesPersonWiseTransactionSummaryAppService(
        IRepository<SalesTeamEntry, Guid> teamEntryRepo,
        IRepository<SalesInvoice, Guid> salesInvoiceRepo,
        IRepository<SalesOrder, Guid> salesOrderRepo,
        IRepository<DeliveryNote, Guid> deliveryNoteRepo,
        IRepository<Item, Guid> itemRepo,
        IRepository<Customer, Guid> customerRepo,
        IRepository<Territory, Guid> territoryRepo,
        IRepository<Warehouse, Guid> warehouseRepo,
        IRepository<SalesPerson, Guid> salesPersonRepo,
        IRepository<Company, Guid> companyRepo)
    {
        _teamEntryRepo = teamEntryRepo;
        _salesInvoiceRepo = salesInvoiceRepo;
        _salesOrderRepo = salesOrderRepo;
        _deliveryNoteRepo = deliveryNoteRepo;
        _itemRepo = itemRepo;
        _customerRepo = customerRepo;
        _territoryRepo = territoryRepo;
        _warehouseRepo = warehouseRepo;
        _salesPersonRepo = salesPersonRepo;
        _companyRepo = companyRepo;
    }

    public async Task<SalesPersonWiseTransactionSummaryReportDto> GetReportAsync(GetSalesPersonWiseTransactionSummaryInput input)
    {
        var company = await _companyRepo.FindAsync(input.CompanyId);
        var currency = company?.CurrencyCode ?? "MYR";

        var docType = input.DocType?.Trim() switch
        {
            "Sales Order" => "Sales Order",
            "Delivery Note" => "Delivery Note",
            _ => "Sales Invoice"
        };
        var parentTypeName = docType.Replace(" ", "");

        // Master records lookup
        var customerQuery = await _customerRepo.GetQueryableAsync();
        var customers = customerQuery.Where(c => c.CompanyId == input.CompanyId).ToList();
        var customerMap = customers.ToDictionary(c => c.Id, c => c);

        var territoryQuery = await _territoryRepo.GetQueryableAsync();
        var territories = territoryQuery.ToList();
        var territoryMap = territories.ToDictionary(t => t.Id, t => t.Name);

        var warehouseQuery = await _warehouseRepo.GetQueryableAsync();
        var warehouses = warehouseQuery.ToList();
        var warehouseMap = warehouses.ToDictionary(w => w.Id, w => w.Name);

        var spQuery = await _salesPersonRepo.GetQueryableAsync();
        var salesPersons = spQuery.ToList();
        var spMap = salesPersons.ToDictionary(sp => sp.Id, sp => sp.Name);

        var itemQuery = await _itemRepo.GetQueryableAsync();
        var items = itemQuery.Where(i => i.CompanyId == input.CompanyId).ToList();
        var itemMap = items.ToDictionary(i => i.Id, i => i);

        var rows = new List<SalesPersonWiseTransactionRowDto>();

        if (docType == "Sales Order")
        {
            var soQuery = await _salesOrderRepo.WithDetailsAsync(so => so.Items);
            var orders = soQuery.Where(so =>
                so.CompanyId == input.CompanyId &&
                so.Status != DocumentStatus.Cancelled &&
                so.Status != DocumentStatus.Draft).ToList();

            if (input.FromDate.HasValue)
                orders = orders.Where(so => so.OrderDate >= input.FromDate.Value).ToList();
            if (input.ToDate.HasValue)
                orders = orders.Where(so => so.OrderDate <= input.ToDate.Value).ToList();
            if (input.CustomerId.HasValue)
                orders = orders.Where(so => so.CustomerId == input.CustomerId.Value).ToList();

            if (orders.Count > 0)
            {
                var orderIds = orders.Select(so => so.Id).ToHashSet();
                var teamQuery = await _teamEntryRepo.GetQueryableAsync();
                var entries = teamQuery.Where(e =>
                    e.ParentType == parentTypeName &&
                    orderIds.Contains(e.ParentId)).ToList();

                if (input.SalesPersonId.HasValue)
                    entries = entries.Where(e => e.SalesPersonId == input.SalesPersonId.Value).ToList();

                var teamByOrder = entries.GroupBy(e => e.ParentId).ToDictionary(g => g.Key, g => g.ToList());

                foreach (var order in orders)
                {
                    if (!teamByOrder.TryGetValue(order.Id, out var orderEntries) || orderEntries.Count == 0)
                        continue;

                    customerMap.TryGetValue(order.CustomerId, out var cust);
                    string? terrName = null;
                    if (cust?.TerritoryId.HasValue == true)
                        territoryMap.TryGetValue(cust.TerritoryId.Value, out terrName);

                    if (input.TerritoryId.HasValue && cust?.TerritoryId != input.TerritoryId.Value)
                        continue;

                    foreach (var item in order.Items)
                    {
                        if (item.Quantity <= 0 && !input.ShowReturnEntries)
                            continue;

                        itemMap.TryGetValue(item.ItemId, out var itm);
                        if (!string.IsNullOrWhiteSpace(input.ItemCode) &&
                            itm != null &&
                            !itm.ItemCode.Contains(input.ItemCode, StringComparison.OrdinalIgnoreCase))
                            continue;

                        var netAmount = Math.Round(item.Quantity * item.UnitPrice, 2);

                        foreach (var entry in orderEntries)
                        {
                            spMap.TryGetValue(entry.SalesPersonId, out var spName);

                            rows.Add(new SalesPersonWiseTransactionRowDto
                            {
                                TransactionId = order.Id,
                                TransactionNumber = order.OrderNumber,
                                DocType = docType,
                                PostingDate = order.OrderDate,
                                CustomerId = order.CustomerId,
                                CustomerName = cust?.Name ?? order.CustomerId.ToString()[..8],
                                TerritoryName = terrName,
                                WarehouseName = null,
                                ItemId = item.ItemId,
                                ItemCode = itm?.ItemCode ?? string.Empty,
                                ItemName = itm?.ItemName ?? item.Description,
                                ItemGroup = itm?.ItemGroup,
                                Quantity = item.Quantity,
                                Rate = item.UnitPrice,
                                BaseNetAmount = netAmount,
                                SalesPersonId = entry.SalesPersonId,
                                SalesPersonName = spName ?? entry.SalesPersonId.ToString()[..8],
                                AllocatedPercentage = entry.AllocatedPercentage,
                                ContributionQuantity = Math.Round(item.Quantity * entry.AllocatedPercentage / 100m, 2),
                                ContributionAmount = Math.Round(netAmount * entry.AllocatedPercentage / 100m, 2),
                            });
                        }
                    }
                }
            }
        }
        else if (docType == "Delivery Note")
        {
            var dnQuery = await _deliveryNoteRepo.WithDetailsAsync(dn => dn.Items);
            var deliveryNotes = dnQuery.Where(dn =>
                dn.CompanyId == input.CompanyId &&
                dn.Status != DocumentStatus.Cancelled &&
                dn.Status != DocumentStatus.Draft).ToList();

            if (input.FromDate.HasValue)
                deliveryNotes = deliveryNotes.Where(dn => dn.PostingDate >= input.FromDate.Value).ToList();
            if (input.ToDate.HasValue)
                deliveryNotes = deliveryNotes.Where(dn => dn.PostingDate <= input.ToDate.Value).ToList();
            if (input.CustomerId.HasValue)
                deliveryNotes = deliveryNotes.Where(dn => dn.CustomerId == input.CustomerId.Value).ToList();

            if (deliveryNotes.Count > 0)
            {
                var dnIds = deliveryNotes.Select(dn => dn.Id).ToHashSet();
                var teamQuery = await _teamEntryRepo.GetQueryableAsync();
                var entries = teamQuery.Where(e =>
                    e.ParentType == parentTypeName &&
                    dnIds.Contains(e.ParentId)).ToList();

                if (input.SalesPersonId.HasValue)
                    entries = entries.Where(e => e.SalesPersonId == input.SalesPersonId.Value).ToList();

                var teamByDn = entries.GroupBy(e => e.ParentId).ToDictionary(g => g.Key, g => g.ToList());

                foreach (var dn in deliveryNotes)
                {
                    if (!teamByDn.TryGetValue(dn.Id, out var dnEntries) || dnEntries.Count == 0)
                        continue;

                    customerMap.TryGetValue(dn.CustomerId, out var cust);
                    string? terrName = null;
                    if (cust?.TerritoryId.HasValue == true)
                        territoryMap.TryGetValue(cust.TerritoryId.Value, out terrName);

                    if (input.TerritoryId.HasValue && cust?.TerritoryId != input.TerritoryId.Value)
                        continue;

                    warehouseMap.TryGetValue(dn.WarehouseId, out var whName);

                    foreach (var item in dn.Items)
                    {
                        if (item.Quantity <= 0 && !input.ShowReturnEntries)
                            continue;

                        itemMap.TryGetValue(item.ItemId, out var itm);
                        if (!string.IsNullOrWhiteSpace(input.ItemCode) &&
                            itm != null &&
                            !itm.ItemCode.Contains(input.ItemCode, StringComparison.OrdinalIgnoreCase))
                            continue;

                        var netAmount = Math.Round(item.Quantity * item.UnitPrice, 2);

                        foreach (var entry in dnEntries)
                        {
                            spMap.TryGetValue(entry.SalesPersonId, out var spName);

                            rows.Add(new SalesPersonWiseTransactionRowDto
                            {
                                TransactionId = dn.Id,
                                TransactionNumber = dn.DeliveryNumber,
                                DocType = docType,
                                PostingDate = dn.PostingDate,
                                CustomerId = dn.CustomerId,
                                CustomerName = cust?.Name ?? dn.CustomerId.ToString()[..8],
                                TerritoryName = terrName,
                                WarehouseName = whName,
                                ItemId = item.ItemId,
                                ItemCode = itm?.ItemCode ?? string.Empty,
                                ItemName = itm?.ItemName ?? item.Description,
                                ItemGroup = itm?.ItemGroup,
                                Quantity = item.Quantity,
                                Rate = item.UnitPrice,
                                BaseNetAmount = netAmount,
                                SalesPersonId = entry.SalesPersonId,
                                SalesPersonName = spName ?? entry.SalesPersonId.ToString()[..8],
                                AllocatedPercentage = entry.AllocatedPercentage,
                                ContributionQuantity = Math.Round(item.Quantity * entry.AllocatedPercentage / 100m, 2),
                                ContributionAmount = Math.Round(netAmount * entry.AllocatedPercentage / 100m, 2),
                            });
                        }
                    }
                }
            }
        }
        else // Sales Invoice
        {
            var siQuery = await _salesInvoiceRepo.WithDetailsAsync(si => si.Items);
            var invoices = siQuery.Where(si =>
                si.CompanyId == input.CompanyId &&
                si.Status != DocumentStatus.Cancelled &&
                si.Status != DocumentStatus.Draft).ToList();

            if (input.FromDate.HasValue)
                invoices = invoices.Where(si => si.IssueDate >= input.FromDate.Value).ToList();
            if (input.ToDate.HasValue)
                invoices = invoices.Where(si => si.IssueDate <= input.ToDate.Value).ToList();
            if (input.CustomerId.HasValue)
                invoices = invoices.Where(si => si.CustomerId == input.CustomerId.Value).ToList();

            if (invoices.Count > 0)
            {
                var invoiceIds = invoices.Select(si => si.Id).ToHashSet();
                var teamQuery = await _teamEntryRepo.GetQueryableAsync();
                var entries = teamQuery.Where(e =>
                    e.ParentType == parentTypeName &&
                    invoiceIds.Contains(e.ParentId)).ToList();

                if (input.SalesPersonId.HasValue)
                    entries = entries.Where(e => e.SalesPersonId == input.SalesPersonId.Value).ToList();

                var teamByInvoice = entries.GroupBy(e => e.ParentId).ToDictionary(g => g.Key, g => g.ToList());

                foreach (var invoice in invoices)
                {
                    if (!teamByInvoice.TryGetValue(invoice.Id, out var invEntries) || invEntries.Count == 0)
                        continue;

                    customerMap.TryGetValue(invoice.CustomerId, out var cust);
                    string? terrName = null;
                    if (cust?.TerritoryId.HasValue == true)
                        territoryMap.TryGetValue(cust.TerritoryId.Value, out terrName);

                    if (input.TerritoryId.HasValue && cust?.TerritoryId != input.TerritoryId.Value)
                        continue;

                    foreach (var item in invoice.Items)
                    {
                        if (item.Quantity <= 0 && !input.ShowReturnEntries)
                            continue;

                        itemMap.TryGetValue(item.ItemId, out var itm);
                        if (!string.IsNullOrWhiteSpace(input.ItemCode) &&
                            itm != null &&
                            !itm.ItemCode.Contains(input.ItemCode, StringComparison.OrdinalIgnoreCase))
                            continue;

                        var netAmount = Math.Round(item.Quantity * item.UnitPrice, 2);

                        foreach (var entry in invEntries)
                        {
                            spMap.TryGetValue(entry.SalesPersonId, out var spName);

                            rows.Add(new SalesPersonWiseTransactionRowDto
                            {
                                TransactionId = invoice.Id,
                                TransactionNumber = invoice.InvoiceNumber,
                                DocType = docType,
                                PostingDate = invoice.IssueDate,
                                CustomerId = invoice.CustomerId,
                                CustomerName = cust?.Name ?? invoice.CustomerId.ToString()[..8],
                                TerritoryName = terrName,
                                WarehouseName = null,
                                ItemId = item.ItemId,
                                ItemCode = itm?.ItemCode ?? string.Empty,
                                ItemName = itm?.ItemName ?? item.Description,
                                ItemGroup = itm?.ItemGroup,
                                Quantity = item.Quantity,
                                Rate = item.UnitPrice,
                                BaseNetAmount = netAmount,
                                SalesPersonId = entry.SalesPersonId,
                                SalesPersonName = spName ?? entry.SalesPersonId.ToString()[..8],
                                AllocatedPercentage = entry.AllocatedPercentage,
                                ContributionQuantity = Math.Round(item.Quantity * entry.AllocatedPercentage / 100m, 2),
                                ContributionAmount = Math.Round(netAmount * entry.AllocatedPercentage / 100m, 2),
                            });
                        }
                    }
                }
            }
        }

        var sortedRows = rows
            .OrderByDescending(r => r.PostingDate)
            .ThenBy(r => r.TransactionNumber)
            .ThenBy(r => r.ItemCode)
            .ThenBy(r => r.SalesPersonName)
            .ToList();

        return new SalesPersonWiseTransactionSummaryReportDto
        {
            Currency = currency,
            DocType = docType,
            Rows = sortedRows,
            TotalQuantity = sortedRows.Sum(r => r.ContributionQuantity),
            TotalNetAmount = sortedRows.Sum(r => r.ContributionAmount),
            TotalContributionQuantity = sortedRows.Sum(r => r.ContributionQuantity),
            TotalContributionAmount = sortedRows.Sum(r => r.ContributionAmount),
            TransactionCount = sortedRows.Select(r => r.TransactionId).Distinct().Count(),
        };
    }
}
