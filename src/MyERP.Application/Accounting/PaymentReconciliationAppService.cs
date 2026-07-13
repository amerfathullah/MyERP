using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

/// <summary>
/// Payment Reconciliation AppService — matches unallocated payments to outstanding invoices.
/// Implements the ERPNext "Payment Reconciliation" tool pattern:
/// 1. Get outstanding invoices for a party
/// 2. Reconcile (allocate payment amounts to specific invoices)
/// 3. Unreconcile (delink a previous allocation)
/// </summary>
[Authorize(MyERPPermissions.PaymentEntries.Default)]
public class PaymentReconciliationAppService : ApplicationService
{
    private readonly PaymentLedgerService _pleService;
    private readonly IRepository<PaymentLedgerEntry, Guid> _pleRepository;
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepository;
    private readonly IRepository<PurchaseInvoice, Guid> _purchaseInvoiceRepository;

    public PaymentReconciliationAppService(
        PaymentLedgerService pleService,
        IRepository<PaymentLedgerEntry, Guid> pleRepository,
        IRepository<SalesInvoice, Guid> salesInvoiceRepository,
        IRepository<PurchaseInvoice, Guid> purchaseInvoiceRepository)
    {
        _pleService = pleService;
        _pleRepository = pleRepository;
        _salesInvoiceRepository = salesInvoiceRepository;
        _purchaseInvoiceRepository = purchaseInvoiceRepository;
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
    /// Creates PLE entries linking each payment to the allocated invoice, reducing outstanding.
    /// Also updates the invoice entity's AmountPaid.
    /// </summary>
    public async Task ReconcileAsync(ReconcilePaymentDto input)
    {
        // Get default account for the party (simplified — uses first PLE entry's account)
        var pleQuery = await _pleRepository.GetQueryableAsync();
        var partyAccount = pleQuery
            .Where(p => p.PartyType == input.PartyType && p.PartyId == input.PartyId)
            .Select(p => p.AccountId)
            .FirstOrDefault();

        foreach (var alloc in input.Allocations)
        {
            // Create PLE reconciliation entry (validates stale outstanding)
            await _pleService.ReconcileAsync(
                companyId: input.CompanyId,
                postingDate: Clock.Now.Date,
                accountId: partyAccount,
                partyType: input.PartyType,
                partyId: input.PartyId,
                paymentVoucherType: alloc.PaymentVoucherType,
                paymentVoucherId: alloc.PaymentVoucherId,
                invoiceVoucherType: alloc.InvoiceVoucherType,
                invoiceVoucherId: alloc.InvoiceVoucherId,
                allocatedAmount: alloc.AllocatedAmount,
                allocatedAmountInAccountCurrency: alloc.AllocatedAmount,
                accountCurrency: "MYR");

            // Update the invoice's AmountPaid
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
    /// Sets PLE entries as delinked and reduces AmountPaid on the invoice.
    /// </summary>
    public async Task UnreconcileAsync(UnreconcileDto input)
    {
        // Get the amount that was allocated (sum of non-delinked PLE for this pair)
        var pleQuery = await _pleRepository.GetQueryableAsync();
        var allocatedAmount = pleQuery
            .Where(p => p.VoucherType == input.PaymentVoucherType
                && p.VoucherId == input.PaymentVoucherId
                && p.AgainstVoucherType == input.InvoiceVoucherType
                && p.AgainstVoucherId == input.InvoiceVoucherId
                && !p.Delinked)
            .Sum(p => Math.Abs(p.AmountInAccountCurrency));

        // Delink the PLE entries
        await _pleService.UnreconcileAsync(
            input.PaymentVoucherType,
            input.PaymentVoucherId,
            input.InvoiceVoucherType,
            input.InvoiceVoucherId);

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
