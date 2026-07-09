using MyERP.Inventory.DomainServices;
using Shouldly;
using Xunit;

namespace MyERP.Inventory;

public class StockValuationTests
{
    [Fact]
    public void StockBalance_ValuationRate_Calculated()
    {
        var balance = new StockBalance(100, 5000);

        balance.ValuationRate.ShouldBe(50);
    }

    [Fact]
    public void StockBalance_ZeroQuantity_RateIsZero()
    {
        var balance = new StockBalance(0, 0);

        balance.ValuationRate.ShouldBe(0);
    }

    [Fact]
    public void WeightedAverage_AfterTwoPurchases()
    {
        // Purchase 1: 10 units @ 100 = value 1000
        // Purchase 2: 5 units @ 120 = value 600
        // Total: 15 units, value 1600 => avg rate = 106.67

        var balance1 = new StockBalance(10, 1000); // after first purchase
        var newQty = balance1.Quantity + 5;
        var newValue = balance1.Value + (5 * 120);
        var balance2 = new StockBalance(newQty, newValue);

        balance2.Quantity.ShouldBe(15);
        balance2.Value.ShouldBe(1600);
        balance2.ValuationRate.ShouldBeInRange(106.66m, 106.67m);
    }

    [Fact]
    public void WeightedAverage_StockOut_UsesCurrentRate()
    {
        // Balance: 15 units @ avg 106.67 = value 1600
        // Sell 5 units => out at 106.67 each
        // Remaining: 10 units, value = 10 * 106.67 = 1066.7

        var balance = new StockBalance(15, 1600);
        var outRate = balance.ValuationRate; // 106.67
        var outQty = -5m;
        var newQty = balance.Quantity + outQty;
        var newValue = newQty * outRate;

        newQty.ShouldBe(10);
        newValue.ShouldBeInRange(1066.6m, 1066.7m);
    }

    [Fact]
    public void WeightedAverage_FullDepletion_ValueIsZero()
    {
        var balance = new StockBalance(10, 500);
        var outQty = -10m;
        var newQty = balance.Quantity + outQty;
        var newValue = newQty > 0 ? newQty * balance.ValuationRate : 0;

        newQty.ShouldBe(0);
        newValue.ShouldBe(0);
    }

    [Fact]
    public void WeightedAverage_MultipleTransactions()
    {
        // Simulate: Buy 20 @ 50, Buy 30 @ 60, Sell 25
        decimal qty = 0, value = 0;

        // Buy 20 @ 50
        qty += 20; value += 20 * 50; // qty=20, val=1000, rate=50

        // Buy 30 @ 60
        qty += 30; value += 30 * 60; // qty=50, val=2800, rate=56

        var rateAfterBuys = value / qty;
        rateAfterBuys.ShouldBe(56);

        // Sell 25 @ weighted avg (56)
        qty -= 25;
        value = qty * rateAfterBuys; // qty=25, val=1400

        qty.ShouldBe(25);
        value.ShouldBe(1400);
        (value / qty).ShouldBe(56); // rate unchanged after out
    }
}
