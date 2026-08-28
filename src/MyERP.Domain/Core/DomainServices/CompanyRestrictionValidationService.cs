using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Core.DomainServices;

/// <summary>
/// Validates that transactions do not reference company-restricted masters from other companies.
/// Per ERPNext PR #57352: transaction-level enforcement of company restrictions.
/// 
/// Flow: collect all Item/Customer/Supplier IDs referenced in a document → 
/// query which ones have restrict_to_companies=true → 
/// check if the transaction's company is in their allowed_companies list →
/// block if any are restricted.
/// </summary>
public class CompanyRestrictionValidationService : DomainService
{
    private readonly IRepository<CompanyRestrictionEntry, Guid> _restrictionEntryRepo;
    private readonly IRepository<Item, Guid> _itemRepo;
    private readonly IRepository<Customer, Guid> _customerRepo;
    private readonly IRepository<Supplier, Guid> _supplierRepo;
    private readonly IRepository<Account, Guid> _accountRepo;
    private readonly IRepository<Warehouse, Guid> _warehouseRepo;

    /// <summary>
    /// Document types exempt from company restriction validation.
    /// Per ERPNext: system-generated documents that shouldn't be blocked.
    /// </summary>
    private static readonly HashSet<string> ExemptDocumentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Asset", "BankTransaction", "ExchangeRateRevaluation", "LandedCostVoucher",
        "PosClosingEntry", "PosInvoiceMergeLog", "PaymentReconciliation",
        "ProcessPaymentReconciliation", "RepostAccountingLedger", "RepostItemValuation",
        "RepostPaymentLedger", "SerialNo", "SerialAndBatchBundle", "UnreconcilePayment"
    };

    public CompanyRestrictionValidationService(
        IRepository<CompanyRestrictionEntry, Guid> restrictionEntryRepo,
        IRepository<Item, Guid> itemRepo,
        IRepository<Customer, Guid> customerRepo,
        IRepository<Supplier, Guid> supplierRepo,
        IRepository<Account, Guid> accountRepo,
        IRepository<Warehouse, Guid> warehouseRepo)
    {
        _restrictionEntryRepo = restrictionEntryRepo;
        _itemRepo = itemRepo;
        _customerRepo = customerRepo;
        _supplierRepo = supplierRepo;
        _accountRepo = accountRepo;
        _warehouseRepo = warehouseRepo;
    }

    /// <summary>
    /// Validates that all referenced Item/Customer/Supplier masters allow the transaction's company.
    /// Call this from AppService CreateAsync/UpdateAsync for all transaction documents.
    /// </summary>
    /// <param name="documentType">The document type being validated (e.g., "SalesOrder").</param>
    /// <param name="companyId">The company of the transaction.</param>
    /// <param name="itemIds">Item IDs referenced in the document (from items/child rows).</param>
    /// <param name="customerIds">Customer IDs referenced (usually 0 or 1).</param>
    /// <param name="supplierIds">Supplier IDs referenced (usually 0 or 1).</param>
    /// <param name="accountIds">GL Account IDs referenced (JE lines, payment from/to accounts, etc.).</param>
    /// <param name="warehouseIds">Warehouse IDs referenced (stock movement source/target).</param>
    public async Task ValidateTransactionCompanyAsync(
        string documentType,
        Guid companyId,
        IReadOnlyCollection<Guid>? itemIds = null,
        IReadOnlyCollection<Guid>? customerIds = null,
        IReadOnlyCollection<Guid>? supplierIds = null,
        IReadOnlyCollection<Guid>? accountIds = null,
        IReadOnlyCollection<Guid>? warehouseIds = null)
    {
        if (IsExemptDocumentType(documentType))
            return;

        if (companyId == Guid.Empty)
            return;

        var blockedNames = new List<string>();

        // Check Items
        if (itemIds is { Count: > 0 })
        {
            var blocked = await GetBlockedItemNamesAsync(itemIds, companyId);
            blockedNames.AddRange(blocked.Select(n => $"Item: {n}"));
        }

        // Check Customers
        if (customerIds is { Count: > 0 })
        {
            var blocked = await GetBlockedCustomerNamesAsync(customerIds, companyId);
            blockedNames.AddRange(blocked.Select(n => $"Customer: {n}"));
        }

        // Check Suppliers
        if (supplierIds is { Count: > 0 })
        {
            var blocked = await GetBlockedSupplierNamesAsync(supplierIds, companyId);
            blockedNames.AddRange(blocked.Select(n => $"Supplier: {n}"));
        }

        // Check Accounts
        if (accountIds is { Count: > 0 })
        {
            var blocked = await GetBlockedAccountNamesAsync(accountIds, companyId);
            blockedNames.AddRange(blocked.Select(n => $"Account: {n}"));
        }

        // Check Warehouses
        if (warehouseIds is { Count: > 0 })
        {
            var blocked = await GetBlockedWarehouseNamesAsync(warehouseIds, companyId);
            blockedNames.AddRange(blocked.Select(n => $"Warehouse: {n}"));
        }

        if (blockedNames.Count > 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.CompanyRestrictionBlocked)
                .WithData("documentType", documentType)
                .WithData("blockedMasters", string.Join(", ", blockedNames))
                .WithData("companyId", companyId);
        }
    }

    /// <summary>
    /// Checks whether a document type is exempt from restriction validation.
    /// </summary>
    public static bool IsExemptDocumentType(string documentType)
    {
        return ExemptDocumentTypes.Contains(documentType);
    }

    private async Task<List<string>> GetBlockedItemNamesAsync(IReadOnlyCollection<Guid> itemIds, Guid companyId)
    {
        var queryable = await _itemRepo.GetQueryableAsync();
        var referencedItems = queryable
            .Where(i => itemIds.Contains(i.Id))
            .Select(i => new { i.Id, i.ItemCode, i.CompanyId, i.RestrictToCompanies })
            .ToList();

        // Hard rule: Item.CompanyId is the item's owning company — a document can never
        // reference an item owned by a different company, regardless of RestrictToCompanies.
        var blocked = referencedItems
            .Where(i => i.CompanyId != companyId)
            .Select(i => i.ItemCode)
            .ToList();

        var restrictedItems = referencedItems.Where(i => i.CompanyId == companyId && i.RestrictToCompanies).ToList();
        if (restrictedItems.Count > 0)
        {
            var restrictedIds = restrictedItems.Select(i => i.Id).ToList();
            var allowed = await GetAllowedParentIdsAsync("Item", restrictedIds, companyId);
            blocked.AddRange(restrictedItems.Where(i => !allowed.Contains(i.Id)).Select(i => i.ItemCode));
        }

        return blocked;
    }

    private async Task<List<string>> GetBlockedCustomerNamesAsync(IReadOnlyCollection<Guid> customerIds, Guid companyId)
    {
        var queryable = await _customerRepo.GetQueryableAsync();
        var referencedCustomers = queryable
            .Where(c => customerIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name, c.CompanyId, c.RestrictToCompanies })
            .ToList();

        var blocked = referencedCustomers
            .Where(c => c.CompanyId != companyId)
            .Select(c => c.Name)
            .ToList();

        var restrictedCustomers = referencedCustomers.Where(c => c.CompanyId == companyId && c.RestrictToCompanies).ToList();
        if (restrictedCustomers.Count > 0)
        {
            var restrictedIds = restrictedCustomers.Select(c => c.Id).ToList();
            var allowed = await GetAllowedParentIdsAsync("Customer", restrictedIds, companyId);
            blocked.AddRange(restrictedCustomers.Where(c => !allowed.Contains(c.Id)).Select(c => c.Name));
        }

        return blocked;
    }

    private async Task<List<string>> GetBlockedSupplierNamesAsync(IReadOnlyCollection<Guid> supplierIds, Guid companyId)
    {
        var queryable = await _supplierRepo.GetQueryableAsync();
        var referencedSuppliers = queryable
            .Where(s => supplierIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Name, s.CompanyId, s.RestrictToCompanies })
            .ToList();

        var blocked = referencedSuppliers
            .Where(s => s.CompanyId != companyId)
            .Select(s => s.Name)
            .ToList();

        var restrictedSuppliers = referencedSuppliers.Where(s => s.CompanyId == companyId && s.RestrictToCompanies).ToList();
        if (restrictedSuppliers.Count > 0)
        {
            var restrictedIds = restrictedSuppliers.Select(s => s.Id).ToList();
            var allowed = await GetAllowedParentIdsAsync("Supplier", restrictedIds, companyId);
            blocked.AddRange(restrictedSuppliers.Where(s => !allowed.Contains(s.Id)).Select(s => s.Name));
        }

        return blocked;
    }

    private async Task<List<string>> GetBlockedAccountNamesAsync(IReadOnlyCollection<Guid> accountIds, Guid companyId)
    {
        var queryable = await _accountRepo.GetQueryableAsync();
        return queryable
            .Where(a => accountIds.Contains(a.Id) && a.CompanyId != companyId)
            .Select(a => a.AccountName)
            .ToList();
    }

    private async Task<List<string>> GetBlockedWarehouseNamesAsync(IReadOnlyCollection<Guid> warehouseIds, Guid companyId)
    {
        var queryable = await _warehouseRepo.GetQueryableAsync();
        return queryable
            .Where(w => warehouseIds.Contains(w.Id) && w.CompanyId != companyId)
            .Select(w => w.Name)
            .ToList();
    }

    private async Task<HashSet<Guid>> GetAllowedParentIdsAsync(string parentType, List<Guid> parentIds, Guid companyId)
    {
        var queryable = await _restrictionEntryRepo.GetQueryableAsync();
        var allowedParents = queryable
            .Where(r => r.ParentType == parentType
                     && parentIds.Contains(r.ParentId)
                     && r.CompanyId == companyId)
            .Select(r => r.ParentId)
            .ToList();

        return new HashSet<Guid>(allowedParents);
    }
}
