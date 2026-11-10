using System;
using System.Collections.Generic;
using MyERP.Accounting.Entities;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Domain.Tests.Sales;

/// <summary>
/// Unit tests for Dunning payment schedule allocation and cross-currency balances per
/// ERPNext PR #64585 / commit d38249cba3 and PR #64509 / commit 6838a4c395.
/// </summary>
public class DunningScheduleAndCurrencyParityTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();

    [Fact]
    public void GetPaymentScheduleOutstanding_DeductsUnallocatedPayments_WhenForeignCurrency()
    {
        var invoice = new SalesInvoice(Guid.NewGuid(), _companyId, _customerId, "SINV-USD-001", DateTime.UtcNow.Date.AddDays(-30))
        {
            CurrencyCode = "USD",
            ExchangeRate = 4.5m,
            GrandTotal = 200m,
            AmountPaid = 50m // 50 USD unallocated payment
        };

        var sched1 = new PaymentScheduleEntry(Guid.NewGuid(), "SalesInvoice", invoice.Id, DateTime.UtcNow.Date.AddDays(-20), 50m, 100m);
        var sched2 = new PaymentScheduleEntry(Guid.NewGuid(), "SalesInvoice", invoice.Id, DateTime.UtcNow.Date.AddDays(-10), 50m, 100m);

        var schedules = new List<PaymentScheduleEntry> { sched1, sched2 };

        var scheduleMap = DunningManager.GetPaymentScheduleOutstanding(
            invoice, schedules, partyAccountCurrency: "MYR", inAccountCurrency: false);

        // Oldest installment (sched1) has 50 USD paid from unallocated, remaining 50 USD
        scheduleMap[sched1.Id].ShouldBe(50m);
        // Second installment (sched2) has full 100 USD remaining
        scheduleMap[sched2.Id].ShouldBe(100m);
        // Total schedule outstanding equals invoice.OutstandingAmount (150 USD)
        (scheduleMap[sched1.Id] + scheduleMap[sched2.Id]).ShouldBe(invoice.OutstandingAmount);
    }

    [Fact]
    public void GetPaymentScheduleOutstanding_InAccountCurrency_ConvertsAndDeducts()
    {
        var invoice = new SalesInvoice(Guid.NewGuid(), _companyId, _customerId, "SINV-USD-002", DateTime.UtcNow.Date.AddDays(-30))
        {
            CurrencyCode = "USD",
            ExchangeRate = 4.5m,
            GrandTotal = 200m,
            AmountPaid = 50m
        };

        var sched1 = new PaymentScheduleEntry(Guid.NewGuid(), "SalesInvoice", invoice.Id, DateTime.UtcNow.Date.AddDays(-20), 50m, 100m);
        var sched2 = new PaymentScheduleEntry(Guid.NewGuid(), "SalesInvoice", invoice.Id, DateTime.UtcNow.Date.AddDays(-10), 50m, 100m);

        var schedules = new List<PaymentScheduleEntry> { sched1, sched2 };

        var scheduleMap = DunningManager.GetPaymentScheduleOutstanding(
            invoice, schedules, partyAccountCurrency: "MYR", inAccountCurrency: true);

        // In account currency (MYR):
        // sched1: 50 USD * 4.5 = 225 MYR
        scheduleMap[sched1.Id].ShouldBe(225m);
        // sched2: 100 USD * 4.5 = 450 MYR
        scheduleMap[sched2.Id].ShouldBe(450m);
        // Total = 675 MYR = 150 USD * 4.5
        (scheduleMap[sched1.Id] + scheduleMap[sched2.Id]).ShouldBe(invoice.OutstandingAmount * invoice.ExchangeRate);
    }

    [Fact]
    public void Dunning_AddOverduePayment_MaintainsTransactionCurrencyOutstanding()
    {
        var dunning = new Dunning(Guid.NewGuid(), _companyId, _customerId, DateTime.UtcNow.Date, 1)
        {
            Currency = "USD",
            ConversionRate = 4.5m
        };

        var invoiceId = Guid.NewGuid();
        // Overdue payment row records 100 USD outstanding
        dunning.AddOverduePayment(invoiceId, 100m, DateTime.UtcNow.Date.AddDays(-15), 15);

        dunning.TotalOutstanding.ShouldBe(100m);
        dunning.GrandTotal.ShouldBe(100m);
    }
}
