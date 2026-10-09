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
    private readonly IRepository<JournalEntry, Guid>? _journalRepository;

    public SalesRegisterAppService(
        IRepository<SalesInvoice, Guid> invoiceRepository,
        IRepository<PaymentEntry, Guid> paymentRepository,
        IRepository<Customer, Guid>? customerRepository = null,
        IRepository<CustomerGroup, Guid>? customerGroupRepository = null,
        IRepository<JournalEntry, Guid>? journalRepository = null)
    {
        _invoiceRepository = invoiceRepository;
        _paymentRepository = paymentRepository;
        _customerRepository = customerRepository;
        _customerGroupRepository = customerGroupRepository;
        _journalRepository = journalRepository;
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

    private async Task<IQueryable<JournalEntry>> GetJournalQueryableWithLinesAsync()
    {
        if (_journalRepository == null) return Enumerable.Empty<JournalEntry>().AsQueryable();
        try
        {
            var withDetails = await _journalRepository.WithDetailsAsync(j => j.Lines);
            if (withDetails != null) return withDetails;
        }
        catch
        {
            // Fallback if WithDetailsAsync is not configured or unsupported in mock/provider
        }
        return await _journalRepository.GetQueryableAsync();
    }

    private async Task<HashSet<Guid>> GetCustomerGroupWithDescendantIdsAsync(Guid rootGroupId)
    {
        var result = new HashSet<Guid> { rootGroupId };
        if (_customerGroupRepository == null) return result;

        try
        {
            var allGroups = await _customerGroupRepository.GetListAsync();
            if (allGroups != null)
            {
                var queue = new Queue<Guid>();
                queue.Enqueue(rootGroupId);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    var children = allGroups.Where(g => g.ParentId == current).Select(g => g.Id);
                    foreach (var childId in children)
                    {
                        if (result.Add(childId))
                        {
                            queue.Enqueue(childId);
                        }
                    }
                }
            }
        }
        catch
        {
            // Fallback if repository fails or in test setup
        }

        return result;
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
            var targetGroupIds = await GetCustomerGroupWithDescendantIdsAsync(input.CustomerGroupId.Value);
            var custQuery = await _customerRepository.GetQueryableAsync();
            var matchedCustomerIds = custQuery
                .Where(c => c.CustomerGroupId.HasValue && targetGroupIds.Contains(c.CustomerGroupId.Value))
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
                var targetGroupIds = await GetCustomerGroupWithDescendantIdsAsync(input.CustomerGroupId.Value);
                var cust = await _customerRepository.FindAsync(customerId);
                if (cust == null || !cust.CustomerGroupId.HasValue || !targetGroupIds.Contains(cust.CustomerGroupId.Value))
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

            decimal priorDebits = priorInvoices.Sum(si => si.IsReturn ? 0 : GetReceivableDebit(si))
                + priorPayments.Where(pe => pe.PaymentType == PaymentType.Pay).Sum(pe => pe.TotalSettledBaseAmount > 0 ? pe.TotalSettledBaseAmount : pe.PaidAmount);
            decimal priorCredits = priorInvoices.Sum(si => (si.IsReturn ? GetReceivableCreditForReturn(si) : 0) + GetInInvoiceReceivableCredit(si))
                + priorPayments.Where(pe => pe.PaymentType != PaymentType.Pay).Sum(pe => pe.TotalSettledBaseAmount > 0 ? pe.TotalSettledBaseAmount : pe.PaidAmount);

            // Journal Entry rows prior to 'from' date (per ERPNext PR #59888 / commit 4f95812874)
            if (_journalRepository != null)
            {
                var jeQuery = await GetJournalQueryableWithLinesAsync();
                var priorJournals = jeQuery
                    .Where(j => j.CompanyId == input.CompanyId
                             && j.Status == DocumentStatus.Posted
                             && j.PostingDate < from
                             && j.Lines.Any(l => l.PartyType == "Customer" && l.PartyId == customerId))
                    .ToList();

                foreach (var je in priorJournals)
                {
                    foreach (var line in je.Lines.Where(l => l.PartyType == "Customer" && l.PartyId == customerId))
                    {
                        priorDebits += line.Debit;
                        priorCredits += line.Credit;
                    }
                }
            }

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
                decimal debit = si.IsReturn ? 0 : GetReceivableDebit(si);
                decimal credit = (si.IsReturn ? GetReceivableCreditForReturn(si) : 0) + inInvoiceCredit;
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

            // Journal Entry rows in period (per ERPNext PR #59888 / commits 4f95812874 & fd1c361cfc)
            if (_journalRepository != null)
            {
                var jeQuery = await GetJournalQueryableWithLinesAsync();
                var periodJournals = jeQuery
                    .Where(j => j.CompanyId == input.CompanyId
                             && j.Status == DocumentStatus.Posted
                             && j.PostingDate >= from
                             && j.PostingDate <= to
                             && j.Lines.Any(l => l.PartyType == "Customer" && l.PartyId == customerId))
                    .ToList();

                foreach (var je in periodJournals)
                {
                    foreach (var line in je.Lines.Where(l => l.PartyType == "Customer" && l.PartyId == customerId))
                    {
                        lines.Add(new SalesRegisterLineDto
                        {
                            VoucherType = "Journal Entry",
                            JournalEntryId = je.Id,
                            InvoiceNumber = je.EntryNumber ?? "JE",
                            PostingDate = je.PostingDate,
                            CustomerId = customerId,
                            GrandTotal = line.Debit > 0 ? line.Debit : line.Credit,
                            Debit = line.Debit,
                            Credit = line.Credit,
                        });
                    }
                }
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
            Debit = si.IsReturn ? 0 : GetReceivableDebit(si),
            Credit = (si.IsReturn ? GetReceivableCreditForReturn(si) : 0) + GetInInvoiceReceivableCredit(si),
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

    private IDisposable? TryDisableSoftDelete()
    {
        try
        {
            return LazyServiceProvider?.LazyGetService<Volo.Abp.Data.IDataFilter>()?.Disable<Volo.Abp.ISoftDelete>();
        }
        catch
        {
            return null;
        }
    }

    private async Task PopulateCustomerDetailsAsync(List<SalesRegisterLineDto> lines)
    {
        if (lines.Count == 0) return;

        Dictionary<Guid, Customer> customerMap = new();
        Dictionary<Guid, string>? groupMap = null;

        if (_customerRepository != null)
        {
            using (TryDisableSoftDelete())
            {
                var customerIds = lines.Select(l => l.CustomerId).Where(id => id != Guid.Empty).Distinct().ToList();
                if (customerIds.Count > 0)
                {
                    var custQuery = await _customerRepository.GetQueryableAsync();
                    var customers = custQuery.Where(c => customerIds.Contains(c.Id)).ToList();
                    customerMap = customers.ToDictionary(c => c.Id);

                    var groupIds = customers.Where(c => c.CustomerGroupId.HasValue).Select(c => c.CustomerGroupId!.Value).Distinct().ToList();
                    if (_customerGroupRepository != null && groupIds.Count > 0)
                    {
                        var groupQuery = await _customerGroupRepository.GetQueryableAsync();
                        groupMap = groupQuery.Where(g => groupIds.Contains(g.Id)).ToDictionary(g => g.Id, g => g.Name);
                    }
                }
            }
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
            else
            {
                // Per ERPNext PR #59624 (commits e591af5f58, ec9e9863fb): guard against missing customer and preserve existing name/group
                line.CustomerName = !string.IsNullOrWhiteSpace(line.CustomerName)
                    ? line.CustomerName
                    : (line.CustomerId != Guid.Empty ? line.CustomerId.ToString() : string.Empty);
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

    /// <summary>
    /// Amount the invoice debits to its receivable, rounded like its GL entry.
    /// Per ERPNext PR #59930 / commit 1ae23fadef: debit the rounded total only when a rounding adjustment was posted.
    /// </summary>
    private static decimal GetReceivableDebit(SalesInvoice inv)
    {
        if (inv.BaseRoundingAdjustment != 0 && inv.BaseRoundedTotal != 0)
        {
            return inv.BaseRoundedTotal;
        }

        return inv.BaseGrandTotal != 0 ? inv.BaseGrandTotal : inv.GrandTotal;
    }

    private static decimal GetReceivableCreditForReturn(SalesInvoice inv)
    {
        if (inv.BaseRoundingAdjustment != 0 && inv.BaseRoundedTotal != 0)
        {
            return Math.Abs(inv.BaseRoundedTotal);
        }

        return Math.Abs(inv.BaseGrandTotal != 0 ? inv.BaseGrandTotal : inv.GrandTotal);
    }
}
