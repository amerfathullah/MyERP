using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using MyERP.Sales;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Purchasing;

[Authorize(MyERPPermissions.PurchaseInvoices.Default)]
public class PurchaseRegisterAppService : ApplicationService, IPurchaseRegisterAppService
{
    private readonly IRepository<PurchaseInvoice, Guid> _invoiceRepository;
    private readonly IRepository<PaymentEntry, Guid> _paymentRepository;
    private readonly IRepository<Supplier, Guid>? _supplierRepository;
    private readonly IRepository<SupplierGroup, Guid>? _supplierGroupRepository;

    public PurchaseRegisterAppService(
        IRepository<PurchaseInvoice, Guid> invoiceRepository,
        IRepository<PaymentEntry, Guid> paymentRepository,
        IRepository<Supplier, Guid>? supplierRepository = null,
        IRepository<SupplierGroup, Guid>? supplierGroupRepository = null)
    {
        _invoiceRepository = invoiceRepository;
        _paymentRepository = paymentRepository;
        _supplierRepository = supplierRepository;
        _supplierGroupRepository = supplierGroupRepository;
    }

    private async Task<IQueryable<PaymentEntry>> GetPaymentQueryableWithTaxesAsync()
    {
        try
        {
            var withDetails = await _paymentRepository.WithDetailsAsync(p => p.Taxes);
            if (withDetails != null) return withDetails;
        }
        catch
        {
            // Fallback if WithDetailsAsync is not configured or unsupported in mock/provider
        }
        return await _paymentRepository.GetQueryableAsync();
    }

    public async Task<RegisterReportDto<PurchaseRegisterLineDto>> GetReportAsync(RegisterFilterDto input)
    {
        var from = input.FromDate ?? DateTime.UtcNow.AddMonths(-1).Date;
        var to = input.ToDate ?? DateTime.UtcNow.Date;

        var query = await _invoiceRepository.GetQueryableAsync();
        var invoicesQuery = query
            .Where(pi => pi.CompanyId == input.CompanyId
                      && pi.Status == DocumentStatus.Posted
                      && pi.IssueDate >= from
                      && pi.IssueDate <= to);

        if (input.SupplierId.HasValue)
        {
            invoicesQuery = invoicesQuery.Where(pi => pi.SupplierId == input.SupplierId.Value);
        }

        if (input.SupplierGroupId.HasValue && _supplierRepository != null)
        {
            var suppQuery = await _supplierRepository.GetQueryableAsync();
            var matchedSupplierIds = suppQuery
                .Where(s => s.SupplierGroupId == input.SupplierGroupId.Value)
                .Select(s => s.Id)
                .ToList();
            invoicesQuery = invoicesQuery.Where(pi => matchedSupplierIds.Contains(pi.SupplierId));
        }

        var invoices = invoicesQuery.ToList();

        // If IncludePayments and SupplierId provided: merge opening balance, invoices, and payments (with deductions per PR #58437)
        if (input.IncludePayments && input.SupplierId.HasValue)
        {
            var supplierId = input.SupplierId.Value;

            if (input.SupplierGroupId.HasValue && _supplierRepository != null)
            {
                var supp = await _supplierRepository.FindAsync(supplierId);
                if (supp == null || supp.SupplierGroupId != input.SupplierGroupId.Value)
                {
                    return new RegisterReportDto<PurchaseRegisterLineDto>();
                }
            }
            var peQuery = await GetPaymentQueryableWithTaxesAsync();

            // Calculate opening balance before 'from' date (payables ledger: credits increase liability, debits decrease)
            var priorInvoices = query
                .Where(pi => pi.CompanyId == input.CompanyId
                          && pi.SupplierId == supplierId
                          && pi.Status == DocumentStatus.Posted
                          && pi.IssueDate < from)
                .ToList();

            var priorPayments = peQuery
                .Where(pe => pe.CompanyId == input.CompanyId
                          && pe.PartyType == "Supplier"
                          && pe.PartyId == supplierId
                          && pe.Status == DocumentStatus.Posted
                          && pe.PostingDate < from)
                .ToList();

            decimal priorCredits = priorInvoices.Sum(pi => pi.IsReturn ? 0 : pi.GrandTotal)
                + priorPayments.Where(pe => pe.PaymentType == PaymentType.Receive).Sum(pe => pe.TotalSettledBaseAmount > 0 ? pe.TotalSettledBaseAmount : pe.PaidAmount);
            decimal priorDebits = priorInvoices.Sum(pi => pi.IsReturn ? pi.GrandTotal : 0)
                + priorPayments.Where(pe => pe.PaymentType != PaymentType.Receive).Sum(pe => pe.TotalSettledBaseAmount > 0 ? pe.TotalSettledBaseAmount : pe.PaidAmount);
            decimal openingBalance = priorCredits - priorDebits;

            var periodPayments = peQuery
                .Where(pe => pe.CompanyId == input.CompanyId
                          && pe.PartyType == "Supplier"
                          && pe.PartyId == supplierId
                          && pe.Status == DocumentStatus.Posted
                          && pe.PostingDate >= from
                          && pe.PostingDate <= to)
                .ToList();

            var lines = new List<PurchaseRegisterLineDto>();

            // Opening row
            lines.Add(new PurchaseRegisterLineDto
            {
                VoucherType = "Opening",
                InvoiceNumber = "Opening Balance",
                PostingDate = from,
                SupplierId = supplierId,
                Debit = openingBalance < 0 ? Math.Abs(openingBalance) : 0,
                Credit = openingBalance > 0 ? openingBalance : 0,
                Balance = openingBalance
            });

            // Invoice rows
            foreach (var pi in invoices)
            {
                decimal debit = pi.IsReturn ? Math.Abs(pi.GrandTotal) : 0;
                decimal credit = pi.IsReturn ? 0 : pi.GrandTotal;
                lines.Add(new PurchaseRegisterLineDto
                {
                    VoucherType = pi.IsReturn ? "Debit Note" : "Purchase Invoice",
                    InvoiceId = pi.Id,
                    InvoiceNumber = pi.InvoiceNumber ?? "",
                    PostingDate = pi.IssueDate,
                    SupplierId = pi.SupplierId,
                    NetTotal = pi.NetTotal,
                    TaxAmount = pi.TaxAmount,
                    GrandTotal = pi.GrandTotal,
                    AmountPaid = pi.AmountPaid,
                    Outstanding = pi.OutstandingAmount,
                    IsReturn = pi.IsReturn,
                    Debit = debit,
                    Credit = credit,
                });
            }

            // Payment rows (including deductions per PR #58437 / commit dbe153a15e)
            foreach (var pe in periodPayments)
            {
                decimal settledAmount = pe.TotalSettledBaseAmount > 0 ? pe.TotalSettledBaseAmount : pe.PaidAmount;
                bool isRefund = pe.PaymentType == PaymentType.Receive;
                decimal debit = isRefund ? 0 : settledAmount;
                decimal credit = isRefund ? settledAmount : 0;
                lines.Add(new PurchaseRegisterLineDto
                {
                    VoucherType = isRefund ? "Refund" : "Payment Entry",
                    PaymentEntryId = pe.Id,
                    InvoiceNumber = pe.PaymentNumber ?? "PE",
                    PostingDate = pe.PostingDate,
                    SupplierId = supplierId,
                    GrandTotal = settledAmount,
                    AmountPaid = settledAmount,
                    Debit = debit,
                    Credit = credit,
                });
            }

            // Order chronologically (opening first, then by date, then by voucher type)
            var openingRow = lines[0];
            var orderedDetails = lines.Skip(1).OrderBy(l => l.PostingDate).ThenBy(l => l.VoucherType).ToList();

            decimal runningBalance = openingBalance;
            foreach (var row in orderedDetails)
            {
                runningBalance += (row.Credit - row.Debit);
                row.Balance = runningBalance;
            }

            var allItems = new List<PurchaseRegisterLineDto> { openingRow };
            allItems.AddRange(orderedDetails);

            await PopulateSupplierDetailsAsync(allItems);

            return new RegisterReportDto<PurchaseRegisterLineDto>
            {
                Count = allItems.Count,
                Items = allItems,
                TotalNet = invoices.Sum(pi => pi.NetTotal),
                TotalTax = invoices.Sum(pi => pi.TaxAmount),
                TotalGrand = invoices.Sum(pi => pi.GrandTotal),
            };
        }

        // Standard register (invoices only)
        var sortedInvoices = invoices.OrderByDescending(pi => pi.IssueDate).ToList();
        var items = sortedInvoices.Select(pi => new PurchaseRegisterLineDto
        {
            VoucherType = pi.IsReturn ? "Debit Note" : "Purchase Invoice",
            InvoiceId = pi.Id,
            InvoiceNumber = pi.InvoiceNumber ?? "",
            PostingDate = pi.IssueDate,
            SupplierId = pi.SupplierId,
            NetTotal = pi.NetTotal,
            TaxAmount = pi.TaxAmount,
            GrandTotal = pi.GrandTotal,
            AmountPaid = pi.AmountPaid,
            Outstanding = pi.OutstandingAmount,
            IsReturn = pi.IsReturn,
            Debit = pi.IsReturn ? Math.Abs(pi.GrandTotal) : 0,
            Credit = pi.IsReturn ? 0 : pi.GrandTotal,
        }).ToList();

        await PopulateSupplierDetailsAsync(items);

        return new RegisterReportDto<PurchaseRegisterLineDto>
        {
            Count = items.Count,
            Items = items,
            TotalNet = sortedInvoices.Sum(pi => pi.NetTotal),
            TotalTax = sortedInvoices.Sum(pi => pi.TaxAmount),
            TotalGrand = sortedInvoices.Sum(pi => pi.GrandTotal),
        };
    }

    private async Task PopulateSupplierDetailsAsync(List<PurchaseRegisterLineDto> lines)
    {
        if (_supplierRepository == null || lines.Count == 0) return;

        var supplierIds = lines.Select(l => l.SupplierId).Distinct().ToList();
        var supplierQuery = await _supplierRepository.GetQueryableAsync();
        var suppliers = supplierQuery.Where(s => supplierIds.Contains(s.Id)).ToList();
        var supplierMap = suppliers.ToDictionary(s => s.Id);

        var groupIds = suppliers.Where(s => s.SupplierGroupId.HasValue).Select(s => s.SupplierGroupId!.Value).Distinct().ToList();
        Dictionary<Guid, string>? groupMap = null;
        if (_supplierGroupRepository != null && groupIds.Count > 0)
        {
            var groupQuery = await _supplierGroupRepository.GetQueryableAsync();
            groupMap = groupQuery.Where(g => groupIds.Contains(g.Id)).ToDictionary(g => g.Id, g => g.Name);
        }

        foreach (var line in lines)
        {
            if (supplierMap.TryGetValue(line.SupplierId, out var supplier))
            {
                line.SupplierName = supplier.Name;
                line.SupplierGroupId = supplier.SupplierGroupId;
                if (supplier.SupplierGroupId.HasValue && groupMap != null && groupMap.TryGetValue(supplier.SupplierGroupId.Value, out var groupName))
                {
                    line.SupplierGroupName = groupName;
                }
            }
        }
    }
}
