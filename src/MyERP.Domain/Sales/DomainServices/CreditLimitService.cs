using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Sales.Entities;
using MyERP.Settings;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Settings;

namespace MyERP.Sales.DomainServices;

/// <summary>
/// Validates customer credit limit before allowing transaction submission.
/// Credit limit check is enforced at SO submit, DN submit, and SI submit (per ERPNext rules).
/// Resolution: per-company CustomerCreditLimit → fallback to Customer.CreditLimit (global).
/// Per DO-NOT: "Implement credit limit check only at SO — must also enforce at DN and SI submit"
/// </summary>
public class CreditLimitService : DomainService
{
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IRepository<SalesInvoice, Guid> _invoiceRepository;
    private readonly IRepository<SalesOrder, Guid> _orderRepository;
    private readonly IRepository<CustomerCreditLimit, Guid> _creditLimitRepository;
    private readonly ISettingProvider _settingProvider;

    public CreditLimitService(
        IRepository<Customer, Guid> customerRepository,
        IRepository<SalesInvoice, Guid> invoiceRepository,
        IRepository<SalesOrder, Guid> orderRepository,
        IRepository<CustomerCreditLimit, Guid> creditLimitRepository,
        ISettingProvider settingProvider)
    {
        _customerRepository = customerRepository;
        _invoiceRepository = invoiceRepository;
        _orderRepository = orderRepository;
        _creditLimitRepository = creditLimitRepository;
        _settingProvider = settingProvider;
    }

    /// <summary>
    /// Validates that the customer's credit limit is not exceeded by the new transaction amount.
    /// Resolution chain: per-company CustomerCreditLimit → Customer.CreditLimit (global fallback).
    /// Per-company bypass flag, and the configured Credit Controller role, both skip the check
    /// entirely (blanket bypass, enforced nowhere). Customer.BypassCreditLimitCheckAtSalesOrder
    /// is narrower — pass isAtSalesOrder=true only from the Sales Order submit call site; Delivery
    /// Note and Sales Invoice submit must keep calling this with isAtSalesOrder=false (the default)
    /// so they still enforce the limit even for a customer whose SO check is bypassed.
    /// Outstanding = sum of unpaid posted invoices for the company.
    /// </summary>
    public async Task ValidateCreditLimitAsync(
        Guid customerId, decimal newTransactionAmount, Guid? companyId = null,
        string[]? currentUserRoles = null, bool isAtSalesOrder = false)
    {
        var customer = await _customerRepository.GetAsync(customerId);

        var controllerRole = await _settingProvider.GetOrNullAsync(MyERPSettings.Selling.CreditControllerRole);
        if (!string.IsNullOrWhiteSpace(controllerRole) && currentUserRoles != null
            && currentUserRoles.Contains(controllerRole, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        if (isAtSalesOrder && customer.BypassCreditLimitCheckAtSalesOrder)
            return;

        // Resolve per-company credit limit if company is specified
        decimal creditLimit = customer.CreditLimit;
        bool bypass = false;

        if (companyId.HasValue)
        {
            var perCompanyLimits = await _creditLimitRepository.GetQueryableAsync();
            var companyLimit = perCompanyLimits.FirstOrDefault(
                cl => cl.CustomerId == customerId && cl.CompanyId == companyId.Value);

            if (companyLimit != null)
            {
                if (companyLimit.BypassCreditLimitCheck)
                {
                    bypass = true;
                }
                else
                {
                    creditLimit = companyLimit.CreditLimit;
                }
            }
        }

        // Bypass flag = no limit check for this company
        if (bypass)
            return;

        // No limit set = unlimited credit (0 = no enforcement)
        if (creditLimit <= 0)
            return;

        var outstanding = await GetCustomerOutstandingAsync(customerId, companyId);
        var totalExposure = outstanding + newTransactionAmount;

        if (totalExposure > creditLimit)
        {
            throw new BusinessException("MyERP:03002")
                .WithData("customerName", customer.Name)
                .WithData("creditLimit", creditLimit)
                .WithData("outstanding", outstanding)
                .WithData("newAmount", newTransactionAmount);
        }
    }

    /// <summary>
    /// Validates that the customer does not have overdue invoices exceeding the configured threshold.
    /// Per ERPNext check_overdue_billing_threshold(): blocks new SI when overdue amount exceeds threshold.
    /// Gated by Accounts Settings.EnableOverdueBillingThreshold — off by default, matching the
    /// pre-existing Angular toggle that (until this fix) had nothing behind it to actually gate.
    /// Role bypass via Accounts Settings.OverdueBillingBypassRole, mirroring the
    /// MaintainSameRate/RoleToOverrideStopAction bypass-role pattern used elsewhere in this codebase.
    /// Resolution: per-company CustomerCreditLimit.OverdueBillingThreshold → 0 = disabled.
    /// </summary>
    public async Task ValidateOverdueBillingThresholdAsync(
        Guid customerId, Guid companyId, string[]? currentUserRoles = null)
    {
        var enabled = await _settingProvider.GetOrNullAsync(MyERPSettings.Accounts.EnableOverdueBillingThreshold);
        if (enabled != "true") return;

        var bypassRole = await _settingProvider.GetOrNullAsync(MyERPSettings.Accounts.OverdueBillingBypassRole);
        if (!string.IsNullOrWhiteSpace(bypassRole) && currentUserRoles != null
            && currentUserRoles.Contains(bypassRole, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        // Resolve per-company overdue threshold
        var perCompanyLimits = await _creditLimitRepository.GetQueryableAsync();
        var companyLimit = perCompanyLimits.FirstOrDefault(
            cl => cl.CustomerId == customerId && cl.CompanyId == companyId);

        var threshold = companyLimit?.OverdueBillingThreshold ?? 0;

        // 0 = disabled (no overdue enforcement)
        if (threshold <= 0)
            return;

        // Calculate overdue amount: posted invoices past due date with outstanding > 0
        // Per ERPNext PR #57786 (commit b5a3815a64): reads from Payment Ledger to reflect post-submit settlements
        var today = DateTime.UtcNow.Date;
        var pleService = LazyServiceProvider.LazyGetService<Accounting.DomainServices.PaymentLedgerService>();
        decimal overdueAmount = 0m;

        if (pleService != null)
        {
            overdueAmount = await pleService.GetOverdueAmountAsync("Customer", customerId, companyId, today);
        }

        if (overdueAmount == 0)
        {
            var invoiceQuery = await _invoiceRepository.GetQueryableAsync();
            overdueAmount = invoiceQuery
                .Where(i => i.CustomerId == customerId
                    && i.CompanyId == companyId
                    && i.Status == Core.DocumentStatus.Posted
                    && i.GrandTotal > i.AmountPaid
                    && i.DueDate < today)
                .Sum(i => i.GrandTotal - i.AmountPaid);
        }

        if (overdueAmount > threshold)
        {
            var customer = await _customerRepository.GetAsync(customerId);
            throw new BusinessException("MyERP:03021")
                .WithData("customerName", customer.Name)
                .WithData("overdueAmount", overdueAmount)
                .WithData("threshold", threshold);
        }
    }

    /// <summary>
    /// Gets total outstanding amount for a customer (posted invoices with outstanding > 0).
    /// Optionally scoped to a specific company.
    /// </summary>
    public async Task<decimal> GetCustomerOutstandingAsync(Guid customerId, Guid? companyId = null)
    {
        var invoiceQuery = await _invoiceRepository.GetQueryableAsync();
        var query = invoiceQuery
            .Where(i => i.CustomerId == customerId
                && i.Status == Core.DocumentStatus.Posted
                && i.GrandTotal > i.AmountPaid);

        if (companyId.HasValue)
            query = query.Where(i => i.CompanyId == companyId.Value);

        var outstanding = query.Sum(i => i.GrandTotal - i.AmountPaid);
        return outstanding;
    }

    /// <summary>
    /// Validates customer credit limit configuration (Gotcha #302):
    /// (1) Same company cannot appear twice in credit_limit list.
    /// (2) New credit limit cannot be set below current outstanding amount for that company.
    /// </summary>
    public async Task ValidateCustomerCreditLimitsAsync(
        Guid customerId, IEnumerable<CustomerCreditLimit> creditLimits)
    {
        var list = creditLimits.ToList();
        var duplicates = list.GroupBy(cl => cl.CompanyId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicates.Any())
        {
            throw new BusinessException(MyERPDomainErrorCodes.DuplicateRecord)
                .WithData("reason", "Same company cannot appear twice in customer credit limits");
        }

        foreach (var limit in list)
        {
            if (limit.CreditLimit > 0 && !limit.BypassCreditLimitCheck)
            {
                var outstanding = await GetCustomerOutstandingAsync(customerId, limit.CompanyId);
                if (limit.CreditLimit < outstanding)
                {
                    throw new BusinessException("MyERP:03002")
                        .WithData("reason", "Cannot set credit limit below current outstanding amount")
                        .WithData("creditLimit", limit.CreditLimit)
                        .WithData("outstanding", outstanding);
                }
            }
        }
    }
}
