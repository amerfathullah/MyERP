using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Sales.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

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

    public CreditLimitService(
        IRepository<Customer, Guid> customerRepository,
        IRepository<SalesInvoice, Guid> invoiceRepository,
        IRepository<SalesOrder, Guid> orderRepository,
        IRepository<CustomerCreditLimit, Guid> creditLimitRepository)
    {
        _customerRepository = customerRepository;
        _invoiceRepository = invoiceRepository;
        _orderRepository = orderRepository;
        _creditLimitRepository = creditLimitRepository;
    }

    /// <summary>
    /// Validates that the customer's credit limit is not exceeded by the new transaction amount.
    /// Resolution chain: per-company CustomerCreditLimit → Customer.CreditLimit (global fallback).
    /// Per-company bypass flag skips the check entirely.
    /// Outstanding = sum of unpaid posted invoices for the company.
    /// </summary>
    public async Task ValidateCreditLimitAsync(Guid customerId, decimal newTransactionAmount, Guid? companyId = null)
    {
        var customer = await _customerRepository.GetAsync(customerId);

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
    /// Gets total outstanding amount for a customer (posted invoices with outstanding > 0).
    /// Optionally scoped to a specific company.
    /// </summary>
    private async Task<decimal> GetCustomerOutstandingAsync(Guid customerId, Guid? companyId = null)
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
}
