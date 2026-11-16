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
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Shared;

public class DateRangeRegressionTests
{
    [Fact]
    public void DateRangeHelper_NormalizesStartOfDayAndExclusiveNextDay()
    {
        var input = new DateTime(2026, 11, 16, 15, 42, 10);

        DateRangeHelper.StartOfDay(input).ShouldBe(new DateTime(2026, 11, 16, 0, 0, 0));
        DateRangeHelper.ExclusiveNextDay(input).ShouldBe(new DateTime(2026, 11, 17, 0, 0, 0));
    }

    [Fact]
    public async Task StatementOfAccounts_GetAllPartyStatements_IncludesSameDayAfternoonInvoices()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var customerRepo = Substitute.For<IRepository<Customer, Guid>>();
        customerRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Customer>
            {
                new Customer(customerId, companyId, "Evening Buyer Sdn Bhd")
            }.AsQueryable()));

        var afternoonInv = new SalesInvoice(
            Guid.NewGuid(),
            companyId,
            customerId,
            "SINV-PM-001",
            new DateTime(2026, 11, 16, 18, 30, 0));
        afternoonInv.AddItem(Guid.NewGuid(), "Service", 1m, 450m, 0m);
        afternoonInv.Submit();
        afternoonInv.Post();

        var siRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        siRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<SalesInvoice> { afternoonInv }.AsQueryable()));

        var piRepo = Substitute.For<IRepository<PurchaseInvoice, Guid>>();
        var peRepo = Substitute.For<IRepository<PaymentEntry, Guid>>();
        peRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<PaymentEntry>().AsQueryable()));
        var supRepo = Substitute.For<IRepository<Supplier, Guid>>();

        var service = new StatementOfAccountsAppService(
            siRepo,
            piRepo,
            peRepo,
            customerRepo,
            supRepo);

        var result = await service.ProcessBatchStatementAsync(new BatchStatementOfAccountsInput
        {
            CompanyId = companyId,
            PartyType = "Customer",
            FromDate = new DateTime(2026, 11, 16),
            ToDate = new DateTime(2026, 11, 16),
            IncludeZeroBalance = false,
            IncludeAging = true
        });

        result.Statements.Count.ShouldBe(1);
        result.Statements[0].InvoicedAmount.ShouldBe(450m);
        result.Statements[0].ClosingBalance.ShouldBe(450m);
    }
}
