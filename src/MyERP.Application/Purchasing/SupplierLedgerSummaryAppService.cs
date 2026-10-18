using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Purchasing;

[Authorize(MyERPPermissions.PurchaseInvoices.Default)]
public class SupplierLedgerSummaryAppService : ApplicationService, ISupplierLedgerSummaryAppService
{
    private readonly IRepository<Supplier, Guid> _supplierRepo;
    private readonly IRepository<PurchaseInvoice, Guid> _invoiceRepo;
    private readonly IRepository<PaymentEntry, Guid> _paymentRepo;
    private readonly IRepository<JournalEntry, Guid> _journalRepo;
    private readonly IRepository<Company, Guid> _companyRepo;

    public SupplierLedgerSummaryAppService(
        IRepository<Supplier, Guid> supplierRepo,
        IRepository<PurchaseInvoice, Guid> invoiceRepo,
        IRepository<PaymentEntry, Guid> paymentRepo,
        IRepository<JournalEntry, Guid> journalRepo,
        IRepository<Company, Guid> companyRepo)
    {
        _supplierRepo = supplierRepo;
        _invoiceRepo = invoiceRepo;
        _paymentRepo = paymentRepo;
        _journalRepo = journalRepo;
        _companyRepo = companyRepo;
    }

    public async Task<SupplierLedgerSummaryReportDto> GetReportAsync(SupplierLedgerSummaryRequestDto input)
    {
        var company = await _companyRepo.FindAsync(input.CompanyId);
        var companyCurrency = company?.CurrencyCode ?? "MYR";

        var today = DateTime.Today;
        var fromDate = input.FromDate?.Date ?? new DateTime(today.Year, today.Month, 1).AddMonths(-1);
        var toDate = input.ToDate?.Date.AddDays(1).AddTicks(-1) ?? today.AddDays(1).AddTicks(-1);

        // 1. Load Suppliers
        var supQuery = await _supplierRepo.GetQueryableAsync();
        supQuery = supQuery.Where(s => s.CompanyId == input.CompanyId);

        if (input.SupplierId.HasValue)
        {
            supQuery = supQuery.Where(s => s.Id == input.SupplierId.Value);
        }
        if (input.SupplierGroupId.HasValue)
        {
            supQuery = supQuery.Where(s => s.SupplierGroupId == input.SupplierGroupId.Value);
        }

        var suppliers = supQuery.ToList();
        if (!suppliers.Any())
        {
            return new SupplierLedgerSummaryReportDto
            {
                CompanyCurrency = companyCurrency,
                FromDate = fromDate,
                ToDate = toDate,
                Rows = new List<SupplierLedgerSummaryRowDto>(),
                TotalRow = new SupplierLedgerSummaryRowDto { Currency = companyCurrency }
            };
        }

        var supplierIds = suppliers.Select(s => s.Id).ToHashSet();

        // 2. Load Supplier Groups for naming
        var groupIds = suppliers.Where(s => s.SupplierGroupId.HasValue).Select(s => s.SupplierGroupId!.Value).Distinct().ToList();
        var groupDict = new Dictionary<Guid, string>();
        if (groupIds.Any())
        {
            var groupRepo = LazyServiceProvider.LazyGetService<IRepository<SupplierGroup, Guid>>();
            if (groupRepo != null)
            {
                var gQuery = await groupRepo.GetQueryableAsync();
                groupDict = gQuery.Where(g => groupIds.Contains(g.Id)).ToDictionary(g => g.Id, g => g.Name);
            }
        }

        // 3. Load Invoices up to toDate
        var invQuery = await _invoiceRepo.GetQueryableAsync();
        var invoices = invQuery
            .Where(i => i.CompanyId == input.CompanyId
                        && i.Status != DocumentStatus.Draft
                        && i.Status != DocumentStatus.Cancelled
                        && supplierIds.Contains(i.SupplierId)
                        && i.IssueDate <= toDate)
            .ToList();

        // 4. Load Payments up to toDate
        var payQuery = await _paymentRepo.GetQueryableAsync();
        var payments = payQuery
            .Where(p => p.CompanyId == input.CompanyId
                        && p.PartyType == "Supplier"
                        && p.PartyId.HasValue
                        && supplierIds.Contains(p.PartyId.Value)
                        && p.Status != DocumentStatus.Draft
                        && p.Status != DocumentStatus.Cancelled
                        && p.PostingDate <= toDate)
            .ToList();

        // 5. Load Manual Journal Entry lines up to toDate
        var jeQuery = await _journalRepo.WithDetailsAsync(j => j.Lines);
        var journals = jeQuery
            .Where(j => j.CompanyId == input.CompanyId
                        && j.Status == DocumentStatus.Posted
                        && (j.ReferenceType == null || j.ReferenceType == "JournalEntry")
                        && j.PostingDate <= toDate)
            .ToList();

        var flatJeLines = journals
            .SelectMany(j => j.Lines.Select(l => new { j.PostingDate, Line = l }))
            .Where(x => x.Line.PartyType == "Supplier" && x.Line.PartyId.HasValue && supplierIds.Contains(x.Line.PartyId.Value))
            .ToList();

        // 6. Aggregate by Supplier
        var rows = new List<SupplierLedgerSummaryRowDto>();

        foreach (var supplier in suppliers)
        {
            var sId = supplier.Id;

            // Invoices: opening vs in-period
            var priorInvoices = invoices.Where(i => i.SupplierId == sId && i.IssueDate < fromDate).ToList();
            var periodInvoices = invoices.Where(i => i.SupplierId == sId && i.IssueDate >= fromDate && i.IssueDate <= toDate).ToList();

            decimal openingBalance = 0m;
            foreach (var inv in priorInvoices)
            {
                var amt = Math.Abs(inv.BaseGrandTotal != 0 ? inv.BaseGrandTotal : inv.GrandTotal);
                // For supplier (payable): normal bill increases credit (+), debit note decreases credit (-)
                openingBalance += (inv.IsReturn || inv.IsDebitNote) ? -amt : amt;
            }

            decimal invoicedAmount = 0m;
            decimal returnAmount = 0m;
            foreach (var inv in periodInvoices)
            {
                var amt = Math.Abs(inv.BaseGrandTotal != 0 ? inv.BaseGrandTotal : inv.GrandTotal);
                if (inv.IsReturn || inv.IsDebitNote)
                {
                    returnAmount += amt;
                }
                else
                {
                    invoicedAmount += amt;
                }
            }

            // Payments: opening vs in-period
            var priorPayments = payments.Where(p => p.PartyId == sId && p.PostingDate < fromDate).ToList();
            var periodPayments = payments.Where(p => p.PartyId == sId && p.PostingDate >= fromDate && p.PostingDate <= toDate).ToList();

            foreach (var pay in priorPayments)
            {
                var rate = pay.ExchangeRate > 0 ? pay.ExchangeRate : 1m;
                var amt = pay.PaidAmount * rate;
                // Pay reduces supplier payable credit (-); Receive (refund) increases supplier payable credit (+)
                openingBalance += pay.PaymentType == PaymentType.Pay ? -amt : amt;
            }

            decimal paidAmount = 0m;
            foreach (var pay in periodPayments)
            {
                var rate = pay.ExchangeRate > 0 ? pay.ExchangeRate : 1m;
                var amt = pay.PaidAmount * rate;
                if (pay.PaymentType == PaymentType.Pay)
                {
                    paidAmount += amt;
                }
                else
                {
                    paidAmount -= amt;
                }
            }

            // Manual Journal entries
            var priorJeLines = flatJeLines.Where(x => x.Line.PartyId == sId && x.PostingDate < fromDate).ToList();
            var periodJeLines = flatJeLines.Where(x => x.Line.PartyId == sId && x.PostingDate >= fromDate && x.PostingDate <= toDate).ToList();

            foreach (var jl in priorJeLines)
            {
                // Debit decreases payable (-); Credit increases payable (+)
                openingBalance += !jl.Line.IsDebit ? jl.Line.Amount : -jl.Line.Amount;
            }

            decimal adjustmentAmount = 0m;
            foreach (var jl in periodJeLines)
            {
                // Debit decreases payable (-); Credit increases payable (+)
                adjustmentAmount += !jl.Line.IsDebit ? jl.Line.Amount : -jl.Line.Amount;
            }

            var closingBalance = openingBalance + invoicedAmount - paidAmount - returnAmount + adjustmentAmount;

            // Only include suppliers with activity or balance (matching ERPNext behavior)
            if (openingBalance == 0m && invoicedAmount == 0m && paidAmount == 0m && returnAmount == 0m && adjustmentAmount == 0m && closingBalance == 0m)
            {
                continue;
            }

            string? groupName = null;
            if (supplier.SupplierGroupId.HasValue) groupDict.TryGetValue(supplier.SupplierGroupId.Value, out groupName);

            // Payable convention: positive balance is Credit (we owe supplier), negative balance is Debit (advance/prepayment)
            var drOrCr = closingBalance > 0.005m ? "Cr" : (closingBalance < -0.005m ? "Dr" : string.Empty);

            rows.Add(new SupplierLedgerSummaryRowDto
            {
                SupplierId = supplier.Id,
                SupplierCode = supplier.SupplierCode ?? supplier.Name,
                SupplierName = supplier.Name,
                SupplierGroupName = groupName,
                OpeningBalance = openingBalance,
                InvoicedAmount = invoicedAmount,
                PaidAmount = paidAmount,
                ReturnAmount = returnAmount,
                AdjustmentAmount = adjustmentAmount,
                ClosingBalance = closingBalance,
                DrOrCr = drOrCr,
                Currency = supplier.DefaultCurrency ?? companyCurrency
            });
        }

        // Sort by SupplierName
        rows = rows.OrderBy(r => r.SupplierName).ToList();

        // 7. Calculate Totals
        var totalOpening = rows.Sum(r => r.OpeningBalance);
        var totalInvoiced = rows.Sum(r => r.InvoicedAmount);
        var totalPaid = rows.Sum(r => r.PaidAmount);
        var totalReturn = rows.Sum(r => r.ReturnAmount);
        var totalAdjustment = rows.Sum(r => r.AdjustmentAmount);
        var totalClosing = rows.Sum(r => r.ClosingBalance);
        var totalDrOrCr = totalClosing > 0.005m ? "Cr" : (totalClosing < -0.005m ? "Dr" : string.Empty);

        var totalRow = new SupplierLedgerSummaryRowDto
        {
            SupplierName = "Total",
            OpeningBalance = totalOpening,
            InvoicedAmount = totalInvoiced,
            PaidAmount = totalPaid,
            ReturnAmount = totalReturn,
            AdjustmentAmount = totalAdjustment,
            ClosingBalance = totalClosing,
            DrOrCr = totalDrOrCr,
            Currency = companyCurrency
        };

        return new SupplierLedgerSummaryReportDto
        {
            CompanyCurrency = companyCurrency,
            FromDate = fromDate,
            ToDate = toDate,
            Rows = rows,
            TotalRow = totalRow
        };
    }
}
