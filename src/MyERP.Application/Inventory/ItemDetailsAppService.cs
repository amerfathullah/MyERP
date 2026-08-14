using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

/// <summary>
/// Resolves item details for transaction forms — auto-populates fields when user selects an item.
/// Delegates to ItemDetailsResolverService for proper 3-tier resolution:
/// ItemDefault (per company) → Item → ItemGroup hierarchy → Company defaults.
/// Per ERPNext get_item_details.py (1850 lines, 56 functions).
/// </summary>
[Authorize]
public class ItemDetailsAppService : ApplicationService, IItemDetailsAppService
{
    private readonly ItemDetailsResolverService _resolver;
    private readonly IRepository<Bin, Guid> _binRepo;

    public ItemDetailsAppService(
        ItemDetailsResolverService resolver,
        IRepository<Bin, Guid> binRepo)
    {
        _resolver = resolver;
        _binRepo = binRepo;
    }

    /// <summary>
    /// Resolves item defaults for a transaction row.
    /// Per ERPNext: get_basic_details → 45 fields resolved from Item → ItemDefault → ItemGroup → Brand → Company defaults.
    /// Per PR #57515: item.check_permission() validates caller has read access to the specific item.
    /// </summary>
    public async Task<ItemDetailsDto> GetItemDetailsAsync(GetItemDetailsInput input)
    {
        // Per PR #57515: verify item exists and user has access (company restriction check)
        var itemRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Item, Guid>>();
        var item = await itemRepo.FindAsync(input.ItemId);
        if (item == null)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ItemInactive)
                .WithData("itemCode", input.ItemId.ToString())
                .WithData("itemName", "Unknown");
        }

        // Company restriction check: if item has RestrictToCompanies and companyId is provided,
        // verify the item is allowed for this company
        if (item.RestrictToCompanies && input.CompanyId.HasValue)
        {
            var restrictionRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.CompanyRestrictionEntry, Guid>>();
            var query = await restrictionRepo.GetQueryableAsync();
            var isAllowed = query.Any(r =>
                r.ParentType == "Item" &&
                r.ParentId == input.ItemId &&
                r.CompanyId == input.CompanyId.Value);
            if (!isAllowed)
            {
                throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.CompanyRestrictionBlocked)
                    .WithData("masterName", item.ItemName ?? item.ItemCode);
            }
        }

        var txType = input.TransactionType == "Buying"
            ? TransactionType.Buying
            : TransactionType.Selling;

        var context = new ItemResolutionContext
        {
            ItemId = input.ItemId,
            CompanyId = input.CompanyId,
            TransactionType = txType,
            WarehouseOverride = input.WarehouseId,
            PartyId = txType == TransactionType.Buying ? input.SupplierId : input.CustomerId,
            PriceListId = input.PriceListId,
            TransactionDate = input.TransactionDate,
        };

        var resolved = await _resolver.ResolveAsync(context);

        var result = new ItemDetailsDto
        {
            ItemId = resolved.ItemId,
            ItemCode = resolved.ItemCode,
            ItemName = resolved.ItemName,
            Description = resolved.Description,
            Uom = resolved.Uom,
            StockUom = resolved.StockUom,
            ConversionFactor = resolved.ConversionFactor,
            IsStockItem = resolved.IsStockItem,
            HasBatchNo = resolved.HasBatchNo,
            HasSerialNo = resolved.HasSerialNo,
            ItemGroup = resolved.ItemGroup,
            Rate = resolved.Rate,
            WarehouseId = resolved.WarehouseId,
            IncomeAccountId = resolved.IncomeAccountId,
            ExpenseAccountId = resolved.ExpenseAccountId,
            ActualQty = resolved.ActualQty,
            ProjectedQty = resolved.ProjectedQty,
            ReservedQty = resolved.ReservedQty,
            AvailableQty = resolved.AvailableQty,
            CompanyTotalStock = resolved.CompanyTotalStock,
            LastPurchaseRate = resolved.LastPurchaseRate,
            MinOrderQty = resolved.MinOrderQty,
            DefaultSupplierId = resolved.DefaultSupplierId,
            DefaultDiscountPercentage = resolved.DefaultDiscountPercentage,
        };

        // Resolve valuation rate from Bin for margin display (per ERPNext: shows cost to enable GP visibility)
        if (resolved.IsStockItem)
        {
            try
            {
                var whId = resolved.WarehouseId ?? input.WarehouseId ?? Guid.Empty;
                if (whId != Guid.Empty)
                {
                    var binQuery = await _binRepo.GetQueryableAsync();
                    var bin = binQuery.FirstOrDefault(b =>
                        b.ItemId == input.ItemId && b.WarehouseId == whId && b.ActualQty > 0);
                    if (bin != null)
                        result.ValuationRate = bin.ValuationRate;
                }
            }
            catch { /* Non-blocking: valuation rate is advisory for margin display */ }
        }

        // Blanket Order rate resolution per ERPNext update_party_blanket_order
        if (input.CompanyId.HasValue && (input.CustomerId.HasValue || input.SupplierId.HasValue))
        {
            try
            {
                var boRateService = LazyServiceProvider
                    .LazyGetRequiredService<Sales.DomainServices.BlanketOrderRateService>();
                var partyId = input.CustomerId ?? input.SupplierId!.Value;
                var orderType = txType == TransactionType.Selling ? "Selling" : "Buying";
                var txDate = input.TransactionDate ?? DateTime.UtcNow;

                var boResult = await boRateService.GetRateAsync(
                    input.CompanyId.Value, partyId, input.ItemId, orderType, txDate);

                if (boResult != null)
                {
                    result.BlanketOrderId = boResult.BlanketOrderId;
                    result.BlanketOrderNumber = boResult.BlanketOrderNumber;
                    result.BlanketOrderRate = boResult.Rate;
                    result.BlanketOrderRemainingQty = boResult.RemainingQty;
                    // BO rate takes precedence over standard rate
                    if (boResult.Rate > 0)
                        result.Rate = boResult.Rate;
                }
            }
            catch { /* Non-blocking: BO resolution failure doesn't prevent item selection */ }
        }

        return result;
    }
}

