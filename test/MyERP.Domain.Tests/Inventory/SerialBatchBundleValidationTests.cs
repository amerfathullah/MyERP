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

    [Fact]
    public async Task ValidateBundleCompanyAsync_BundleBelongingToAnotherDraft_ThrowsValidationFailed()
    {
        var repo = Substitute.For<IRepository<SerialAndBatchBundle, Guid>>();
        var service = new SerialBatchBundleValidationService(repo);

        var companyId = Guid.NewGuid();
        var bundleId = Guid.NewGuid();
        var draftVoucherA = Guid.NewGuid();
        var draftVoucherB = Guid.NewGuid();
        var bundle = new SerialAndBatchBundle(
            bundleId, companyId, Guid.NewGuid(), Guid.NewGuid(),
            BundleTransactionType.Outward, "StockEntry", draftVoucherA, DateTime.UtcNow);

        repo.GetListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<SerialAndBatchBundle, bool>>>())
            .Returns(Task.FromResult(new List<SerialAndBatchBundle> { bundle }));

        // Different draft cannot take over the bundle (PR #66994)
        var ex = await Should.ThrowAsync<BusinessException>(async () =>
            await service.ValidateBundleCompanyAsync(
                companyId,
                new[] { (Guid?)bundleId },
                expectedVoucherType: "StockEntry",
                expectedVoucherId: draftVoucherB));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);

        // Owner draft itself succeeds
        await Should.NotThrowAsync(async () =>
            await service.ValidateBundleCompanyAsync(
                companyId,
                new[] { (Guid?)bundleId },
                expectedVoucherType: "StockEntry",
                expectedVoucherId: draftVoucherA));
    }

    [Fact]
    public void SetEntriesTotalQty_BatchBundle_AdjustsLastEntryToMatchPostedStockQty()
    {
        // Per ERPNext PR #66747 & PR #66762: batch transfer bundle's last entry absorbs rounding diff
        var companyId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var whId = Guid.NewGuid();
        var bundle = new SerialAndBatchBundle(
            Guid.NewGuid(), companyId, itemId, whId,
            BundleTransactionType.Outward, "PurchaseReceipt", Guid.NewGuid(), DateTime.UtcNow)
        {
            HasBatchNo = true,
            HasSerialNo = false
        };

        var batch1 = Guid.NewGuid();
        var batch2 = Guid.NewGuid();
        bundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), bundle.Id, 4.3333m, 10m, batchId: batch1));
        bundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), bundle.Id, 5.6670m, 10m, batchId: batch2));

        bundle.SetEntriesTotalQty(10.000m, precision: 4);

        var entries = bundle.Entries.ToList();
        entries[0].Qty.ShouldBe(4.3333m);
        entries[1].Qty.ShouldBe(5.6667m);
        bundle.TotalQty.ShouldBe(10.0000m);
    }

    [Fact]
    public void CreateTransferPackage_SizesBatchPackageAndExcludesRejectedSerials()
    {
        var companyId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var transitWh = Guid.NewGuid();
        var sourceEntries = new[]
        {
            new SerialAndBatchEntry(Guid.NewGuid(), Guid.NewGuid(), -3m, 25m, serialNo: "SN-A"),
            new SerialAndBatchEntry(Guid.NewGuid(), Guid.NewGuid(), -1m, 25m, serialNo: "SN-REJ"),
        };

        var acceptedPackage = SerialAndBatchBundle.CreateTransferPackage(
            Guid.NewGuid(), companyId, itemId, transitWh,
            BundleTransactionType.Inward, "PurchaseReceipt", Guid.NewGuid(), DateTime.UtcNow,
            sourceEntries, hasSerialNo: true, excludeSerialNos: new[] { "SN-REJ" });

        acceptedPackage.Entries.Count.ShouldBe(1);
        acceptedPackage.Entries.Single().SerialNo.ShouldBe("SN-A");
        acceptedPackage.Entries.Single().Qty.ShouldBe(3m);
        acceptedPackage.TotalQty.ShouldBe(3m);
    }
}
