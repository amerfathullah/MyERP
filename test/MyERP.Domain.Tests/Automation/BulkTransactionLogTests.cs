using System;
using MyERP.Automation.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Automation;

public class BulkTransactionLogTests
{
    [Fact]
    public void Should_Create_Log_And_Handle_Lifecycle()
    {
        var logId = Guid.NewGuid();
        var log = new BulkTransactionLog(logId, "Sales Order Batch Conversion", DateTime.UtcNow);

        log.Title.ShouldBe("Sales Order Batch Conversion");
        log.TotalEntries.ShouldBe(0);

        var detail1Id = Guid.NewGuid();
        var d1 = log.AddDetail(detail1Id, "SO-2026-0001", "Sales Order", "Delivery Note");
        log.TotalEntries.ShouldBe(1);
        d1.Status.ShouldBe(BulkTransactionStatus.Queued);

        var detail2Id = Guid.NewGuid();
        var d2 = log.AddDetail(detail2Id, "SO-2026-0002", "Sales Order", "Delivery Note");
        log.TotalEntries.ShouldBe(2);

        // Record Success for detail1
        log.RecordSuccess(detail1Id);
        d1.Status.ShouldBe(BulkTransactionStatus.Success);
        log.SucceededCount.ShouldBe(1);
        log.FailedCount.ShouldBe(0);

        // Record Failure for detail2
        log.RecordFailure(detail2Id, "Insufficient inventory in warehouse");
        d2.Status.ShouldBe(BulkTransactionStatus.Failed);
        d2.ErrorDescription.ShouldNotBeNull();
        d2.ErrorDescription.ShouldContain("Insufficient inventory");
        log.SucceededCount.ShouldBe(1);
        log.FailedCount.ShouldBe(1);

        // Retry detail2
        log.RetryDetail(detail2Id);
        d2.Status.ShouldBe(BulkTransactionStatus.Retried);
        d2.RetriedCount.ShouldBe(1);
        log.FailedCount.ShouldBe(0);
    }
}
