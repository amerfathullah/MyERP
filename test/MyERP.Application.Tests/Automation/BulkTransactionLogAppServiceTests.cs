using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Automation;

public abstract class BulkTransactionLogAppServiceTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IBulkTransactionLogAppService _service;

    protected BulkTransactionLogAppServiceTests()
    {
        _service = GetRequiredService<IBulkTransactionLogAppService>();
    }

    [Fact]
    public async Task BulkTransactionLog_Should_Support_End_To_End_Execution()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var log = await _service.CreateAsync(new CreateBulkTransactionLogDto
            {
                Title = "Sales Order Mass Processing",
                BatchDate = DateTime.UtcNow,
                Details = new List<CreateBulkTransactionLogDetailDto>
                {
                    new()
                    {
                        TransactionName = "SO-2026-9999",
                        FromDocType = "Sales Order",
                        ToDocType = "Sales Invoice"
                    }
                }
            });

            log.Id.ShouldNotBe(Guid.Empty);
            log.Details.Count.ShouldBe(1);
            var detailId = log.Details[0].Id;

            // Fail detail
            var failedLog = await _service.RecordDetailResultAsync(log.Id, detailId, new RecordBulkTransactionResultDto
            {
                IsSuccess = false,
                ErrorDescription = "Customer credit limit exceeded"
            });
            failedLog.FailedCount.ShouldBe(1);

            // Retry detail
            var retriedLog = await _service.RetryDetailAsync(log.Id, detailId);
            retriedLog.Details.ShouldContain(d => d.Id == detailId && d.Status == BulkTransactionStatus.Retried);
        });
    }

    [Fact]
    public async Task RetryDetailAsync_WhenLaterRunAlreadySucceeded_MarksRetriedWithoutChangingFailedStatus()
    {
        // Per ERPNext PR #60007 / commit a439708aad: do not re-execute if already succeeded in later run
        await WithUnitOfWorkAsync(async () =>
        {
            var firstLog = await _service.CreateAsync(new CreateBulkTransactionLogDto
            {
                Title = "Run 1",
                BatchDate = DateTime.UtcNow.AddMinutes(-10),
                Details = new List<CreateBulkTransactionLogDetailDto>
                {
                    new()
                    {
                        TransactionName = "SO-2026-TEST-RETRY",
                        FromDocType = "Sales Order",
                        ToDocType = "Sales Invoice"
                    }
                }
            });

            var firstDetailId = firstLog.Details[0].Id;
            await _service.RecordDetailResultAsync(firstLog.Id, firstDetailId, new RecordBulkTransactionResultDto
            {
                IsSuccess = false,
                ErrorDescription = "Failed initial attempt"
            });

            // Simulate later successful run for same source doc
            var laterLog = await _service.CreateAsync(new CreateBulkTransactionLogDto
            {
                Title = "Run 2 - Later Successful",
                BatchDate = DateTime.UtcNow,
                Details = new List<CreateBulkTransactionLogDetailDto>
                {
                    new()
                    {
                        TransactionName = "SO-2026-TEST-RETRY",
                        FromDocType = "Sales Order",
                        ToDocType = "Sales Invoice"
                    }
                }
            });

            var laterDetailId = laterLog.Details[0].Id;
            await _service.RecordDetailResultAsync(laterLog.Id, laterDetailId, new RecordBulkTransactionResultDto
            {
                IsSuccess = true
            });

            // Retrying the first failed detail should detect the later success
            var result = await _service.RetryDetailAsync(firstLog.Id, firstDetailId);
            var detail = result.Details.First(d => d.Id == firstDetailId);

            // Status remains Failed (not Retried), but RetriedCount is incremented to 1
            detail.Status.ShouldBe(BulkTransactionStatus.Failed);
            detail.RetriedCount.ShouldBe(1);
        });
    }
}
