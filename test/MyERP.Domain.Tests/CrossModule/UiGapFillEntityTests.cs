using System;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Sales.Entities;
using Xunit;

namespace MyERP.Domain.Tests.CrossModule;

/// <summary>
/// Tests for entities added during the Angular UI gap-fill session:
/// CouponCode, AccountCategory, PackingSlip, PartyLink, DeliveryScheduleEntry, CostCenterAllocation
/// </summary>
public class UiGapFillEntityTests
{
    #region CouponCode — Usage Tracking

    [Fact]
    public void CouponCode_RecordUse_IncrementsUsed()
    {
        var coupon = new CouponCode(Guid.NewGuid(), "SUMMER2026", "Summer Sale", CouponType.Promotional,
            Guid.NewGuid(), null);
        coupon.MaximumUse = 5;

        coupon.RecordUse();
        coupon.RecordUse();

        Assert.Equal(2, coupon.Used);
    }

    [Fact]
    public void CouponCode_RecordUse_ExceedsMaximum_Throws()
    {
        var coupon = new CouponCode(Guid.NewGuid(), "SINGLE", "One Use", CouponType.GiftCard,
            Guid.NewGuid(), null);
        coupon.MaximumUse = 1;
        coupon.RecordUse();

        Assert.Throws<Volo.Abp.BusinessException>(() => coupon.RecordUse());
    }

    [Fact]
    public void CouponCode_ReverseUse_DecrementsUsed()
    {
        var coupon = new CouponCode(Guid.NewGuid(), "REV", "Reverse Test", CouponType.Promotional,
            Guid.NewGuid(), null);
        coupon.RecordUse();
        coupon.RecordUse();

        coupon.ReverseUse();

        Assert.Equal(1, coupon.Used);
    }

    [Fact]
    public void CouponCode_ReverseUse_NeverNegative()
    {
        var coupon = new CouponCode(Guid.NewGuid(), "NEG", "Negative Guard", CouponType.Promotional,
            Guid.NewGuid(), null);

        coupon.ReverseUse(); // from 0

        Assert.Equal(0, coupon.Used);
    }

    [Fact]
    public void CouponCode_IsValid_ExpiredDate_ReturnsFalse()
    {
        var coupon = new CouponCode(Guid.NewGuid(), "EXP", "Expired", CouponType.Promotional,
            Guid.NewGuid(), null);
        coupon.ValidFrom = new DateTime(2025, 1, 1);
        coupon.ValidUpto = new DateTime(2025, 12, 31);

        Assert.False(coupon.IsValid(new DateTime(2026, 6, 1), null));
    }

    [Fact]
    public void CouponCode_IsValid_WithinDateRange_ReturnsTrue()
    {
        var coupon = new CouponCode(Guid.NewGuid(), "VALID", "Valid", CouponType.Promotional,
            Guid.NewGuid(), null);
        coupon.ValidFrom = new DateTime(2026, 1, 1);
        coupon.ValidUpto = new DateTime(2026, 12, 31);
        coupon.IsEnabled = true;
        coupon.MaximumUse = 10;

        Assert.True(coupon.IsValid(new DateTime(2026, 7, 15), null));
    }

    [Fact]
    public void CouponCode_IsValid_Disabled_ReturnsFalse()
    {
        var coupon = new CouponCode(Guid.NewGuid(), "DIS", "Disabled", CouponType.Promotional,
            Guid.NewGuid(), null);
        coupon.IsEnabled = false;

        Assert.False(coupon.IsValid(DateTime.UtcNow, null));
    }

    [Fact]
    public void CouponCode_GeneratePromotionalCode_Returns8Chars()
    {
        var code = CouponCode.GeneratePromotionalCode("Summer Sale 2026!");
        Assert.Equal(8, code.Length);
        Assert.Equal(code.ToUpper(), code);
    }

    [Fact]
    public void CouponCode_GenerateGiftCardCode_Returns10Chars()
    {
        var code = CouponCode.GenerateGiftCardCode();
        Assert.Equal(10, code.Length);
        Assert.Equal(code.ToUpper(), code);
    }

    #endregion

    #region AccountCategory

    [Fact]
    public void AccountCategory_Create_SetsProperties()
    {
        var cat = new AccountCategory(Guid.NewGuid(), "Cash and Cash Equivalents", "Asset");

        Assert.Equal("Cash and Cash Equivalents", cat.Name);
        Assert.Equal("Asset", cat.RootType);
    }

    [Fact]
    public void AccountCategory_EmptyName_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new AccountCategory(Guid.NewGuid(), "", "Asset"));
    }

    [Fact]
    public void AccountCategory_EmptyRootType_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new AccountCategory(Guid.NewGuid(), "Test", ""));
    }

    [Fact]
    public void AccountCategory_Description_CanBeSet()
    {
        var cat = new AccountCategory(Guid.NewGuid(), "Revenue from Operations", "Income");
        cat.Description = "Operating revenue accounts";

        Assert.Equal("Operating revenue accounts", cat.Description);
    }

    #endregion

    #region PackingSlip

    [Fact]
    public void PackingSlip_InvalidCaseRange_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new PackingSlip(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 5, 3)); // from > to
    }

    [Fact]
    public void PackingSlip_ValidCaseRange_Creates()
    {
        var slip = new PackingSlip(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, 5);

        Assert.Equal(1, slip.FromCaseNo);
        Assert.Equal(5, slip.ToCaseNo);
    }

    [Fact]
    public void PackingSlip_AddItem_IncreasesWeight()
    {
        var slip = new PackingSlip(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, 3);

        slip.AddItem(Guid.NewGuid(), 10, 2.5m); // 10 qty, 2.5 net weight

        Assert.Equal(2.5m, slip.NetWeight);
    }

    [Fact]
    public void PackingSlip_Submit_ChangesStatus()
    {
        var slip = new PackingSlip(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, 1);
        slip.AddItem(Guid.NewGuid(), 5, 1m);

        slip.Submit();

        Assert.Equal(DocumentStatus.Submitted, slip.Status);
    }

    [Fact]
    public void PackingSlip_Cancel_FromSubmitted()
    {
        var slip = new PackingSlip(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, 1);
        slip.AddItem(Guid.NewGuid(), 5, 1m);
        slip.Submit();

        slip.Cancel();

        Assert.Equal(DocumentStatus.Cancelled, slip.Status);
    }

    [Fact]
    public void PackingSlip_AddItem_AfterSubmit_Throws()
    {
        var slip = new PackingSlip(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, 1);
        slip.AddItem(Guid.NewGuid(), 5, 1m);
        slip.Submit();

        Assert.Throws<Volo.Abp.BusinessException>(() =>
            slip.AddItem(Guid.NewGuid(), 3, 0.5m));
    }

    #endregion

    #region PartyLink

    [Fact]
    public void PartyLink_SelfLink_Throws()
    {
        var partyId = Guid.NewGuid();
        Assert.Throws<Volo.Abp.BusinessException>(() =>
            new PartyLink(Guid.NewGuid(), "Customer", partyId, "Customer", partyId, null));
    }

    [Fact]
    public void PartyLink_ValidBidirectional_Creates()
    {
        var link = new PartyLink(Guid.NewGuid(), "Customer", Guid.NewGuid(), "Supplier", Guid.NewGuid(), null);

        Assert.Equal("Customer", link.PrimaryPartyType);
        Assert.Equal("Supplier", link.SecondaryPartyType);
    }

    [Fact]
    public void PartyLink_DifferentType_SameId_Allowed()
    {
        var sharedId = Guid.NewGuid();
        // Same entity ID but different party types = valid (different logical entities)
        var link = new PartyLink(Guid.NewGuid(), "Customer", sharedId, "Supplier", sharedId, null);

        Assert.NotNull(link);
    }

    #endregion

    #region DeliveryScheduleEntry

    [Fact]
    public void DeliveryScheduleEntry_RecordDelivery_ReducesPending()
    {
        var entry = new DeliveryScheduleEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 8, 1), 100m, null);

        entry.RecordDelivery(40m);

        Assert.Equal(40m, entry.DeliveredQty);
        Assert.Equal(60m, entry.PendingQty);
    }

    [Fact]
    public void DeliveryScheduleEntry_FullDelivery_IsComplete()
    {
        var entry = new DeliveryScheduleEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 8, 1), 50m, null);

        entry.RecordDelivery(50m);

        Assert.True(entry.IsFullyDelivered);
        Assert.Equal(0m, entry.PendingQty);
    }

    [Fact]
    public void DeliveryScheduleEntry_PendingNeverNegative()
    {
        var entry = new DeliveryScheduleEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 8, 1), 30m, null);

        entry.RecordDelivery(50m); // over-delivers

        Assert.Equal(50m, entry.DeliveredQty);
        Assert.Equal(0m, entry.PendingQty); // clamped to 0, not -20
    }

    #endregion

    #region CostCenterAllocation

    [Fact]
    public void CostCenterAllocation_Distribute_EvenSplit()
    {
        var alloc = new CostCenterAllocation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 7, 1), null);
        alloc.AddEntry(Guid.NewGuid(), 50m);
        alloc.AddEntry(Guid.NewGuid(), 50m);

        var result = alloc.Distribute(1000m);

        Assert.Equal(2, result.Count);
        Assert.Equal(500m, result[0].Amount);
        Assert.Equal(500m, result[1].Amount);
    }

    [Fact]
    public void CostCenterAllocation_Distribute_UnevenRemainder()
    {
        var alloc = new CostCenterAllocation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 7, 1), null);
        alloc.AddEntry(Guid.NewGuid(), 33.33m);
        alloc.AddEntry(Guid.NewGuid(), 33.33m);
        alloc.AddEntry(Guid.NewGuid(), 33.34m);

        var result = alloc.Distribute(100m);

        // First entry absorbs remainder per ERPNext pattern
        var sum = result[0].Amount + result[1].Amount + result[2].Amount;
        Assert.Equal(100m, sum); // exact total preserved
    }

    [Fact]
    public void CostCenterAllocation_ValidatePercentages_MustSum100()
    {
        var alloc = new CostCenterAllocation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 7, 1), null);
        alloc.AddEntry(Guid.NewGuid(), 40m);
        alloc.AddEntry(Guid.NewGuid(), 40m);
        // Only 80% — should fail

        Assert.Throws<Volo.Abp.BusinessException>(() => alloc.ValidatePercentages());
    }

    [Fact]
    public void CostCenterAllocation_SelfReference_Throws()
    {
        var mainCcId = Guid.NewGuid();
        var alloc = new CostCenterAllocation(Guid.NewGuid(), Guid.NewGuid(), mainCcId,
            new DateTime(2026, 7, 1), null);

        Assert.Throws<Volo.Abp.BusinessException>(() => alloc.AddEntry(mainCcId, 100m));
    }

    #endregion
}
