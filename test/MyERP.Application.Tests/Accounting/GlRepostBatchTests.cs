using System;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Accounting;

/// <summary>
/// Regression coverage for GlRepostAppService.RepostBatchAsync's own vouchers-resolution/aggregation
/// layer, found while auditing the Angular side for zero-caller proxy methods: repostBatch had no
/// UI path — the GL Repost page could only repost one voucher at a time. Existing backend coverage
/// (GlRepostServiceTests) only unit-tests static helpers/DTOs (AllowedVoucherTypes, GlRepostResult),
/// never RepostBatchAsync itself — added a UI batch panel plus this test for the aggregation logic
/// specifically added for batching (unresolvable vouchers counted as skipped, not thrown).
/// </summary>
public abstract class GlRepostBatchTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task RepostBatchAsync_AllVouchersUnresolvable_SkipsAllWithoutThrowing()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var glRepostAppService = GetRequiredService<IGlRepostAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "GL Repost Batch Test Co"), autoSave: true);

            var result = await glRepostAppService.RepostBatchAsync(new RepostBatchGlDto
            {
                CompanyId = company.Id,
                Vouchers =
                {
                    new RepostVoucherRefDto { VoucherType = "SalesInvoice", VoucherId = Guid.NewGuid() },
                    new RepostVoucherRefDto { VoucherType = "PurchaseInvoice", VoucherId = Guid.NewGuid() },
                },
            });

            result.SkippedCount.ShouldBe(2);
            result.TotalProcessed.ShouldBe(2);
            result.SuccessCount.ShouldBe(0);
            result.HasErrors.ShouldBeFalse();
        });
    }

    [Fact]
    public async Task RepostBatchAsync_EmptyVoucherList_ReturnsZeroCounts()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var glRepostAppService = GetRequiredService<IGlRepostAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "GL Repost Batch Test Co 2"), autoSave: true);

            var result = await glRepostAppService.RepostBatchAsync(new RepostBatchGlDto
            {
                CompanyId = company.Id,
            });

            result.TotalProcessed.ShouldBe(0);
            result.SkippedCount.ShouldBe(0);
            result.SuccessCount.ShouldBe(0);
        });
    }
}
