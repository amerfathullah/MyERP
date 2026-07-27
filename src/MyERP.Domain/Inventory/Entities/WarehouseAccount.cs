using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Inventory.Entities;

/// <summary>
/// Maps a Warehouse to its GL accounts for perpetual inventory.
/// Per ERPNext: each warehouse (leaf) can have a specific GL account for stock balance.
/// When perpetual inventory is enabled, every stock movement creates a corresponding GL entry.
/// The WarehouseAccount entity defines WHICH GL accounts are used per warehouse.
///
/// Resolution chain per ERPNext:
///   1. WarehouseAccount for specific warehouse (this entity)
///   2. Company.DefaultInventoryAccountId (fallback)
///   3. Error if neither configured
/// </summary>
public class WarehouseAccount : AuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid CompanyId { get; set; }

    /// <summary>
    /// GL account for stock balance (Asset type — typically "Stock In Hand" or "Inventory").
    /// Stock-in SLE → DR this account; Stock-out SLE → CR this account.
    /// </summary>
    public Guid AccountId { get; set; }

    /// <summary>
    /// Optional: separate account for stock received but not billed (SRBNB/GRNI).
    /// Used on Purchase Receipt → DR Stock, CR SRBNB.
    /// Falls back to Company.StockReceivedButNotBilledAccountId if null.
    /// </summary>
    public Guid? StockReceivedButNotBilledAccountId { get; set; }

    /// <summary>
    /// Optional: separate account for stock delivered but not billed (SDBNB).
    /// Used on Delivery Note → DR SDBNB, CR Stock (when SI not yet created).
    /// Falls back to Company.StockDeliveredButNotBilledAccountId if null.
    /// </summary>
    public Guid? StockDeliveredButNotBilledAccountId { get; set; }

    /// <summary>
    /// Optional: stock adjustment account for reconciliation differences.
    /// Falls back to Company.StockAdjustmentAccountId if null.
    /// </summary>
    public Guid? StockAdjustmentAccountId { get; set; }

    protected WarehouseAccount() { }

    public WarehouseAccount(Guid id, Guid warehouseId, Guid companyId, Guid accountId, Guid? tenantId = null)
        : base(id)
    {
        WarehouseId = warehouseId;
        CompanyId = companyId;
        AccountId = accountId;
        TenantId = tenantId;
    }
}
