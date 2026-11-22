using System;
using System.Collections.Generic;
using System.Linq;

namespace MyERP.Inventory.DomainServices;

/// <summary>
/// Independent deterministic stock ledger replay engine for one (Item, Warehouse) stream.
/// Replays entries from the first Stock Ledger Entry using pure valuation rules without calling
/// StockValuationService, allowingStockValuationComparison to detect valuation drift or missing reposts.
/// Maps to ERPNext erpnext/stock/expected_valuation.py.
/// </summary>
public static class ExpectedValuationReplayEngine
{
    public const string BasisIncomingRate = "Incoming rate";
    public const string BasisMovingAverage = "Moving average";
    public const string BasisFifo = "FIFO queue";
    public const string BasisLifo = "LIFO stack";
    public const string BasisPurchaseReturn = "Rate of the returned receipt";
    public const string BasisBatchAverage = "Batch average";
    public const string BasisSerialRate = "Serial no receipt rate";
    public const string BasisReconciled = "Reconciled qty and rate";
    public const string BasisWriteOff = "Write-off of leftover value";
    public const string BasisStandardCost = "Standard cost";
    public const string BasisNoStock = "No stock on hand (fallback rate)";
    public const string BasisAdjustmentReset = "Counted out by an Adjustment Entry at the ledger's value";
    public const string BasisAdjusted = "Set by an Adjustment Entry";
    public const string AdjustmentEntryPurpose = "Adjustment Entry";

    public const decimal QtyNoise = 0.000000001m;

    public sealed record LotMovement(
        decimal Qty,
        decimal Rate = 0m,
        Guid? BatchId = null,
        string? SerialNo = null);

    public sealed record ReconciledBalance(
        decimal Qty,
        decimal ValuationRate,
        decimal CurrentValuationRate);

    public sealed record AdjustmentRowInput(
        decimal Qty,
        decimal ValuationRate,
        Guid? BatchId = null,
        IReadOnlyList<string>? SerialNos = null);

    public sealed record LedgerEntryInput(
        Guid SleId,
        DateTime PostingDate,
        DateTime PostingDateTime,
        DateTime CreationTime,
        string? VoucherType,
        Guid? VoucherId,
        Guid? VoucherDetailNo,
        decimal QuantityChange,
        decimal IncomingRate,
        decimal OutgoingRate,
        decimal BalanceQuantity,
        decimal ValuationRate,
        decimal BalanceValue,
        decimal StockValueDifference,
        Guid? BatchId,
        Guid? SerialNoId,
        string? SerialNoNumber,
        Guid? SerialAndBatchBundleId,
        bool IsAdjustmentEntry,
        bool HasInventoryDimensions = false,
        IReadOnlyList<LotMovement>? BundleMovements = null,
        ReconciledBalance? ReconciledBalance = null,
        bool IsAdjustmentVoucher = false,
        bool IsLastAdjustmentEntry = false,
        IReadOnlyList<AdjustmentRowInput>? AdjustmentRows = null,
        decimal? PurchaseReturnRate = null,
        decimal? EffectiveStandardRate = null);

    public sealed record ItemReplayContext(
        ValuationMethod ValuationMethod,
        bool HasSerialNo,
        bool HasBatchNo,
        bool UseSerialNoWiseValuation,
        bool DoNotUseBatchwiseValuation,
        int CurrencyPrecision,
        decimal QtyTolerance,
        decimal ValueTolerance,
        IReadOnlySet<Guid> BatchwiseValuedBatchIds)
    {
        public bool ValuesEverythingInPool => HasSerialNo && !UseSerialNoWiseValuation;

        public bool BatchwiseValuationIsOff =>
            ValuesEverythingInPool
            || (ValuationMethod == ValuationMethod.WeightedAverage && DoNotUseBatchwiseValuation);

        public bool IsValuedBatchWise(Guid? batchId) =>
            batchId.HasValue
            && !BatchwiseValuationIsOff
            && BatchwiseValuedBatchIds.Contains(batchId.Value);

        public bool IsReconciledBalance(LedgerEntryInput entry)
        {
            if (!IsVoucherType(entry.VoucherType, "StockReconciliation")
                || entry.IsAdjustmentEntry
                || entry.BatchId.HasValue
                || entry.HasInventoryDimensions)
            {
                return false;
            }

            if (ValuesEverythingInPool && !HasBatchNo)
            {
                return true;
            }

            return !entry.SerialAndBatchBundleId.HasValue && !entry.SerialNoId.HasValue;
        }

        public bool IsValuedAsAWole(LedgerEntryInput entry)
        {
            if (ValuationMethod != ValuationMethod.WeightedAverage)
                return false;

            if (ValuesEverythingInPool)
                return true;

            return !entry.SerialAndBatchBundleId.HasValue
                && !entry.SerialNoId.HasValue
                && !IsValuedBatchWise(entry.BatchId);
        }
    }

    public sealed record ExpectedValuationResult(
        decimal QtyAfterTransaction,
        decimal StockValueDifference,
        decimal StockValue,
        decimal ValuationRate,
        string Basis);

    private interface IStockPool
    {
        decimal Qty { get; }
        decimal Value { get; }
        decimal LastRate { get; }
        void Receive(decimal qty, decimal rate);
        decimal Issue(decimal qty, decimal? preferredRate = null);
        void SetBalance(decimal qty, decimal rate);
        void Clear();
    }

    private sealed class MovingAveragePool : IStockPool
    {
        public decimal Qty { get; private set; }
        public decimal Rate { get; set; }
        public decimal Value => Qty * Rate;
        public decimal LastRate => Rate;

        public void Receive(decimal qty, decimal rate)
        {
            var newQty = Qty + qty;
            if (Qty <= 0m && newQty >= 0m)
            {
                Rate = rate;
            }
            else if (newQty > 0m)
            {
                Rate = (Value + qty * rate) / newQty;
            }
            else if (Rate == 0m)
            {
                Rate = rate;
            }

            Qty = newQty;
        }

        public decimal Issue(decimal qty, decimal? preferredRate = null)
        {
            Qty -= qty;
            return qty * Rate;
        }

        public void SetBalance(decimal qty, decimal rate)
        {
            Qty = qty;
            Rate = rate;
        }

        public void Clear() => Qty = 0m;
    }

    private sealed class QueuePool : IStockPool
    {
        private readonly bool _isLifo;
        private readonly List<(decimal Qty, decimal Rate)> _layers = new();

        public QueuePool(bool isLifo) => _isLifo = isLifo;

        public decimal Qty => _layers.Sum(l => l.Qty);
        public decimal Value => _layers.Sum(l => l.Qty * l.Rate);
        public decimal LastRate { get; private set; }

        public void Receive(decimal qty, decimal rate)
        {
            if (rate != 0m)
                LastRate = rate;

            if (_layers.Count > 0 && _layers[^1].Qty <= 0m)
            {
                var balance = _layers[^1].Qty + qty;
                _layers[^1] = balance > 0m ? (balance, rate) : (balance, _layers[^1].Rate);
            }
            else if (_layers.Count > 0 && _layers[^1].Rate == rate)
            {
                _layers[^1] = (_layers[^1].Qty + qty, rate);
            }
            else
            {
                _layers.Add((qty, rate));
            }
        }

        public decimal Issue(decimal qty, decimal? preferredRate = null)
        {
            var valueBefore = Value;
            var remaining = qty;

            while (remaining > 0m)
            {
                if (_layers.Count == 0)
                {
                    _layers.Add((0m, preferredRate ?? LastRate));
                }

                var index = GetLayerIndexToConsume(preferredRate);
                var (layerQty, layerRate) = _layers[index];
                if (layerRate != 0m)
                    LastRate = layerRate;

                if (layerQty > remaining)
                {
                    _layers[index] = (layerQty - remaining, layerRate);
                    remaining = 0m;
                    continue;
                }

                _layers.RemoveAt(index);
                remaining -= layerQty;
                if (_layers.Count == 0 && remaining > 0m)
                {
                    _layers.Add((-remaining, preferredRate ?? layerRate));
                    remaining = 0m;
                }
            }

            return valueBefore - Value;
        }

        private int GetLayerIndexToConsume(decimal? preferredRate)
        {
            // Per ERPNext PR #67376 (commit 0c62d7399b): LIFO purchase returns also consume the layer
            // carrying the returned receipt's rate first, scanning from the top of the stack.
            if (preferredRate.HasValue)
            {
                if (_isLifo)
                {
                    for (var i = _layers.Count - 1; i >= 0; i--)
                    {
                        if (_layers[i].Rate == preferredRate.Value)
                            return i;
                    }
                }
                else
                {
                    for (var i = 0; i < _layers.Count; i++)
                    {
                        if (_layers[i].Rate == preferredRate.Value)
                            return i;
                    }
                }
            }

            return _isLifo ? _layers.Count - 1 : 0;
        }

        public void SetBalance(decimal qty, decimal rate)
        {
            _layers.Clear();
            _layers.Add((qty, rate));
            if (rate != 0m)
                LastRate = rate;
        }

        public void Clear() => _layers.Clear();
    }

    private sealed class BatchLot
    {
        public decimal Qty { get; set; }
        public decimal Value { get; set; }
        public decimal AverageRate => Qty != 0m ? Value / Qty : 0m;
    }

    private sealed class ItemWarehouseStock
    {
        public ValuationMethod ValuationMethod { get; }
        public IStockPool Pool { get; private set; }
        public Dictionary<Guid, BatchLot> BatchLots { get; private set; } = new();
        public Dictionary<string, decimal> SerialRates { get; private set; } = new(StringComparer.Ordinal);
        public Dictionary<string, decimal> SerialsIssuedBeforeReceipt { get; private set; } = new(StringComparer.Ordinal);
        public decimal LastValuationRate { get; set; }
        public decimal CarriedValue { get; set; }
        public decimal LedgerRateOfEntry { get; set; }

        public ItemWarehouseStock(ValuationMethod valuationMethod)
        {
            ValuationMethod = valuationMethod;
            Pool = CreatePool(valuationMethod);
        }

        private static IStockPool CreatePool(ValuationMethod method) =>
            method is ValuationMethod.FIFO or ValuationMethod.LIFO
                ? new QueuePool(method == ValuationMethod.LIFO)
                : new MovingAveragePool();

        public decimal Qty =>
            Pool.Qty
            + BatchLots.Values.Sum(b => b.Qty)
            + SerialRates.Count
            - SerialsIssuedBeforeReceipt.Count;

        public decimal Value =>
            Pool.Value
            + BatchLots.Values.Sum(b => b.Value)
            + SerialRates.Values.Sum()
            - SerialsIssuedBeforeReceipt.Values.Sum();

        public string PoolBasis => ValuationMethod switch
        {
            ValuationMethod.FIFO => BasisFifo,
            ValuationMethod.LIFO => BasisLifo,
            _ => BasisMovingAverage,
        };

        public decimal GetRateToFallBackOn()
        {
            if (Qty > 0m && Value > 0m)
                return Value / Qty;

            if (LastValuationRate != 0m)
                return LastValuationRate;

            if (Pool.LastRate != 0m)
                return Pool.LastRate;

            return LedgerRateOfEntry;
        }

        public void ReceiveIntoPool(decimal qty, decimal rate) => Pool.Receive(qty, rate);

        public void IssueFromPool(decimal qty, decimal? preferredRate = null)
        {
            if (Pool.Qty <= 0m && Pool.LastRate == 0m)
            {
                Pool.Receive(0m, preferredRate ?? GetRateToFallBackOn());
            }

            Pool.Issue(qty, preferredRate);
        }

        public void ReceiveIntoBatch(Guid batchId, decimal qty, decimal rate)
        {
            if (!BatchLots.TryGetValue(batchId, out var lot))
            {
                lot = new BatchLot();
                BatchLots[batchId] = lot;
            }

            lot.Qty += qty;
            lot.Value += qty * rate;
        }

        public bool IssueFromBatch(Guid batchId, decimal qty)
        {
            if (!BatchLots.TryGetValue(batchId, out var lot))
            {
                lot = new BatchLot();
                BatchLots[batchId] = lot;
            }

            var hasStock = lot.Qty > 0m;
            var rate = hasStock ? lot.AverageRate : GetRateToFallBackOn();

            lot.Qty -= qty;
            lot.Value -= qty * rate;
            if (Math.Abs(lot.Qty) < QtyNoise)
            {
                BatchLots.Remove(batchId);
            }

            return hasStock;
        }

        public void ReceiveSerial(string serialNo, decimal rate)
        {
            if (SerialsIssuedBeforeReceipt.Remove(serialNo))
                return;

            SerialRates[serialNo] = rate;
        }

        public bool IssueSerial(string serialNo)
        {
            if (SerialRates.Remove(serialNo))
                return true;

            SerialsIssuedBeforeReceipt[serialNo] = GetRateToFallBackOn();
            return false;
        }

        public void SetReconciledBalance(decimal qty, decimal rate) => Pool.SetBalance(qty, rate);

        public void Clear()
        {
            Pool = CreatePool(ValuationMethod);
            BatchLots = new Dictionary<Guid, BatchLot>();
            SerialRates = new Dictionary<string, decimal>(StringComparer.Ordinal);
            SerialsIssuedBeforeReceipt = new Dictionary<string, decimal>(StringComparer.Ordinal);
        }

        public void WriteOffLeftoverValue()
        {
            Pool.Clear();
            var spent = BatchLots.Where(kv => Math.Abs(kv.Value.Qty) < QtyNoise).Select(kv => kv.Key).ToList();
            foreach (var key in spent)
                BatchLots.Remove(key);
        }
    }

    public static IReadOnlyList<(LedgerEntryInput Entry, ExpectedValuationResult Expected)> ReplayLedger(
        IReadOnlyList<LedgerEntryInput> entries,
        ItemReplayContext context)
    {
        var stock = new ItemWarehouseStock(context.ValuationMethod);
        var results = new List<(LedgerEntryInput, ExpectedValuationResult)>(entries.Count);

        foreach (var entry in entries)
        {
            results.Add((entry, ValueLedgerEntry(stock, entry, context)));
        }

        return results;
    }

    private static ExpectedValuationResult ValueLedgerEntry(
        ItemWarehouseStock stock,
        LedgerEntryInput entry,
        ItemReplayContext context)
    {
        if (entry.IsAdjustmentVoucher)
            return ValueAdjustmentEntry(stock, entry, context);

        if (context.ValuationMethod == ValuationMethod.StandardCost)
            return ValueAtStandardCost(stock, entry, context);

        var carriedValueBefore = stock.CarriedValue;
        var valueBefore = stock.Value;
        stock.LedgerRateOfEntry = entry.QuantityChange != 0m
            ? Math.Abs(entry.StockValueDifference / entry.QuantityChange)
            : 0m;

        string basis;
        if (context.IsReconciledBalance(entry))
        {
            var (reconciledQty, reconciledRate) = ResolveReconciledBalance(entry, context.CurrencyPrecision);
            stock.SetReconciledBalance(reconciledQty, reconciledRate);
            basis = BasisReconciled;
        }
        else if (entry.IsAdjustmentEntry && entry.QuantityChange == 0m)
        {
            basis = BasisWriteOff;
        }
        else if (entry.QuantityChange > 0m)
        {
            basis = ReceiveStock(stock, entry, context);
        }
        else if (entry.QuantityChange < 0m)
        {
            basis = IssueStock(stock, entry, context);
        }
        else
        {
            basis = BasisIncomingRate;
        }

        if (Math.Abs(stock.Qty) < context.QtyTolerance)
        {
            if (Math.Abs(stock.CarriedValue + stock.Value - valueBefore) >= context.ValueTolerance)
            {
                basis = basis == BasisWriteOff ? basis : $"{basis}, {BasisWriteOff}";
            }

            stock.WriteOffLeftoverValue();
            stock.CarriedValue = 0m;
        }
        else if (basis == BasisReconciled || context.IsValuedAsAWole(entry))
        {
            stock.CarriedValue = Math.Round(stock.Value, context.CurrencyPrecision, MidpointRounding.AwayFromZero);
            CarryMovingAverageRateFromRoundedValue(stock);
        }
        else
        {
            stock.CarriedValue = Math.Round(
                stock.CarriedValue + stock.Value - valueBefore,
                context.CurrencyPrecision,
                MidpointRounding.AwayFromZero);
        }

        return MakeExpectedValues(stock, carriedValueBefore, basis);
    }

    private static ExpectedValuationResult ValueAdjustmentEntry(
        ItemWarehouseStock stock,
        LedgerEntryInput entry,
        ItemReplayContext context)
    {
        if (!entry.IsLastAdjustmentEntry)
        {
            stock.CarriedValue = entry.BalanceValue;
            return new ExpectedValuationResult(
                entry.BalanceQuantity,
                entry.StockValueDifference,
                entry.BalanceValue,
                entry.ValuationRate,
                BasisAdjustmentReset);
        }

        var carriedValueBefore = entry.BalanceValue - entry.StockValueDifference;
        stock.Clear();

        if (entry.AdjustmentRows != null)
        {
            foreach (var row in entry.AdjustmentRows)
            {
                var valueBefore = stock.Value;
                if (row.SerialNos != null && row.SerialNos.Count > 0)
                {
                    foreach (var sn in row.SerialNos)
                    {
                        if (context.ValuesEverythingInPool || context.ValuationMethod == ValuationMethod.StandardCost)
                            stock.ReceiveIntoPool(1m, row.ValuationRate);
                        else
                            stock.ReceiveSerial(sn, row.ValuationRate);
                    }
                }
                else if (row.BatchId.HasValue
                         && context.ValuationMethod != ValuationMethod.StandardCost
                         && context.IsValuedBatchWise(row.BatchId))
                {
                    stock.ReceiveIntoBatch(row.BatchId.Value, row.Qty, row.ValuationRate);
                }
                else if (row.Qty != 0m)
                {
                    stock.ReceiveIntoPool(row.Qty, row.ValuationRate);
                }

                // Per ERPNext commits 85a5c97646 and 55d0f687a8: the ledger counts in every row as an entry of its own,
                // and values a Standard Cost item's whole balance at the standard rate after each row.
                if (context.ValuationMethod == ValuationMethod.StandardCost)
                {
                    stock.CarriedValue = Math.Round(stock.Value, context.CurrencyPrecision, MidpointRounding.AwayFromZero);
                }
                else
                {
                    stock.CarriedValue = Math.Round(
                        stock.CarriedValue + stock.Value - valueBefore,
                        context.CurrencyPrecision,
                        MidpointRounding.AwayFromZero);
                }
            }
        }

        CarryMovingAverageRateFromRoundedValue(stock);

        return MakeExpectedValues(stock, carriedValueBefore, BasisAdjusted);
    }

    private static void CarryMovingAverageRateFromRoundedValue(ItemWarehouseStock stock)
    {
        if (stock.Pool is MovingAveragePool avgPool && avgPool.Qty != 0m && stock.Qty == avgPool.Qty)
        {
            avgPool.Rate = stock.CarriedValue / avgPool.Qty;
        }
    }

    private static string ReceiveStock(ItemWarehouseStock stock, LedgerEntryInput entry, ItemReplayContext context)
    {
        foreach (var movement in GetLotMovements(entry, context))
        {
            if (!string.IsNullOrWhiteSpace(movement.SerialNo))
                stock.ReceiveSerial(movement.SerialNo, movement.Rate);
            else if (movement.BatchId.HasValue && context.IsValuedBatchWise(movement.BatchId))
                stock.ReceiveIntoBatch(movement.BatchId.Value, movement.Qty, movement.Rate);
            else
                stock.ReceiveIntoPool(movement.Qty, movement.Rate);
        }

        return BasisIncomingRate;
    }

    private static string IssueStock(ItemWarehouseStock stock, LedgerEntryInput entry, ItemReplayContext context)
    {
        var bases = new List<string>();
        var returnRate = GetPurchaseReturnRate(entry, context);

        foreach (var movement in GetLotMovements(entry, context))
        {
            string b;
            if (!string.IsNullOrWhiteSpace(movement.SerialNo))
            {
                var wasOnHand = stock.IssueSerial(movement.SerialNo);
                b = wasOnHand ? BasisSerialRate : BasisNoStock;
            }
            else if (movement.BatchId.HasValue && context.IsValuedBatchWise(movement.BatchId))
            {
                var hadStock = stock.IssueFromBatch(movement.BatchId.Value, movement.Qty);
                b = hadStock ? BasisBatchAverage : BasisNoStock;
            }
            else
            {
                var hadStock = stock.Pool.Qty > 0m;
                stock.IssueFromPool(movement.Qty, returnRate);
                b = returnRate.HasValue
                    ? BasisPurchaseReturn
                    : (hadStock ? stock.PoolBasis : BasisNoStock);
            }

            if (!bases.Contains(b))
                bases.Add(b);
        }

        return string.Join(", ", bases);
    }

    private static ExpectedValuationResult ValueAtStandardCost(
        ItemWarehouseStock stock,
        LedgerEntryInput entry,
        ItemReplayContext context)
    {
        var carriedValueBefore = stock.CarriedValue;
        var rate = entry.EffectiveStandardRate ?? entry.ValuationRate;

        decimal qty;
        if (context.IsReconciledBalance(entry) && !entry.SerialAndBatchBundleId.HasValue)
        {
            (qty, _) = ResolveReconciledBalance(entry, context.CurrencyPrecision);
        }
        else
        {
            qty = stock.Pool.Qty + entry.QuantityChange;
        }

        stock.Pool.SetBalance(qty, rate);
        stock.CarriedValue = Math.Round(stock.Value, context.CurrencyPrecision, MidpointRounding.AwayFromZero);

        return MakeExpectedValues(stock, carriedValueBefore, BasisStandardCost);
    }

    private static ExpectedValuationResult MakeExpectedValues(
        ItemWarehouseStock stock,
        decimal carriedValueBefore,
        string basis)
    {
        var qty = stock.Qty;
        if (qty != 0m)
        {
            stock.LastValuationRate = stock.CarriedValue / qty;
        }

        return new ExpectedValuationResult(
            qty,
            stock.CarriedValue - carriedValueBefore,
            stock.CarriedValue,
            stock.LastValuationRate,
            basis);
    }

    private static (decimal Qty, decimal Rate) ResolveReconciledBalance(
        LedgerEntryInput entry,
        int currencyPrecision)
    {
        if (entry.ReconciledBalance != null)
        {
            var rate = entry.ReconciledBalance.ValuationRate;
            if (entry.QuantityChange < 0m
                && (entry.SerialAndBatchBundleId.HasValue || entry.SerialNoId.HasValue))
            {
                rate = entry.ReconciledBalance.CurrentValuationRate;
            }

            // Per ERPNext commit 3bf0644cda: keep a reconciliation or pool rate unrounded in expected valuation
            return (entry.ReconciledBalance.Qty, rate);
        }

        return (entry.BalanceQuantity, entry.ValuationRate);
    }

    /// <summary>
    /// Computes the Moving Average valuation rate after an outflow at a set outgoing rate (such as a purchase return),
    /// clamping <c>newStockValue</c> at 0 so the outflow never drives stock value or valuation rate negative
    /// per ERPNext commit 492e2e0e4a (<c>stock_ledger.py</c>).
    /// </summary>
    public static decimal CalculateMovingAverageRateAfterSetRateOutflow(
        decimal previousStockValue,
        decimal previousQty,
        decimal actualQtyChange,
        decimal outgoingRate)
    {
        var newStockQty = previousQty + actualQtyChange;
        if (newStockQty <= 0m)
        {
            return outgoingRate;
        }

        var newStockValue = previousStockValue + (actualQtyChange * outgoingRate);
        return Math.Max(newStockValue, 0m) / newStockQty;
    }

    /// <summary>
    /// Determines whether an Adjustment Entry ledger row sits after <paramref name="slePostingDateTime"/>
    /// (including an Adjustment Entry at the exact same posting datetime created after the original entry being cancelled)
    /// per ERPNext commit 33a523c99d (<c>get_later_than</c> in <c>valuation_adjustment.py</c>).
    /// </summary>
    public static bool IsAdjustmentEntryLaterThanSle(
        DateTime adjustmentPostingDateTime,
        DateTime adjustmentCreationTime,
        DateTime slePostingDateTime,
        bool isCancellingSle,
        DateTime? originalCancelledSleCreationTime)
    {
        if (adjustmentPostingDateTime > slePostingDateTime)
        {
            return true;
        }

        if (isCancellingSle &&
            originalCancelledSleCreationTime.HasValue &&
            adjustmentPostingDateTime == slePostingDateTime &&
            adjustmentCreationTime > originalCancelledSleCreationTime.Value)
        {
            return true;
        }

        return false;
    }

    private static IReadOnlyList<LotMovement> GetLotMovements(LedgerEntryInput entry, ItemReplayContext context)
    {
        var qty = Math.Abs(entry.QuantityChange);

        if (context.ValuesEverythingInPool)
        {
            return new[] { new LotMovement(qty, entry.IncomingRate) };
        }

        if (entry.SerialAndBatchBundleId.HasValue && entry.BundleMovements is { Count: > 0 })
        {
            return entry.BundleMovements
                .Select(m => new LotMovement(Math.Abs(m.Qty), m.Rate, m.BatchId, m.SerialNo))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(entry.SerialNoNumber))
        {
            var serials = entry.SerialNoNumber
                .Split(new[] { '\n', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return serials
                .Select(sn => new LotMovement(1m, entry.IncomingRate, SerialNo: sn))
                .ToList();
        }

        if (entry.SerialNoId.HasValue)
        {
            return new[] { new LotMovement(1m, entry.IncomingRate, SerialNo: entry.SerialNoId.Value.ToString()) };
        }

        return new[] { new LotMovement(qty, entry.IncomingRate, entry.BatchId) };
    }

    private static decimal? GetPurchaseReturnRate(LedgerEntryInput entry, ItemReplayContext context)
    {
        // Per ERPNext PR #67376 (commit 0c62d7399b): both FIFO and LIFO purchase returns value at receipt rate.
        if (context.ValuationMethod is not (ValuationMethod.FIFO or ValuationMethod.LIFO)
            || context.ValuesEverythingInPool
            || !IsReturnablePurchaseVoucher(entry.VoucherType))
        {
            return null;
        }

        return entry.PurchaseReturnRate;
    }

    public static bool IsReturnablePurchaseVoucher(string? voucherType)
    {
        if (string.IsNullOrWhiteSpace(voucherType)) return false;
        return voucherType.Replace(" ", string.Empty).ToUpperInvariant() is
            "PURCHASERECEIPT" or "PURCHASEINVOICE" or "SUBCONTRACTINGRECEIPT";
    }

    private static bool IsVoucherType(string? actual, string expectedNoSpaces) =>
        !string.IsNullOrWhiteSpace(actual)
        && string.Equals(actual.Replace(" ", string.Empty), expectedNoSpaces, StringComparison.OrdinalIgnoreCase);
}
