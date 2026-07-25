namespace MyERP.Manufacturing;

/// <summary>
/// Types of secondary items produced alongside the main finished good.
/// Per ERPNext v16: BOM Scrap Item renamed to BOM Secondary Item with type classification.
/// Gotcha #85: "BOM Scrap Item renamed to Secondary Item in v16"
/// Gotcha #175: "SE Detail secondary_item_type has 4 options, BOM only has 3"
/// </summary>
public enum SecondaryItemType
{
    /// <summary>Co-Product — joint product with significant value, shares FG cost allocation.</summary>
    CoProduct = 0,

    /// <summary>By-Product — incidental output with lower value.</summary>
    ByProduct = 1,

    /// <summary>Scrap — waste material, may have salvage value.</summary>
    Scrap = 2,

    /// <summary>
    /// Additional Finished Good — Stock Entry only (not BOM level).
    /// Per gotcha #175: visible only for Manufacture purpose, target warehouse, non-FG items.
    /// </summary>
    AdditionalFinishedGood = 3,
}
