using System;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Integration;

/// <summary>
/// Edge case tests for boundary conditions and error paths.
/// </summary>
public class EdgeCaseTests
{
    [Fact]
    public void Budget_SubmitTwice_Throws()
    {
        var b = new Budget(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "CostCenter", Guid.NewGuid());
        b.AddAccount(Guid.NewGuid(), 10000m);
        b.Submit();
        Should.Throw<BusinessException>(() => b.Submit());
    }

    [Fact]
    public void JobCard_HoldFromOpen_Throws()
    {
        var jc = new JobCard(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 50m, 10);
        Should.Throw<BusinessException>(() => jc.Hold());
    }

    [Fact]
    public void JobCard_ResumeFromOpen_Throws()
    {
        var jc = new JobCard(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 50m, 10);
        Should.Throw<BusinessException>(() => jc.Resume());
    }

    [Fact]
    public void StockReservation_NegativeQty_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            new StockReservationEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                Guid.NewGuid(), "SalesOrder", Guid.NewGuid(), -10m));
    }

    [Fact]
    public void PickList_AddAfterSubmit_Throws()
    {
        var pl = new PickList(Guid.NewGuid(), Guid.NewGuid(), "Delivery");
        pl.AddItem(Guid.NewGuid(), Guid.NewGuid(), 10m);
        pl.Submit();
        Should.Throw<BusinessException>(() => pl.AddItem(Guid.NewGuid(), Guid.NewGuid(), 5m));
    }

    [Fact]
    public void Workstation_EmptyName_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            new Workstation(Guid.NewGuid(), Guid.NewGuid(), ""));
    }

    [Fact]
    public void Operation_EmptyName_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            new Operation(Guid.NewGuid(), ""));
    }

    [Fact]
    public void Routing_EmptyName_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            new Routing(Guid.NewGuid(), ""));
    }

    [Fact]
    public void LandedCostVoucher_NegativeCharge_Throws()
    {
        var lcv = new LandedCostVoucher(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        Should.Throw<ArgumentException>(() => lcv.AddCharge("Freight", Guid.NewGuid(), -100m));
    }

    [Fact]
    public void PricingRule_Disabled_NeverMatches()
    {
        var itemId = Guid.NewGuid();
        var rule = new PricingRule(Guid.NewGuid(), "Disabled Rule",
            Sales.PricingRuleApplyOn.ItemCode, Sales.PricingRuleType.Discount)
        {
            ApplyOnId = itemId,
            IsDisabled = true,
            DiscountPercentage = 50m,
        };
        rule.Matches(itemId, null, 100, 1000, DateTime.UtcNow).ShouldBeFalse();
    }

    [Fact]
    public void Subscription_PauseFromCancelled_Throws()
    {
        var sub = new Subscription(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Customer", DateTime.UtcNow, "Monthly");
        sub.Cancel();
        Should.Throw<BusinessException>(() => sub.Pause());
    }

    [Fact]
    public void Dunning_ResolveFromDraft_Throws()
    {
        var d = new Dunning(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, 1);
        d.AddOverduePayment(Guid.NewGuid(), 1000m, DateTime.UtcNow, 30);
        Should.Throw<BusinessException>(() => d.Resolve());
    }

    [Fact]
    public void BlanketOrder_CancelDraft_Throws()
    {
        var bo = new BlanketOrder(Guid.NewGuid(), Guid.NewGuid(), "BO-X", "Selling",
            Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow.AddYears(1));
        bo.AddItem(Guid.NewGuid(), 100, 5m);
        Should.Throw<BusinessException>(() => bo.Cancel()); // must submit first
    }

    [Fact]
    public void SupplierQuotation_CancelDraft_Throws()
    {
        var sq = new Purchasing.Entities.SupplierQuotation(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), DateTime.UtcNow);
        sq.AddItem(Guid.NewGuid(), 10, 5m);
        Should.Throw<BusinessException>(() => sq.Cancel()); // must submit first
    }

    [Fact]
    public void PaymentRequest_SubmitTwice_Throws()
    {
        var pr = new PaymentRequest(Guid.NewGuid(), Guid.NewGuid(), "SalesInvoice",
            Guid.NewGuid(), Guid.NewGuid(), "Customer", 1000m);
        pr.Submit();
        Should.Throw<BusinessException>(() => pr.Submit());
    }

    [Fact]
    public void AssetMovement_SubmitTwice_Throws()
    {
        var am = new Assets.Entities.AssetMovement(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "Transfer", DateTime.UtcNow);
        am.Submit();
        Should.Throw<BusinessException>(() => am.Submit());
    }
}
