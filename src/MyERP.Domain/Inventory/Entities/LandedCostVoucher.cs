using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Core;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Inventory.Entities;

/// <summary>
/// Landed Cost Voucher — distributes additional costs (freight, customs, insurance)
/// to purchase receipt items to adjust valuation. Supports qty-based, amount-based,
/// or manual distribution.
/// Maps to ERPNext stock/doctype/landed_cost_voucher.
/// </summary>
public class LandedCostVoucher : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string? VoucherNumber { get; set; }
    public DateTime PostingDate { get; set; }
    public LandedCostDistributionMethod DistributionMethod { get; set; } = LandedCostDistributionMethod.BasedOnAmount;
    public string? Notes { get; set; }

    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;

    public decimal TotalCharges => _charges.Sum(c => c.Amount);
    public decimal TotalDistributedAmount => _items.Sum(i => i.ApplicableCharges);

    private readonly List<LandedCostItem> _items = new();
    public IReadOnlyList<LandedCostItem> Items => _items.AsReadOnly();

    private readonly List<LandedCostCharge> _charges = new();
    public IReadOnlyList<LandedCostCharge> Charges => _charges.AsReadOnly();

    protected LandedCostVoucher() { }

    public LandedCostVoucher(Guid id, Guid companyId, DateTime postingDate, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        PostingDate = postingDate;
        TenantId = tenantId;
    }

    public void AddItem(Guid receiptId, string receiptType, Guid itemId,
        decimal quantity, decimal amount, string? description = null)
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        _items.Add(new LandedCostItem(Guid.NewGuid(), Id, receiptId, receiptType,
            itemId, quantity, amount, description));
    }

    public void AddCharge(string description, Guid expenseAccountId, decimal amount,
        Guid? costCenterId = null, Guid? projectId = null)
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        if (amount <= 0)
            throw new ArgumentException("Charge amount must be positive.", nameof(amount));

        _charges.Add(new LandedCostCharge(Guid.NewGuid(), Id, description, expenseAccountId, amount)
        {
            CostCenterId = costCenterId,
            ProjectId = projectId,
        });
    }

    /// <summary>
    /// Distributes total charges across receipt items based on the configured method.
    /// </summary>
    public void DistributeCharges()
    {
        if (!_items.Any() || !_charges.Any()) return;

        var totalChargeAmount = TotalCharges;

        switch (DistributionMethod)
        {
            case LandedCostDistributionMethod.BasedOnQuantity:
                var totalQty = Math.Round(_items.Sum(i => i.Quantity), 4);
                if (totalQty == 0)
                {
                    throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                        .WithData("detail", "Total Quantity of all items is zero. Set 'Distribute Charges Based On' to Amount.");
                }
                foreach (var item in _items)
                    item.ApplicableCharges = Math.Round(totalChargeAmount * item.Quantity / totalQty, 2);
                break;

            case LandedCostDistributionMethod.BasedOnAmount:
                var totalAmt = Math.Round(_items.Sum(i => i.Amount), 2);
                if (totalAmt == 0)
                {
                    throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                        .WithData("detail", "Total Amount of all items is zero. Set 'Distribute Charges Based On' to Qty.");
                }
                foreach (var item in _items)
                    item.ApplicableCharges = Math.Round(totalChargeAmount * item.Amount / totalAmt, 2);
                break;

            case LandedCostDistributionMethod.Manual:
                // Manual: ApplicableCharges already set by user
                return;
        }

        // Error diffusion: absorb rounding remainder only across rows whose basis has the same sign
        // as the basis total (ERPNext PR #61434 / commit 0dbddd4c10)
        var diff = Math.Round(totalChargeAmount - _items.Sum(i => i.ApplicableCharges), 2);
        if (diff != 0 && _items.Any())
        {
            decimal basisSign = DistributionMethod == LandedCostDistributionMethod.BasedOnQuantity
                ? (_items.Sum(i => i.Quantity) < 0 ? -1m : 1m)
                : (_items.Sum(i => i.Amount) < 0 ? -1m : 1m);

            var rowsToAdjust = _items
                .Where(i => (DistributionMethod == LandedCostDistributionMethod.BasedOnQuantity ? i.Quantity : i.Amount) * basisSign > 0)
                .ToList();

            if (rowsToAdjust.Count > 0)
            {
                var step = 0.01m * (diff < 0 ? -1m : 1m);
                var steps = (int)Math.Round(Math.Abs(diff) * 100m);
                for (var i = 0; i < steps; i++)
                {
                    var row = rowsToAdjust[(rowsToAdjust.Count - 1) - (i % rowsToAdjust.Count)];
                    row.ApplicableCharges = Math.Round(row.ApplicableCharges + step, 2);
                }
            }
        }
    }

    /// <summary>
    /// Validates that each item's ApplicableCharges sign matches the expected sign from the total charges
    /// and row basis (ERPNext PR #61434 / commit 0dbddd4c10).
    /// </summary>
    public void ValidateChargesFollowTotalSign()
    {
        if (!_items.Any() || !_charges.Any()) return;

        var totalSign = TotalCharges < 0 ? -1m : 1m;
        var basisTotal = DistributionMethod == LandedCostDistributionMethod.BasedOnQuantity
            ? _items.Sum(i => i.Quantity)
            : _items.Sum(i => i.Amount);
        var basisSign = basisTotal < 0 ? -1m : 1m;

        for (var idx = 0; idx < _items.Count; idx++)
        {
            var item = _items[idx];
            if (item.ApplicableCharges == 0m) continue;

            decimal expectedSign;
            if (DistributionMethod == LandedCostDistributionMethod.Manual)
            {
                expectedSign = totalSign;
            }
            else
            {
                var rowBasis = DistributionMethod == LandedCostDistributionMethod.BasedOnQuantity
                    ? item.Quantity
                    : item.Amount;
                if (rowBasis == 0m)
                {
                    var basisLabel = DistributionMethod == LandedCostDistributionMethod.BasedOnQuantity ? "Quantity" : "Amount";
                    throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                        .WithData("detail", $"Row #{idx + 1}: Cannot allocate landed cost when {basisLabel} is zero.");
                }
                expectedSign = (rowBasis * basisSign > 0) ? totalSign : -totalSign;
            }

            if (item.ApplicableCharges * expectedSign < 0)
            {
                var signLabel = expectedSign < 0 ? "negative" : "positive";
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Row #{idx + 1}: Applicable Charges must be {signLabel} ({item.ApplicableCharges:N2}).");
            }
        }
    }

    public void Submit()
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        if (!_items.Any())
            throw new BusinessException(MyERPDomainErrorCodes.LandedCostHasNoItems);
        if (!_charges.Any())
            throw new BusinessException(MyERPDomainErrorCodes.LandedCostHasNoCharges);

        DistributeCharges();
        ValidateChargesFollowTotalSign();

        // Validate total distributed matches total charges (within tolerance)
        var tolerance = 2.0m / (decimal)Math.Pow(10, 2); // currency precision = 2
        if (Math.Abs(TotalDistributedAmount - TotalCharges) > tolerance)
            throw new BusinessException(MyERPDomainErrorCodes.LandedCostDistributionMismatch);

        Status = DocumentStatus.Submitted;
    }

    public void Cancel()
    {
        if (Status != DocumentStatus.Submitted)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Cancelled;
    }
}
