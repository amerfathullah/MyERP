using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Tax.DomainServices;
using MyERP.Tax.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Tax;

public class LowerDeductionCertificateEntityTests
{
    private static LowerDeductionCertificate Create(DateTime validFrom, DateTime validUpto, decimal rate = 2m, decimal limit = 100000m) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "LDC-001", rate, limit, validFrom, validUpto);

    [Fact]
    public void Constructor_ValidFromAfterValidUpto_Throws()
    {
        Should.Throw<BusinessException>(() =>
            Create(new DateTime(2026, 6, 1), new DateTime(2026, 1, 1)));
    }

    [Fact]
    public void CoversDate_WithinRange_ReturnsTrue()
    {
        var ldc = Create(new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));
        ldc.CoversDate(new DateTime(2026, 6, 15)).ShouldBeTrue();
    }

    [Fact]
    public void CoversDate_OutsideRange_ReturnsFalse()
    {
        var ldc = Create(new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));
        ldc.CoversDate(new DateTime(2027, 1, 1)).ShouldBeFalse();
    }

    [Fact]
    public void SetTerms_UpdatesRateAndLimit()
    {
        var ldc = Create(new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));
        ldc.SetTerms(3m, 200000m);
        ldc.Rate.ShouldBe(3m);
        ldc.CertificateLimit.ShouldBe(200000m);
    }
}

public class TaxWithholdingServiceLdcTests
{
    private readonly IRepository<TaxWithholdingEntry, Guid> _entryRepo;
    private readonly IRepository<LowerDeductionCertificate, Guid> _ldcRepo;
    private readonly TaxWithholdingService _service;

    public TaxWithholdingServiceLdcTests()
    {
        _entryRepo = Substitute.For<IRepository<TaxWithholdingEntry, Guid>>();
        _ldcRepo = Substitute.For<IRepository<LowerDeductionCertificate, Guid>>();
        _service = new TaxWithholdingService(_entryRepo, _ldcRepo);
    }

    [Fact]
    public async Task GetLdcDetailsAsync_NoCertificate_ReturnsNull()
    {
        _ldcRepo.GetQueryableAsync().Returns(new List<LowerDeductionCertificate>().AsQueryable());

        var result = await _service.GetLdcDetailsAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.Today);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetLdcDetailsAsync_ValidCertificateNoUtilization_ReturnsFullLimit()
    {
        var companyId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var ldc = new LowerDeductionCertificate(
            Guid.NewGuid(), companyId, supplierId, categoryId,
            "LDC-2026-001", 2m, 100000m,
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));

        _ldcRepo.GetQueryableAsync().Returns(new List<LowerDeductionCertificate> { ldc }.AsQueryable());
        _entryRepo.GetQueryableAsync().Returns(new List<TaxWithholdingEntry>().AsQueryable());

        var result = await _service.GetLdcDetailsAsync(companyId, supplierId, categoryId, new DateTime(2026, 6, 1));

        result.ShouldNotBeNull();
        result!.CertificateNumber.ShouldBe("LDC-2026-001");
        result.LdcRate.ShouldBe(2m);
        result.UnutilizedAmount.ShouldBe(100000m);
    }

    [Fact]
    public async Task GetLdcDetailsAsync_PostingDateOutsideValidity_ReturnsNull()
    {
        var companyId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var ldc = new LowerDeductionCertificate(
            Guid.NewGuid(), companyId, supplierId, categoryId,
            "LDC-2026-001", 2m, 100000m,
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));

        _ldcRepo.GetQueryableAsync().Returns(new List<LowerDeductionCertificate> { ldc }.AsQueryable());

        var result = await _service.GetLdcDetailsAsync(companyId, supplierId, categoryId, new DateTime(2027, 1, 1));

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetLdcDetailsAsync_PartiallyUtilized_ReturnsRemainingLimit()
    {
        var companyId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var ldc = new LowerDeductionCertificate(
            Guid.NewGuid(), companyId, supplierId, categoryId,
            "LDC-2026-001", 2m, 100000m,
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));

        var priorEntry = new TaxWithholdingEntry(
            Guid.NewGuid(), companyId, supplierId, "PurchaseInvoice", Guid.NewGuid(),
            Guid.NewGuid(), 2m, 60000m, new DateTime(2026, 3, 1));
        priorEntry.ApplyLDC(2m, "LDC-2026-001");

        _ldcRepo.GetQueryableAsync().Returns(new List<LowerDeductionCertificate> { ldc }.AsQueryable());
        _entryRepo.GetQueryableAsync().Returns(new List<TaxWithholdingEntry> { priorEntry }.AsQueryable());

        var result = await _service.GetLdcDetailsAsync(companyId, supplierId, categoryId, new DateTime(2026, 6, 1));

        result.ShouldNotBeNull();
        result!.UnutilizedAmount.ShouldBe(40000m); // 100000 - 60000
    }

    [Fact]
    public async Task GetLdcDetailsAsync_FullyUtilized_ReturnsNull()
    {
        var companyId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var ldc = new LowerDeductionCertificate(
            Guid.NewGuid(), companyId, supplierId, categoryId,
            "LDC-2026-001", 2m, 50000m,
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));

        var priorEntry = new TaxWithholdingEntry(
            Guid.NewGuid(), companyId, supplierId, "PurchaseInvoice", Guid.NewGuid(),
            Guid.NewGuid(), 2m, 50000m, new DateTime(2026, 3, 1));
        priorEntry.ApplyLDC(2m, "LDC-2026-001");

        _ldcRepo.GetQueryableAsync().Returns(new List<LowerDeductionCertificate> { ldc }.AsQueryable());
        _entryRepo.GetQueryableAsync().Returns(new List<TaxWithholdingEntry> { priorEntry }.AsQueryable());

        var result = await _service.GetLdcDetailsAsync(companyId, supplierId, categoryId, new DateTime(2026, 6, 1));

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetLdcDetailsAsync_CancelledEntry_ExcludedFromUtilization()
    {
        var companyId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var ldc = new LowerDeductionCertificate(
            Guid.NewGuid(), companyId, supplierId, categoryId,
            "LDC-2026-001", 2m, 50000m,
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));

        var cancelledEntry = new TaxWithholdingEntry(
            Guid.NewGuid(), companyId, supplierId, "PurchaseInvoice", Guid.NewGuid(),
            Guid.NewGuid(), 2m, 50000m, new DateTime(2026, 3, 1));
        cancelledEntry.ApplyLDC(2m, "LDC-2026-001");
        cancelledEntry.Submit();
        cancelledEntry.Cancel();

        _ldcRepo.GetQueryableAsync().Returns(new List<LowerDeductionCertificate> { ldc }.AsQueryable());
        _entryRepo.GetQueryableAsync().Returns(new List<TaxWithholdingEntry> { cancelledEntry }.AsQueryable());

        var result = await _service.GetLdcDetailsAsync(companyId, supplierId, categoryId, new DateTime(2026, 6, 1));

        result.ShouldNotBeNull();
        result!.UnutilizedAmount.ShouldBe(50000m); // cancelled entry doesn't count against the limit
    }
}
