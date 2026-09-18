using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

[Authorize(MyERPPermissions.SalesInvoices.Default)]
public class SalesPaymentSummaryAppService : ApplicationService, ISalesPaymentSummaryAppService
{
    private readonly IRepository<SalesInvoice, Guid> _invoiceRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;
    private readonly IRepository<PosProfile, Guid> _posProfileRepository;
    private readonly IRepository<PaymentEntry, Guid> _paymentEntryRepository;

    public SalesPaymentSummaryAppService(
        IRepository<SalesInvoice, Guid> invoiceRepository,
        IRepository<Warehouse, Guid> warehouseRepository,
        IRepository<PosProfile, Guid> posProfileRepository,
        IRepository<PaymentEntry, Guid> paymentEntryRepository)
    {
        _invoiceRepository = invoiceRepository;
        _warehouseRepository = warehouseRepository;
        _posProfileRepository = posProfileRepository;
        _paymentEntryRepository = paymentEntryRepository;
    }

    public async Task<SalesPaymentSummaryReportDto> GetReportAsync(SalesPaymentSummaryFilterDto input)
    {
        var from = input.FromDate ?? DateTime.UtcNow.AddMonths(-1).Date;
        var to = input.ToDate ?? DateTime.UtcNow.Date;

        var query = await _invoiceRepository.GetQueryableAsync();
        var invoices = query
            .Where(si => si.CompanyId == input.CompanyId
                      && si.IsPos
                      && si.Status == DocumentStatus.Posted
                      && si.IssueDate >= from
                      && si.IssueDate <= to)
            .WhereIf(input.WarehouseId.HasValue, si => si.WarehouseId == input.WarehouseId!.Value)
            .WhereIf(input.PosProfileId.HasValue, si => si.PosProfileId == input.PosProfileId!.Value)
            .WhereIf(input.CustomerId.HasValue, si => si.CustomerId == input.CustomerId!.Value)
            .ToList();

        if (invoices.Count == 0)
        {
            return new SalesPaymentSummaryReportDto();
        }

        // Warehouse names
        var warehouseIds = invoices.Where(si => si.WarehouseId.HasValue).Select(si => si.WarehouseId!.Value).Distinct().ToList();
        var whQuery = await _warehouseRepository.GetQueryableAsync();
        var warehouseMap = whQuery
            .Where(w => warehouseIds.Contains(w.Id))
            .ToDictionary(w => w.Id, w => w.Name);

        // POS Profile map for fallback mode / warehouse
        var profileIds = invoices.Where(si => si.PosProfileId.HasValue).Select(si => si.PosProfileId!.Value).Distinct().ToList();
        var posProfiles = await _posProfileRepository.GetListAsync(p => profileIds.Contains(p.Id));
        var profileMap = posProfiles.ToDictionary(p => p.Id);

        // Fetch payments made against these invoices
        var invoiceIds = invoices.Select(si => si.Id).ToList();
        var peQuery = await _paymentEntryRepository.GetQueryableAsync();
        var paymentEntries = peQuery
            .Where(pe => pe.Status == DocumentStatus.Posted
                      && pe.AgainstOrderId.HasValue
                      && invoiceIds.Contains(pe.AgainstOrderId.Value))
            .ToList();
        var paymentModeLookup = paymentEntries
            .GroupBy(pe => pe.AgainstOrderId!.Value)
            .ToDictionary(g => g.Key, g => g.First().ModeOfPayment);

        // Group POS invoices by (PostingDate, WarehouseId, CreatorId)
        // Per ERPNext PR #59130 (commit 2aab7f4f72):
        // CostCenter and ModeOfPayment must come from the EARLIEST invoice in each row,
        // ordered in memory by (CreationTime, InvoiceNumber.ToLowerInvariant()) to eliminate
        // database text collation divergence and cross-invoice pairing.
        var groups = invoices
            .GroupBy(si => new
            {
                Date = si.IssueDate.Date,
                WarehouseId = si.WarehouseId ?? (si.PosProfileId.HasValue && profileMap.TryGetValue(si.PosProfileId.Value, out var prof) ? prof.WarehouseId : Guid.Empty),
                CashierId = si.CreatorId ?? Guid.Empty
            })
            .ToList();

        var rows = new List<SalesPaymentSummaryRowDto>();

        foreach (var g in groups)
        {
            var sortedInvoices = g
                .OrderBy(si => si.CreationTime)
                .ThenBy(si => si.InvoiceNumber.ToLowerInvariant())
                .ToList();

            var earliestInvoice = sortedInvoices.First();
            var modeOfPayment = paymentModeLookup.GetValueOrDefault(earliestInvoice.Id) ?? "Cash";
            var warehouseName = warehouseMap.GetValueOrDefault(g.Key.WarehouseId, "Default Warehouse");

            var netTotal = sortedInvoices.Sum(si => si.NetTotal);
            var totalTaxes = sortedInvoices.Sum(si => si.TaxAmount);
            var paidAmount = sortedInvoices.Sum(si => si.AmountPaid);
            var outstandingAmount = sortedInvoices.Sum(si => si.OutstandingAmount);

            rows.Add(new SalesPaymentSummaryRowDto
            {
                PostingDate = g.Key.Date,
                Cashier = g.Key.CashierId.ToString().Substring(0, 8),
                WarehouseId = g.Key.WarehouseId,
                WarehouseName = warehouseName,
                CostCenter = "Main",
                ModeOfPayment = modeOfPayment,
                NetTotal = Math.Round(netTotal, 2),
                TotalTaxes = Math.Round(totalTaxes, 2),
                PaidAmount = Math.Round(paidAmount, 2),
                OutstandingAmount = Math.Round(outstandingAmount, 2)
            });
        }

        return new SalesPaymentSummaryReportDto
        {
            Rows = rows.OrderBy(r => r.PostingDate).ThenBy(r => r.WarehouseName).ToList(),
            TotalNet = Math.Round(rows.Sum(r => r.NetTotal), 2),
            TotalTaxes = Math.Round(rows.Sum(r => r.TotalTaxes), 2),
            TotalPaid = Math.Round(rows.Sum(r => r.PaidAmount), 2),
            TotalOutstanding = Math.Round(rows.Sum(r => r.OutstandingAmount), 2)
        };
    }
}
