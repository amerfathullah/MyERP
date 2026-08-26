using System;
using System.Collections.Generic;
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
}
