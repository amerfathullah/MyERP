using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Inventory.Entities;

/// <summary>
/// Item Lead Time — defines manufacturing and purchasing lead time profiles for an item.
/// Maps to ERPNext stock/doctype/item_lead_time.
/// </summary>
public class ItemLeadTime : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid ItemId { get; set; }

    /// <summary>
    /// Item code identifier kept synchronized with the linked Item's <c>ItemCode</c> when the Item is renamed
    /// per ERPNext commits f5b0a6992c and 5abab30259 (<c>rename_lead_time</c> in <c>item.py</c>).
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? ItemCode { get; set; }

    // Workstation & Shift configuration
    public int ShiftTimeInHours { get; set; } = ItemLeadTimeConsts.DefaultShiftTimeInHours;
    public int NoOfWorkstations { get; set; } = ItemLeadTimeConsts.DefaultNoOfWorkstations;
    public int NoOfShifts { get; set; } = ItemLeadTimeConsts.DefaultNoOfShifts;
    public int TotalWorkstationTime { get; set; }

    // Manufacturing time & yield
    public int ManufacturingTimeInMins { get; set; }
    public decimal DailyYield { get; set; } = ItemLeadTimeConsts.DefaultDailyYield;
    public int NoOfUnitsProduced { get; set; }
    public int CapacityPerDay { get; set; }

    // Purchase time
    public int PurchaseTimeDays { get; set; }
    public int BufferTimeDays { get; set; }

    private readonly List<ItemLeadTimeSupplier> _suppliers = new();
    public IReadOnlyList<ItemLeadTimeSupplier> Suppliers => _suppliers.AsReadOnly();

    protected ItemLeadTime() { }

    public ItemLeadTime(
        Guid id,
        Guid itemId,
        int shiftTimeInHours = ItemLeadTimeConsts.DefaultShiftTimeInHours,
        int noOfWorkstations = ItemLeadTimeConsts.DefaultNoOfWorkstations,
        int noOfShifts = ItemLeadTimeConsts.DefaultNoOfShifts,
        int manufacturingTimeInMins = 0,
        decimal dailyYield = ItemLeadTimeConsts.DefaultDailyYield,
        int purchaseTimeDays = 0,
        int bufferTimeDays = 0,
        Guid? tenantId = null)
        : base(id)
    {
        ItemId = itemId;
        ShiftTimeInHours = shiftTimeInHours;
        NoOfWorkstations = noOfWorkstations;
        NoOfShifts = noOfShifts;
        ManufacturingTimeInMins = manufacturingTimeInMins;
        DailyYield = dailyYield;
        PurchaseTimeDays = purchaseTimeDays;
        BufferTimeDays = bufferTimeDays;
        TenantId = tenantId;

        Recalculate();
    }

    public void Recalculate()
    {
        TotalWorkstationTime = ShiftTimeInHours * NoOfWorkstations * NoOfShifts;
        if (ManufacturingTimeInMins > 0)
        {
            NoOfUnitsProduced = (TotalWorkstationTime * 60) / ManufacturingTimeInMins;
            CapacityPerDay = (int)Math.Round((DailyYield * NoOfUnitsProduced) / 100m);
        }
        else
        {
            NoOfUnitsProduced = 0;
            CapacityPerDay = 0;
        }
    }

    public void AddSupplier(Guid supplierId, int purchaseTimeDays, int bufferTimeDays, bool isDefault = false)
    {
        if (_suppliers.Any(s => s.SupplierId == supplierId))
        {
            throw new BusinessException(MyERPDomainErrorCodes.DuplicateRecord)
                .WithData("entity", "Supplier Lead Time");
        }

        if (isDefault)
        {
            foreach (var sup in _suppliers)
            {
                sup.IsDefault = false;
            }
        }

        _suppliers.Add(new ItemLeadTimeSupplier(Guid.NewGuid(), Id, supplierId, purchaseTimeDays, bufferTimeDays, isDefault));
    }

    public void ClearSuppliers() => _suppliers.Clear();

    /// <summary>
    /// Computes fractional lead time in days per ERPNext PR #59007 / commit 2ad4a8c4a4.
    /// Retains fractional days without premature rounding.
    /// </summary>
    public double GetLeadTimeInDays(decimal qty = 1, bool isManufacture = true)
    {
        if (isManufacture)
        {
            if (qty <= 0) return 0;
            var timeInDays = Math.Max(ManufacturingTimeInMins, 0) * (double)qty / 1440.0;
            return timeInDays + BufferTimeDays;
        }
        else
        {
            return PurchaseTimeDays + BufferTimeDays;
        }
    }

    /// <summary>
    /// Computes lead time in days per ERPNext PR #59007 / commit 2ad4a8c4a4 and commit 3f7f8877c9
    /// (rounds fractional days to 4 decimal places before ceiling to prevent floating-point overshoot).
    /// </summary>
    public int CalculateLeadTimeDays(decimal qty = 1, bool isManufacture = true)
    {
        var days = Math.Round((decimal)GetLeadTimeInDays(qty, isManufacture), 4);
        return (int)Math.Ceiling(days);
    }

    /// <summary>
    /// Resolves purchase lead time in calendar days matching ERPNext plan_adapter.resolve_purchase_lead_time
    /// (commits e03a0ac44b and 1cae8b6982):
    /// 1. When a supplier is specified and present in Suppliers, use that supplier's PurchaseTimeDays + BufferTimeDays.
    /// 2. When a supplier is specified without a matching row, fall back to item-level PurchaseTimeDays + BufferTimeDays.
    /// 3. When no supplier is specified, use the default supplier row (IsDefault) if present, else item-level PurchaseTimeDays + BufferTimeDays.
    /// </summary>
    public int ResolvePurchaseLeadTimeDays(Guid? supplierId = null)
    {
        if (supplierId.HasValue && supplierId.Value != Guid.Empty)
        {
            var matched = _suppliers.FirstOrDefault(s => s.SupplierId == supplierId.Value);
            if (matched != null)
            {
                return matched.PurchaseTimeDays + matched.BufferTimeDays;
            }

            return PurchaseTimeDays + BufferTimeDays;
        }

        var defaultRow = _suppliers.FirstOrDefault(s => s.IsDefault);
        if (defaultRow != null)
        {
            return defaultRow.PurchaseTimeDays + defaultRow.BufferTimeDays;
        }

        return PurchaseTimeDays + BufferTimeDays;
    }

    /// <summary>
    /// Keeps <see cref="ItemLeadTime"/> named after its item when the item is renamed,
    /// finding the lead time record by the item's updated <c>ItemCode</c> (or <c>ItemId</c>) and renaming
    /// the lead time identifier to <paramref name="newItemCode"/> if it differs
    /// per ERPNext commits f5b0a6992c and 5abab30259 (<c>rename_lead_time</c> in <c>item.py</c>).
    /// </summary>
    public string SyncOnItemRename(string newItemCode)
    {
        Check.NotNullOrWhiteSpace(newItemCode, nameof(newItemCode));
        ItemCode = newItemCode;
        return ItemCode;
    }

    /// <summary>
    /// Resolves the renamed <c>Item Lead Time</c> document identifier by matching the lead time row whose
    /// <c>ItemCode</c> equals <paramref name="newItemCode"/> (after the item link was updated) and renaming
    /// it to <paramref name="newItemCode"/> when its current document name differs
    /// per ERPNext commits f5b0a6992c and 5abab30259.
    /// </summary>
    public static (bool ShouldRename, string? OldLeadTimeName, string? NewLeadTimeName) ResolveLeadTimeRenameByItemCode(
        IEnumerable<(string LeadTimeName, string ItemCode)>? leadTimeRecords,
        string newItemCode)
    {
        if (leadTimeRecords == null || string.IsNullOrWhiteSpace(newItemCode))
        {
            return (false, null, null);
        }

        var match = leadTimeRecords.FirstOrDefault(r =>
            string.Equals(r.ItemCode, newItemCode, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(match.LeadTimeName)
            && !string.Equals(match.LeadTimeName, newItemCode, StringComparison.Ordinal))
        {
            return (true, match.LeadTimeName, newItemCode);
        }

        return (false, null, null);
    }
}

public class ItemLeadTimeSupplier : FullAuditedEntity<Guid>
{
    public Guid ItemLeadTimeId { get; set; }
    public Guid SupplierId { get; set; }
    public int PurchaseTimeDays { get; set; }
    public int BufferTimeDays { get; set; }
    public bool IsDefault { get; set; }

    protected ItemLeadTimeSupplier() { }

    public ItemLeadTimeSupplier(Guid id, Guid itemLeadTimeId, Guid supplierId, int purchaseTimeDays, int bufferTimeDays, bool isDefault)
        : base(id)
    {
        ItemLeadTimeId = itemLeadTimeId;
        SupplierId = supplierId;
        PurchaseTimeDays = purchaseTimeDays;
        BufferTimeDays = bufferTimeDays;
        IsDefault = isDefault;
    }
}
