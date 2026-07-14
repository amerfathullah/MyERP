using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

/// <summary>
/// Payment Reconciliation AppService — matches unallocated payments to outstanding invoices.
/// Delegates to PaymentReconciliationEngine for business logic.
/// </summary>
[Authorize(MyERPPermissions.PaymentEntries.Default)]
public class PaymentReconciliationAppService : ApplicationService
{
    private readonly PaymentReconciliationEngine _engine;
    private readonly PaymentLedgerService _pleService;
    private readonly IRepository<PaymentLedgerEntry, Guid> _pleRepository;
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepository;
    private readonly IRepository<PurchaseInvoice, Guid> _purchaseInvoiceRepository;
    private readonly IRepository<Company, Guid> _companyRepository;

    public PaymentReconciliationAppService(
        PaymentReconciliationEngine engine,
        PaymentLedgerService pleService,
        IRepository<PaymentLedgerEntry, Guid> pleRepository,
        IRepository<SalesInvoice, Guid> salesInvoiceRepository,
        IRepository<PurchaseInvoice, Guid> purchaseInvoiceRepository,
        IRepository<Company, Guid> companyRepository)
    {
        _engine = engine;
        _pleService = pleService;
        _pleRepository = pleRepository;
        _salesInvoiceRepository = salesInvoiceRepository;
        _purchaseInvoiceRepository = purchaseInvoiceRepository;
        _companyRepository = companyRepository;
    }

    /// <summary>
    /// Get all outstanding invoices for a party (for reconciliation UI).
    /// </summary>
    public async Task<List<OutstandingInvoiceDto>> GetOutstandingInvoicesAsync(string partyType, Guid partyId)
    {
        var vouchers = await _pleService.GetOutstandingVouchersAsync(partyType, partyId);
        return vouchers.Select(v => new OutstandingInvoiceDto
        {
            VoucherId = v.VoucherId,
            VoucherType = v.VoucherType,
            Outstanding = v.Outstanding,
        }).ToList();
    }

    /// <summary>
    /// Reconcile payments against invoices.
    /// Delegates to PaymentReconciliationEngine for stale-outstanding validation
    /// and batch processing. Resolves currency from company (not hardcoded).
    /// </summary>
    public async Task ReconcileAsync(ReconcilePaymentDto input)
    {
        // Resolve account currency from company (not hardcoded)
        var company = await _companyRepository.GetAsync(input.CompanyId);
        var accountCurrency = company.CurrencyCode;

        // Resolve party account
        var pleQuery = await _pleRepository.GetQueryableAsync();
        var partyAccount = pleQuery
            .Where(p => p.PartyType == input.PartyType && p.PartyId == input.PartyId)
            .Select(p => p.AccountId)
            .FirstOrDefault();

        // Build allocation list for engine
        var allocations = input.Allocations.Select(a => new ReconciliationAllocation
        {
            PaymentVoucherType = a.PaymentVoucherType,
            PaymentVoucherId = a.PaymentVoucherId,
            InvoiceVoucherType = a.InvoiceVoucherType,
            InvoiceVoucherId = a.InvoiceVoucherId,
            AllocatedAmount = a.AllocatedAmount,
        }).ToList();

        // Delegate to engine (handles stale-outstanding, batch, error isolation)
        var result = await _engine.ReconcileBatchAsync(
            input.CompanyId, input.PartyType, input.PartyId,
            partyAccount, accountCurrency, allocations);

        // Update invoice AmountPaid for successful allocations
        foreach (var alloc in input.Allocations)
        {
            // Skip allocations that failed in engine
            if (result.Errors.Any(e => e.InvoiceVoucherId == alloc.InvoiceVoucherId))
                continue;

            if (alloc.InvoiceVoucherType == "SalesInvoice")
            {
                var si = await _salesInvoiceRepository.GetAsync(alloc.InvoiceVoucherId);
                si.AmountPaid += alloc.AllocatedAmount;
                await _salesInvoiceRepository.UpdateAsync(si);
            }
            else if (alloc.InvoiceVoucherType == "PurchaseInvoice")
            {
                var pi = await _purchaseInvoiceRepository.GetAsync(alloc.InvoiceVoucherId);
                pi.AmountPaid += alloc.AllocatedAmount;
                await _purchaseInvoiceRepository.UpdateAsync(pi);
            }
        }
    }

    /// <summary>
    /// Unreconcile a previous payment-to-invoice allocation.
    /// Delegates to PaymentReconciliationEngine which handles delink + amount tracking.
    /// </summary>
    public async Task UnreconcileAsync(UnreconcileDto input)
    {
        // Engine returns the allocated amount that was delinked
        var allocatedAmount = await _engine.UnreconcileAsync(
            input.PaymentVoucherType, input.PaymentVoucherId,
            input.InvoiceVoucherType, input.InvoiceVoucherId);

        // Reduce the invoice's AmountPaid
        if (allocatedAmount > 0)
        {
            if (input.InvoiceVoucherType == "SalesInvoice")
            {
                var si = await _salesInvoiceRepository.GetAsync(input.InvoiceVoucherId);
                si.AmountPaid = Math.Max(0, si.AmountPaid - allocatedAmount);
                await _salesInvoiceRepository.UpdateAsync(si);
            }
            else if (input.InvoiceVoucherType == "PurchaseInvoice")
            {
                var pi = await _purchaseInvoiceRepository.GetAsync(input.InvoiceVoucherId);
                pi.AmountPaid = Math.Max(0, pi.AmountPaid - allocatedAmount);
                await _purchaseInvoiceRepository.UpdateAsync(pi);
            }
        }
    }
}
