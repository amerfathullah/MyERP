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
