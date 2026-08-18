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

/// <summary>Basis on which withholding tax is deducted. Maps to ERPNext tax_deduction_basis.</summary>
public enum TaxDeductionBasis
{
    NetTotal = 0,
    GrossTotal = 1,
}
