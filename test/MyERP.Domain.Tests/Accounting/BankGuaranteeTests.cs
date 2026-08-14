using System;
using MyERP.Accounting.Entities;
using MyERP.Core;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Accounting;

public class BankGuaranteeTests
{
    private static BankGuarantee CreateBg(
        BankGuaranteeType bgType = BankGuaranteeType.Receiving,
        decimal amount = 10000m,
        int validityDays = 90) =>
        new(Guid.NewGuid(), Guid.NewGuid(), bgType, amount, DateTime.UtcNow.Date,
            validityDays, customerId: Guid.NewGuid(), supplierId: null)
        {
            BankGuaranteeNumber = "BG-2026-0001",
            NameOfBeneficiary = "Acme Corp",
            Bank = "Maybank",
            BankAccountNumber = "1234567890"
        };

    [Fact]
    public void Create_SetsDefaultsAndCalculatesEndDate()
    {
        var bg = CreateBg(BankGuaranteeType.Receiving, 50000m, 60);
        bg.Status.ShouldBe(DocumentStatus.Draft);
        bg.Amount.ShouldBe(50000m);
        bg.ValidityDays.ShouldBe(60);
        bg.EndDate.ShouldBe(bg.StartDate.AddDays(60));
        bg.BgType.ShouldBe(BankGuaranteeType.Receiving);
    }

    [Fact]
    public void Create_WithoutParty_Throws()
    {
        Should.Throw<BusinessException>(() =>
            new BankGuarantee(Guid.NewGuid(), Guid.NewGuid(), BankGuaranteeType.Receiving,
                1000m, DateTime.UtcNow.Date, 30, customerId: null, supplierId: null));
    }

    [Fact]
    public void Submit_Valid_TransitionsToSubmitted()
    {
        var bg = CreateBg();
        bg.Submit();
        bg.Status.ShouldBe(DocumentStatus.Submitted);
    }

    [Fact]
    public void Submit_MissingBgNumber_Throws()
    {
        var bg = CreateBg();
        bg.BankGuaranteeNumber = null;
        Should.Throw<BusinessException>(() => bg.Submit());
    }

    [Fact]
    public void Submit_MissingBeneficiary_Throws()
    {
        var bg = CreateBg();
        bg.NameOfBeneficiary = "";
        Should.Throw<BusinessException>(() => bg.Submit());
    }

    [Fact]
    public void Submit_MissingBank_Throws()
    {
        var bg = CreateBg();
        bg.Bank = "  ";
        Should.Throw<BusinessException>(() => bg.Submit());
    }

    [Fact]
    public void Submit_NonPositiveAmount_Throws()
    {
        var bg = CreateBg(amount: 0m);
        Should.Throw<BusinessException>(() => bg.Submit());
    }

    [Fact]
    public void Cancel_Submitted_TransitionsToCancelled()
    {
        var bg = CreateBg();
        bg.Submit();
        bg.Cancel();
        bg.Status.ShouldBe(DocumentStatus.Cancelled);
    }

    [Fact]
    public void Cancel_Draft_Throws()
    {
        var bg = CreateBg();
        Should.Throw<BusinessException>(() => bg.Cancel());
    }
}
