using System;
using MyERP.Core;
using MyERP.Inventory.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Inventory;

/// <summary>
/// Unit tests for GL-only reposting invariants (ERPNext PR #59307 / commit 18093079d9).
/// </summary>
public class RepostItemValuationGlOnlyTests
{
    private readonly Guid _companyId = Guid.NewGuid();

    [Fact]
    public void RepostItemValuation_ValidateRepostOnlyAccountingLedgers_GlEntryThrows()
    {
        var repost = new RepostItemValuation(Guid.NewGuid(), _companyId, RepostMethod.Transaction, DateTime.Today)
        {
            RepostOnlyAccountingLedgers = true,
            VoucherType = "GL Entry",
            VoucherId = Guid.NewGuid()
        };

        var ex = Should.Throw<BusinessException>(() => repost.ValidateRepostOnlyAccountingLedgers());
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public void RepostItemValuation_ValidateRepostOnlyAccountingLedgers_StockVoucherPasses()
    {
        var repost = new RepostItemValuation(Guid.NewGuid(), _companyId, RepostMethod.Transaction, DateTime.Today)
        {
            RepostOnlyAccountingLedgers = true,
            VoucherType = "Stock Entry",
            VoucherId = Guid.NewGuid()
        };

        Should.NotThrow(() => repost.ValidateRepostOnlyAccountingLedgers());
    }

    [Fact]
    public void RepostItemValuation_ResetRepostOnlyAccountingLedgers_ResetsToFalseWhenNotTransaction()
    {
        var repost = new RepostItemValuation(Guid.NewGuid(), _companyId, RepostMethod.ItemAndWarehouse, DateTime.Today)
        {
            RepostOnlyAccountingLedgers = true
        };

        repost.ResetRepostOnlyAccountingLedgers();
        repost.RepostOnlyAccountingLedgers.ShouldBeFalse();
    }

    [Fact]
    public void RepostItemValuation_IsCoveredBy_SeparatesGlOnlyAndStandardReposts()
    {
        var voucherId = Guid.NewGuid();
        var glOnly = new RepostItemValuation(Guid.NewGuid(), _companyId, RepostMethod.Transaction, DateTime.Today)
        {
            RepostOnlyAccountingLedgers = true,
            VoucherType = "Purchase Receipt",
            VoucherId = voucherId
        };

        var standard = new RepostItemValuation(Guid.NewGuid(), _companyId, RepostMethod.Transaction, DateTime.Today)
        {
            RepostOnlyAccountingLedgers = false,
            VoucherType = "Purchase Receipt",
            VoucherId = voucherId
        };

        // Standard repost does not cover GL-only repost and vice-versa
        glOnly.IsCoveredBy(standard).ShouldBeFalse();
        standard.IsCoveredBy(glOnly).ShouldBeFalse();

        // Matching GL-only repost covers it
        var duplicateGlOnly = new RepostItemValuation(Guid.NewGuid(), _companyId, RepostMethod.Transaction, DateTime.Today)
        {
            RepostOnlyAccountingLedgers = true,
            VoucherType = "Purchase Receipt",
            VoucherId = voucherId
        };
        glOnly.IsCoveredBy(duplicateGlOnly).ShouldBeTrue();
    }
}
