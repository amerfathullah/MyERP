using System;
using MyERP.Core;
using MyERP.Sales.Entities;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests.Sales;

/// <summary>
/// Unit tests for Dunning fee and interest claimability, auto-resolution with unpaid fees,
/// and reopening on payment cancellation (ERPNext PR #58227).
/// </summary>
public class DunningResolutionAndFeeTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();

    [Fact]
    public void Dunning_FeeAndInterest_CalculatesCorrectAmounts()
    {
        var dunning = new Dunning(Guid.NewGuid(), _companyId, _customerId, DateTime.UtcNow, 1);
        dunning.AddOverduePayment(Guid.NewGuid(), 1000m, DateTime.UtcNow.AddDays(-30), 30);
        dunning.DunningFee = 50m;
        dunning.InterestAmount = 25m;

        Assert.Equal(75m, dunning.DunningAmount);
        Assert.Equal(0m, dunning.PaidDunningAmount);
        Assert.Equal(75m, dunning.UnpaidDunningAmount);
        Assert.Equal(1075m, dunning.GrandTotal);
    }

    [Fact]
    public void Dunning_RecordDunningPayment_UpdatesPaidAndUnpaidAmounts()
    {
        var dunning = new Dunning(Guid.NewGuid(), _companyId, _customerId, DateTime.UtcNow, 1);
        dunning.AddOverduePayment(Guid.NewGuid(), 1000m, DateTime.UtcNow.AddDays(-30), 30);
        dunning.DunningFee = 50m;
        dunning.InterestAmount = 25m;

        // Partial payment of dunning fee
        dunning.RecordDunningPayment(30m);
        Assert.Equal(30m, dunning.PaidDunningAmount);
        Assert.Equal(45m, dunning.UnpaidDunningAmount);

        // Full payment
        dunning.RecordDunningPayment(45m);
        Assert.Equal(75m, dunning.PaidDunningAmount);
        Assert.Equal(0m, dunning.UnpaidDunningAmount);

        // Reversal of payment (e.g. cancelled PE)
        dunning.RecordDunningPayment(-20m);
        Assert.Equal(55m, dunning.PaidDunningAmount);
        Assert.Equal(20m, dunning.UnpaidDunningAmount);
    }

    [Fact]
    public void Dunning_RecordDunningPayment_DoesNotAllowNegativePaidAmount()
    {
        var dunning = new Dunning(Guid.NewGuid(), _companyId, _customerId, DateTime.UtcNow, 1);
        dunning.AddOverduePayment(Guid.NewGuid(), 1000m, DateTime.UtcNow.AddDays(-30), 30);
        dunning.DunningFee = 50m;

        dunning.RecordDunningPayment(-100m);
        Assert.Equal(0m, dunning.PaidDunningAmount);
        Assert.Equal(50m, dunning.UnpaidDunningAmount);
    }

    [Fact]
    public void Dunning_Reopen_TransitionsFromPostedToSubmitted()
    {
        var dunning = new Dunning(Guid.NewGuid(), _companyId, _customerId, DateTime.UtcNow, 1);
        dunning.AddOverduePayment(Guid.NewGuid(), 1000m, DateTime.UtcNow.AddDays(-30), 30);
        dunning.Submit();
        Assert.Equal(DocumentStatus.Submitted, dunning.Status);

        dunning.Resolve();
        Assert.Equal(DocumentStatus.Posted, dunning.Status);

        // Reopen when payment cancelled and invoice owed again
        dunning.Reopen();
        Assert.Equal(DocumentStatus.Submitted, dunning.Status);
    }

    [Fact]
    public void Dunning_Reopen_ThrowsIfNotPosted()
    {
        var dunning = new Dunning(Guid.NewGuid(), _companyId, _customerId, DateTime.UtcNow, 1);
        dunning.AddOverduePayment(Guid.NewGuid(), 1000m, DateTime.UtcNow.AddDays(-30), 30);
        dunning.Submit();

        Assert.Throws<BusinessException>(() => dunning.Reopen());
    }

    [Fact]
    public void Dunning_SubmitWithoutOverduePayments_Throws()
    {
        var dunning = new Dunning(Guid.NewGuid(), _companyId, _customerId, DateTime.UtcNow, 1);
        Assert.Throws<BusinessException>(() => dunning.Submit());
    }
}
