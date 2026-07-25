using System;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Sales.Entities;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests.EntityGapFill;

public class PartyLinkTests
{
    [Fact]
    public void Create_ValidLink_SetsProperties()
    {
        var link = new PartyLink(Guid.NewGuid(), "Customer", Guid.NewGuid(), "Supplier", Guid.NewGuid());
        Assert.Equal("Customer", link.PrimaryPartyType);
        Assert.Equal("Supplier", link.SecondaryPartyType);
    }

    [Fact]
    public void Create_SelfLink_Throws()
    {
        var partyId = Guid.NewGuid();
        Assert.Throws<BusinessException>(() =>
            new PartyLink(Guid.NewGuid(), "Customer", partyId, "Customer", partyId));
    }

    [Fact]
    public void Create_SameIdDifferentType_Succeeds()
    {
        var partyId = Guid.NewGuid();
        var link = new PartyLink(Guid.NewGuid(), "Customer", partyId, "Supplier", partyId);
        Assert.NotNull(link);
    }

    [Fact]
    public void Create_EmptyPrimaryType_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new PartyLink(Guid.NewGuid(), "", Guid.NewGuid(), "Supplier", Guid.NewGuid()));
    }

    [Fact]
    public void Create_EmptyPrimaryId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new PartyLink(Guid.NewGuid(), "Customer", Guid.Empty, "Supplier", Guid.NewGuid()));
    }
}

public class CouponCodeTests
{
    [Fact]
    public void Create_Promotional_SetsDefaults()
    {
        var coupon = new CouponCode(Guid.NewGuid(), "SUMMER20", "Summer Sale", CouponType.Promotional, Guid.NewGuid());
        Assert.Equal("SUMMER20", coupon.Code);
        Assert.Equal(CouponType.Promotional, coupon.CouponType);
        Assert.Equal(0, coupon.MaximumUse);
        Assert.Equal(0, coupon.Used);
        Assert.True(coupon.IsEnabled);
    }

    [Fact]
    public void Create_GiftCard_ForcesMaxUseOne()
    {
        var coupon = new CouponCode(Guid.NewGuid(), "GIFT123", "Gift Card", CouponType.GiftCard, Guid.NewGuid());
        Assert.Equal(1, coupon.MaximumUse);
    }

    [Fact]
    public void RecordUse_IncreasesUsedCount()
    {
        var coupon = new CouponCode(Guid.NewGuid(), "TEST", "Test", CouponType.Promotional, Guid.NewGuid());
        coupon.MaximumUse = 5;
        coupon.RecordUse();
        Assert.Equal(1, coupon.Used);
    }

    [Fact]
    public void RecordUse_ExceedsMaximum_Throws()
    {
        var coupon = new CouponCode(Guid.NewGuid(), "TEST", "Test", CouponType.GiftCard, Guid.NewGuid());
        coupon.RecordUse(); // used=1, max=1
        Assert.Throws<BusinessException>(() => coupon.RecordUse());
    }

    [Fact]
    public void ReverseUse_DecreasesUsedCount()
    {
        var coupon = new CouponCode(Guid.NewGuid(), "TEST", "Test", CouponType.Promotional, Guid.NewGuid());
        coupon.RecordUse();
        coupon.RecordUse();
        coupon.ReverseUse();
        Assert.Equal(1, coupon.Used);
    }

    [Fact]
    public void ReverseUse_NeverNegative()
    {
        var coupon = new CouponCode(Guid.NewGuid(), "TEST", "Test", CouponType.Promotional, Guid.NewGuid());
        coupon.ReverseUse();
        Assert.Equal(0, coupon.Used);
    }

    [Fact]
    public void IsValid_Enabled_ReturnsTrue()
    {
        var coupon = new CouponCode(Guid.NewGuid(), "TEST", "Test", CouponType.Promotional, Guid.NewGuid());
        Assert.True(coupon.IsValid(DateTime.UtcNow));
    }

    [Fact]
    public void IsValid_Disabled_ReturnsFalse()
    {
        var coupon = new CouponCode(Guid.NewGuid(), "TEST", "Test", CouponType.Promotional, Guid.NewGuid());
        coupon.IsEnabled = false;
        Assert.False(coupon.IsValid(DateTime.UtcNow));
    }

    [Fact]
    public void IsValid_Expired_ReturnsFalse()
    {
        var coupon = new CouponCode(Guid.NewGuid(), "TEST", "Test", CouponType.Promotional, Guid.NewGuid());
        coupon.ValidUpto = DateTime.UtcNow.AddDays(-1);
        Assert.False(coupon.IsValid(DateTime.UtcNow));
    }

    [Fact]
    public void IsValid_BeforeValidFrom_ReturnsFalse()
    {
        var coupon = new CouponCode(Guid.NewGuid(), "TEST", "Test", CouponType.Promotional, Guid.NewGuid());
        coupon.ValidFrom = DateTime.UtcNow.AddDays(1);
        Assert.False(coupon.IsValid(DateTime.UtcNow));
    }

    [Fact]
    public void IsValid_GiftCard_WrongCustomer_ReturnsFalse()
    {
        var customerId = Guid.NewGuid();
        var coupon = new CouponCode(Guid.NewGuid(), "GIFT", "Gift", CouponType.GiftCard, Guid.NewGuid());
        coupon.CustomerId = customerId;
        Assert.False(coupon.IsValid(DateTime.UtcNow, Guid.NewGuid()));
    }

    [Fact]
    public void IsValid_GiftCard_CorrectCustomer_ReturnsTrue()
    {
        var customerId = Guid.NewGuid();
        var coupon = new CouponCode(Guid.NewGuid(), "GIFT", "Gift", CouponType.GiftCard, Guid.NewGuid());
        coupon.CustomerId = customerId;
        Assert.True(coupon.IsValid(DateTime.UtcNow, customerId));
    }

    [Fact]
    public void GeneratePromotionalCode_ExtractsLetters()
    {
        var code = CouponCode.GeneratePromotionalCode("Summer 2026 Sale!");
        Assert.Equal("SUMMERSA", code); // 8 non-digit letter-or-digit uppercase chars (S,U,M,M,E,R,S,A)
    }

    [Fact]
    public void GenerateGiftCardCode_Returns10Chars()
    {
        var code = CouponCode.GenerateGiftCardCode();
        Assert.Equal(10, code.Length);
        Assert.Equal(code, code.ToUpper());
    }

    [Fact]
    public void IsValid_UsedEqualsMax_ReturnsFalse()
    {
        var coupon = new CouponCode(Guid.NewGuid(), "TEST", "Test", CouponType.Promotional, Guid.NewGuid());
        coupon.MaximumUse = 1;
        coupon.RecordUse();
        Assert.False(coupon.IsValid(DateTime.UtcNow));
    }

    [Fact]
    public void UnlimitedUse_Zero_AlwaysValid()
    {
        var coupon = new CouponCode(Guid.NewGuid(), "TEST", "Test", CouponType.Promotional, Guid.NewGuid());
        coupon.MaximumUse = 0; // unlimited
        for (int i = 0; i < 100; i++) coupon.RecordUse();
        Assert.True(coupon.IsValid(DateTime.UtcNow));
    }
}

public class CustomerCreditLimitTests
{
    [Fact]
    public void Create_SetsProperties()
    {
        var limit = new CustomerCreditLimit(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 50000m);
        Assert.Equal(50000m, limit.CreditLimit);
        Assert.False(limit.BypassCreditLimitCheck);
        Assert.Equal(0m, limit.OverdueBillingThreshold);
    }

    [Fact]
    public void Bypass_CanBeEnabled()
    {
        var limit = new CustomerCreditLimit(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 50000m);
        limit.BypassCreditLimitCheck = true;
        Assert.True(limit.BypassCreditLimitCheck);
    }

    [Fact]
    public void OverdueThreshold_CanBeSet()
    {
        var limit = new CustomerCreditLimit(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 50000m);
        limit.OverdueBillingThreshold = 10000m;
        Assert.Equal(10000m, limit.OverdueBillingThreshold);
    }
}

public class PackingSlipTests
{
    [Fact]
    public void Create_SetsProperties()
    {
        var slip = new PackingSlip(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, 5);
        Assert.Equal(1, slip.FromCaseNo);
        Assert.Equal(5, slip.ToCaseNo);
        Assert.Equal(5, slip.NumberOfCases);
    }

    [Fact]
    public void Create_InvalidCaseRange_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new PackingSlip(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 5, 3));
    }

    [Fact]
    public void Create_ZeroCaseNo_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new PackingSlip(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0, 3));
    }

    [Fact]
    public void AddItem_IncreasesNetWeight()
    {
        var slip = new PackingSlip(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, 1);
        slip.AddItem(Guid.NewGuid(), 10, 5.5m, "Item A");
        slip.AddItem(Guid.NewGuid(), 5, 3.2m, "Item B");
        Assert.Equal(8.7m, slip.NetWeight);
        Assert.Equal(2, slip.Items.Count);
    }

    [Fact]
    public void AddItem_ZeroQty_Throws()
    {
        var slip = new PackingSlip(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, 1);
        Assert.Throws<ArgumentException>(() => slip.AddItem(Guid.NewGuid(), 0, 1m));
    }

    [Fact]
    public void Submit_RequiresItems()
    {
        var slip = new PackingSlip(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, 1);
        Assert.Throws<BusinessException>(() => slip.Submit());
    }

    [Fact]
    public void Submit_WithItems_Succeeds()
    {
        var slip = new PackingSlip(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, 1);
        slip.AddItem(Guid.NewGuid(), 10, 5m);
        slip.Submit();
        Assert.Equal(DocumentStatus.Submitted, slip.Status);
    }

    [Fact]
    public void Cancel_FromSubmitted()
    {
        var slip = new PackingSlip(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, 1);
        slip.AddItem(Guid.NewGuid(), 10, 5m);
        slip.Submit();
        slip.Cancel();
        Assert.Equal(DocumentStatus.Cancelled, slip.Status);
    }

    [Fact]
    public void AddItem_AfterSubmit_Throws()
    {
        var slip = new PackingSlip(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, 1);
        slip.AddItem(Guid.NewGuid(), 10, 5m);
        slip.Submit();
        Assert.Throws<BusinessException>(() => slip.AddItem(Guid.NewGuid(), 5, 2m));
    }

    [Fact]
    public void HasOverlap_DetectsOverlapping()
    {
        Assert.True(PackingSlip.HasOverlap(1, 5, 3, 8));  // from1 between existing
        Assert.True(PackingSlip.HasOverlap(3, 8, 1, 5));  // existing contains from2
        Assert.True(PackingSlip.HasOverlap(1, 10, 3, 5)); // existing inside new
    }

    [Fact]
    public void HasOverlap_NoOverlap()
    {
        Assert.False(PackingSlip.HasOverlap(1, 5, 6, 10));
        Assert.False(PackingSlip.HasOverlap(6, 10, 1, 5));
    }

    [Fact]
    public void HasOverlap_AdjacentNotOverlapping()
    {
        Assert.False(PackingSlip.HasOverlap(1, 5, 6, 10)); // touching but not overlapping
    }

    [Fact]
    public void HasOverlap_ExactBoundary()
    {
        Assert.True(PackingSlip.HasOverlap(1, 5, 5, 10)); // shares case 5
    }

    [Fact]
    public void SingleCase_NumberOfCases_IsOne()
    {
        var slip = new PackingSlip(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 7, 7);
        Assert.Equal(1, slip.NumberOfCases);
    }
}
