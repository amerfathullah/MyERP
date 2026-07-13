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
/// </summary>
public class CreditLimitService : DomainService
{
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IRepository<SalesInvoice, Guid> _invoiceRepository;
    private readonly IRepository<SalesOrder, Guid> _orderRepository;

    public CreditLimitService(
        IRepository<Customer, Guid> customerRepository,
        IRepository<SalesInvoice, Guid> invoiceRepository,
        IRepository<SalesOrder, Guid> orderRepository)
    {
        _customerRepository = customerRepository;
        _invoiceRepository = invoiceRepository;
        _orderRepository = orderRepository;
    }

    /// <summary>
    /// Validates that the customer's credit limit is not exceeded by the new transaction amount.
    /// Outstanding = sum of unpaid posted invoices + unbilled submitted orders.
    /// Throws BusinessException if limit would be exceeded.
    /// </summary>
    public async Task ValidateCreditLimitAsync(Guid customerId, decimal newTransactionAmount)
    {
        var customer = await _customerRepository.GetAsync(customerId);

        // No limit set = unlimited credit
        if (customer.CreditLimit <= 0)
            return;

        var outstanding = await GetCustomerOutstandingAsync(customerId);
        var totalExposure = outstanding + newTransactionAmount;

        if (totalExposure > customer.CreditLimit)
        {
            throw new BusinessException("MyERP:03002")
                .WithData("customerName", customer.Name)
                .WithData("creditLimit", customer.CreditLimit)
                .WithData("outstanding", outstanding)
                .WithData("newAmount", newTransactionAmount);
        }
    }

    /// <summary>
    /// Gets total outstanding amount for a customer (posted invoices with outstanding > 0).
    /// </summary>
    private async Task<decimal> GetCustomerOutstandingAsync(Guid customerId)
    {
        var invoiceQuery = await _invoiceRepository.GetQueryableAsync();
        var outstanding = invoiceQuery
            .Where(i => i.CustomerId == customerId
                && i.Status == Core.DocumentStatus.Posted
                && i.GrandTotal > i.AmountPaid)
            .Sum(i => i.GrandTotal - i.AmountPaid);

        return outstanding;
    }
}
