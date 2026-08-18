using System;
using System.Collections.Generic;
using MyERP.Core;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Assets.Entities;

/// <summary>
/// Asset Shift Allocation — reassigns shift factors to an asset's unbooked depreciation periods,
/// accelerating or decelerating depreciation for multi-shift operations.
/// Maps to ERPNext assets/doctype/asset_shift_allocation.
/// A period already booked (journaled) can never have its shift changed.
/// </summary>
public class AssetShiftAllocation : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public string AllocationNumber { get; set; } = null!;
    public Guid AssetId { get; set; }
    public Guid? FinanceBookId { get; set; }
    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;

    public List<AssetShiftAllocationLine> Lines { get; private set; } = new();

    protected AssetShiftAllocation() { }

    public AssetShiftAllocation(Guid id, string allocationNumber, Guid assetId, Guid? financeBookId = null, Guid? tenantId = null)
        : base(id)
    {
        AllocationNumber = Check.NotNullOrWhiteSpace(allocationNumber, nameof(allocationNumber), AssetShiftAllocationConsts.MaxAllocationNumberLength);
        AssetId = assetId;
        FinanceBookId = financeBookId;
        TenantId = tenantId;
    }

    public AssetShiftAllocationLine AssignShift(Guid id, Guid scheduleEntryId, Guid shiftFactorId, DateTime scheduleDate)
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        var line = Lines.Find(l => l.ScheduleEntryId == scheduleEntryId);
        if (line != null)
        {
            line.ShiftFactorId = shiftFactorId;
            return line;
        }

        line = new AssetShiftAllocationLine(id, Id, scheduleEntryId, shiftFactorId, scheduleDate, TenantId);
        Lines.Add(line);
        return line;
    }

    /// <summary>Records the computed result (amount/accumulated) for a line after reallocation.</summary>
    public void SetLineResult(Guid scheduleEntryId, decimal depreciationAmount, decimal accumulatedDepreciation)
    {
        var line = Lines.Find(l => l.ScheduleEntryId == scheduleEntryId);
        if (line == null) return;
        line.DepreciationAmount = depreciationAmount;
        line.AccumulatedDepreciation = accumulatedDepreciation;
    }

    public void Submit()
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        if (Lines.Count == 0)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition).WithData("reason", "At least one shift assignment is required.");
        Status = DocumentStatus.Submitted;
    }

    public void Cancel()
    {
        if (Status != DocumentStatus.Submitted)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Cancelled;
    }
}
