using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using MyERP.Shared;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

[Authorize(MyERPPermissions.PaymentEntries.Default)]
public class PaymentEntryAppService : ApplicationService
{
    private readonly IRepository<PaymentEntry, Guid> _repository;
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepository;
    private readonly IRepository<PurchaseInvoice, Guid> _purchaseInvoiceRepository;
    private readonly IRepository<PaymentScheduleEntry, Guid> _scheduleRepository;
    private readonly IRepository<PaymentLedgerEntry, Guid> _pleRepository;
    private readonly IDocumentNumberGenerator _numberGenerator;
    private readonly DocumentPostingOrchestrator _postingOrchestrator;
    private readonly IRepository<DocumentActivityLog, Guid> _activityLogRepository;

    public PaymentEntryAppService(
        IRepository<PaymentEntry, Guid> repository,
        IRepository<SalesInvoice, Guid> salesInvoiceRepository,
        IRepository<PurchaseInvoice, Guid> purchaseInvoiceRepository,
        IRepository<PaymentScheduleEntry, Guid> scheduleRepository,
        IRepository<PaymentLedgerEntry, Guid> pleRepository,
        IDocumentNumberGenerator numberGenerator,
        DocumentPostingOrchestrator postingOrchestrator,
        IRepository<DocumentActivityLog, Guid> activityLogRepository)
    {
        _repository = repository;
        _salesInvoiceRepository = salesInvoiceRepository;
        _purchaseInvoiceRepository = purchaseInvoiceRepository;
        _scheduleRepository = scheduleRepository;
        _pleRepository = pleRepository;
        _numberGenerator = numberGenerator;
        _postingOrchestrator = postingOrchestrator;
        _activityLogRepository = activityLogRepository;
    }

    public async Task<PaymentEntryDto> GetAsync(Guid id)
    {
        var pe = await _repository.GetAsync(id);
        return MapToDto(pe);
    }

    public async Task<PagedResultDto<PaymentEntryDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();

        if (input.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter.ToLower();
            query = query.Where(x => x.PaymentNumber != null && x.PaymentNumber.ToLower().Contains(filter));
        }

        if (!string.IsNullOrWhiteSpace(input.Status) && Enum.TryParse<Core.DocumentStatus>(input.Status, true, out var status))
            query = query.Where(x => x.Status == status);

        var count = query.Count();
        var list = query
            .OrderByDescending(x => x.PostingDate)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        return new PagedResultDto<PaymentEntryDto>(count, list.Select(MapToDto).ToList());
    }

    [Authorize(MyERPPermissions.PaymentEntries.Create)]
    public async Task<PaymentEntryDto> CreateAsync(CreatePaymentEntryDto input)
    {
        // Input validation
        Check.NotDefaultOrNull<Guid>(input.CompanyId, nameof(input.CompanyId));
        Check.NotDefaultOrNull<Guid>(input.PaidFromAccountId, nameof(input.PaidFromAccountId));
        Check.NotDefaultOrNull<Guid>(input.PaidToAccountId, nameof(input.PaidToAccountId));
        if (input.PaidAmount <= 0)
            throw new Volo.Abp.BusinessException("MyERP:01008")
                .WithData("field", "PaidAmount");
        if (input.PostingDate == default)
            input.PostingDate = DateTime.UtcNow.Date;

        var paymentNumber = await _numberGenerator.GenerateAsync("PaymentEntry", input.CompanyId);
        var pe = new PaymentEntry(
            GuidGenerator.Create(), input.CompanyId, input.PaymentType, input.PostingDate,
            input.PaidAmount, input.PaidFromAccountId, input.PaidToAccountId);

        pe.PaymentNumber = paymentNumber;
        pe.ModeOfPayment = input.ModeOfPayment;
        pe.PartyType = input.PartyType;
        pe.PartyId = input.PartyId;
        pe.ReferenceNumber = input.ReferenceNumber;
        pe.Notes = input.Notes;
        pe.AgainstOrderId = input.AgainstOrderId;
        pe.AgainstOrderType = input.AgainstOrderType;

        // Multi-currency: auto-resolve exchange rate if not explicitly provided
        // Per ERPNext: when payment currency ≠ company currency, fetch rate from CurrencyExchange
        if (input.ExchangeRate > 0 && input.ExchangeRate != 1m)
        {
            pe.ExchangeRate = input.ExchangeRate;
        }
        else if (!string.IsNullOrWhiteSpace(input.PaymentCurrency))
        {
            var companyRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.Company, Guid>>();
            var company = await companyRepo.GetAsync(input.CompanyId);
            if (!string.IsNullOrWhiteSpace(company.CurrencyCode)
                && !input.PaymentCurrency.Equals(company.CurrencyCode, StringComparison.OrdinalIgnoreCase))
            {
                var exchangeService = LazyServiceProvider.LazyGetRequiredService<CurrencyExchangeService>();
                var rate = await exchangeService.GetExchangeRateAsync(
                    input.PaymentCurrency, company.CurrencyCode, input.PostingDate);
                pe.ExchangeRate = rate;
            }
        }
        else
        {
            pe.ExchangeRate = input.ExchangeRate;
        }

        // Multi-reference allocation takes precedence over legacy single-invoice field
        if (input.References != null && input.References.Count > 0)
        {
            foreach (var refDto in input.References)
            {
                var reference = new PaymentEntryReference(
                    GuidGenerator.Create(),
                    pe.Id,
                    refDto.ReferenceType,
                    refDto.ReferenceId,
                    totalAmount: refDto.AllocatedAmount, // will be corrected at PostAsync
                    outstandingAmount: refDto.AllocatedAmount,
                    allocatedAmount: refDto.AllocatedAmount);
                reference.ExchangeRate = refDto.ExchangeRate;
                pe.References.Add(reference);
            }
        }
        else if (input.AgainstInvoiceId.HasValue)
        {
            // Legacy single-invoice backwards compatibility
            pe.AgainstInvoiceId = input.AgainstInvoiceId;
            pe.AgainstInvoiceType = input.AgainstInvoiceType;
        }

        await _repository.InsertAsync(pe, autoSave: true);
        return MapToDto(pe);
    }

    [Authorize(MyERPPermissions.PaymentEntries.Submit)]
    public async Task<PaymentEntryDto> SubmitAsync(Guid id)
    {
        var pe = await _repository.GetAsync(id);
        pe.Submit();
        await _repository.UpdateAsync(pe, autoSave: true);
        return MapToDto(pe);
    }

    [Authorize(MyERPPermissions.PaymentEntries.Submit)]
    public async Task<PaymentEntryDto> PostAsync(Guid id)
    {
        var pe = await _repository.GetAsync(id);

        // Supplier hold enforcement: HoldType.All or HoldType.Payments blocks PE
        if (pe.PartyType == "Supplier" && pe.PartyId.HasValue)
        {
            var supplierRepo = LazyServiceProvider
                .LazyGetRequiredService<IRepository<MyERP.Purchasing.Entities.Supplier, Guid>>();
            var supplier = await supplierRepo.GetAsync(pe.PartyId.Value);
            if (supplier.HoldType == MyERP.Purchasing.SupplierHoldType.All
                || supplier.HoldType == MyERP.Purchasing.SupplierHoldType.Payments)
            {
                throw new BusinessException(MyERPDomainErrorCodes.SupplierOnHold)
                    .WithData("supplierName", supplier.Name)
                    .WithData("holdType", supplier.HoldType.ToString());
            }
        }

        pe.Post();

        // Term-based allocation validation: if invoice uses payment terms with
        // allocate_payment_based_on_payment_terms, each reference must specify PaymentTermId
        if (pe.References != null && pe.References.Any())
        {
            var peManager = LazyServiceProvider
                .LazyGetRequiredService<MyERP.Accounting.DomainServices.PaymentEntryManager>();
            await peManager.ValidateTermBasedAllocationAsync(pe, async (refId) =>
            {
                // Check if the referenced invoice has a payment terms template with term-based allocation
                var scheduleRepo = LazyServiceProvider
                    .LazyGetRequiredService<IRepository<MyERP.Accounting.Entities.PaymentScheduleEntry, Guid>>();
                var scheduleQuery = await scheduleRepo.GetQueryableAsync();
                return scheduleQuery.Any(s => s.ParentId == refId);
            });
        }

        // Resolve source exchange rate from linked invoice for gain/loss calculation
        if (pe.AgainstInvoiceId.HasValue)
        {
            if (pe.AgainstInvoiceType == "SalesInvoice")
            {
                var si = await _salesInvoiceRepository.GetAsync(pe.AgainstInvoiceId.Value);
                pe.SourceExchangeRate = si.ExchangeRate;

                // Stale outstanding validation: hard error only when outstanding > 0 but payment exceeds it
                if (si.OutstandingAmount > 0 && pe.PaidAmount > si.OutstandingAmount)
                {
                    throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.OverAllocation)
                        .WithData("outstanding", si.OutstandingAmount)
                        .WithData("allocated", pe.PaidAmount);
                }
                // Per ERPNext validate_paid_invoices: outstanding <= 0 on non-return = soft warning only
                // Do NOT throw — log and allow PE to proceed
            }
            else if (pe.AgainstInvoiceType == "PurchaseInvoice")
            {
                var pi = await _purchaseInvoiceRepository.GetAsync(pe.AgainstInvoiceId.Value);
                pe.SourceExchangeRate = pi.ExchangeRate;

                // Stale outstanding validation: hard error only when outstanding > 0
                if (pi.OutstandingAmount > 0 && pe.PaidAmount > pi.OutstandingAmount)
                {
                    throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.OverAllocation)
                        .WithData("outstanding", pi.OutstandingAmount)
                        .WithData("allocated", pe.PaidAmount);
                }
            }
        }

        // GL posting + PLE to reduce outstanding on allocated invoice
        if (pe.PartyType != null && pe.PartyId.HasValue && pe.AgainstInvoiceId.HasValue)
        {
            var allocations = new[]
            {
                new PaymentAllocation
                {
                    VoucherType = pe.AgainstInvoiceType ?? "SalesInvoice",
                    VoucherId = pe.AgainstInvoiceId.Value,
                    AllocatedAmount = pe.PaidAmount
                }
            };

            await _postingOrchestrator.PostPaymentEntryAsync(
                pe,
                partyAccountId: pe.PaidToAccountId,
                partyType: pe.PartyType,
                partyId: pe.PartyId.Value,
                accountCurrency: pe.CurrencyCode,
                exchangeRate: pe.ExchangeRate,
                allocations: allocations);

            // Update the linked invoice's AmountPaid
            if (pe.AgainstInvoiceType == "SalesInvoice")
            {
                var si = await _salesInvoiceRepository.GetAsync(pe.AgainstInvoiceId.Value);
                si.AmountPaid += pe.PaidAmount;
                await _salesInvoiceRepository.UpdateAsync(si);
            }
            else if (pe.AgainstInvoiceType == "PurchaseInvoice")
            {
                var pi = await _purchaseInvoiceRepository.GetAsync(pe.AgainstInvoiceId.Value);
                pi.AmountPaid += pe.PaidAmount;
                await _purchaseInvoiceRepository.UpdateAsync(pi);
            }

            // Allocate payment to schedule entries (FIFO by due date)
            var scheduleQuery = await _scheduleRepository.GetQueryableAsync();
            var scheduleEntries = scheduleQuery
                .Where(s => s.ParentType == pe.AgainstInvoiceType
                         && s.ParentId == pe.AgainstInvoiceId.Value)
                .OrderBy(s => s.DueDate)
                .ToList();

            if (scheduleEntries.Any())
            {
                var remainingPayment = pe.PaidAmount;
                foreach (var entry in scheduleEntries)
                {
                    if (remainingPayment <= 0) break;
                    var allocated = entry.RecordPayment(remainingPayment);
                    remainingPayment -= allocated;
                    await _scheduleRepository.UpdateAsync(entry);
                }
            }

            // Exchange Gain/Loss JE: when payment rate differs from invoice rate
            if (pe.ExchangeGainLoss != 0 && pe.ExchangeRate != pe.SourceExchangeRate)
            {
                var company = await LazyServiceProvider
                    .LazyGetRequiredService<IRepository<MyERP.Core.Entities.Company, Guid>>()
                    .GetAsync(pe.CompanyId);

                if (company.ExchangeGainLossAccountId.HasValue)
                {
                    // Resolve fiscal year for the posting date
                    var fyRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Accounting.Entities.FiscalYear, Guid>>();
                    var fyQuery = await fyRepo.GetQueryableAsync();
                    var fy = fyQuery.FirstOrDefault(f => f.CompanyId == pe.CompanyId
                        && f.StartDate <= pe.PostingDate && f.EndDate >= pe.PostingDate);

                    if (fy != null)
                    {
                        var je = new MyERP.Accounting.Entities.JournalEntry(
                            GuidGenerator.Create(), pe.CompanyId, fy.Id, pe.PostingDate, pe.TenantId);

                        var gainLossAmount = Math.Abs(pe.ExchangeGainLoss);
                        if (pe.ExchangeGainLoss > 0)
                        {
                            // Gain: DR Bank/Receivable, CR Exchange Gain
                            je.AddLine(pe.PaidToAccountId, gainLossAmount, true, "Exchange Gain");
                            je.AddLine(company.ExchangeGainLossAccountId.Value, gainLossAmount, false, "Exchange Gain");
                        }
                        else
                        {
                            // Loss: DR Exchange Loss, CR Bank/Payable
                            je.AddLine(company.ExchangeGainLossAccountId.Value, gainLossAmount, true, "Exchange Loss");
                            je.AddLine(pe.PaidToAccountId, gainLossAmount, false, "Exchange Loss");
                        }

                        je.Validate();
                        je.Post();
                        await LazyServiceProvider
                            .LazyGetRequiredService<IRepository<MyERP.Accounting.Entities.JournalEntry, Guid>>()
                            .InsertAsync(je);
                    }
                }
            }
        }

        // Advance payment against order (no invoice linked yet)
        if (pe.IsAdvance && pe.AgainstOrderId.HasValue)
        {
            if (pe.AgainstOrderType == "SalesOrder")
            {
                var so = await LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Sales.Entities.SalesOrder, Guid>>()
                    .GetAsync(pe.AgainstOrderId.Value);
                so.AdvancePaid += pe.PaidAmount;
                await LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Sales.Entities.SalesOrder, Guid>>()
                    .UpdateAsync(so);
            }
            else if (pe.AgainstOrderType == "PurchaseOrder")
            {
                var po = await LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Purchasing.Entities.PurchaseOrder, Guid>>()
                    .GetAsync(pe.AgainstOrderId.Value);
                po.AdvancePaid += pe.PaidAmount;
                await LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Purchasing.Entities.PurchaseOrder, Guid>>()
                    .UpdateAsync(po);
            }
        }

        // Multi-reference allocation: when PE has explicit references, validate + allocate per reference
        if (pe.References?.Any() == true)
        {
            // Build PLE allocations for multi-ref posting
            var multiAllocations = new List<PaymentAllocation>();

            foreach (var refRow in pe.References)
            {
                if (refRow.ReferenceType == "SalesInvoice")
                {
                    var si = await _salesInvoiceRepository.GetAsync(refRow.ReferenceId);

                    // Stale outstanding validation per reference (prevents concurrent over-allocation)
                    if (si.OutstandingAmount > 0 && refRow.AllocatedAmount > si.OutstandingAmount)
                    {
                        throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.OverAllocation)
                            .WithData("outstanding", si.OutstandingAmount)
                            .WithData("allocated", refRow.AllocatedAmount);
                    }

                    si.AmountPaid += refRow.AllocatedAmount;
                    await _salesInvoiceRepository.UpdateAsync(si);

                    multiAllocations.Add(new PaymentAllocation
                    {
                        VoucherType = "SalesInvoice",
                        VoucherId = refRow.ReferenceId,
                        AllocatedAmount = refRow.AllocatedAmount
                    });

                    // FIFO payment schedule allocation per referenced invoice
                    await AllocateToPaymentScheduleAsync(refRow.ReferenceType, refRow.ReferenceId, refRow.AllocatedAmount);
                }
                else if (refRow.ReferenceType == "PurchaseInvoice")
                {
                    var pi = await _purchaseInvoiceRepository.GetAsync(refRow.ReferenceId);

                    if (pi.OutstandingAmount > 0 && refRow.AllocatedAmount > pi.OutstandingAmount)
                    {
                        throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.OverAllocation)
                            .WithData("outstanding", pi.OutstandingAmount)
                            .WithData("allocated", refRow.AllocatedAmount);
                    }

                    pi.AmountPaid += refRow.AllocatedAmount;
                    await _purchaseInvoiceRepository.UpdateAsync(pi);

                    multiAllocations.Add(new PaymentAllocation
                    {
                        VoucherType = "PurchaseInvoice",
                        VoucherId = refRow.ReferenceId,
                        AllocatedAmount = refRow.AllocatedAmount
                    });

                    await AllocateToPaymentScheduleAsync(refRow.ReferenceType, refRow.ReferenceId, refRow.AllocatedAmount);
                }
            }

            // Create PLE entries for multi-ref allocations (was missing — payment ledger was incomplete)
            if (multiAllocations.Any() && pe.PartyType != null && pe.PartyId.HasValue)
            {
                await _postingOrchestrator.PostPaymentEntryAsync(
                    pe,
                    partyAccountId: pe.PaidToAccountId,
                    partyType: pe.PartyType,
                    partyId: pe.PartyId.Value,
                    accountCurrency: pe.CurrencyCode,
                    exchangeRate: pe.ExchangeRate,
                    allocations: multiAllocations.ToArray());
            }
        }

        await _repository.UpdateAsync(pe, autoSave: true);

        // Audit trail
        await _activityLogRepository.InsertAsync(new DocumentActivityLog(
            GuidGenerator.Create(), "PaymentEntry", pe.Id, "Posted",
            pe.CompanyId, pe.PaymentNumber, "Submitted", "Posted",
            CurrentUser.Id, tenantId: pe.TenantId));

        // Notify: payment received (for sales team visibility)
        if (pe.PartyType == "Customer" && pe.PartyId.HasValue && CurrentUser.Id.HasValue)
        {
            try
            {
                var notifSvc = LazyServiceProvider
                    .LazyGetRequiredService<Notification.DomainServices.BusinessNotificationService>();
                await notifSvc.NotifyPaymentReceivedAsync(
                    CurrentUser.Id.Value,
                    pe.PartyType ?? "Customer",
                    pe.PaidAmount,
                    pe.CurrencyCode ?? "MYR",
                    pe.Id,
                    pe.TenantId);
            }
            catch { /* Non-critical */ }
        }

        return MapToDto(pe);
    }

    /// <summary>
    /// Cancel a posted payment entry — reverses all effects:
    /// reduces invoice AmountPaid, reverses PLE, deallocates payment schedule entries,
    /// reduces SO/PO AdvancePaid for advance payments.
    /// </summary>
    [Authorize(MyERPPermissions.PaymentEntries.Cancel)]
    public async Task<PaymentEntryDto> CancelAsync(Guid id)
    {
        var pe = await _repository.GetAsync(id);

        // Validate posting period is not frozen/closed (reversals can't post to locked periods)
        await _postingOrchestrator.ValidatePostingPeriodAsync(pe.CompanyId, pe.PostingDate, "PaymentEntry");

        // Guard: prevent cancel if PE has been used in reconciliation (non-delinked PLE entries)
        var pleQuery = await _pleRepository.GetQueryableAsync();
        var activeReconciliationEntries = pleQuery
            .Where(p => p.VoucherType == "PaymentEntry"
                     && p.VoucherId == pe.Id
                     && !p.Delinked
                     && !p.IsReversal
                     && p.AgainstVoucherType != "PaymentEntry") // Exclude self-referencing PLEs
            .Count();

        if (activeReconciliationEntries > 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.PaymentEntryUsedInReconciliation)
                .WithData("paymentNumber", pe.PaymentNumber ?? "")
                .WithData("reconciliationCount", activeReconciliationEntries);
        }

        pe.Cancel();

        // Reverse invoice AmountPaid
        if (pe.AgainstInvoiceId.HasValue)
        {
            if (pe.AgainstInvoiceType == "SalesInvoice")
            {
                var si = await _salesInvoiceRepository.GetAsync(pe.AgainstInvoiceId.Value);
                si.AmountPaid = Math.Max(0, si.AmountPaid - pe.PaidAmount);
                await _salesInvoiceRepository.UpdateAsync(si);
            }
            else if (pe.AgainstInvoiceType == "PurchaseInvoice")
            {
                var pi = await _purchaseInvoiceRepository.GetAsync(pe.AgainstInvoiceId.Value);
                pi.AmountPaid = Math.Max(0, pi.AmountPaid - pe.PaidAmount);
                await _purchaseInvoiceRepository.UpdateAsync(pi);
            }

            // Reverse payment schedule allocations
            var scheduleQuery = await _scheduleRepository.GetQueryableAsync();
            var scheduleEntries = scheduleQuery
                .Where(s => s.ParentType == pe.AgainstInvoiceType
                         && s.ParentId == pe.AgainstInvoiceId.Value)
                .OrderByDescending(s => s.DueDate) // Reverse: latest-first
                .ToList();

            if (scheduleEntries.Any())
            {
                var remainingReversal = pe.PaidAmount;
                foreach (var entry in scheduleEntries)
                {
                    if (remainingReversal <= 0) break;
                    var reversable = Math.Min(remainingReversal, entry.PaidAmount);
                    entry.PaidAmount -= reversable;
                    remainingReversal -= reversable;
                    await _scheduleRepository.UpdateAsync(entry);
                }
            }
        }

        // Reverse advance payment on SO/PO
        if (pe.IsAdvance && pe.AgainstOrderId.HasValue)
        {
            if (pe.AgainstOrderType == "SalesOrder")
            {
                var so = await LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Sales.Entities.SalesOrder, Guid>>()
                    .GetAsync(pe.AgainstOrderId.Value);
                so.AdvancePaid = Math.Max(0, so.AdvancePaid - pe.PaidAmount);
                await LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Sales.Entities.SalesOrder, Guid>>()
                    .UpdateAsync(so);
            }
            else if (pe.AgainstOrderType == "PurchaseOrder")
            {
                var po = await LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Purchasing.Entities.PurchaseOrder, Guid>>()
                    .GetAsync(pe.AgainstOrderId.Value);
                po.AdvancePaid = Math.Max(0, po.AdvancePaid - pe.PaidAmount);
                await LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Purchasing.Entities.PurchaseOrder, Guid>>()
                    .UpdateAsync(po);
            }
        }

        // Reverse multi-reference allocations
        if (pe.References?.Any() == true)
        {
            foreach (var refRow in pe.References)
            {
                if (refRow.ReferenceType == "SalesInvoice")
                {
                    var si = await _salesInvoiceRepository.GetAsync(refRow.ReferenceId);
                    si.AmountPaid = Math.Max(0, si.AmountPaid - refRow.AllocatedAmount);
                    await _salesInvoiceRepository.UpdateAsync(si);
                }
                else if (refRow.ReferenceType == "PurchaseInvoice")
                {
                    var pi = await _purchaseInvoiceRepository.GetAsync(refRow.ReferenceId);
                    pi.AmountPaid = Math.Max(0, pi.AmountPaid - refRow.AllocatedAmount);
                    await _purchaseInvoiceRepository.UpdateAsync(pi);
                }
            }
        }

        await _repository.UpdateAsync(pe, autoSave: true);

        // Audit trail
        await _activityLogRepository.InsertAsync(new DocumentActivityLog(
            GuidGenerator.Create(), "PaymentEntry", pe.Id, "Cancelled",
            pe.CompanyId, pe.PaymentNumber, "Posted", "Cancelled",
            CurrentUser.Id, tenantId: pe.TenantId));

        return MapToDto(pe);
    }

    /// <summary>
    /// Gets outstanding invoices for a party (used by PE form to select allocation targets).
    /// Returns invoices with outstanding > 0 for the given party.
    /// </summary>
    public async Task<List<OutstandingInvoiceForPaymentDto>> GetOutstandingForPartyAsync(
        string partyType, Guid partyId, Guid companyId)
    {
        var results = new List<OutstandingInvoiceForPaymentDto>();

        if (partyType == "Customer")
        {
            var siRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.SalesInvoice, Guid>>();
            var siQuery = await siRepo.GetQueryableAsync();
            var outstanding = siQuery
                .Where(si => si.CustomerId == partyId
                    && si.CompanyId == companyId
                    && si.Status == Core.DocumentStatus.Posted
                    && (si.GrandTotal - si.AmountPaid) > 0)
                .Select(si => new { si.Id, si.InvoiceNumber, si.IssueDate, si.DueDate, si.GrandTotal, si.AmountPaid, si.CurrencyCode })
                .ToList();

            results.AddRange(outstanding.Select(si => new OutstandingInvoiceForPaymentDto
            {
                InvoiceId = si.Id,
                InvoiceNumber = si.InvoiceNumber,
                IssueDate = si.IssueDate,
                DueDate = si.DueDate,
                GrandTotal = si.GrandTotal,
                Outstanding = si.GrandTotal - si.AmountPaid,
                CurrencyCode = si.CurrencyCode,
                InvoiceType = "SalesInvoice"
            }));
        }
        else if (partyType == "Supplier")
        {
            var piRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Purchasing.Entities.PurchaseInvoice, Guid>>();
            var piQuery = await piRepo.GetQueryableAsync();
            var outstanding = piQuery
                .Where(pi => pi.SupplierId == partyId
                    && pi.CompanyId == companyId
                    && pi.Status == Core.DocumentStatus.Posted
                    && (pi.GrandTotal - pi.AmountPaid) > 0)
                .Select(pi => new { pi.Id, pi.InvoiceNumber, pi.IssueDate, pi.DueDate, pi.GrandTotal, pi.AmountPaid, pi.CurrencyCode })
                .ToList();

            results.AddRange(outstanding.Select(pi => new OutstandingInvoiceForPaymentDto
            {
                InvoiceId = pi.Id,
                InvoiceNumber = pi.InvoiceNumber,
                IssueDate = pi.IssueDate,
                DueDate = pi.DueDate,
                GrandTotal = pi.GrandTotal,
                Outstanding = pi.GrandTotal - pi.AmountPaid,
                CurrencyCode = pi.CurrencyCode,
                InvoiceType = "PurchaseInvoice"
            }));
        }

        return results.OrderBy(r => r.DueDate ?? r.IssueDate).ToList();
    }

    private static PaymentEntryDto MapToDto(PaymentEntry pe) => new()
    {
        Id = pe.Id,
        CompanyId = pe.CompanyId,
        PaymentNumber = pe.PaymentNumber,
        PaymentType = pe.PaymentType.ToString(),
        PostingDate = pe.PostingDate,
        ModeOfPayment = pe.ModeOfPayment,
        PaidAmount = pe.PaidAmount,
        CurrencyCode = pe.CurrencyCode,
        Status = pe.Status.ToString(),
        ReferenceNumber = pe.ReferenceNumber
    };

    /// <summary>
    /// Allocates payment to invoice's payment schedule entries in FIFO order (earliest due date first).
    /// Reused by both legacy single-invoice and multi-reference allocation paths.
    /// </summary>
    private async Task AllocateToPaymentScheduleAsync(string invoiceType, Guid invoiceId, decimal amount)
    {
        var scheduleQuery = await _scheduleRepository.GetQueryableAsync();
        var scheduleEntries = scheduleQuery
            .Where(s => s.ParentType == invoiceType && s.ParentId == invoiceId)
            .OrderBy(s => s.DueDate)
            .ToList();

        if (!scheduleEntries.Any()) return;

        var remaining = amount;
        foreach (var entry in scheduleEntries)
        {
            if (remaining <= 0) break;
            var allocated = entry.RecordPayment(remaining);
            remaining -= allocated;
            await _scheduleRepository.UpdateAsync(entry);
        }
    }
}
