using System;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Sales;

public class SubscriptionCancellationTests
{
    private readonly SubscriptionBillingEngine _engine;

    public SubscriptionCancellationTests()
    {
        _engine = new SubscriptionBillingEngine(null!);
    }

    [Fact]
    public void DetermineStatus_WhenCancelled_StaysCancelledEvenWithoutOutstandingInvoices()
    {
        var sub = new Subscription(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Customer", DateTime.UtcNow.Date, "Monthly");
        sub.Cancel(DateTime.UtcNow.Date);

        var status = _engine.DetermineStatus(sub, DateTime.UtcNow.Date, hasOutstandingInvoices: false, isFullyRefunded: false);
        status.ShouldBe(SubscriptionStatus.Cancelled);
        sub.CancellationDate.ShouldNotBeNull();
    }

    [Fact]
    public void DetermineStatus_WhenCancelAtPeriodEndAndPeriodEnded_ReturnsCancelled()
    {
        var startDate = new DateTime(2026, 1, 1);
        var periodEnd = new DateTime(2026, 1, 31);
        var sub = new Subscription(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Customer", startDate, "Monthly")
        {
            CancelAtPeriodEnd = true,
            CurrentInvoiceStart = startDate,
            CurrentInvoiceEnd = periodEnd
        };

        // Before period end -> Active
        var statusBefore = _engine.DetermineStatus(sub, new DateTime(2026, 1, 15), hasOutstandingInvoices: false, isFullyRefunded: false);
        statusBefore.ShouldBe(SubscriptionStatus.Active);

        // At/after period end -> Cancelled
        var statusAfter = _engine.DetermineStatus(sub, new DateTime(2026, 2, 1), hasOutstandingInvoices: false, isFullyRefunded: false);
        statusAfter.ShouldBe(SubscriptionStatus.Cancelled);
    }

    [Fact]
    public void Reactivate_WhenCancelled_Throws()
    {
        var sub = new Subscription(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Customer", DateTime.UtcNow.Date, "Monthly");
        sub.Cancel();

        var ex = Should.Throw<BusinessException>(() => sub.Reactivate());
        ex.Code.ShouldBe(MyERPDomainErrorCodes.InvalidStatusTransition);
    }
}
