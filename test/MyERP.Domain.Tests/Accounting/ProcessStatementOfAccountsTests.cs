using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using NSubstitute;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.Accounting;

/// <summary>
/// Unit tests for Batch Statement of Accounts generation, filtering, and aging calculations.
/// Verifies rules from erpnext/accounts/doctype/process_statement_of_accounts (#5998).
/// </summary>
public class ProcessStatementOfAccountsTests
{
    private readonly IRepository<SalesInvoice, Guid> _siRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
    private readonly IRepository<PurchaseInvoice, Guid> _piRepo = Substitute.For<IRepository<PurchaseInvoice, Guid>>();
    private readonly IRepository<PaymentEntry, Guid> _peRepo = Substitute.For<IRepository<PaymentEntry, Guid>>();
    private readonly IRepository<Customer, Guid> _customerRepo = Substitute.For<IRepository<Customer, Guid>>();
    private readonly IRepository<Supplier, Guid> _supplierRepo = Substitute.For<IRepository<Supplier, Guid>>();

    private readonly StatementOfAccountsAppService _appService;

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _customer1Id = Guid.NewGuid();
    private readonly Guid _customer2Id = Guid.NewGuid();

    public ProcessStatementOfAccountsTests()
    {
        _appService = new StatementOfAccountsAppService(
            _siRepo, _piRepo, _peRepo, _customerRepo, _supplierRepo);
    }

    [Fact]
    public async Task ProcessBatchStatementAsync_Customer_CalculatesBalancesAndAgingAccurately()
    {
        var customers = new List<Customer>
        {
            new Customer(_customer1Id, _companyId, "Alpha Traders"),
            new Customer(_customer2Id, _companyId, "Beta Corp")
        };
        _customerRepo.GetQueryableAsync().Returns(Task.FromResult(customers.AsQueryable()));

        var fromDate = new DateTime(2026, 6, 1);
        var toDate = new DateTime(2026, 6, 30);

        // Alpha: Prior Invoice 1000, Period Invoice 2000, Period Payment 500
        var si1 = new SalesInvoice(Guid.NewGuid(), _companyId, _customer1Id, "INV-2026-0001", new DateTime(2026, 5, 15));
        si1.AddItem(Guid.NewGuid(), "Item 1", 1m, 1000m, 0m);
        si1.Submit();
        si1.Post();

        var si2 = new SalesInvoice(Guid.NewGuid(), _companyId, _customer1Id, "INV-2026-0002", new DateTime(2026, 6, 10))
        {
            DueDate = new DateTime(2026, 6, 10)
        };
        si2.AddItem(Guid.NewGuid(), "Item 2", 1m, 2000m, 0m);
        si2.Submit();
        si2.Post();

        var salesInvoices = new List<SalesInvoice> { si1, si2 };
        _siRepo.GetQueryableAsync().Returns(Task.FromResult(salesInvoices.AsQueryable()));

        var pe1 = new PaymentEntry(Guid.NewGuid(), _companyId, PaymentType.Pay, new DateTime(2026, 6, 20), 500m, Guid.NewGuid(), Guid.NewGuid())
        {
            PartyType = "Customer",
            PartyId = _customer1Id
        };
        pe1.Submit();
        pe1.Post();
        var payments = new List<PaymentEntry> { pe1 };
        _peRepo.GetQueryableAsync().Returns(Task.FromResult(payments.AsQueryable()));

        var input = new BatchStatementOfAccountsInput
        {
            CompanyId = _companyId,
            PartyType = "Customer",
            FromDate = fromDate,
            ToDate = toDate,
            IncludeZeroBalance = false,
            IncludeAging = true
        };

        var result = await _appService.ProcessBatchStatementAsync(input);

        Assert.NotNull(result);
        Assert.Single(result.Statements); // Customer 2 has zero balance, filtered out
        var alpha = result.Statements[0];
        Assert.Equal(_customer1Id, alpha.PartyId);
        Assert.Equal("Alpha Traders", alpha.PartyName);
        Assert.Equal(1000m, alpha.OpeningBalance);
        Assert.Equal(2000m, alpha.InvoicedAmount);
        Assert.Equal(500m, alpha.PaidAmount);
        Assert.Equal(2500m, alpha.ClosingBalance);
        Assert.NotNull(alpha.Aging);
        Assert.Equal(3000m, alpha.Aging.TotalOutstanding); // 1000 (age 46 days -> 31-60) + 2000 (age 20 days -> 0-30)
        Assert.Equal(2000m, alpha.Aging.Current_0_30);
        Assert.Equal(1000m, alpha.Aging.Age_31_60);
    }

    [Fact]
    public async Task ProcessBatchStatementAsync_InvalidDateRange_ThrowsValidationException()
    {
        var input = new BatchStatementOfAccountsInput
        {
            CompanyId = _companyId,
            PartyType = "Customer",
            FromDate = new DateTime(2026, 6, 30),
            ToDate = new DateTime(2026, 6, 1) // from > to
        };

        var ex = await Assert.ThrowsAsync<BusinessException>(() => _appService.ProcessBatchStatementAsync(input));
        Assert.Equal(MyERPDomainErrorCodes.InvalidDateRange, ex.Code);
    }
}
