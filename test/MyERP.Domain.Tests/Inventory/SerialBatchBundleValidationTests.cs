using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Inventory;

public class SerialBatchBundleValidationTests
{
    [Fact]
    public async Task ValidateBundleCompanyAsync_MatchingCompany_Succeeds()
    {
        var repo = Substitute.For<IRepository<SerialAndBatchBundle, Guid>>();
        var service = new SerialBatchBundleValidationService(repo);

        var companyId = Guid.NewGuid();
        var bundleId = Guid.NewGuid();
        var bundle = new SerialAndBatchBundle(
            bundleId, companyId, Guid.NewGuid(), Guid.NewGuid(),
            BundleTransactionType.Outward, "DeliveryNote", Guid.NewGuid(), DateTime.UtcNow);

        repo.GetListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<SerialAndBatchBundle, bool>>>())
            .Returns(Task.FromResult(new List<SerialAndBatchBundle> { bundle }));

        await Should.NotThrowAsync(async () =>
            await service.ValidateBundleCompanyAsync(companyId, new[] { (Guid?)bundleId }));
    }

    [Fact]
    public async Task ValidateBundleCompanyAsync_MismatchedCompany_ThrowsCompanyRestrictionBlocked()
    {
        var repo = Substitute.For<IRepository<SerialAndBatchBundle, Guid>>();
        var service = new SerialBatchBundleValidationService(repo);

        var companyA = Guid.NewGuid();
        var companyB = Guid.NewGuid();
        var bundleId = Guid.NewGuid();
        var bundle = new SerialAndBatchBundle(
            bundleId, companyB, Guid.NewGuid(), Guid.NewGuid(),
            BundleTransactionType.Outward, "DeliveryNote", Guid.NewGuid(), DateTime.UtcNow);

        repo.GetListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<SerialAndBatchBundle, bool>>>())
            .Returns(Task.FromResult(new List<SerialAndBatchBundle> { bundle }));

        var ex = await Should.ThrowAsync<BusinessException>(async () =>
            await service.ValidateBundleCompanyAsync(companyA, new[] { (Guid?)bundleId }));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.CompanyRestrictionBlocked);
    }

    [Fact]
    public async Task ValidateBundleCompanyAsync_CancelledBundle_ThrowsInvalidStatusTransition()
    {
        var repo = Substitute.For<IRepository<SerialAndBatchBundle, Guid>>();
        var service = new SerialBatchBundleValidationService(repo);

        var companyId = Guid.NewGuid();
        var bundleId = Guid.NewGuid();
        var bundle = new SerialAndBatchBundle(
            bundleId, companyId, Guid.NewGuid(), Guid.NewGuid(),
            BundleTransactionType.Outward, "DeliveryNote", Guid.NewGuid(), DateTime.UtcNow);
        bundle.Cancel();

        repo.GetListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<SerialAndBatchBundle, bool>>>())
            .Returns(Task.FromResult(new List<SerialAndBatchBundle> { bundle }));

        var ex = await Should.ThrowAsync<BusinessException>(async () =>
            await service.ValidateBundleCompanyAsync(companyId, new[] { (Guid?)bundleId }));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.InvalidStatusTransition);
    }
}
