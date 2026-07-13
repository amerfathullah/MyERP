using MyERP.Inventory.DomainServices;
using Shouldly;
using Xunit;

namespace MyERP.Inventory;

public class FifoValuationTests
{
    [Fact]
    public void Fifo_AddStock_AppendsBins()
    {
        var queue = new FifoValuation();

        queue.AddStock(10, 100); // 10 @ 100
        queue.AddStock(5, 120);  // 5 @ 120

        queue.TotalQty.ShouldBe(15);
        queue.TotalValue.ShouldBe(10 * 100 + 5 * 120); // 1600
    }

    [Fact]
    public void Fifo_AddStock_MergesSameRate()
    {
        var queue = new FifoValuation();

        queue.AddStock(10, 100);
        queue.AddStock(5, 100); // same rate — merges

        queue.TotalQty.ShouldBe(15);
        queue.TotalValue.ShouldBe(1500);
    }

    [Fact]
    public void Fifo_RemoveStock_ConsumesFromFront()
    {
        var queue = new FifoValuation();
        queue.AddStock(10, 100); // bin 0
        queue.AddStock(5, 120);  // bin 1

        var consumed = queue.RemoveStock(12); // consumes 10@100 + 2@120

        FifoValuation.GetOutgoingRate(consumed).ShouldBe((10m * 100 + 2m * 120) / 12m);
        queue.TotalQty.ShouldBe(3); // 3 remaining from bin 1
        queue.TotalValue.ShouldBe(3 * 120);
    }

    [Fact]
    public void Fifo_RemoveStock_PartialBinConsumption()
    {
        var queue = new FifoValuation();
        queue.AddStock(10, 50);
        queue.AddStock(10, 60);

        var consumed = queue.RemoveStock(5); // partial from bin 0

        FifoValuation.GetOutgoingRate(consumed).ShouldBe(50); // all from first bin
        queue.TotalQty.ShouldBe(15);
        queue.TotalValue.ShouldBe(5 * 50 + 10 * 60); // 850
    }

    [Fact]
    public void Fifo_RemoveStock_RateMatchedConsumption()
    {
        var queue = new FifoValuation();
        queue.AddStock(10, 100); // bin 0
        queue.AddStock(5, 150);  // bin 1
        queue.AddStock(8, 100);  // bin 2 (same rate as bin 0)

        // Outgoing rate 100 should match bins with rate 100 first
        var consumed = queue.RemoveStock(12, outgoingRate: 100);

        // Should consume from bin 0 (10@100) then bin 2 (2@100)
        FifoValuation.GetOutgoingRate(consumed).ShouldBe(100);
        queue.TotalQty.ShouldBe(11); // 5@150 + 6@100
    }

    [Fact]
    public void Fifo_RemoveStock_GoesNegative()
    {
        var queue = new FifoValuation();
        queue.AddStock(5, 100);

        var consumed = queue.RemoveStock(8, outgoingRate: 100);

        queue.TotalQty.ShouldBe(-3); // negative stock
        consumed.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Fifo_AddStock_RecoverFromNegative()
    {
        var queue = new FifoValuation();
        queue.AddStock(5, 100);
        queue.RemoveStock(8); // go to -3

        queue.AddStock(10, 120); // recover: -3 + 10 = 7

        queue.TotalQty.ShouldBe(7);
        // When negative bin combines with incoming, rate = incoming rate
        queue.TotalValue.ShouldBe(7 * 120);
    }

    [Fact]
    public void Lifo_RemoveStock_ConsumesFromBack()
    {
        var queue = new FifoValuation(isLifo: true);
        queue.AddStock(10, 100); // bin 0
        queue.AddStock(5, 120);  // bin 1 (newest)

        var consumed = queue.RemoveStock(7); // LIFO: consume from bin 1 first

        FifoValuation.GetOutgoingRate(consumed).ShouldBe((5m * 120 + 2m * 100) / 7m);
        queue.TotalQty.ShouldBe(8); // 8 remaining from bin 0
        queue.TotalValue.ShouldBe(8 * 100);
    }

    [Fact]
    public void Fifo_SerializeDeserialize_Roundtrip()
    {
        var queue = new FifoValuation();
        queue.AddStock(10, 100.5m);
        queue.AddStock(5, 200.75m);

        var json = queue.Serialize();
        var restored = FifoValuation.Deserialize(json);

        restored.TotalQty.ShouldBe(15);
        restored.TotalValue.ShouldBe(10 * 100.5m + 5 * 200.75m);
    }

    [Fact]
    public void Fifo_EmptyDeserialize_ReturnsEmptyQueue()
    {
        var queue = FifoValuation.Deserialize(null);

        queue.TotalQty.ShouldBe(0);
        queue.TotalValue.ShouldBe(0);
    }

    [Fact]
    public void Fifo_ComplexScenario_BuyBuySellBuySell()
    {
        var queue = new FifoValuation();

        // Buy 100 @ 10
        queue.AddStock(100, 10);
        queue.TotalQty.ShouldBe(100);

        // Buy 50 @ 12
        queue.AddStock(50, 12);
        queue.TotalQty.ShouldBe(150);
        queue.TotalValue.ShouldBe(100 * 10 + 50 * 12); // 1600

        // Sell 120 (FIFO: 100@10 + 20@12 = 1240/120 = 10.33)
        var consumed1 = queue.RemoveStock(120);
        var rate1 = FifoValuation.GetOutgoingRate(consumed1);
        rate1.ShouldBeInRange(10.33m, 10.34m);
        queue.TotalQty.ShouldBe(30);
        queue.TotalValue.ShouldBe(30 * 12); // 360

        // Buy 80 @ 15
        queue.AddStock(80, 15);
        queue.TotalQty.ShouldBe(110);
        queue.TotalValue.ShouldBe(30 * 12 + 80 * 15); // 1560

        // Sell 50 (FIFO: 30@12 + 20@15 = 660/50 = 13.2)
        var consumed2 = queue.RemoveStock(50);
        var rate2 = FifoValuation.GetOutgoingRate(consumed2);
        rate2.ShouldBe(13.2m);
        queue.TotalQty.ShouldBe(60);
        queue.TotalValue.ShouldBe(60 * 15); // 900
    }

    [Fact]
    public void Lifo_ComplexScenario_BuyBuySellBuySell()
    {
        var queue = new FifoValuation(isLifo: true);

        // Buy 100 @ 10
        queue.AddStock(100, 10);

        // Buy 50 @ 12
        queue.AddStock(50, 12);

        // Sell 30 (LIFO: all from top bin 50@12 → 30@12)
        var consumed1 = queue.RemoveStock(30);
        FifoValuation.GetOutgoingRate(consumed1).ShouldBe(12);
        queue.TotalQty.ShouldBe(120);

        // Buy 20 @ 15
        queue.AddStock(20, 15);

        // Sell 40 (LIFO: 20@15 + 20@12 → avg 13.5)
        var consumed2 = queue.RemoveStock(40);
        FifoValuation.GetOutgoingRate(consumed2).ShouldBe((20m * 15 + 20m * 12) / 40m);
        queue.TotalQty.ShouldBe(100);
    }
}
