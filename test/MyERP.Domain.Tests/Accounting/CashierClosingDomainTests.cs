using System;
using MyERP.Accounting.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Accounting;

public class CashierClosingDomainTests
{
    [Fact]
    public void Should_Calculate_NetAmount_Correctly()
    {
        var id = Guid.NewGuid();
        var closing = new CashierClosing(
            id,
            closingNumber: "POS-CLO-20260826-0001",
            userId: Guid.NewGuid(),
            userName: "cashier1",
            date: DateTime.UtcNow.Date,
            fromTime: new TimeSpan(8, 0, 0),
            toTime: new TimeSpan(17, 0, 0),
            expense: 50,
            custody: 200,
            returns: 30,
            outstandingAmount: 100);

        closing.AddPayment(Guid.NewGuid(), "Cash", 500);
        closing.AddPayment(Guid.NewGuid(), "Credit Card", 300);

        // net_amount = total_payments (800) + outstanding (100) + expense (50) - custody (200) + returns (30) = 780
        closing.NetAmount.ShouldBe(780);
    }

    [Fact]
    public void Should_Throw_When_FromTime_GreaterOrEqual_ToTime()
    {
        Should.Throw<BusinessException>(() =>
        {
            new CashierClosing(
                Guid.NewGuid(),
                "POS-CLO-TEST",
                Guid.NewGuid(),
                "cashier1",
                DateTime.UtcNow.Date,
                fromTime: new TimeSpan(18, 0, 0),
                toTime: new TimeSpan(8, 0, 0));
        });
    }

    [Fact]
    public void Should_Submit_Successfully()
    {
        var closing = new CashierClosing(
            Guid.NewGuid(),
            "POS-CLO-002",
            Guid.NewGuid(),
            "cashier1",
            DateTime.UtcNow.Date,
            fromTime: new TimeSpan(9, 0, 0),
            toTime: new TimeSpan(18, 0, 0));

        closing.Submit();
        closing.IsSubmitted.ShouldBeTrue();
    }
}
