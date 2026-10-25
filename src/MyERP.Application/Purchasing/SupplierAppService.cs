using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Purchasing;

public class SupplierAppService :
    CrudAppService<
        Supplier,
        SupplierDto,
        Guid,
        GetSupplierListDto,
        CreateUpdateSupplierDto>,
    ISupplierAppService
{
    public SupplierAppService(IRepository<Supplier, Guid> repository)
        : base(repository)
    {
        GetPolicyName = MyERPPermissions.Suppliers.Default;
        GetListPolicyName = MyERPPermissions.Suppliers.Default;
        CreatePolicyName = MyERPPermissions.Suppliers.Create;
        UpdatePolicyName = MyERPPermissions.Suppliers.Edit;
        DeletePolicyName = MyERPPermissions.Suppliers.Delete;
    }

    /// <summary>
    /// Prevent deletion of suppliers with active orders or posted invoices.
    /// </summary>
    public override async Task DeleteAsync(Guid id)
    {
        var poRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PurchaseOrder, Guid>>();
        var poQuery = await poRepo.GetQueryableAsync();
        var hasActiveOrders = poQuery.Any(po =>
            po.SupplierId == id
            && po.Status != DocumentStatus.Draft
            && po.Status != DocumentStatus.Cancelled);

        if (hasActiveOrders)
        {
            throw new BusinessException("MyERP:04008")
                .WithData("partyType", "Supplier")
                .WithData("reason", "Supplier has active Purchase Orders.");
        }

        var piRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PurchaseInvoice, Guid>>();
        var piQuery = await piRepo.GetQueryableAsync();
        var hasPostedInvoices = piQuery.Any(pi =>
            pi.SupplierId == id
            && (pi.Status == DocumentStatus.Posted || pi.Status == DocumentStatus.Submitted));

        if (hasPostedInvoices)
        {
            throw new BusinessException("MyERP:04008")
                .WithData("partyType", "Supplier")
                .WithData("reason", "Supplier has posted Purchase Invoices.");
        }

        await base.DeleteAsync(id);
    }

    public override async Task<PagedResultDto<SupplierDto>> GetListAsync(GetSupplierListDto input)
    {
        var filter = input.Filter;

        if (string.IsNullOrWhiteSpace(filter))
        {
            return await base.GetListAsync(input);
        }

        var queryable = await Repository.GetQueryableAsync();

        queryable = queryable.Where(s =>
            s.Name.Contains(filter)
            || (s.SupplierCode != null && s.SupplierCode.Contains(filter))
            || (s.Tin != null && s.Tin.Contains(filter)));

        var totalCount = queryable.Count();
        var items = queryable
            .OrderBy(s => s.Name)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        return new PagedResultDto<SupplierDto>(
            totalCount,
            items.Select(ObjectMapper.Map<Supplier, SupplierDto>).ToList());
    }

    public override async Task<SupplierDto> CreateAsync(CreateUpdateSupplierDto input)
    {
        await ValidateSupplierAsync(input);
        return await base.CreateAsync(input);
    }

    public override async Task<SupplierDto> UpdateAsync(Guid id, CreateUpdateSupplierDto input)
    {
        await ValidateSupplierAsync(input, id);
        return await base.UpdateAsync(id, input);
    }

    private async Task ValidateSupplierAsync(CreateUpdateSupplierDto input, Guid? currentId = null)
    {
        if (input.RepresentsCompanyId.HasValue)
        {
            if (input.RepresentsCompanyId.Value == input.CompanyId)
            {
                throw new BusinessException(MyERPDomainErrorCodes.PartyCannotRepresentOwnCompany);
            }

            if (input.IsActive)
            {
                var suppQuery = await Repository.GetQueryableAsync();
                var existing = suppQuery.FirstOrDefault(s =>
                    s.RepresentsCompanyId == input.RepresentsCompanyId.Value
                    && s.IsActive
                    && (!currentId.HasValue || s.Id != currentId.Value));

                if (existing != null)
                {
                    var companyRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Core.Entities.Company, Guid>>();
                    var company = await companyRepo.FindAsync(input.RepresentsCompanyId.Value);
                    var companyName = company?.Name ?? input.RepresentsCompanyId.Value.ToString();

                    throw new BusinessException(MyERPDomainErrorCodes.InternalPartyAlreadyExists)
                        .WithData("partyType", "Supplier")
                        .WithData("partyName", existing.Name)
                        .WithData("companyName", companyName);
                }
            }
        }

        // ERPNext validate_account_head: the party account must belong to the supplier's company
        // and be a postable (non-group, enabled) ledger.
        if (input.DefaultPayableAccountId.HasValue)
        {
            var accountRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Accounting.Entities.Account, Guid>>();
            var account = await accountRepo.GetAsync(input.DefaultPayableAccountId.Value);
            if (account.CompanyId != input.CompanyId)
                throw new BusinessException(MyERPDomainErrorCodes.CompanyMismatch);
            if (account.IsGroup)
                throw new BusinessException(MyERPDomainErrorCodes.AccountIsGroup);
            if (!account.IsActive)
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Account {account.AccountName} is disabled.");
        }

        var groupRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Core.Entities.SupplierGroup, Guid>>();
        var query = await groupRepo.GetQueryableAsync();
        var trimmed = input.Name.Trim();
        var groupExists = query.Any(g => g.Name.ToLower() == trimmed.ToLower());
        if (groupExists)
        {
            throw new BusinessException(MyERPDomainErrorCodes.SupplierNameCannotMatchSupplierGroup)
                .WithData("name", input.Name);
        }

        // Per ERPNext PR #61829: prevent selection of group type supplier group in supplier master
        if (input.SupplierGroupId.HasValue)
        {
            var supplierGroup = query.FirstOrDefault(g => g.Id == input.SupplierGroupId.Value);
            if (supplierGroup != null && supplierGroup.IsGroup)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", "Cannot select a Group type Supplier Group. Please select a non-group Supplier Group.");
            }
        }
    }

    protected override Supplier MapToEntity(CreateUpdateSupplierDto input)
    {
        var supplier = new Supplier(
            GuidGenerator.Create(), input.CompanyId, input.Name, CurrentTenant.Id);
        MapToEntity(input, supplier);
        return supplier;
    }

    protected override void MapToEntity(CreateUpdateSupplierDto input, Supplier entity)
    {
        entity.SetName(input.Name);
        entity.SupplierCode = input.SupplierCode;
        entity.Tin = input.Tin;
        entity.RegistrationNumber = input.RegistrationNumber;
        entity.SstRegistrationNumber = input.SstRegistrationNumber;
        entity.IdType = input.IdType;
        entity.IdValue = input.IdValue;
        entity.ContactPerson = input.ContactPerson;
        entity.Phone = input.Phone;
        entity.Email = input.Email;
        entity.Website = input.Website;
        entity.Address = input.Address;
        entity.City = input.City;
        entity.State = input.State;
        entity.PostalCode = input.PostalCode;
        entity.Country = input.Country;
        entity.DefaultPayableAccountId = input.DefaultPayableAccountId;
        entity.IsActive = input.IsActive;
        entity.HoldType = input.HoldType;
        entity.PreventPurchaseOrders = input.PreventPurchaseOrders;
        entity.PreventRfqs = input.PreventRfqs;
        entity.IsFrozen = input.IsFrozen;
        entity.AllowPurchaseInvoiceWithoutPurchaseOrder = input.AllowPurchaseInvoiceWithoutPurchaseOrder;
        entity.AllowPurchaseInvoiceWithoutPurchaseReceipt = input.AllowPurchaseInvoiceWithoutPurchaseReceipt;
        entity.RepresentsCompanyId = input.RepresentsCompanyId;
        entity.SupplierGroupId = input.SupplierGroupId;
        entity.TaxWithholdingCategory = input.TaxWithholdingCategory;
        entity.DefaultPaymentTermsTemplateId = input.DefaultPaymentTermsTemplateId;
        entity.DefaultPriceListId = input.DefaultPriceListId;
        entity.RestrictToCompanies = input.RestrictToCompanies;
    }

    /// <summary>
    /// Returns the KPIs, payables ageing, monthly purchases/payments trend, delivery performance, and purchasing pipeline for a supplier and company.
    /// Per ERPNext buying/doctype/supplier/supplier_overview.py (PR #60554 / commit 8d60c08296).
    /// </summary>
    public async Task<SupplierOverviewDto> GetSupplierOverviewAsync(GetSupplierOverviewInputDto input)
    {
        var supplier = await Repository.GetAsync(input.SupplierId);
        var companyRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Company, Guid>>();
        var company = await companyRepo.GetAsync(input.CompanyId);

        var asOfDate = DateTime.UtcNow.Date;
        var (fromDate, toDate) = await ResolvePeriodAsync(input.Period, input.CompanyId, asOfDate);

        var piRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PurchaseInvoice, Guid>>();
        var piQ = await piRepo.GetQueryableAsync();

        // All non-draft non-cancelled posted/submitted purchase invoices for this supplier & company
        var supplierInvoices = piQ
            .Where(pi => pi.SupplierId == input.SupplierId
                && pi.CompanyId == input.CompanyId
                && !pi.IsOpening
                && (pi.Status == DocumentStatus.Posted || pi.Status == DocumentStatus.Submitted))
            .ToList();

        // 1. Position: Net Purchases, Outstanding, Overdue, Advances
        var currentPeriodInvoices = supplierInvoices
            .Where(pi => !pi.IsReturn && pi.IssueDate >= fromDate && pi.IssueDate <= toDate)
            .ToList();
        var currentNetPurchases = currentPeriodInvoices.Sum(pi => pi.BaseNetTotal > 0 ? pi.BaseNetTotal : (pi.NetTotal > 0 ? pi.NetTotal : pi.GrandTotal));

        var prevFromDate = fromDate.AddYears(-1);
        var prevToDate = toDate.AddYears(-1);
        var prevPeriodInvoices = supplierInvoices
            .Where(pi => !pi.IsReturn && pi.IssueDate >= prevFromDate && pi.IssueDate <= prevToDate)
            .ToList();
        var prevNetPurchases = prevPeriodInvoices.Sum(pi => pi.BaseNetTotal > 0 ? pi.BaseNetTotal : (pi.NetTotal > 0 ? pi.NetTotal : pi.GrandTotal));

        var outstandingInvoices = supplierInvoices
            .Where(pi => !pi.IsReturn && (pi.GrandTotal - pi.AmountPaid - pi.WriteOffAmount - pi.TotalAdvance) > 0)
            .ToList();
        var outstandingTotal = outstandingInvoices.Sum(pi => pi.GrandTotal - pi.AmountPaid - pi.WriteOffAmount - pi.TotalAdvance);

        // Days to pay (DPO) = outstanding / (trailing annual purchases / 365)
        var trailing365Start = asOfDate.AddDays(-365);
        var trailingPurchases = supplierInvoices
            .Where(pi => !pi.IsReturn && pi.IssueDate >= trailing365Start && pi.IssueDate <= asOfDate)
            .Sum(pi => pi.BaseNetTotal > 0 ? pi.BaseNetTotal : (pi.NetTotal > 0 ? pi.NetTotal : pi.GrandTotal));
        int? daysToPay = null;
        if (trailingPurchases > 0)
        {
            var dailyPurchases = trailingPurchases / 365m;
            if (dailyPurchases > 0)
            {
                var dpo = (int)Math.Round(outstandingTotal / dailyPurchases);
                if (dpo <= 730) daysToPay = dpo;
            }
        }

        var overdueInvoices = outstandingInvoices
            .Where(pi => pi.DueDate.HasValue && pi.DueDate.Value.Date < asOfDate)
            .ToList();
        var overdueTotal = overdueInvoices.Sum(pi => pi.GrandTotal - pi.AmountPaid - pi.WriteOffAmount - pi.TotalAdvance);

        var asOf30DaysAgo = asOfDate.AddDays(-30);
        var overdue30Total = supplierInvoices
            .Where(pi => !pi.IsReturn
                && (pi.GrandTotal - pi.AmountPaid - pi.WriteOffAmount - pi.TotalAdvance) > 0
                && pi.DueDate.HasValue && pi.DueDate.Value.Date < asOf30DaysAgo)
            .Sum(pi => pi.GrandTotal - pi.AmountPaid - pi.WriteOffAmount - pi.TotalAdvance);

        // Unallocated Advances from PaymentEntry
        var peRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PaymentEntry, Guid>>();
        var peQ = await peRepo.GetQueryableAsync();
        var supplierPayments = peQ
            .Where(pe => pe.CompanyId == input.CompanyId
                && pe.PartyType == "Supplier"
                && pe.PartyId == input.SupplierId
                && (pe.Status == DocumentStatus.Submitted || pe.Status == DocumentStatus.Posted))
            .ToList();
        var advances = supplierPayments.Sum(pe => pe.UnallocatedAmount);

        // 2. Trend: Purchases vs Payments (PR #60554 / commit 8d60c08296)
        // Includes PaymentEntry, cash Purchase Invoices (is_paid=1), and Bank/Cash JournalEntry payments
        var jeRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<JournalEntry, Guid>>();
        var jeList = (await jeRepo.WithDetailsAsync(je => je.Lines))
            .Where(je => je.CompanyId == input.CompanyId
                && je.Status == DocumentStatus.Posted
                && (je.VoucherType == JournalEntryVoucherType.BankEntry || je.VoucherType == JournalEntryVoucherType.CashEntry)
                && je.PostingDate >= fromDate && je.PostingDate <= toDate)
            .ToList();

        var points = new List<SupplierTrendPointDto>();
        var cursor = new DateTime(fromDate.Year, fromDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endMonth = new DateTime(toDate.Year, toDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        while (cursor <= endMonth)
        {
            var monthPurchased = supplierInvoices
                .Where(pi => !pi.IsReturn && pi.IssueDate.Year == cursor.Year && pi.IssueDate.Month == cursor.Month)
                .Sum(pi => pi.BaseGrandTotal > 0 ? pi.BaseGrandTotal : pi.GrandTotal);

            var monthPaid = supplierPayments
                .Where(pe => pe.PostingDate.Year == cursor.Year && pe.PostingDate.Month == cursor.Month)
                .Sum(pe => pe.PaymentType == PaymentType.Pay ? pe.PaidAmount : -pe.ReceivedAmount);

            var cashInvoicePaid = supplierInvoices
                .Where(pi => pi.AmountPaid > 0 && pi.IssueDate.Year == cursor.Year && pi.IssueDate.Month == cursor.Month)
                .Sum(pi => pi.AmountPaid);

            var monthJournalPaid = jeList
                .Where(je => je.PostingDate.Year == cursor.Year && je.PostingDate.Month == cursor.Month)
                .SelectMany(je => je.Lines)
                .Where(l => l.PartyType == "Supplier" && l.PartyId == input.SupplierId)
                .Sum(l => l.Debit - l.Credit);

            var totalPaidForMonth = monthPaid + cashInvoicePaid + monthJournalPaid;

            points.Add(new SupplierTrendPointDto
            {
                Label = cursor.ToString("MMM"),
                Title = cursor.ToString("MMM yyyy"),
                Purchased = Math.Round(monthPurchased, 2),
                Paid = Math.Round(totalPaidForMonth, 2)
            });

            cursor = cursor.AddMonths(1);
        }

        // 3. Delivery Performance & Orders
        var poRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PurchaseOrder, Guid>>();
        var poList = (await poRepo.WithDetailsAsync(po => po.Items))
            .Where(po => po.SupplierId == input.SupplierId
                && po.CompanyId == input.CompanyId
                && po.Status != DocumentStatus.Draft
                && po.Status != DocumentStatus.Cancelled)
            .ToList();

        var onTimeCount = 0;
        var lateCount = 0;
        var overdueDeliveryCount = 0;
        var countedOrders = new HashSet<Guid>();

        foreach (var po in poList.Where(po => po.Status != DocumentStatus.Closed))
        {
            foreach (var item in po.Items.Where(i => !i.IsClosed))
            {
                var dueDate = item.GetEffectiveExpectedDate(po.ExpectedDeliveryDate) ?? po.OrderDate;
                if (dueDate < fromDate || dueDate > toDate)
                    continue;

                countedOrders.Add(po.Id);

                if (item.ReceivedQty >= item.Quantity)
                {
                    if (item.LastReceiptDate.HasValue && item.LastReceiptDate.Value.Date > dueDate.Date)
                    {
                        lateCount++;
                    }
                    else
                    {
                        onTimeCount++;
                    }
                }
                else if (dueDate.Date < asOfDate)
                {
                    overdueDeliveryCount++;
                }
            }
        }

        var linesDue = onTimeCount + lateCount + overdueDeliveryCount;
        decimal? onTimeRate = linesDue > 0
            ? Math.Round((decimal)onTimeCount / linesDue * 100m, 1)
            : null;

        var scRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<SupplierScorecard, Guid>>();
        var scQ = await scRepo.GetQueryableAsync();
        var hasScorecard = scQ.Any(sc => sc.SupplierId == input.SupplierId && sc.CompanyId == input.CompanyId);

        // 4. Pipeline: Quotations, Delivery Receipt, Billing, Late Deliveries
        var sqRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<SupplierQuotation, Guid>>();
        var sqQ = await sqRepo.GetQueryableAsync();
        var openQuotes = sqQ
            .Where(sq => sq.SupplierId == input.SupplierId
                && sq.CompanyId == input.CompanyId
                && sq.Status == DocumentStatus.Submitted)
            .ToList();
        var expiringQuotes = openQuotes.Count(sq => sq.ValidTill.HasValue && sq.ValidTill.Value.Date >= asOfDate && sq.ValidTill.Value.Date <= asOfDate.AddDays(7));

        var openPos = poList.Where(po => po.Status != DocumentStatus.Closed).ToList();
        var receiptDict = new Dictionary<string, decimal>();
        var billingDict = new Dictionary<string, decimal>();
        var lateDict = new Dictionary<string, decimal>();
        var upcomingDueDates = new List<DateTime>();
        DateTime? oldestOverdueDate = null;

        foreach (var po in openPos)
        {
            var poNumber = po.OrderNumber ?? po.Id.ToString();
            foreach (var item in po.Items.Where(i => !i.IsClosed))
            {
                var qty = item.Quantity;
                var rate = item.UnitPrice;
                var received = Math.Min(Math.Max(0m, item.ReceivedQty), qty);

                // Pending billing: received - billed
                var unbilled = Math.Max(0m, received - item.BilledQty) * rate;
                if (unbilled > 0)
                {
                    billingDict[poNumber] = billingDict.GetValueOrDefault(poNumber) + unbilled;
                }

                // Pending receipt: qty - received
                var pendingReceipt = Math.Max(0m, qty - received) * rate;
                if (pendingReceipt > 0)
                {
                    receiptDict[poNumber] = receiptDict.GetValueOrDefault(poNumber) + pendingReceipt;

                    var due = item.GetEffectiveExpectedDate(po.ExpectedDeliveryDate);
                    if (due.HasValue)
                    {
                        if (due.Value.Date < asOfDate)
                        {
                            lateDict[poNumber] = lateDict.GetValueOrDefault(poNumber) + pendingReceipt;
                            if (oldestOverdueDate == null || due.Value.Date < oldestOverdueDate.Value)
                            {
                                oldestOverdueDate = due.Value.Date;
                            }
                        }
                        else
                        {
                            upcomingDueDates.Add(due.Value.Date);
                        }
                    }
                }
            }
        }

        // 5. Ageing: Not Due, 1-30, 31-60, 61-90, 90+
        decimal notDueAmount = 0m, b1Amount = 0m, b2Amount = 0m, b3Amount = 0m, b4Amount = 0m;
        foreach (var inv in outstandingInvoices)
        {
            var bal = inv.GrandTotal - inv.AmountPaid - inv.WriteOffAmount - inv.TotalAdvance;
            if (!inv.DueDate.HasValue || inv.DueDate.Value.Date >= asOfDate)
            {
                notDueAmount += bal;
            }
            else
            {
                var overdueDays = (int)(asOfDate - inv.DueDate.Value.Date).TotalDays;
                if (overdueDays <= 30) b1Amount += bal;
                else if (overdueDays <= 60) b2Amount += bal;
                else if (overdueDays <= 90) b3Amount += bal;
                else b4Amount += bal;
            }
        }

        var buckets = new List<SupplierAgeingBucketDto>
        {
            new() { Key = "not_due", Label = "Not due", Value = Math.Round(notDueAmount, 2), IsOverdue = false },
            new() { Key = "b1", Label = "1–30 days", Value = Math.Round(b1Amount, 2), IsOverdue = true },
            new() { Key = "b2", Label = "31–60 days", Value = Math.Round(b2Amount, 2), IsOverdue = true },
            new() { Key = "b3", Label = "61–90 days", Value = Math.Round(b3Amount, 2), IsOverdue = true },
            new() { Key = "b4", Label = "90+ days", Value = Math.Round(b4Amount, 2), IsOverdue = true },
        };
        var totalAgeing = Math.Round(notDueAmount + b1Amount + b2Amount + b3Amount + b4Amount, 2);
        var totalOverdue = Math.Round(b1Amount + b2Amount + b3Amount + b4Amount, 2);

        return new SupplierOverviewDto
        {
            SupplierId = supplier.Id,
            SupplierName = supplier.Name,
            CompanyId = input.CompanyId,
            Currency = company.CurrencyCode,
            Period = input.Period,
            FromDate = fromDate,
            ToDate = toDate,
            AsOfDate = asOfDate,
            Position = new SupplierPositionDto
            {
                NetPurchases = new SupplierMetricDto
                {
                    Value = Math.Round(currentNetPurchases, 2),
                    Count = currentPeriodInvoices.Count,
                    Delta = CalculatePctChange(currentNetPurchases, prevNetPurchases),
                    DeltaPositiveIsGood = true
                },
                Outstanding = new SupplierOutstandingMetricDto
                {
                    Value = Math.Round(outstandingTotal, 2),
                    UnpaidCount = outstandingInvoices.Count,
                    DaysToPay = daysToPay
                },
                Overdue = new SupplierMetricDto
                {
                    Value = Math.Round(overdueTotal, 2),
                    Count = overdueInvoices.Count,
                    Delta = CalculatePctChange(overdueTotal, overdue30Total),
                    DeltaPositiveIsGood = false
                },
                Advances = new SupplierMetricDto
                {
                    Value = Math.Round(advances, 2),
                    Count = supplierPayments.Count(p => p.UnallocatedAmount > 0)
                }
            },
            Trend = new SupplierTrendDto
            {
                Points = points,
                TotalPurchased = Math.Round(points.Sum(p => p.Purchased), 2),
                TotalPaid = Math.Round(points.Sum(p => p.Paid), 2)
            },
            Delivery = new SupplierOverviewDeliveryDto
            {
                OnTime = onTimeCount,
                Late = lateCount,
                Overdue = overdueDeliveryCount,
                Lines = linesDue,
                Orders = countedOrders.Count,
                OnTimeRate = onTimeRate,
                HasScorecard = hasScorecard
            },
            Pipeline = new SupplierPipelineDto
            {
                Quotations = new SupplierQuotationTileDto
                {
                    Value = Math.Round(openQuotes.Sum(q => q.GrandTotal), 2),
                    Count = openQuotes.Count,
                    Expiring = expiringQuotes
                },
                Receipt = new SupplierPipelineTileDto
                {
                    Value = Math.Round(receiptDict.Values.Sum(), 2),
                    Count = receiptDict.Count,
                    NextDue = upcomingDueDates.Count > 0 ? upcomingDueDates.Min() : null,
                    Orders = receiptDict.Keys.OrderBy(k => k).ToList()
                },
                Billing = new SupplierPipelineTileDto
                {
                    Value = Math.Round(billingDict.Values.Sum(), 2),
                    Count = billingDict.Count,
                    Orders = billingDict.Keys.OrderBy(k => k).ToList()
                },
                Late = new SupplierDeliveryTileDto
                {
                    Value = Math.Round(lateDict.Values.Sum(), 2),
                    Count = lateDict.Count,
                    DaysLate = oldestOverdueDate.HasValue ? (int)(asOfDate - oldestOverdueDate.Value).TotalDays : null,
                    Orders = lateDict.Keys.OrderBy(k => k).ToList()
                }
            },
            Ageing = new SupplierAgeingDto
            {
                Buckets = buckets,
                Total = totalAgeing,
                Overdue = totalOverdue,
                OverduePct = totalAgeing > 0 ? Math.Round(totalOverdue / totalAgeing * 100m, 1) : 0m
            },
            UnallocatedAdvances = Math.Round(advances, 2)
        };
    }

    /// <summary>
    /// Fetches recent transactions across Purchase Invoices, Purchase Orders, Purchase Receipts, and Payment Entries for supplier.
    /// Per ERPNext buying/doctype/supplier/supplier_overview.py get_supplier_transactions().
    /// </summary>
    public async Task<List<SupplierTransactionDto>> GetSupplierTransactionsAsync(GetSupplierTransactionsInputDto input)
    {
        var limit = Math.Clamp(input.MaxResultCount, 1, 100);
        var rows = new List<SupplierTransactionDto>();

        var docType = input.DocType ?? "All";

        if (string.Equals(docType, "All", StringComparison.OrdinalIgnoreCase) || string.Equals(docType, "Purchase Invoice", StringComparison.OrdinalIgnoreCase))
        {
            var piRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PurchaseInvoice, Guid>>();
            var piQ = await piRepo.GetQueryableAsync();
            var piList = piQ
                .Where(pi => pi.SupplierId == input.SupplierId && pi.CompanyId == input.CompanyId && (pi.Status == DocumentStatus.Posted || pi.Status == DocumentStatus.Submitted))
                .OrderByDescending(pi => pi.IssueDate)
                .ThenByDescending(pi => pi.CreationTime)
                .Take(limit)
                .ToList();

            foreach (var pi in piList)
            {
                var outstanding = Math.Max(0m, pi.GrandTotal - pi.AmountPaid - pi.WriteOffAmount - pi.TotalAdvance);
                rows.Add(new SupplierTransactionDto
                {
                    Id = pi.Id,
                    TransactionNumber = pi.InvoiceNumber ?? pi.Id.ToString(),
                    DocType = "Purchase Invoice",
                    TypeLabel = "Purchase Invoice",
                    Date = pi.IssueDate,
                    Status = pi.IsReturn ? "Debit Note" : pi.Status.ToString(),
                    Amount = pi.GrandTotal,
                    OutstandingAmount = outstanding
                });
            }
        }

        if (string.Equals(docType, "All", StringComparison.OrdinalIgnoreCase) || string.Equals(docType, "Purchase Order", StringComparison.OrdinalIgnoreCase))
        {
            var poRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PurchaseOrder, Guid>>();
            var poQ = await poRepo.GetQueryableAsync();
            var poList = poQ
                .Where(po => po.SupplierId == input.SupplierId && po.CompanyId == input.CompanyId && po.Status != DocumentStatus.Draft && po.Status != DocumentStatus.Cancelled)
                .OrderByDescending(po => po.OrderDate)
                .ThenByDescending(po => po.CreationTime)
                .Take(limit)
                .ToList();

            foreach (var po in poList)
            {
                rows.Add(new SupplierTransactionDto
                {
                    Id = po.Id,
                    TransactionNumber = po.OrderNumber ?? po.Id.ToString(),
                    DocType = "Purchase Order",
                    TypeLabel = "Purchase Order",
                    Date = po.OrderDate,
                    Status = po.Status.ToString(),
                    Amount = po.GrandTotal,
                    OutstandingAmount = null
                });
            }
        }

        if (string.Equals(docType, "All", StringComparison.OrdinalIgnoreCase) || string.Equals(docType, "Purchase Receipt", StringComparison.OrdinalIgnoreCase))
        {
            var prRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PurchaseReceipt, Guid>>();
            var prQ = await prRepo.GetQueryableAsync();
            var prList = prQ
                .Where(pr => pr.SupplierId == input.SupplierId && pr.CompanyId == input.CompanyId && (pr.Status == DocumentStatus.Posted || pr.Status == DocumentStatus.Submitted))
                .OrderByDescending(pr => pr.PostingDate)
                .ThenByDescending(pr => pr.CreationTime)
                .Take(limit)
                .ToList();

            foreach (var pr in prList)
            {
                rows.Add(new SupplierTransactionDto
                {
                    Id = pr.Id,
                    TransactionNumber = pr.ReceiptNumber ?? pr.Id.ToString(),
                    DocType = "Purchase Receipt",
                    TypeLabel = "Purchase Receipt",
                    Date = pr.PostingDate,
                    Status = pr.IsReturn ? "Return" : pr.Status.ToString(),
                    Amount = pr.GrandTotal,
                    OutstandingAmount = null
                });
            }
        }

        if (string.Equals(docType, "All", StringComparison.OrdinalIgnoreCase) || string.Equals(docType, "Payment Entry", StringComparison.OrdinalIgnoreCase))
        {
            var peRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PaymentEntry, Guid>>();
            var peQ = await peRepo.GetQueryableAsync();
            var peList = peQ
                .Where(pe => pe.CompanyId == input.CompanyId && pe.PartyType == "Supplier" && pe.PartyId == input.SupplierId && (pe.Status == DocumentStatus.Submitted || pe.Status == DocumentStatus.Posted))
                .OrderByDescending(pe => pe.PostingDate)
                .ThenByDescending(pe => pe.CreationTime)
                .Take(limit)
                .ToList();

            foreach (var pe in peList)
            {
                rows.Add(new SupplierTransactionDto
                {
                    Id = pe.Id,
                    TransactionNumber = pe.PaymentNumber ?? pe.Id.ToString(),
                    DocType = "Payment Entry",
                    TypeLabel = "Payment Entry",
                    Date = pe.PostingDate,
                    Status = pe.Status.ToString(),
                    Amount = pe.PaidAmount,
                    OutstandingAmount = pe.UnallocatedAmount > 0 ? pe.UnallocatedAmount : null
                });
            }
        }

        return rows
            .OrderByDescending(r => r.Date)
            .ThenByDescending(r => r.Id)
            .Take(limit)
            .ToList();
    }

    /// <summary>
    /// Fetches all distinct company IDs where the supplier has transactions.
    /// Per ERPNext get_supplier_companies().
    /// </summary>
    public async Task<List<Guid>> GetSupplierCompanyIdsAsync(Guid supplierId)
    {
        var companyIds = new HashSet<Guid>();

        var supplier = await Repository.FindAsync(supplierId);
        if (supplier != null && supplier.CompanyId != Guid.Empty)
        {
            companyIds.Add(supplier.CompanyId);
        }

        var piRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PurchaseInvoice, Guid>>();
        var piQ = await piRepo.GetQueryableAsync();
        var piCompanyIds = piQ
            .Where(pi => pi.SupplierId == supplierId && (pi.Status == DocumentStatus.Posted || pi.Status == DocumentStatus.Submitted))
            .Select(pi => pi.CompanyId)
            .Distinct()
            .ToList();
        foreach (var cId in piCompanyIds)
            companyIds.Add(cId);

        var poRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PurchaseOrder, Guid>>();
        var poQ = await poRepo.GetQueryableAsync();
        var poCompanyIds = poQ
            .Where(po => po.SupplierId == supplierId && po.Status != DocumentStatus.Draft && po.Status != DocumentStatus.Cancelled)
            .Select(po => po.CompanyId)
            .Distinct()
            .ToList();
        foreach (var cId in poCompanyIds)
            companyIds.Add(cId);

        var prRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PurchaseReceipt, Guid>>();
        var prQ = await prRepo.GetQueryableAsync();
        var prCompanyIds = prQ
            .Where(pr => pr.SupplierId == supplierId && (pr.Status == DocumentStatus.Posted || pr.Status == DocumentStatus.Submitted))
            .Select(pr => pr.CompanyId)
            .Distinct()
            .ToList();
        foreach (var cId in prCompanyIds)
            companyIds.Add(cId);

        var sqRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<SupplierQuotation, Guid>>();
        var sqQ = await sqRepo.GetQueryableAsync();
        var sqCompanyIds = sqQ
            .Where(sq => sq.SupplierId == supplierId && sq.Status == DocumentStatus.Submitted)
            .Select(sq => sq.CompanyId)
            .Distinct()
            .ToList();
        foreach (var cId in sqCompanyIds)
            companyIds.Add(cId);

        var peRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PaymentEntry, Guid>>();
        var peQ = await peRepo.GetQueryableAsync();
        var peCompanyIds = peQ
            .Where(pe => pe.PartyType == "Supplier" && pe.PartyId == supplierId && (pe.Status == DocumentStatus.Submitted || pe.Status == DocumentStatus.Posted))
            .Select(pe => pe.CompanyId)
            .Distinct()
            .ToList();
        foreach (var cId in peCompanyIds)
            companyIds.Add(cId);

        return companyIds.OrderBy(id => id).ToList();
    }

    private async Task<(DateTime fromDate, DateTime toDate)> ResolvePeriodAsync(string period, Guid companyId, DateTime asOfDate)
    {
        if (string.Equals(period, "Last 12 months", StringComparison.OrdinalIgnoreCase))
        {
            return (asOfDate.AddMonths(-12), asOfDate);
        }

        if (string.Equals(period, "This quarter", StringComparison.OrdinalIgnoreCase))
        {
            var quarterStartMonth = ((asOfDate.Month - 1) / 3) * 3 + 1;
            var from = new DateTime(asOfDate.Year, quarterStartMonth, 1, 0, 0, 0, DateTimeKind.Utc);
            return (from, asOfDate);
        }

        var fyRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<FiscalYear, Guid>>();
        var fyQ = await fyRepo.GetQueryableAsync();
        var currentFy = fyQ.FirstOrDefault(fy => fy.CompanyId == companyId && fy.StartDate <= asOfDate && fy.EndDate >= asOfDate);

        if (currentFy == null)
        {
            return (asOfDate.AddMonths(-12), asOfDate);
        }

        if (string.Equals(period, "Last fiscal year", StringComparison.OrdinalIgnoreCase))
        {
            var prevDate = currentFy.StartDate.AddDays(-1);
            var prevFy = fyQ.FirstOrDefault(fy => fy.CompanyId == companyId && fy.StartDate <= prevDate && fy.EndDate >= prevDate);
            if (prevFy != null)
            {
                return (prevFy.StartDate, prevFy.EndDate);
            }
        }

        return (currentFy.StartDate, asOfDate < currentFy.EndDate ? asOfDate : currentFy.EndDate);
    }

    private static decimal? CalculatePctChange(decimal current, decimal previous)
    {
        if (previous == 0) return null;
        return Math.Round((current - previous) / Math.Abs(previous) * 100m, 1);
    }
}

