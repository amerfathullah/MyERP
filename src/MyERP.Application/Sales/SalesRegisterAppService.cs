using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
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
    private readonly IRepository<Customer, Guid>? _customerRepository;
    private readonly IRepository<CustomerGroup, Guid>? _customerGroupRepository;

    public SalesRegisterAppService(
        IRepository<SalesInvoice, Guid> invoiceRepository,
        IRepository<PaymentEntry, Guid> paymentRepository,
        IRepository<Customer, Guid>? customerRepository = null,
        IRepository<CustomerGroup, Guid>? customerGroupRepository = null)
    {
        _invoiceRepository = invoiceRepository;
        _paymentRepository = paymentRepository;
        _customerRepository = customerRepository;
        _customerGroupRepository = customerGroupRepository;
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

        if (input.CustomerGroupId.HasValue && _customerRepository != null)
        {
            var custQuery = await _customerRepository.GetQueryableAsync();
            var matchedCustomerIds = custQuery
                .Where(c => c.CustomerGroupId == input.CustomerGroupId.Value)
                .Select(c => c.Id)
                .ToList();
            invoicesQuery = invoicesQuery.Where(si => matchedCustomerIds.Contains(si.CustomerId));
        }

        var invoices = invoicesQuery.ToList();

        // If IncludePayments and CustomerId provided: merge opening balance, invoices, and payments (with deductions per PR #58437)
        if (input.IncludePayments && input.CustomerId.HasValue)
        {
            var customerId = input.CustomerId.Value;

            if (input.CustomerGroupId.HasValue && _customerRepository != null)
            {
                var cust = await _customerRepository.FindAsync(customerId);
                if (cust == null || cust.CustomerGroupId != input.CustomerGroupId.Value)
                {
                    return new RegisterReportDto<SalesRegisterLineDto>();
                }
            }
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
            decimal priorCredits = priorInvoices.Sum(si => (si.IsReturn ? si.GrandTotal : 0) + GetInInvoiceReceivableCredit(si))
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

            // Invoice rows (per ERPNext PR #57927 / commit 40c356d166: include in-invoice settlements)
            foreach (var si in invoices)
            {
                decimal inInvoiceCredit = GetInInvoiceReceivableCredit(si);
                decimal debit = si.IsReturn ? 0 : si.GrandTotal;
                decimal credit = (si.IsReturn ? Math.Abs(si.GrandTotal) : 0) + inInvoiceCredit;
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

            await PopulateCustomerDetailsAsync(allItems);

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
        var items = sortedInvoices.Select(si => new SalesRegisterLineDto
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
            Credit = (si.IsReturn ? Math.Abs(si.GrandTotal) : 0) + GetInInvoiceReceivableCredit(si),
        }).ToList();

        await PopulateCustomerDetailsAsync(items);

        return new RegisterReportDto<SalesRegisterLineDto>
        {
            Count = items.Count,
            Items = items,
            TotalNet = sortedInvoices.Sum(si => si.NetTotal),
            TotalTax = sortedInvoices.Sum(si => si.TaxAmount),
            TotalGrand = sortedInvoices.Sum(si => si.GrandTotal),
        };
    }

    private async Task PopulateCustomerDetailsAsync(List<SalesRegisterLineDto> lines)
    {
        if (_customerRepository == null || lines.Count == 0) return;

        var customerIds = lines.Select(l => l.CustomerId).Distinct().ToList();
        var custQuery = await _customerRepository.GetQueryableAsync();
        var customers = custQuery.Where(c => customerIds.Contains(c.Id)).ToList();
        var customerMap = customers.ToDictionary(c => c.Id);

        var groupIds = customers.Where(c => c.CustomerGroupId.HasValue).Select(c => c.CustomerGroupId!.Value).Distinct().ToList();
        Dictionary<Guid, string>? groupMap = null;
        if (_customerGroupRepository != null && groupIds.Count > 0)
        {
            var groupQuery = await _customerGroupRepository.GetQueryableAsync();
            groupMap = groupQuery.Where(g => groupIds.Contains(g.Id)).ToDictionary(g => g.Id, g => g.Name);
        }

        foreach (var line in lines)
        {
            if (customerMap.TryGetValue(line.CustomerId, out var customer))
            {
                line.CustomerName = customer.Name;
                line.CustomerGroupId = customer.CustomerGroupId;
                if (customer.CustomerGroupId.HasValue && groupMap != null && groupMap.TryGetValue(customer.CustomerGroupId.Value, out var groupName))
                {
                    line.CustomerGroupName = groupName;
                }
            }
        }
    }

    /// <summary>
    /// Credits that the invoice itself posts to the receivable (mirrors its GL entries).
    /// Per ERPNext PR #57927 / commit 40c356d166:
    /// - Loyalty redemption credits the receivable on all invoices.
    /// - POS payments and write-offs credit the receivable on POS invoices.
    /// </summary>
    private static decimal GetInInvoiceReceivableCredit(SalesInvoice si)
    {
        decimal credit = si.LoyaltyRedemptionAmount;
        if (si.IsPos)
        {
            credit += si.AmountPaid + si.WriteOffAmount;
        }
        return credit;
    }
}
