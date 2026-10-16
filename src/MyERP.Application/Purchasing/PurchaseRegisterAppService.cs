using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
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
    private readonly IRepository<JournalEntry, Guid>? _journalRepository;
    private readonly IRepository<PurchaseOrder, Guid>? _purchaseOrderRepository;
    private readonly IRepository<PurchaseReceipt, Guid>? _purchaseReceiptRepository;
    private readonly IRepository<Item, Guid>? _itemRepository;
    private readonly IRepository<ItemGroup, Guid>? _itemGroupRepository;

    public PurchaseRegisterAppService(
        IRepository<PurchaseInvoice, Guid> invoiceRepository,
        IRepository<PaymentEntry, Guid> paymentRepository,
        IRepository<Supplier, Guid>? supplierRepository = null,
        IRepository<SupplierGroup, Guid>? supplierGroupRepository = null,
        IRepository<JournalEntry, Guid>? journalRepository = null,
        IRepository<PurchaseOrder, Guid>? purchaseOrderRepository = null,
        IRepository<PurchaseReceipt, Guid>? purchaseReceiptRepository = null,
        IRepository<Item, Guid>? itemRepository = null,
        IRepository<ItemGroup, Guid>? itemGroupRepository = null)
    {
        _invoiceRepository = invoiceRepository;
        _paymentRepository = paymentRepository;
        _supplierRepository = supplierRepository;
        _supplierGroupRepository = supplierGroupRepository;
        _journalRepository = journalRepository;
        _purchaseOrderRepository = purchaseOrderRepository;
        _purchaseReceiptRepository = purchaseReceiptRepository;
        _itemRepository = itemRepository;
        _itemGroupRepository = itemGroupRepository;
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

    private async Task<HashSet<Guid>> GetSupplierGroupWithDescendantIdsAsync(Guid rootGroupId)
    {
        var result = new HashSet<Guid> { rootGroupId };
        if (_supplierGroupRepository == null) return result;

        try
        {
            var allGroups = await _supplierGroupRepository.GetListAsync();
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

    private async Task<Dictionary<Guid, bool>> GetInternalSupplierMapAsync(IEnumerable<Guid> supplierIds, Guid companyId)
    {
        var map = new Dictionary<Guid, bool>();
        if (_supplierRepository == null) return map;
        var distinctIds = supplierIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (distinctIds.Count == 0) return map;

        try
        {
            var query = await _supplierRepository.GetQueryableAsync();
            var suppliers = query.Where(s => distinctIds.Contains(s.Id)).ToList();
            foreach (var s in suppliers)
            {
                map[s.Id] = s.RepresentsCompanyId.HasValue && s.RepresentsCompanyId.Value == companyId;
            }
        }
        catch
        {
            // Fallback
        }
        return map;
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
            var targetGroupIds = await GetSupplierGroupWithDescendantIdsAsync(input.SupplierGroupId.Value);
            var suppQuery = await _supplierRepository.GetQueryableAsync();
            var matchedSupplierIds = suppQuery
                .Where(s => s.SupplierGroupId.HasValue && targetGroupIds.Contains(s.SupplierGroupId.Value))
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
                var targetGroupIds = await GetSupplierGroupWithDescendantIdsAsync(input.SupplierGroupId.Value);
                var supp = await _supplierRepository.FindAsync(supplierId);
                if (supp == null || !supp.SupplierGroupId.HasValue || !targetGroupIds.Contains(supp.SupplierGroupId.Value))
                {
                    return new RegisterReportDto<PurchaseRegisterLineDto>();
                }
            }
            var peQuery = await GetPaymentQueryableWithTaxesAsync();
            var internalMap = await GetInternalSupplierMapAsync(invoices.Select(i => i.SupplierId).Concat(new[] { supplierId }), input.CompanyId);

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

            decimal priorCredits = priorInvoices.Sum(pi => internalMap.GetValueOrDefault(pi.SupplierId) ? 0 : (pi.IsReturn ? 0 : GetPayableCredit(pi)))
                + priorPayments.Where(pe => pe.PaymentType == PaymentType.Receive).Sum(pe => pe.TotalSettledBaseAmount > 0 ? pe.TotalSettledBaseAmount : pe.PaidAmount);
            decimal priorDebits = priorInvoices.Sum(pi => internalMap.GetValueOrDefault(pi.SupplierId) ? 0 : ((pi.IsReturn ? GetPayableDebitForReturn(pi) : 0) + GetInInvoicePayableDebit(pi)))
                + priorPayments.Where(pe => pe.PaymentType != PaymentType.Receive).Sum(pe => pe.TotalSettledBaseAmount > 0 ? pe.TotalSettledBaseAmount : pe.PaidAmount);

            // Journal Entry rows prior to 'from' date (per ERPNext PR #59888 / commit 4f95812874)
            if (_journalRepository != null)
            {
                var jeQuery = await GetJournalQueryableWithLinesAsync();
                var priorJournals = jeQuery
                    .Where(j => j.CompanyId == input.CompanyId
                             && j.Status == DocumentStatus.Posted
                             && j.PostingDate < from
                             && j.Lines.Any(l => l.PartyType == "Supplier" && l.PartyId == supplierId))
                    .ToList();

                foreach (var je in priorJournals)
                {
                    foreach (var line in je.Lines.Where(l => l.PartyType == "Supplier" && l.PartyId == supplierId))
                    {
                        priorCredits += line.Credit;
                        priorDebits += line.Debit;
                    }
                }
            }

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

            // Invoice rows (per ERPNext PR #59888: internal transfers credit 0, in-invoice debits include write-offs & payments)
            foreach (var pi in invoices)
            {
                bool isInternal = internalMap.GetValueOrDefault(pi.SupplierId);
                decimal inInvoiceDebit = isInternal ? 0 : GetInInvoicePayableDebit(pi);
                decimal debit = isInternal ? 0 : ((pi.IsReturn ? GetPayableDebitForReturn(pi) : 0) + inInvoiceDebit);
                decimal credit = isInternal ? 0 : (pi.IsReturn ? 0 : GetPayableCredit(pi));
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

            // Journal Entry rows in period (per ERPNext PR #59888 / commits 4f95812874 & fd1c361cfc)
            if (_journalRepository != null)
            {
                var jeQuery = await GetJournalQueryableWithLinesAsync();
                var periodJournals = jeQuery
                    .Where(j => j.CompanyId == input.CompanyId
                             && j.Status == DocumentStatus.Posted
                             && j.PostingDate >= from
                             && j.PostingDate <= to
                             && j.Lines.Any(l => l.PartyType == "Supplier" && l.PartyId == supplierId))
                    .ToList();

                foreach (var je in periodJournals)
                {
                    foreach (var line in je.Lines.Where(l => l.PartyType == "Supplier" && l.PartyId == supplierId))
                    {
                        lines.Add(new PurchaseRegisterLineDto
                        {
                            VoucherType = "Journal Entry",
                            JournalEntryId = je.Id,
                            InvoiceNumber = je.EntryNumber ?? "JE",
                            PostingDate = je.PostingDate,
                            SupplierId = supplierId,
                            GrandTotal = line.Credit > 0 ? line.Credit : line.Debit,
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
        var internalStandardMap = await GetInternalSupplierMapAsync(invoices.Select(pi => pi.SupplierId), input.CompanyId);
        var sortedInvoices = invoices.OrderByDescending(pi => pi.IssueDate).ToList();
        var items = sortedInvoices.Select(pi =>
        {
            bool isInternal = internalStandardMap.GetValueOrDefault(pi.SupplierId);
            decimal inInvoiceDebit = isInternal ? 0 : GetInInvoicePayableDebit(pi);
            decimal debit = isInternal ? 0 : ((pi.IsReturn ? GetPayableDebitForReturn(pi) : 0) + inInvoiceDebit);
            decimal credit = isInternal ? 0 : (pi.IsReturn ? 0 : GetPayableCredit(pi));
            return new PurchaseRegisterLineDto
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
            };
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

    private async Task PopulateSupplierDetailsAsync(List<PurchaseRegisterLineDto> lines)
    {
        if (lines.Count == 0) return;

        Dictionary<Guid, Supplier> supplierMap = new();
        Dictionary<Guid, string>? groupMap = null;

        if (_supplierRepository != null)
        {
            using (TryDisableSoftDelete())
            {
                var supplierIds = lines.Select(l => l.SupplierId).Where(id => id != Guid.Empty).Distinct().ToList();
                if (supplierIds.Count > 0)
                {
                    var supplierQuery = await _supplierRepository.GetQueryableAsync();
                    var suppliers = supplierQuery.Where(s => supplierIds.Contains(s.Id)).ToList();
                    supplierMap = suppliers.ToDictionary(s => s.Id);

                    var groupIds = suppliers.Where(s => s.SupplierGroupId.HasValue).Select(s => s.SupplierGroupId!.Value).Distinct().ToList();
                    if (_supplierGroupRepository != null && groupIds.Count > 0)
                    {
                        var groupQuery = await _supplierGroupRepository.GetQueryableAsync();
                        groupMap = groupQuery.Where(g => groupIds.Contains(g.Id)).ToDictionary(g => g.Id, g => g.Name);
                    }
                }
            }
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
            else
            {
                // Guard against missing supplier and preserve existing name/group
                line.SupplierName = !string.IsNullOrWhiteSpace(line.SupplierName)
                    ? line.SupplierName
                    : (line.SupplierId != Guid.Empty ? line.SupplierId.ToString() : string.Empty);
            }
        }
    }

    /// <summary>
    /// Amount the invoice credits to its payable, rounded like its GL entry.
    /// Per ERPNext PR #59930 / commit 1ae23fadef: credit the rounded total only when a rounding adjustment was posted.
    /// </summary>
    private static decimal GetPayableCredit(PurchaseInvoice inv)
    {
        if (inv.BaseRoundingAdjustment != 0 && inv.BaseRoundedTotal != 0)
        {
            return inv.BaseRoundedTotal;
        }

        return inv.BaseGrandTotal != 0 ? inv.BaseGrandTotal : inv.GrandTotal;
    }

    private static decimal GetPayableDebitForReturn(PurchaseInvoice inv)
    {
        if (inv.BaseRoundingAdjustment != 0 && inv.BaseRoundedTotal != 0)
        {
            return Math.Abs(inv.BaseRoundedTotal);
        }

        return Math.Abs(inv.BaseGrandTotal != 0 ? inv.BaseGrandTotal : inv.GrandTotal);
    }

    /// <summary>
    /// Amount the invoice settles against its own payable, as in its GL entries.
    /// Per ERPNext PR #59888 (commit 7dda7c3335):
    /// Includes write-off for all invoices, plus direct paid amount for paid/cash invoices.
    /// </summary>
    private static decimal GetInInvoicePayableDebit(PurchaseInvoice inv)
    {
        decimal rate = inv.ExchangeRate > 0 ? inv.ExchangeRate : 1m;
        decimal debit = inv.WriteOffAmount * rate;
        if (inv.AmountPaid > 0)
        {
            debit += inv.AmountPaid * rate;
        }
        return debit;
    }

    /// <summary>
    /// Item-wise Purchase Register report.
    /// Maps to ERPNext accounts/report/item_wise_purchase_register.
    /// Per ERPNext PR #60047:
    /// - Includes other charges in item total (commit 7d3592adaa)
    /// - Expands child groups in item group filter (commit e8f536cc2e)
    /// - Links receipts of invoices made from an order via PurchaseOrderItemId (commit ff9fb72baf & ebab2f50dc)
    /// </summary>
    public async Task<RegisterReportDto<ItemWisePurchaseRegisterLineDto>> GetItemWiseReportAsync(ItemWiseRegisterFilterDto input)
    {
        var from = input.FromDate ?? DateTime.UtcNow.AddMonths(-1).Date;
        var to = input.ToDate ?? DateTime.UtcNow.Date;

        var query = await _invoiceRepository.WithDetailsAsync(pi => pi.Items);
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
            var targetGroupIds = await GetSupplierGroupWithDescendantIdsAsync(input.SupplierGroupId.Value);
            var suppQuery = await _supplierRepository.GetQueryableAsync();
            var matchedSupplierIds = suppQuery
                .Where(s => s.SupplierGroupId.HasValue && targetGroupIds.Contains(s.SupplierGroupId.Value))
                .Select(s => s.Id)
                .ToList();
            invoicesQuery = invoicesQuery.Where(pi => matchedSupplierIds.Contains(pi.SupplierId));
        }

        var invoices = invoicesQuery.ToList();
        if (invoices.Count == 0)
        {
            return new RegisterReportDto<ItemWisePurchaseRegisterLineDto>();
        }

        // 1. Supplier details
        var supplierIds = invoices.Select(pi => pi.SupplierId).Distinct().ToList();
        Dictionary<Guid, Supplier> supplierMap = new();
        Dictionary<Guid, string> supplierGroupMap = new();
        if (_supplierRepository != null && supplierIds.Count > 0)
        {
            var suppQuery = await _supplierRepository.GetQueryableAsync();
            supplierMap = suppQuery.Where(s => supplierIds.Contains(s.Id)).ToDictionary(s => s.Id);
            if (_supplierGroupRepository != null)
            {
                var allGroups = await _supplierGroupRepository.GetListAsync();
                supplierGroupMap = allGroups.ToDictionary(g => g.Id, g => g.Name);
            }
        }

        // 2. Item details
        var allItemIds = invoices.SelectMany(pi => pi.Items).Select(i => i.ItemId).Distinct().ToList();
        var itemRepo = _itemRepository ?? LazyServiceProvider.LazyGetService<IRepository<Item, Guid>>();
        Dictionary<Guid, Item> itemMap = new();
        if (itemRepo != null && allItemIds.Count > 0)
        {
            var iQuery = await itemRepo.GetQueryableAsync();
            itemMap = iQuery.Where(i => allItemIds.Contains(i.Id)).ToDictionary(i => i.Id);
        }

        // 3. Item Group hierarchy filter (PR #60047 commit e8f536cc2e)
        HashSet<Guid>? validGroupIds = null;
        if (input.ItemGroupId.HasValue)
        {
            validGroupIds = await GetItemGroupWithDescendantIdsAsync(input.ItemGroupId.Value);
        }

        // 4. Trace Purchase Orders and Receipts (PR #60047 commits ff9fb72baf & ebab2f50dc)
        var allPoItemIds = invoices.SelectMany(pi => pi.Items)
            .Where(i => i.PurchaseOrderItemId.HasValue)
            .Select(i => i.PurchaseOrderItemId!.Value)
            .Distinct()
            .ToList();

        var poRepo = _purchaseOrderRepository ?? LazyServiceProvider.LazyGetService<IRepository<PurchaseOrder, Guid>>();
        Dictionary<Guid, string> poItemOrderNumberMap = new();
        if (poRepo != null && allPoItemIds.Count > 0)
        {
            var poQuery = await poRepo.WithDetailsAsync(po => po.Items);
            var poList = poQuery.Where(po => po.CompanyId == input.CompanyId && po.Items.Any(i => allPoItemIds.Contains(i.Id))).ToList();
            foreach (var po in poList)
            {
                foreach (var poItem in po.Items.Where(i => allPoItemIds.Contains(i.Id)))
                {
                    poItemOrderNumberMap[poItem.Id] = po.OrderNumber;
                }
            }
        }

        var prRepo = _purchaseReceiptRepository ?? LazyServiceProvider.LazyGetService<IRepository<PurchaseReceipt, Guid>>();
        Dictionary<Guid, string> prItemReceiptNumberMap = new();
        Dictionary<Guid, List<string>> poItemReceiptNumbersMap = new();
        if (prRepo != null)
        {
            var prQuery = await prRepo.WithDetailsAsync(pr => pr.Items);
            var prList = prQuery.Where(pr => pr.CompanyId == input.CompanyId).ToList();
            foreach (var pr in prList)
            {
                foreach (var prItem in pr.Items)
                {
                    prItemReceiptNumberMap[prItem.Id] = pr.ReceiptNumber;
                    if (prItem.PurchaseOrderItemId.HasValue)
                    {
                        if (!poItemReceiptNumbersMap.TryGetValue(prItem.PurchaseOrderItemId.Value, out var list))
                        {
                            list = new List<string>();
                            poItemReceiptNumbersMap[prItem.PurchaseOrderItemId.Value] = list;
                        }
                        if (!list.Contains(pr.ReceiptNumber))
                        {
                            list.Add(pr.ReceiptNumber);
                        }
                    }
                }
            }
        }

        var rows = new List<ItemWisePurchaseRegisterLineDto>();
        foreach (var pi in invoices.OrderByDescending(i => i.IssueDate))
        {
            supplierMap.TryGetValue(pi.SupplierId, out var supplier);
            var suppName = supplier?.Name ?? pi.SupplierId.ToString();
            var suppGroupName = string.Empty;
            if (supplier != null && supplier.SupplierGroupId.HasValue)
            {
                supplierGroupMap.TryGetValue(supplier.SupplierGroupId.Value, out suppGroupName);
            }

            decimal rateMultiplier = pi.ExchangeRate > 0 ? pi.ExchangeRate : 1m;

            foreach (var item in pi.Items)
            {
                if (input.ItemId.HasValue && item.ItemId != input.ItemId.Value) continue;
                itemMap.TryGetValue(item.ItemId, out var itemMaster);
                if (!string.IsNullOrWhiteSpace(input.ItemCode) && itemMaster != null && !string.Equals(itemMaster.ItemCode, input.ItemCode, StringComparison.OrdinalIgnoreCase)) continue;
                if (validGroupIds != null)
                {
                    if (itemMaster == null || !itemMaster.ItemGroupId.HasValue || !validGroupIds.Contains(itemMaster.ItemGroupId.Value)) continue;
                }

                // Resolve Purchase Order number
                string? poNumber = null;
                if (item.PurchaseOrderItemId.HasValue)
                {
                    poItemOrderNumberMap.TryGetValue(item.PurchaseOrderItemId.Value, out poNumber);
                }

                // Resolve Purchase Receipt number(s) (PR #60047 commits ff9fb72baf & ebab2f50dc)
                string? prNumber = null;
                if (item.PurchaseReceiptItemId.HasValue && prItemReceiptNumberMap.TryGetValue(item.PurchaseReceiptItemId.Value, out var directPr))
                {
                    prNumber = directPr;
                }
                else if (item.PurchaseOrderItemId.HasValue && poItemReceiptNumbersMap.TryGetValue(item.PurchaseOrderItemId.Value, out var linkedPrs) && linkedPrs.Count > 0)
                {
                    prNumber = string.Join(", ", linkedPrs);
                }

                var itemCode = itemMaster?.ItemCode ?? item.ItemId.ToString();
                var itemName = itemMaster?.ItemName ?? item.Description;
                var itemGroup = itemMaster?.ItemGroup ?? string.Empty;
                var rate = Math.Round(item.UnitPrice * rateMultiplier, 2);
                var amount = Math.Round(item.LineTotal * rateMultiplier, 2);
                var totalTax = Math.Round(item.TaxAmount * rateMultiplier, 2);
                var otherCharges = Math.Round(item.LandedCostVoucherAmount * rateMultiplier, 2);
                // Per ERPNext PR #60047 (commit 7d3592adaa): total includes other charges
                var total = amount + totalTax + otherCharges;

                rows.Add(new ItemWisePurchaseRegisterLineDto
                {
                    InvoiceId = pi.Id,
                    InvoiceNumber = pi.InvoiceNumber ?? string.Empty,
                    PostingDate = pi.IssueDate,
                    SupplierId = pi.SupplierId,
                    SupplierName = suppName,
                    SupplierGroupId = supplier?.SupplierGroupId,
                    SupplierGroupName = suppGroupName,
                    ItemId = item.ItemId,
                    ItemCode = itemCode,
                    ItemName = itemName,
                    ItemGroup = itemGroup,
                    Description = item.Description,
                    StockQty = item.StockQty,
                    StockUom = item.StockUom,
                    Rate = rate,
                    Amount = amount,
                    TotalTax = totalTax,
                    TotalOtherCharges = otherCharges,
                    Total = total,
                    PurchaseOrderNumber = poNumber,
                    PurchaseReceiptNumber = prNumber,
                    Currency = pi.CurrencyCode,
                });
            }
        }

        return new RegisterReportDto<ItemWisePurchaseRegisterLineDto>
        {
            Count = rows.Count,
            Items = rows,
            TotalNet = rows.Sum(r => r.Amount),
            TotalTax = rows.Sum(r => r.TotalTax),
            TotalGrand = rows.Sum(r => r.Total),
        };
    }

    private async Task<HashSet<Guid>> GetItemGroupWithDescendantIdsAsync(Guid rootGroupId)
    {
        var result = new HashSet<Guid> { rootGroupId };
        var groupRepo = _itemGroupRepository ?? LazyServiceProvider.LazyGetService<IRepository<ItemGroup, Guid>>();
        if (groupRepo == null) return result;

        try
        {
            var allGroups = await groupRepo.GetListAsync();
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
                        if (result.Add(childId)) queue.Enqueue(childId);
                    }
                }
            }
        }
        catch
        {
        }
        return result;
    }
}
