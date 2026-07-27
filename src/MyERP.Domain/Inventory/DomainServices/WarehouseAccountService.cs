using System;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Inventory.DomainServices;

/// <summary>
/// Resolves the GL account for a given warehouse for perpetual inventory GL posting.
/// 
/// Per ERPNext stock/utils.py → get_warehouse_account():
///   Resolution chain:
///   1. WarehouseAccount entity for the specific (warehouse, company)
///   2. Parent warehouse's WarehouseAccount (traverse tree up)
///   3. Company.DefaultInventoryAccountId (fallback)
///   4. Error if all null
///
/// Also resolves SRBNB and SDBNB accounts with same fallback pattern.
/// 
/// Per gotcha #15: Warehouse→Account 5-level resolution chain.
/// Per gotcha #2864: BaseStockGLComposer uses this for DR Stock account on PR, CR Stock on DN.
/// </summary>
public class WarehouseAccountService : DomainService
{
    private readonly IRepository<WarehouseAccount, Guid> _warehouseAccountRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;
    private readonly IRepository<Company, Guid> _companyRepository;

    public WarehouseAccountService(
        IRepository<WarehouseAccount, Guid> warehouseAccountRepository,
        IRepository<Warehouse, Guid> warehouseRepository,
        IRepository<Company, Guid> companyRepository)
    {
        _warehouseAccountRepository = warehouseAccountRepository;
        _warehouseRepository = warehouseRepository;
        _companyRepository = companyRepository;
    }

    /// <summary>
    /// Resolves the stock GL account for a warehouse.
    /// Per ERPNext: used for DR on stock-in (PR/Manufacture/Receipt) and CR on stock-out (DN/Issue).
    /// </summary>
    public async Task<Guid> ResolveStockAccountAsync(Guid warehouseId, Guid companyId)
    {
        // Level 1: Direct warehouse account mapping
        var warehouseAccount = await _warehouseAccountRepository.FindAsync(
            wa => wa.WarehouseId == warehouseId && wa.CompanyId == companyId);

        if (warehouseAccount != null)
            return warehouseAccount.AccountId;

        // Level 2: Check warehouse's own DefaultAccountId
        var warehouse = await _warehouseRepository.GetAsync(warehouseId);
        if (warehouse.DefaultAccountId.HasValue)
            return warehouse.DefaultAccountId.Value;

        // Level 3: Walk up parent warehouse hierarchy (max 10 levels)
        var currentWarehouseId = warehouse.ParentWarehouseId;
        for (int depth = 0; depth < 10 && currentWarehouseId.HasValue; depth++)
        {
            var parentAccount = await _warehouseAccountRepository.FindAsync(
                wa => wa.WarehouseId == currentWarehouseId.Value && wa.CompanyId == companyId);
            if (parentAccount != null)
                return parentAccount.AccountId;

            var parentWarehouse = await _warehouseRepository.FindAsync(currentWarehouseId.Value);
            if (parentWarehouse?.DefaultAccountId.HasValue == true)
                return parentWarehouse.DefaultAccountId.Value;

            currentWarehouseId = parentWarehouse?.ParentWarehouseId;
        }

        // Level 4: Company default
        var company = await _companyRepository.GetAsync(companyId);
        if (company.DefaultInventoryAccountId.HasValue)
            return company.DefaultInventoryAccountId.Value;

        // Level 5: Error — no account configured
        throw new BusinessException(MyERPDomainErrorCodes.AccountIsGroup)
            .WithData("message", $"No stock account configured for warehouse '{warehouse.Name}' or company defaults.");
    }

    /// <summary>
    /// Resolves the SRBNB (Stock Received But Not Billed) account for purchase receipts.
    /// Per ERPNext: DR Stock, CR SRBNB on Purchase Receipt submit.
    /// </summary>
    public async Task<Guid> ResolveSrbnbAccountAsync(Guid warehouseId, Guid companyId)
    {
        var warehouseAccount = await _warehouseAccountRepository.FindAsync(
            wa => wa.WarehouseId == warehouseId && wa.CompanyId == companyId);

        if (warehouseAccount?.StockReceivedButNotBilledAccountId.HasValue == true)
            return warehouseAccount.StockReceivedButNotBilledAccountId.Value;

        var company = await _companyRepository.GetAsync(companyId);
        return company.StockReceivedButNotBilledAccountId
            ?? throw new BusinessException(MyERPDomainErrorCodes.AccountIsGroup)
                .WithData("message", "No SRBNB account configured.");
    }

    /// <summary>
    /// Resolves the SDBNB (Stock Delivered But Not Billed) account for delivery notes.
    /// Per ERPNext gotcha #2864: DN 4-branch SDBNB logic.
    /// </summary>
    public async Task<Guid> ResolveSdbnbAccountAsync(Guid warehouseId, Guid companyId)
    {
        var warehouseAccount = await _warehouseAccountRepository.FindAsync(
            wa => wa.WarehouseId == warehouseId && wa.CompanyId == companyId);

        if (warehouseAccount?.StockDeliveredButNotBilledAccountId.HasValue == true)
            return warehouseAccount.StockDeliveredButNotBilledAccountId.Value;

        var company = await _companyRepository.GetAsync(companyId);
        return company.StockDeliveredButNotBilledAccountId
            ?? throw new BusinessException(MyERPDomainErrorCodes.AccountIsGroup)
                .WithData("message", "No SDBNB account configured.");
    }

    /// <summary>
    /// Resolves the stock adjustment account for stock reconciliation entries.
    /// </summary>
    public async Task<Guid> ResolveStockAdjustmentAccountAsync(Guid warehouseId, Guid companyId)
    {
        var warehouseAccount = await _warehouseAccountRepository.FindAsync(
            wa => wa.WarehouseId == warehouseId && wa.CompanyId == companyId);

        if (warehouseAccount?.StockAdjustmentAccountId.HasValue == true)
            return warehouseAccount.StockAdjustmentAccountId.Value;

        var company = await _companyRepository.GetAsync(companyId);
        // Stock adjustment fallback: Company.StockAdjustmentAccountId not on entity yet → use expense
        return company.DefaultExpenseAccountId
            ?? throw new BusinessException(MyERPDomainErrorCodes.AccountIsGroup)
                .WithData("message", "No stock adjustment account configured.");
    }
}
