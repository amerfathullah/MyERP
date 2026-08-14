namespace MyERP.Tax;

public enum TaxType
{
    Sales = 0,
    Service = 1,
    Exempt = 2,
    ZeroRated = 3,
    OutOfScope = 4
}

/// <summary>Selling or Buying template type.</summary>
public enum TaxTemplateType
{
    Selling = 0,
    Buying = 1,
}
