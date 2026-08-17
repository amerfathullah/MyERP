using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Manufacturing;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Manufacturing.Entities;

/// <summary>
/// BOM Creator — a staging tree for building a multi-level Bill of Materials in one pass.
/// Rows reference their parent item via <see cref="BomCreatorItem.FgItemId"/> (the item they are a
/// component of, either the top-level finished good or another expandable row's item). Running
/// <see cref="MyERP.Manufacturing.DomainServices.BomCreatorService.CreateBomsAsync"/> walks the tree
/// bottom-up and creates one Bill of Materials per expandable item, linking sub-assemblies via
/// BomItem.SubBomId.
/// Maps to ERPNext manufacturing/doctype/bom_creator.
/// </summary>
public class BomCreator : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public Guid FinishedGoodItemId { get; set; }
    public decimal Qty { get; set; } = 1;
    public string? Uom { get; set; }

    public bool IsPhantom { get; set; }
    public Guid? RoutingId { get; set; }
    public Guid? DefaultWarehouseId { get; set; }

    /// <summary>Valuation Rate, Last Purchase Rate, or Price List.</summary>
    public string RmCostAsPer { get; set; } = "Valuation Rate";
    public decimal RawMaterialCost { get; set; }
    public string? Remarks { get; set; }

    public BomCreatorStatus Status { get; private set; } = BomCreatorStatus.Draft;
    public string? ErrorLog { get; private set; }

    private readonly List<BomCreatorItem> _items = new();
    public IReadOnlyList<BomCreatorItem> Items => _items.AsReadOnly();

    protected BomCreator() { }

    public BomCreator(Guid id, Guid companyId, Guid finishedGoodItemId, decimal qty, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        FinishedGoodItemId = finishedGoodItemId;
        Qty = qty;
        TenantId = tenantId;
    }

    public void Validate()
    {
        if (!_items.Any())
            throw new BusinessException(MyERPDomainErrorCodes.BomCreatorRequiresItems);
        if (Status != BomCreatorStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.BomCreatorAlreadyProcessed);
    }

    public BomCreatorItem AddItem(Guid itemId, string itemName, Guid fgItemId, decimal qty, decimal rate,
        bool isExpandable = false, string? uom = null, decimal conversionFactor = 1m, string stockUom = "Unit")
    {
        var item = new BomCreatorItem(Guid.NewGuid(), Id, itemId, itemName, fgItemId, qty, rate,
            isExpandable, uom, conversionFactor, stockUom);
        _items.Add(item);
        return item;
    }

    public void ClearItems() => _items.Clear();

    public void RecalculateCost()
    {
        RawMaterialCost = _items.Where(i => i.FgItemId == FinishedGoodItemId).Sum(i => i.Amount);
    }

    public void MarkCompleted()
    {
        Status = BomCreatorStatus.Completed;
        ErrorLog = null;
    }

    public void MarkFailed(string errorMessage)
    {
        Status = BomCreatorStatus.Failed;
        ErrorLog = errorMessage;
    }
}

/// <summary>
/// A single row of a BOM Creator's staging tree. <see cref="FgItemId"/> points at the item this
/// row is a component of (top-level finished good, or another row's ItemId when nested).
/// </summary>
public class BomCreatorItem : Entity<Guid>
{
    public Guid BomCreatorId { get; set; }

    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = null!;

    /// <summary>The item (top-level FG or another row's ItemId) this row is a component of.</summary>
    public Guid FgItemId { get; set; }

    /// <summary>When true, this item gets its own generated BOM (a sub-assembly) instead of being a plain raw material.</summary>
    public bool IsExpandable { get; set; }

    public decimal Qty { get; set; } = 1;
    public decimal Rate { get; set; }
    public decimal Amount => Qty * Rate;
    public string? Uom { get; set; }
    public decimal ConversionFactor { get; set; } = 1m;
    public string StockUom { get; set; } = "Unit";

    public Guid? OperationId { get; set; }
    public bool IsSubcontracted { get; set; }
    public bool IsPhantomItem { get; set; }
    public bool SourcedBySupplier { get; set; }
    public string? Instruction { get; set; }

    /// <summary>Set once this row's BOM has been generated (only meaningful when IsExpandable).</summary>
    public bool BomCreated { get; set; }

    protected BomCreatorItem() { }

    public BomCreatorItem(Guid id, Guid bomCreatorId, Guid itemId, string itemName, Guid fgItemId,
        decimal qty, decimal rate, bool isExpandable, string? uom, decimal conversionFactor, string stockUom) : base(id)
    {
        BomCreatorId = bomCreatorId;
        ItemId = itemId;
        ItemName = itemName;
        FgItemId = fgItemId;
        Qty = qty;
        Rate = rate;
        IsExpandable = isExpandable;
        Uom = uom;
        ConversionFactor = conversionFactor;
        StockUom = stockUom;
    }

    public override object[] GetKeys() => new object[] { Id };
}
