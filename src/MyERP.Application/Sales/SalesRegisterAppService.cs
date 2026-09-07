using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

[Authorize(MyERPPermissions.SalesInvoices.Default)]
public class SalesRegisterAppService : ApplicationService, ISalesRegisterAppService
{
    private readonly IRepository<SalesInvoice, Guid> _invoiceRepository;
    private readonly IRepository<PaymentEntry, Guid> _paymentRepository;

    public SalesRegisterAppService(
        IRepository<SalesInvoice, Guid> invoiceRepository,
        IRepository<PaymentEntry, Guid> paymentRepository)
    {
        _invoiceRepository = invoiceRepository;
        _paymentRepository = paymentRepository;
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

    public async Task<RegisterReportDto<SalesRegisterLineDto>> GetReportAsync(RegisterFilterDto input)
    {
        var from = input.FromDate ?? DateTime.UtcNow.AddMonths(-1).Date;
        var to = input.ToDate ?? DateTime.UtcNow.Date;

        var query = await _invoiceRepository.GetQueryableAsync();
        var invoicesQuery = query
            .Where(si => si.CompanyId == input.CompanyId
                      && si.Status == DocumentStatus.Posted
                      && si.IssueDate >= from
                      && si.IssueDate <= to);

        if (input.CustomerId.HasValue)
        {
            invoicesQuery = invoicesQuery.Where(si => si.CustomerId == input.CustomerId.Value);
        }

        var invoices = invoicesQuery.ToList();

        // If IncludePayments and CustomerId provided: merge opening balance, invoices, and payments (with deductions per PR #58437)
        if (input.IncludePayments && input.CustomerId.HasValue)
        {
            var customerId = input.CustomerId.Value;
            var peQuery = await GetPaymentQueryableWithTaxesAsync();

            // Calculate opening balance before 'from' date
            var priorInvoices = query
                .Where(si => si.CompanyId == input.CompanyId
                          && si.CustomerId == customerId
                          && si.Status == DocumentStatus.Posted
                          && si.IssueDate < from)
                .ToList();

            var priorPayments = peQuery
                .Where(pe => pe.CompanyId == input.CompanyId
                          && pe.PartyType == "Customer"
                          && pe.PartyId == customerId
                          && pe.Status == DocumentStatus.Posted
                          && pe.PostingDate < from)
                .ToList();

            decimal priorDebits = priorInvoices.Sum(si => si.IsReturn ? 0 : si.GrandTotal)
                + priorPayments.Where(pe => pe.PaymentType == PaymentType.Pay).Sum(pe => pe.TotalSettledBaseAmount > 0 ? pe.TotalSettledBaseAmount : pe.PaidAmount);
            decimal priorCredits = priorInvoices.Sum(si => si.IsReturn ? si.GrandTotal : 0)
                + priorPayments.Where(pe => pe.PaymentType != PaymentType.Pay).Sum(pe => pe.TotalSettledBaseAmount > 0 ? pe.TotalSettledBaseAmount : pe.PaidAmount);
            decimal openingBalance = priorDebits - priorCredits;

            var periodPayments = peQuery
                .Where(pe => pe.CompanyId == input.CompanyId
                          && pe.PartyType == "Customer"
                          && pe.PartyId == customerId
                          && pe.Status == DocumentStatus.Posted
                          && pe.PostingDate >= from
                          && pe.PostingDate <= to)
                .ToList();

            var lines = new List<SalesRegisterLineDto>();

            // Opening row
            lines.Add(new SalesRegisterLineDto
            {
                VoucherType = "Opening",
                InvoiceNumber = "Opening Balance",
                PostingDate = from,
                CustomerId = customerId,
                Debit = openingBalance > 0 ? openingBalance : 0,
                Credit = openingBalance < 0 ? Math.Abs(openingBalance) : 0,
                Balance = openingBalance
            });

            // Invoice rows
            foreach (var si in invoices)
            {
                decimal debit = si.IsReturn ? 0 : si.GrandTotal;
                decimal credit = si.IsReturn ? Math.Abs(si.GrandTotal) : 0;
                lines.Add(new SalesRegisterLineDto
                {
                    VoucherType = si.IsReturn ? "Credit Note" : "Sales Invoice",
                    InvoiceId = si.Id,
                    InvoiceNumber = si.InvoiceNumber ?? "",
                    PostingDate = si.IssueDate,
                    CustomerId = si.CustomerId,
                    NetTotal = si.NetTotal,
                    TaxAmount = si.TaxAmount,
                    GrandTotal = si.GrandTotal,
                    AmountPaid = si.AmountPaid,
                    Outstanding = si.OutstandingAmount,
                    IsReturn = si.IsReturn,
                    Debit = debit,
                    Credit = credit,
                });
            }

            // Payment rows (including deductions per PR #58437 / commit dbe153a15e)
            foreach (var pe in periodPayments)
            {
                decimal settledAmount = pe.TotalSettledBaseAmount > 0 ? pe.TotalSettledBaseAmount : pe.PaidAmount;
                bool isRefund = pe.PaymentType == PaymentType.Pay;
                decimal debit = isRefund ? settledAmount : 0;
                decimal credit = isRefund ? 0 : settledAmount;
                lines.Add(new SalesRegisterLineDto
                {
                    VoucherType = isRefund ? "Refund" : "Payment Entry",
                    PaymentEntryId = pe.Id,
                    InvoiceNumber = pe.PaymentNumber ?? "PE",
                    PostingDate = pe.PostingDate,
                    CustomerId = customerId,
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
                runningBalance += (row.Debit - row.Credit);
                row.Balance = runningBalance;
            }

            var allItems = new List<SalesRegisterLineDto> { openingRow };
            allItems.AddRange(orderedDetails);

            return new RegisterReportDto<SalesRegisterLineDto>
            {
                Count = allItems.Count,
                Items = allItems,
                TotalNet = invoices.Sum(si => si.NetTotal),
                TotalTax = invoices.Sum(si => si.TaxAmount),
                TotalGrand = invoices.Sum(si => si.GrandTotal),
            };
        }

        // Standard register (invoices only)
        var sortedInvoices = invoices.OrderByDescending(si => si.IssueDate).ToList();
        return new RegisterReportDto<SalesRegisterLineDto>
        {
            Count = sortedInvoices.Count,
            Items = sortedInvoices.Select(si => new SalesRegisterLineDto
            {
                VoucherType = si.IsReturn ? "Credit Note" : "Sales Invoice",
                InvoiceId = si.Id,
                InvoiceNumber = si.InvoiceNumber ?? "",
                PostingDate = si.IssueDate,
                CustomerId = si.CustomerId,
                NetTotal = si.NetTotal,
                TaxAmount = si.TaxAmount,
                GrandTotal = si.GrandTotal,
                AmountPaid = si.AmountPaid,
                Outstanding = si.OutstandingAmount,
                IsReturn = si.IsReturn,
                Debit = si.IsReturn ? 0 : si.GrandTotal,
                Credit = si.IsReturn ? Math.Abs(si.GrandTotal) : 0,
            }).ToList(),
            TotalNet = sortedInvoices.Sum(si => si.NetTotal),
            TotalTax = sortedInvoices.Sum(si => si.TaxAmount),
            TotalGrand = sortedInvoices.Sum(si => si.GrandTotal),
        };
    }
}
