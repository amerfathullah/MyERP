using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core.DomainServices;
using MyERP.Permissions;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using MyERP.Shared;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

/// <summary>
/// POS Closing Entry AppService — manages POS shift closure and reconciliation.
/// Per ERPNext: closing aggregates POS invoices per cashier shift, calculates variance,
/// and triggers consolidation into a single Sales Invoice for GL posting.
/// </summary>
[Authorize(MyERPPermissions.SalesInvoices.Default)]
public class PosClosingAppService : ApplicationService, IPosClosingAppService
{
    private readonly IRepository<PosClosingEntry, Guid> _repository;
    private readonly PosConsolidationService _consolidationService;
    private readonly IRepository<SalesInvoice, Guid> _invoiceRepository;
    private readonly IDocumentNumberGenerator _numberGenerator;

    public PosClosingAppService(
        IRepository<PosClosingEntry, Guid> repository,
        PosConsolidationService consolidationService,
        IRepository<SalesInvoice, Guid> invoiceRepository,
        IDocumentNumberGenerator numberGenerator)
    {
        _repository = repository;
        _consolidationService = consolidationService;
        _invoiceRepository = invoiceRepository;
        _numberGenerator = numberGenerator;
    }

    public async Task<PosClosingDto> GetAsync(Guid id)
    {
        var entry = await _repository.GetAsync(id);
        return ObjectMapper.Map<PosClosingEntry, PosClosingDto>(entry);
    }

    public async Task<PagedResultDto<PosClosingDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        if (input.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);

        if (!string.IsNullOrWhiteSpace(input.Status) && Enum.TryParse<PosClosingStatus>(input.Status, true, out var status))
            query = query.Where(x => x.Status == status);

        var count = query.Count();
        var list = query.OrderByDescending(x => x.PostingDate)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<PosClosingDto>(count, list.Select(x => ObjectMapper.Map<PosClosingEntry, PosClosingDto>(x)).ToList());
    }

    /// <summary>
    /// Creates a POS Closing Entry for the current shift.
    /// Links POS invoices and payment mode balances.
    /// </summary>
    [Authorize(MyERPPermissions.SalesInvoices.Submit)]
    public async Task<PosClosingDto> CreateAsync(CreatePosClosingDto input)
    {
        var entry = new PosClosingEntry(
            GuidGenerator.Create(), input.CompanyId, input.PosProfileId,
            input.PosOpeningEntryId, input.UserId, CurrentTenant.Id);

        foreach (var inv in input.Invoices)
            entry.AddInvoice(inv.PosInvoiceId, inv.InvoiceNumber, inv.GrandTotal);

        foreach (var pay in input.Payments)
            entry.AddPayment(pay.ModeOfPaymentId, pay.ModeName, pay.ExpectedAmount, pay.ClosingAmount);

        await _repository.InsertAsync(entry, autoSave: true);
        return ObjectMapper.Map<PosClosingEntry, PosClosingDto>(entry);
    }

    [Authorize(MyERPPermissions.SalesInvoices.Submit)]
    public async Task<PosClosingDto> SubmitAsync(Guid id)
    {
        var entry = await _repository.GetAsync(id);

        // PR #57203: idempotent recovery — if already submitted (failed consolidation),
        // skip Submit() and retry consolidation only
        if (entry.Status == PosClosingStatus.Submitted)
        {
            // Already submitted, just return current state (retry consolidation separately)
            return ObjectMapper.Map<PosClosingEntry, PosClosingDto>(entry);
        }

        entry.Submit();

        // POS Consolidation: merge individual POS invoices into consolidated SI for GL posting
        // Per ERPNext pos_invoice_merge_log: creates single SI per dimension group
        if (entry.Invoices.Any())
        {
            var invoiceIds = entry.Invoices.Select(i => i.PosInvoiceId).ToList();

            // Use first invoice's customer as the consolidation customer
            var firstInvoice = await _invoiceRepository.FindAsync(entry.Invoices.First().PosInvoiceId);
            var customerId = firstInvoice?.CustomerId ?? Guid.Empty;

            var results = await _consolidationService.ConsolidateAsync(
                invoiceIds, entry.CompanyId, customerId, entry.PostingDate);

            // Create the consolidated Sales Invoice from the first result
            if (results.Any())
            {
                var primary = results.First();
                var invoiceNumber = await _numberGenerator.GenerateAsync("SalesInvoice", entry.CompanyId);

                // Use currency from first source invoice (all POS invoices in a shift share the same currency)
                var currencyCode = firstInvoice?.CurrencyCode ?? "MYR";

                var consolidatedSi = new SalesInvoice(
                    GuidGenerator.Create(), primary.CompanyId, primary.CustomerId,
                    invoiceNumber, primary.PostingDate, CurrentTenant.Id);
                consolidatedSi.CurrencyCode = currencyCode;

                foreach (var item in primary.Items)
                {
                    consolidatedSi.AddItem(item.ItemId, item.Description, item.Quantity, item.UnitPrice, 0m);
                }

                consolidatedSi.Submit();
                consolidatedSi.Post();
                await _invoiceRepository.InsertAsync(consolidatedSi, autoSave: true);

                // GL + PLE posting for the consolidated invoice.
                // Per ERPNext: individual POS Invoices carry no GL of their own — only the
                // consolidated Sales Invoice posts to the ledger (pos-invoice-full.md line 201).
                // This was previously missing entirely: POS shifts never reached the GL.
                await PostConsolidatedInvoiceGlAsync(entry, consolidatedSi);

                entry.ConsolidatedSalesInvoiceId = consolidatedSi.Id;

                // Mark each source POS invoice with the consolidated SI to prevent re-consolidation
                foreach (var invId in invoiceIds)
                {
                    var sourceInvoice = await _invoiceRepository.FindAsync(invId);
                    if (sourceInvoice != null)
                    {
                        sourceInvoice.ConsolidatedSalesInvoiceId = consolidatedSi.Id;
                        await _invoiceRepository.UpdateAsync(sourceInvoice);
                    }
                }
            }
        }

        await _repository.UpdateAsync(entry, autoSave: true);
        return ObjectMapper.Map<PosClosingEntry, PosClosingDto>(entry);
    }

    [Authorize(MyERPPermissions.SalesInvoices.Submit)]
    public async Task<PosClosingDto> CancelAsync(Guid id)
    {
        var entry = await _repository.GetAsync(id);
        entry.Cancel();
        await _repository.UpdateAsync(entry, autoSave: true);
        return ObjectMapper.Map<PosClosingEntry, PosClosingDto>(entry);
    }

    [Authorize(MyERPPermissions.SalesInvoices.Submit)]
    public async Task<PosClosingDto> RetryAsync(Guid id)
    {
        var entry = await _repository.GetAsync(id);
        entry.Retry();
        await _repository.UpdateAsync(entry, autoSave: true);
        return ObjectMapper.Map<PosClosingEntry, PosClosingDto>(entry);
    }

    /// <summary>
    /// Posts GL + PLE for a freshly-consolidated POS Sales Invoice:
    /// 1. Standard SI GL (Receivable DR / Revenue+Tax CR) + PLE DR via the normal posting orchestrator.
    /// 2. A payment JE per POS Closing payment mode: DR mode's default account / CR receivable
    ///    (per gl-posting-patterns: "POS payment | payment_mode.account DR | doc.debit_to CR").
    /// 3. A PLE reconciliation entry so the invoice's outstanding nets to zero (cash was already
    ///    collected during the shift — the consolidated SI should not show as unpaid).
    /// </summary>
    private async Task PostConsolidatedInvoiceGlAsync(PosClosingEntry entry, SalesInvoice consolidatedSi)
    {
        var companyRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Core.Entities.Company, Guid>>();
        var company = await companyRepo.GetAsync(consolidatedSi.CompanyId);

        var receivableAccountId = consolidatedSi.DebitToAccountId != Guid.Empty
            ? consolidatedSi.DebitToAccountId
            : company.DefaultReceivableAccountId
                ?? throw new BusinessException("MyERP:02001")
                    .WithData("reason", "No receivable account configured. Set Default Receivable Account in Company settings.");
        consolidatedSi.DebitToAccountId = receivableAccountId;

        var postingOrchestrator = LazyServiceProvider
            .LazyGetRequiredService<Accounting.DomainServices.DocumentPostingOrchestrator>();
        await postingOrchestrator.PostSalesInvoiceAsync(consolidatedSi, receivableAccountId);

        // Payment JE: one DR/CR pair per payment mode with a positive expected amount.
        var paidModes = entry.Payments.Where(p => p.ExpectedAmount > 0).ToList();
        if (paidModes.Count == 0) return;

        var fyRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<FiscalYear, Guid>>();
        var fyQuery = await fyRepo.GetQueryableAsync();
        var fiscalYear = fyQuery.FirstOrDefault(fy =>
            fy.CompanyId == consolidatedSi.CompanyId
            && fy.StartDate <= consolidatedSi.IssueDate
            && fy.EndDate >= consolidatedSi.IssueDate);
        if (fiscalYear == null) return;

        var modeOfPaymentRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<ModeOfPayment, Guid>>();

        var je = new JournalEntry(
            GuidGenerator.Create(), consolidatedSi.CompanyId, fiscalYear.Id,
            consolidatedSi.IssueDate, consolidatedSi.TenantId)
        {
            ReferenceType = "PosClosingEntry",
            ReferenceId = entry.Id,
        };

        var totalPaid = 0m;
        foreach (var pay in paidModes)
        {
            var mop = await modeOfPaymentRepo.FindAsync(pay.ModeOfPaymentId);
            var accountId = mop?.DefaultAccountId
                ?? throw new BusinessException("MyERP:02001")
                    .WithData("reason", $"Mode of Payment '{pay.ModeName}' has no default account configured for this company.");

            // Cap at the invoice grand total — any excess is cashier variance, handled at closing reconciliation, not GL.
            var amount = Math.Min(pay.ExpectedAmount, consolidatedSi.GrandTotal - totalPaid);
            if (amount <= 0) continue;

            je.AddLineWithDimensions(accountId, amount, true, null, null, null, $"POS Payment - {pay.ModeName}");
            je.AddLineWithDimensions(receivableAccountId, amount, false, null, null, null, $"POS Payment - {pay.ModeName}");
            totalPaid += amount;
        }

        if (totalPaid <= 0) return;

        je.Validate();
        je.Post();
        await LazyServiceProvider.LazyGetRequiredService<IRepository<JournalEntry, Guid>>().InsertAsync(je);

        var pleService = LazyServiceProvider.LazyGetRequiredService<Accounting.DomainServices.PaymentLedgerService>();
        await pleService.ReconcileAsync(
            consolidatedSi.CompanyId, consolidatedSi.IssueDate, receivableAccountId,
            "Customer", consolidatedSi.CustomerId,
            "PosClosingEntry", entry.Id,
            "SalesInvoice", consolidatedSi.Id,
            totalPaid, totalPaid, consolidatedSi.CurrencyCode, consolidatedSi.TenantId);

        consolidatedSi.AmountPaid = totalPaid;
        await _invoiceRepository.UpdateAsync(consolidatedSi);
    }

    /// <summary>
    /// Calculates expected payment amounts per payment mode for a POS shift.
    /// Formula per ERPNext: Expected = Opening Balance + Sum(POS Invoice payments during shift).
    /// Returns: per-MOP expected amount (used to pre-fill closing form before cashier enters actuals).
    /// </summary>
    public async Task<List<PosExpectedPaymentDto>> CalculateExpectedAmountsAsync(Guid posOpeningEntryId)
    {
        // 1. Get the opening entry with payment modes and opening balances
        var openingRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PosOpeningEntry, Guid>>();
        var opening = (await openingRepo.WithDetailsAsync())
            .FirstOrDefault(o => o.Id == posOpeningEntryId);
        if (opening == null) return [];

        // 2. Find total POS revenue during shift from invoices in the same company/date
        var invoiceQuery = await _invoiceRepository.GetQueryableAsync();
        var shiftInvoices = invoiceQuery
            .Where(si => si.CompanyId == opening.CompanyId
                      && si.IssueDate >= opening.OpeningDate
                      && si.Status != Core.DocumentStatus.Cancelled
                      && si.ConsolidatedSalesInvoiceId == null) // Not yet consolidated = POS invoices
            .ToList();

        var totalInvoiceAmount = shiftInvoices.Sum(i => i.GrandTotal);

        // 3. For each payment mode, calculate: opening balance + proportional invoice amount
        // In simplified POS (single payment mode), all invoice revenue goes to that mode
        // Per ERPNext: POS invoices track payment_mode per invoice; we approximate with proportional split
        var result = new List<PosExpectedPaymentDto>();
        var totalOpening = opening.Payments.Sum(p => p.OpeningAmount);

        foreach (var openingPay in opening.Payments)
        {
            // Default: all POS revenue assumed in first/primary payment mode (Cash)
            // When POS supports multi-MOP per invoice, this should be per-MOP aggregation
            var invoicePortionForMode = openingPay == opening.Payments.First()
                ? totalInvoiceAmount
                : 0m;

            result.Add(new PosExpectedPaymentDto
            {
                ModeOfPaymentId = openingPay.ModeOfPaymentId,
                ModeName = openingPay.ModeName,
                OpeningAmount = openingPay.OpeningAmount,
                InvoiceTotal = invoicePortionForMode,
                ExpectedAmount = openingPay.OpeningAmount + invoicePortionForMode,
            });
        }

        return result;
    }
}

