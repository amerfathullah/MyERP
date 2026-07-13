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

    public void AddCharge(string description, Guid expenseAccountId, decimal amount)
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        if (amount <= 0)
            throw new ArgumentException("Charge amount must be positive.", nameof(amount));

        _charges.Add(new LandedCostCharge(Guid.NewGuid(), Id, description, expenseAccountId, amount));
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
                var totalQty = _items.Sum(i => i.Quantity);
                if (totalQty == 0) return;
                foreach (var item in _items)
                    item.ApplicableCharges = Math.Round(totalChargeAmount * item.Quantity / totalQty, 2);
                break;

            case LandedCostDistributionMethod.BasedOnAmount:
                var totalAmt = _items.Sum(i => i.Amount);
                if (totalAmt == 0) return;
                foreach (var item in _items)
                    item.ApplicableCharges = Math.Round(totalChargeAmount * item.Amount / totalAmt, 2);
                break;

            case LandedCostDistributionMethod.Manual:
                // Manual: ApplicableCharges already set by user
                return;
        }

        // Error diffusion: ensure distributed total matches charges exactly
        var diff = totalChargeAmount - _items.Sum(i => i.ApplicableCharges);
        if (diff != 0 && _items.Any())
            _items.Last().ApplicableCharges += diff;
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
