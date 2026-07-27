using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.Accounting.Entities;
using MyERP.Sales.Entities;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus;

namespace MyERP.Sales.EventHandlers;

/// <summary>
/// Auto-resolves dunnings when linked invoices are fully paid.
/// 
/// Per ERPNext dunning.py `update_linked_dunnings`:
/// - Fires after Payment Entry is posted (reduces invoice outstanding)
/// - Checks ALL submitted dunnings for the customer
/// - If ALL overdue_payments for a dunning have zero outstanding → auto-resolve
/// - Uses payment_schedule.outstanding (not invoice.outstanding_amount) for multi-currency
/// 
/// Per DO-NOT: "Skip dunning level sequencing" — resolved dunnings don't affect future levels.
/// </summary>
public class DunningAutoResolutionHandler :
    ILocalEventHandler<PaymentEntryPostedEvent>,
    ITransientDependency
{
    private readonly IRepository<Dunning, Guid> _dunningRepository;
    private readonly IRepository<SalesInvoice, Guid> _invoiceRepository;
    private readonly ILogger<DunningAutoResolutionHandler> _logger;

    public DunningAutoResolutionHandler(
        IRepository<Dunning, Guid> dunningRepository,
        IRepository<SalesInvoice, Guid> invoiceRepository,
        ILogger<DunningAutoResolutionHandler> logger)
    {
        _dunningRepository = dunningRepository;
        _invoiceRepository = invoiceRepository;
        _logger = logger;
    }

    public async Task HandleEventAsync(PaymentEntryPostedEvent eventData)
    {
        var pe = eventData.PaymentEntry;

        // Only relevant for customer payments (Receive type)
        if (pe.PartyType != "Customer" || pe.PartyId == null)
            return;

        try
        {
            // Find all submitted dunnings for this customer in this company
            var dunningQuery = await _dunningRepository.WithDetailsAsync();
            var submittedDunnings = dunningQuery
                .Where(d => d.CustomerId == pe.PartyId.Value
                         && d.CompanyId == pe.CompanyId
                         && d.Status == Core.DocumentStatus.Submitted)
                .ToList();

            if (!submittedDunnings.Any())
                return;

            // Get current outstanding for all invoices referenced in these dunnings
            var invoiceIds = submittedDunnings
                .SelectMany(d => d.OverduePayments)
                .Select(p => p.SalesInvoiceId)
                .Distinct()
                .ToList();

            var invoiceQuery = await _invoiceRepository.GetQueryableAsync();
            var outstandingMap = invoiceQuery
                .Where(si => invoiceIds.Contains(si.Id))
                .Select(si => new { si.Id, si.OutstandingAmount })
                .ToDictionary(si => si.Id, si => si.OutstandingAmount);

            // Check each dunning: if ALL linked invoices are fully paid, resolve it
            foreach (var dunning in submittedDunnings)
            {
                var allPaid = dunning.OverduePayments.All(p =>
                {
                    var currentOutstanding = outstandingMap.TryGetValue(p.SalesInvoiceId, out var val)
                        ? val : p.OutstandingAmount;
                    return currentOutstanding <= 0;
                });

                if (allPaid)
                {
                    dunning.Resolve();
                    await _dunningRepository.UpdateAsync(dunning);

                    _logger.LogInformation(
                        "Auto-resolved Dunning {DunningId} (Level {Level}) for Customer {CustomerId} — all invoices paid",
                        dunning.Id, dunning.DunningLevel, dunning.CustomerId);
                }
            }
        }
        catch (Exception ex)
        {
            // Non-blocking: dunning resolution failure should not roll back payment
            _logger.LogWarning(ex, "Failed to auto-resolve dunnings for PE {PaymentId}", pe.Id);
        }
    }
}
