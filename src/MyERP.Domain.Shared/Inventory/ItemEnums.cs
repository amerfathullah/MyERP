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
