using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace MyERP.Inventory.Entities;

/// <summary>
/// A single barcode assigned to an Item. An item can carry several (unit barcode,
/// case/carton barcode, a legacy supplier barcode, etc). Maps to ERPNext's Item Barcode child table.
/// </summary>
public class ItemBarcode : Entity<Guid>
{
    public Guid ItemId { get; set; }

    public string Barcode { get; private set; } = null!;
    public BarcodeType BarcodeType { get; set; } = BarcodeType.Ean;

    /// <summary>Whether this is the barcode used by default when printing labels / scanning without a match hint.</summary>
    public bool IsDefault { get; set; }

    protected ItemBarcode() { }

    public ItemBarcode(Guid id, Guid itemId, string barcode, BarcodeType barcodeType = BarcodeType.Ean, bool isDefault = false)
        : base(id)
    {
        ItemId = itemId;
        SetBarcode(barcode);
        BarcodeType = barcodeType;
        IsDefault = isDefault;
    }

    public void SetBarcode(string barcode)
    {
        Barcode = Check.NotNullOrWhiteSpace(barcode, nameof(barcode), 100);
    }
}
