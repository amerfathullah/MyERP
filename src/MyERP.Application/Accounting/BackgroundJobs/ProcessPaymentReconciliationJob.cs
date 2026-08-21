using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting.BackgroundJobs;

/// <summary>
/// Processes one Process Payment Reconciliation request: each execution auto-allocates unreconciled
/// payments against exactly ONE outstanding invoice for the request's party (greedy match via
/// <see cref="PaymentReconciliationEngine.AutoAllocate"/>, applied via
/// <see cref="PaymentReconciliationEngine.ReconcileAndApplyAsync"/>), then re-enqueues itself for the
/// next invoice if the request is still Running. Bounded per-execution work — mirrors ERPNext's
/// chained one-reference-at-a-time job architecture so a party with hundreds of outstanding invoices
/// can't time out a single job run.
/// </summary>
public class ProcessPaymentReconciliationJob : AsyncBackgroundJob<ProcessPaymentReconciliationJobArgs>, ITransientDependency
{
    private readonly IRepository<ProcessPaymentReconciliation, Guid> _requestRepository;
    private readonly PaymentReconciliationEngine _engine;
    private readonly PaymentLedgerService _pleService;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IBackgroundJobManager _jobManager;
    private readonly ILogger<ProcessPaymentReconciliationJob> _logger;

    public ProcessPaymentReconciliationJob(
        IRepository<ProcessPaymentReconciliation, Guid> requestRepository,
        PaymentReconciliationEngine engine,
        PaymentLedgerService pleService,
        IRepository<Company, Guid> companyRepository,
        IBackgroundJobManager jobManager,
        ILogger<ProcessPaymentReconciliationJob> logger)
    {
        _requestRepository = requestRepository;
        _engine = engine;
        _pleService = pleService;
        _companyRepository = companyRepository;
        _jobManager = jobManager;
        _logger = logger;
    }

    public override async Task ExecuteAsync(ProcessPaymentReconciliationJobArgs args)
    {
        var request = await _requestRepository.FindAsync(args.RequestId);
        if (request == null)
        {
            _logger.LogWarning("ProcessPaymentReconciliationJob: {RequestId} not found, skipping.", args.RequestId);
            return;
        }

        if (request.Status == ProcessPaymentReconciliationStatus.Cancelled)
        {
            _logger.LogInformation("ProcessPaymentReconciliationJob: {RequestId} was cancelled, stopping chain.", args.RequestId);
            return;
        }

        try
        {
            request.StartProcessing();
            await _requestRepository.UpdateAsync(request, autoSave: true);

            var invoices = (await _pleService.GetOutstandingVouchersAsync(request.PartyType, request.PartyId))
                .Where(v => v.VoucherType is "SalesInvoice" or "PurchaseInvoice")
                .ToList();
            var payments = await _engine.GetUnreconciledPaymentsAsync(request.PartyType, request.PartyId);

            if (invoices.Count == 0 || payments.Count == 0)
            {
                request.Complete();
                await _requestRepository.UpdateAsync(request, autoSave: true);
                _logger.LogInformation(
                    "ProcessPaymentReconciliationJob: {RequestId} completed — no more outstanding invoices or unreconciled payments.",
                    request.Id);
                return;
            }

            // Bounded per-execution work: allocate against only the next invoice, not the whole list.
            var nextInvoice = invoices[0];
            var allocations = PaymentReconciliationEngine.AutoAllocate(payments, new[] { nextInvoice });

            if (allocations.Count == 0)
            {
                // Payments exist but none can be matched against this invoice's currency/party context
                // (AutoAllocate itself has no such filter today — reaching here in practice means the
                // remaining payments were already fully consumed by a concurrent process). Nothing more
                // this request can do.
                request.Complete();
                await _requestRepository.UpdateAsync(request, autoSave: true);
                return;
            }

            var company = await _companyRepository.GetAsync(request.CompanyId);
            var result = await _engine.ReconcileAndApplyAsync(
                request.CompanyId, request.PartyType, request.PartyId,
                request.ReceivablePayableAccountId, company.CurrencyCode, allocations);

            request.RecordProgress(result.ReconciledCount);
            await _requestRepository.UpdateAsync(request, autoSave: true);

            if (result.HasErrors)
            {
                _logger.LogWarning(
                    "ProcessPaymentReconciliationJob: {RequestId} had {ErrorCount} allocation errors on invoice {InvoiceId}.",
                    request.Id, result.Errors.Count, nextInvoice.VoucherId);
            }

            // Re-enqueue for the next invoice — the next execution's own outstanding/payments query
            // decides whether there's more to do (chained, not a fixed loop count).
            if (request.Status == ProcessPaymentReconciliationStatus.Running)
            {
                await _jobManager.EnqueueAsync(new ProcessPaymentReconciliationJobArgs
                {
                    RequestId = request.Id,
                    TenantId = request.TenantId,
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ProcessPaymentReconciliationJob: {RequestId} failed.", args.RequestId);
            request.RecordFailure(ex.Message);
            await _requestRepository.UpdateAsync(request, autoSave: true);
        }
    }
}

[Serializable]
public class ProcessPaymentReconciliationJobArgs
{
    public Guid RequestId { get; set; }
    public Guid? TenantId { get; set; }
}
