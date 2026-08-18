using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Core;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Manufacturing.Entities;

/// <summary>
/// Sales Forecast — demand-planning master (item x warehouse x period) that feeds
/// <see cref="MasterProductionSchedule"/> via "Generate Demand" / "Create MPS". Maps to ERPNext
/// manufacturing/doctype/sales_forecast: a user picks items + a parent warehouse, then
/// GenerateDemand() projects one demand row per item per Weekly/Monthly period from FromDate.
/// </summary>
public class SalesForecast : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string ForecastNumber { get; set; } = null!;
    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;
    public SalesForecastStatus ForecastStatus { get; private set; } = SalesForecastStatus.Planned;

    public DateTime PostingDate { get; set; }
    public DateTime FromDate { get; set; }
    public SalesForecastFrequency Frequency { get; set; } = SalesForecastFrequency.Monthly;

    /// <summary>Number of weeks/months of demand to project forward from FromDate.</summary>
    public int DemandNumber { get; set; } = 6;

    /// <summary>Demand is aggregated across all child warehouses under this one.</summary>
    public Guid ParentWarehouseId { get; set; }

    private readonly List<SalesForecastSelectedItem> _selectedItems = new();
    public IReadOnlyList<SalesForecastSelectedItem> SelectedItems => _selectedItems.AsReadOnly();

    private readonly List<SalesForecastItem> _items = new();
    public IReadOnlyList<SalesForecastItem> Items => _items.AsReadOnly();

    protected SalesForecast() { }

    public SalesForecast(Guid id, Guid companyId, string forecastNumber, DateTime fromDate,
        Guid parentWarehouseId, Guid? tenantId = null) : base(id)
    {
        CompanyId = companyId;
        ForecastNumber = Check.NotNullOrWhiteSpace(forecastNumber, nameof(forecastNumber), 50);
        FromDate = fromDate;
        ParentWarehouseId = parentWarehouseId;
        PostingDate = DateTime.UtcNow.Date;
        TenantId = tenantId;
    }

    public void SetSelectedItems(IEnumerable<Guid> itemIds)
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        _selectedItems.Clear();
        foreach (var itemId in itemIds.Distinct())
            _selectedItems.Add(new SalesForecastSelectedItem(Guid.NewGuid(), Id, itemId));
    }

    /// <summary>
    /// Mirrors generate_manual_demand: one row per selected item per period, default qty 1.
    /// Delivery dates step forward from FromDate by week (Weekly) or month (Monthly).
    /// Caller resolves item name/UOM (domain layer has no Item repository access).
    /// </summary>
    public void GenerateDemand(IReadOnlyDictionary<Guid, (string ItemName, string Uom)> itemDetails)
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        if (_selectedItems.Count == 0)
            throw new BusinessException(MyERPDomainErrorCodes.SalesForecastHasNoSelectedItems);

        _items.Clear();
        foreach (var selected in _selectedItems)
        {
            var (itemName, uom) = itemDetails[selected.ItemId];
            for (var period = 1; period <= DemandNumber; period++)
            {
                var deliveryDate = Frequency == SalesForecastFrequency.Monthly
                    ? FromDate.AddMonths(period)
                    : FromDate.AddDays(period * 7);

                _items.Add(new SalesForecastItem(Guid.NewGuid(), Id, selected.ItemId, itemName, uom, deliveryDate, 1.0m));
            }
        }
    }

    /// <summary>Called after a Master Production Schedule is created from this forecast (create_mps).</summary>
    public void MarkMpsGenerated()
    {
        if (ForecastStatus == SalesForecastStatus.MpsGenerated)
            throw new BusinessException(MyERPDomainErrorCodes.SalesForecastAlreadyUsedForMps);
        ForecastStatus = SalesForecastStatus.MpsGenerated;
    }

    public void Submit()
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Submitted;
    }

    /// <summary>Cancel or discard — both set ForecastStatus to Cancelled per ERPNext on_discard.</summary>
    public void Cancel()
    {
        if (Status == DocumentStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Cancelled;
        ForecastStatus = SalesForecastStatus.Cancelled;
    }
}

/// <summary>User-selected item scoped into this forecast (ERPNext Table MultiSelect selected_items).</summary>
public class SalesForecastSelectedItem : FullAuditedEntity<Guid>
{
    public Guid SalesForecastId { get; set; }
    public Guid ItemId { get; set; }

    protected SalesForecastSelectedItem() { }

    public SalesForecastSelectedItem(Guid id, Guid salesForecastId, Guid itemId) : base(id)
    {
        SalesForecastId = salesForecastId;
        ItemId = itemId;
    }
}

/// <summary>One projected demand row: item x delivery date x qty. Generated by GenerateDemand().</summary>
public class SalesForecastItem : FullAuditedEntity<Guid>
{
    public Guid SalesForecastId { get; set; }
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = null!;
    public string Uom { get; set; } = "Unit";
    public Guid? WarehouseId { get; set; }
    public DateTime DeliveryDate { get; set; }
    public decimal DemandQty { get; set; }

    protected SalesForecastItem() { }

    public SalesForecastItem(Guid id, Guid salesForecastId, Guid itemId, string itemName, string uom,
        DateTime deliveryDate, decimal demandQty) : base(id)
    {
        SalesForecastId = salesForecastId;
        ItemId = itemId;
        ItemName = itemName;
        Uom = uom;
        DeliveryDate = deliveryDate;
        DemandQty = demandQty;
    }
}
