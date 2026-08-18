namespace MyERP.Inventory;

public enum ItemType
{
    Goods = 0,
    Service = 1,
    FixedAsset = 2
}

public enum ValuationMethod
{
    FIFO = 0,
    WeightedAverage = 1,
    LIFO = 2,
    StandardCost = 3
}

/// <summary>Barcode symbology, for print format / scanner compatibility. Per ERPNext Item Barcode.barcode_type.</summary>
public enum BarcodeType
{
    Ean = 0,
    Upca = 1,
    Code128 = 2,
    Other = 3
}
