namespace MyERP.Accounting;

/// <summary>
/// Determines which account field from the source document to use.
/// </summary>
public enum AccountSource
{
    /// <summary>Use a fixed account specified in the rule.</summary>
    FixedAccount = 0,

    /// <summary>Use the default receivable account from the customer.</summary>
    CustomerReceivable = 1,

    /// <summary>Use the default payable account from the supplier.</summary>
    SupplierPayable = 2,

    /// <summary>Use the income account from the item.</summary>
    ItemIncome = 3,

    /// <summary>Use the expense account from the item.</summary>
    ItemExpense = 4,

    /// <summary>Use a tax payable account.</summary>
    TaxPayable = 5,

    /// <summary>
    /// Use the stock account from WarehouseAccountService resolution chain.
    /// Per ERPNext: warehouse-specific → parent warehouse → company default.
    /// Used for perpetual inventory GL entries on stock documents (PR/DN/SE).
    /// </summary>
    WarehouseStock = 6
}

/// <summary>
/// Determines which amount field from the source document to use.
/// </summary>
public enum AmountSource
{
    /// <summary>Total document amount (net of tax).</summary>
    NetTotal = 0,

    /// <summary>Grand total including tax.</summary>
    GrandTotal = 1,

    /// <summary>Tax amount only.</summary>
    TaxAmount = 2,

    /// <summary>Line item amount.</summary>
    LineAmount = 3,

    /// <summary>
    /// Total stock cost (valuation-based) for inventory documents.
    /// Used for COGS GL entries on Delivery Notes (stock cost, NOT selling price).
    /// Per ERPNext: DN GL uses stock_value_difference from SLEs, not invoice amounts.
    /// </summary>
    StockCostTotal = 4,

    /// <summary>
    /// Total purchase expense after deducting landed cost voucher amounts.
    /// Per ERPNext PR #57475: prevents double counting.
    /// </summary>
    PurchaseExpenseTotal = 5
}
