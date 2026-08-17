namespace MyERP.Sales;

/// <summary>Party-type restriction for a Promotional Scheme's applicability. Maps to ERPNext Promotional Scheme.applicable_for.</summary>
public enum PromotionalSchemeApplicableFor
{
    None = 0,
    Customer = 1,
    CustomerGroup = 2,
    Territory = 3,
    SalesPartner = 4,
    Campaign = 5,
    Supplier = 6,
    SupplierGroup = 7
}
