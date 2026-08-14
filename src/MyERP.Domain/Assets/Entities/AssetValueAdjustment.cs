using System;
using MyERP.Core;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Assets.Entities;

/// <summary>
/// Asset Value Adjustment — adjusts book value of an asset (revaluation or impairment).
/// Posts GL journal entry between Fixed Asset Account and Difference Account.
/// Recalculates remaining depreciation schedule.
/// Maps to ERPNext assets/doctype/asset_value_adjustment.
/// </summary>
public class AssetValueAdjustment : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public string AdjustmentNumber { get; set; } = null!;
    public Guid CompanyId { get; set; }
    public Guid AssetId { get; set; }
    public Guid? FinanceBookId { get; set; }

    public DateTime Date { get; set; }
    public decimal CurrentAssetValue { get; set; }
    public decimal NewAssetValue { get; set; }
    public decimal DifferenceAmount { get; private set; }

    public Guid DifferenceAccountId { get; set; }
    public Guid? CostCenterId { get; set; }
    public Guid? JournalEntryId { get; set; }
    public string? Notes { get; set; }

    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;

    protected AssetValueAdjustment() { }

    public AssetValueAdjustment(
        Guid id,
        string adjustmentNumber,
        Guid companyId,
        Guid assetId,
        DateTime date,
        decimal currentAssetValue,
        decimal newAssetValue,
        Guid differenceAccountId,
        Guid? financeBookId = null,
        Guid? costCenterId = null,
        Guid? tenantId = null)
        : base(id)
    {
        AdjustmentNumber = adjustmentNumber;
        CompanyId = companyId;
        AssetId = assetId;
        Date = date;
        CurrentAssetValue = currentAssetValue;
        NewAssetValue = newAssetValue;
        DifferenceAmount = newAssetValue - currentAssetValue;
        DifferenceAccountId = differenceAccountId;
        FinanceBookId = financeBookId;
        CostCenterId = costCenterId;
        TenantId = tenantId;
    }

    public void UpdateValues(decimal currentAssetValue, decimal newAssetValue)
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        CurrentAssetValue = currentAssetValue;
        NewAssetValue = newAssetValue;
        DifferenceAmount = newAssetValue - currentAssetValue;
    }

    public void Submit()
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        DifferenceAmount = NewAssetValue - CurrentAssetValue;
        Status = DocumentStatus.Submitted;
    }

    public void Cancel()
    {
        if (Status != DocumentStatus.Submitted)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        Status = DocumentStatus.Cancelled;
    }
}
