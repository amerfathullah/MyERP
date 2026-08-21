using System.Collections.Generic;

namespace MyERP.Accounting.DomainServices;

/// <summary>
/// Voucher types the Repost Accounting Ledger tracked-batch tool's number-based voucher picker
/// (<c>RepostAccountingLedgerAppService.ResolveVoucherAsync</c>) knows how to look up. This is a
/// SUBSET of the broader <see cref="GlRepostService.AllowedVoucherTypes"/> — the underlying GL-rebuild
/// mechanism (<see cref="GlRepostService.RepostForVoucherAsync"/>) also supports PurchaseReceipt and
/// DeliveryNote, but this tool's Angular UI only offers what it can resolve "SI-00001" -&gt; Guid for.
///
/// Extend by adding a repository + number-field lookup case to
/// <c>RepostAccountingLedgerAppService.ResolveVoucherAsync</c>/<c>ResolveVoucherByIdAsync</c> for the
/// new type, then add it here — bounded by <see cref="GlRepostService.AllowedVoucherTypes"/>, never
/// beyond it.
/// </summary>
public static class RepostAllowedVoucherTypes
{
    public static readonly IReadOnlySet<string> Values = new HashSet<string>
    {
        "SalesInvoice",
        "PurchaseInvoice",
        "StockEntry",
    };

    public static bool IsAllowed(string voucherType) => Values.Contains(voucherType);
}
