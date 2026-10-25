using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Sales.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

[Authorize(MyERPPermissions.Items.Default)]
public class ProductBundleAppService : ApplicationService, IProductBundleAppService
{
    private readonly IRepository<ProductBundle, Guid> _repository;
    public ProductBundleAppService(IRepository<ProductBundle, Guid> repository) => _repository = repository;

    public async Task<PagedResultDto<ProductBundleDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = (await _repository.WithDetailsAsync()).AsQueryable();
        var totalCount = query.Count();
        var items = query.OrderBy(b => b.ItemName)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<ProductBundleDto>(totalCount, items.Select(x => ObjectMapper.Map<ProductBundle, ProductBundleDto>(x)).ToList());
    }

    [Authorize(MyERPPermissions.Items.Create)]
    public async Task<ProductBundleDto> CreateAsync(CreateProductBundleDto input)
    {
        if (input.Items == null || input.Items.Length == 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustHaveItems);
        }

        // 1. A Product Bundle cannot contain its own parent Item (ERPNext PR #62149 / commit 268e829577)
        if (input.Items.Any(i => i.ComponentItemId == input.ItemId))
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("message", "A Product Bundle cannot contain its own parent Item.")
                .WithData("itemId", input.ItemId);
        }

        var allItemIds = input.Items.Select(i => i.ComponentItemId).Append(input.ItemId).Distinct().ToArray();
        var itemValidation = LazyServiceProvider.LazyGetRequiredService<MyERP.Inventory.DomainServices.ItemTransactionValidationService>();
        await itemValidation.ValidateItemsForTransactionAsync(allItemIds);

        foreach (var item in input.Items)
        {
            if (item.Qty <= 0)
            {
                throw new BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                    .WithData("field", "Qty");
            }
        }

        // 2. Reject nested product bundles (ERPNext PR #62149 / commit 268e829577):
        // 2a. A Product Bundle cannot contain another active Product Bundle
        var bundleQuery = await _repository.GetQueryableAsync();
        var childItemIds = input.Items.Select(i => i.ComponentItemId).Distinct().ToList();
        var childIsBundle = bundleQuery.Any(b => childItemIds.Contains(b.ItemId) && b.IsActive && !b.IsDisabled);
        if (childIsBundle)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("message", "A Product Bundle cannot contain another Product Bundle.");
        }

        // 2b. Validate parent item is not already a component of another active Product Bundle
        var outerBundles = (await _repository.WithDetailsAsync())
            .Where(b => b.IsActive && !b.IsDisabled && b.Items.Any(i => i.ComponentItemId == input.ItemId))
            .ToList();
        if (outerBundles.Any())
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("message", $"Item is already a component of active Product Bundle; nested bundles are not supported.");
        }

        // 3. Child items UOM validation (ERPNext PR #62149 / commit 80590d5e40):
        // Each component's UOM must match its stock UOM, or have an item-level or global conversion configured.
        var itemRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Inventory.Entities.Item, Guid>>();
        var uomRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Inventory.Entities.UomConversion, Guid>>();
        var uomConversions = await uomRepo.GetListAsync();

        foreach (var child in input.Items)
        {
            if (string.IsNullOrWhiteSpace(child.Uom))
                continue;

            var compItem = await itemRepo.FindAsync(child.ComponentItemId);
            if (compItem == null || string.Equals(child.Uom, compItem.Uom, StringComparison.OrdinalIgnoreCase))
                continue;

            var hasConversion = uomConversions.Any(c =>
                ((c.ItemId == compItem.Id || (compItem.VariantOfId.HasValue && c.ItemId == compItem.VariantOfId.Value))
                 && ((string.Equals(c.FromUom, child.Uom, StringComparison.OrdinalIgnoreCase) && string.Equals(c.ToUom, compItem.Uom, StringComparison.OrdinalIgnoreCase))
                     || (string.Equals(c.FromUom, compItem.Uom, StringComparison.OrdinalIgnoreCase) && string.Equals(c.ToUom, child.Uom, StringComparison.OrdinalIgnoreCase))))
                || (c.ItemId == null
                 && ((string.Equals(c.FromUom, child.Uom, StringComparison.OrdinalIgnoreCase) && string.Equals(c.ToUom, compItem.Uom, StringComparison.OrdinalIgnoreCase))
                     || (string.Equals(c.FromUom, compItem.Uom, StringComparison.OrdinalIgnoreCase) && string.Equals(c.ToUom, child.Uom, StringComparison.OrdinalIgnoreCase)))));

            if (!hasConversion)
            {
                throw new BusinessException(MyERPDomainErrorCodes.UomNotConfiguredForItem)
                    .WithData("uom", child.Uom)
                    .WithData("item", compItem.ItemCode ?? compItem.ItemName);
            }
        }

        var bundle = new ProductBundle(GuidGenerator.Create(), input.ItemId, CurrentTenant.Id)
        { ItemName = input.ItemName, Description = input.Description };
        foreach (var item in input.Items)
            bundle.AddItem(item.ComponentItemId, item.Qty, item.ItemName, item.Uom);
        await _repository.InsertAsync(bundle);
        return ObjectMapper.Map<ProductBundle, ProductBundleDto>(bundle);
    }

    [Authorize(MyERPPermissions.Items.Edit)]
    public async Task<ProductBundleDto> DeactivateAsync(Guid id)
    {
        var bundle = await _repository.GetAsync(id);
        bundle.Deactivate();
        await _repository.UpdateAsync(bundle);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "ProductBundle", bundle.Id,
            "Deactivated", Guid.Empty,
            bundle.ItemName ?? bundle.Id.ToString()[..8], "Active", "Disabled", CurrentUser.Id,
            $"Product bundle '{bundle.ItemName}' deactivated", CurrentTenant.Id));

        return ObjectMapper.Map<ProductBundle, ProductBundleDto>(bundle);
    }
}
