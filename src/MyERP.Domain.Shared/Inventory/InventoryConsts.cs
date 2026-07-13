namespace MyERP.Inventory;

public static class ItemConsts
{
    public const int MaxNameLength = 256;
    public const int MaxCodeLength = 50;
    public const int MaxBarcodeLength = 50;
    public const int MaxUomLength = 20;
    public const int MaxDescriptionLength = 2000;
    public const int MaxGroupLength = 100;
    public const int MaxBrandLength = 100;
}

public static class WarehouseConsts
{
    public const int MaxNameLength = 256;
    public const int MaxCodeLength = 20;
    public const int MaxAddressLength = 500;
    public const int MaxCityLength = 100;
    public const int MaxStateLength = 100;
    public const int MaxPostalCodeLength = 10;
    public const int MaxCountryLength = 100;
}

public static class PriceListConsts
{
    public const int MaxNameLength = 200;
    public const int MaxCurrencyCodeLength = 10;
}

public static class ItemPriceConsts
{
    public const int MaxUomLength = 20;
    public const int MaxCurrencyCodeLength = 10;
    public const int MaxBatchNoLength = 50;
}

public static class BatchConsts
{
    public const int MaxBatchNoLength = 100;
    public const int MaxSupplierBatchNoLength = 100;
    public const int MaxDescriptionLength = 500;
    public const int MaxRefDocTypeLength = 50;
}
