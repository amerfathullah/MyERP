using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Purchasing.DomainServices;

/// <summary>
/// Cross-aggregate validation for Subcontracting BOM: item eligibility (stock/non-stock,
/// active) and the "only one active mapping per finished good" rule. Per ERPNext
/// SubcontractingBOM.validate_finished_good / validate_service_item / validate_is_active.
/// </summary>
public class SubcontractingBomValidationService : DomainService
{
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<SubcontractingBom, Guid> _bomRepository;
    private readonly IRepository<Manufacturing.Entities.BillOfMaterials, Guid>? _manufacturingBomRepository;

    public SubcontractingBomValidationService(
        IRepository<Item, Guid> itemRepository,
        IRepository<SubcontractingBom, Guid> bomRepository,
        IRepository<Manufacturing.Entities.BillOfMaterials, Guid>? manufacturingBomRepository = null)
    {
        _itemRepository = itemRepository;
        _bomRepository = bomRepository;
        _manufacturingBomRepository = manufacturingBomRepository;
    }

    public async Task ValidateAsync(Guid id, Guid finishedGoodId, Guid? finishedGoodBomId, Guid serviceItemId, bool isActive)
    {
        var finishedGood = await _itemRepository.GetAsync(finishedGoodId);
        if (!finishedGood.IsActive)
            throw new BusinessException(MyERPDomainErrorCodes.SubcontractingBomFinishedGoodDisabled).WithData("item", finishedGood.ItemName);
        if (!finishedGood.MaintainStock)
            throw new BusinessException(MyERPDomainErrorCodes.SubcontractingBomFinishedGoodNotStockItem).WithData("item", finishedGood.ItemName);

        Item? templateItem = null;
        if (finishedGood.VariantOfId.HasValue)
        {
            templateItem = await _itemRepository.FindAsync(finishedGood.VariantOfId.Value);
        }

        // Per ERPNext PR #59373 (commit 87113d7c2c):
        // Variant finished goods can inherit default BOM from template item
        var hasDefaultBom = finishedGood.DefaultBomId.HasValue || (templateItem?.DefaultBomId.HasValue == true);
        if (!hasDefaultBom)
            throw new BusinessException(MyERPDomainErrorCodes.SubcontractingBomFinishedGoodNoDefaultBom).WithData("item", finishedGood.ItemName);

        // Per ERPNext PR #59373: BOM must belong to finished good or its template item (for variants)
        if (finishedGoodBomId.HasValue)
        {
            var bomRepo = _manufacturingBomRepository ?? LazyServiceProvider?.LazyGetService<IRepository<Manufacturing.Entities.BillOfMaterials, Guid>>();
            if (bomRepo != null)
            {
                var bom = await bomRepo.FindAsync(finishedGoodBomId.Value);
                if (bom != null)
                {
                    var isApplicable = bom.ItemId == finishedGoodId || (templateItem != null && bom.ItemId == templateItem.Id);
                    if (!isApplicable)
                    {
                        throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                            .WithData("detail", $"BOM {bom.BomNumber} does not belong to Item {finishedGood.ItemName}.");
                    }
                }
            }
        }

        var serviceItem = await _itemRepository.GetAsync(serviceItemId);
        if (!serviceItem.IsActive)
            throw new BusinessException(MyERPDomainErrorCodes.SubcontractingBomServiceItemDisabled).WithData("item", serviceItem.ItemName);
        if (serviceItem.MaintainStock)
            throw new BusinessException(MyERPDomainErrorCodes.SubcontractingBomServiceItemIsStockItem).WithData("item", serviceItem.ItemName);

        if (isActive)
        {
            var query = await _bomRepository.GetQueryableAsync();
            var hasOtherActive = query.Any(b => b.FinishedGoodId == finishedGoodId && b.IsActive && b.Id != id);
            if (hasOtherActive)
                throw new BusinessException(MyERPDomainErrorCodes.SubcontractingBomFinishedGoodAlreadyActive).WithData("item", finishedGood.ItemName);
        }
    }

    public Task ValidateAsync(Guid id, Guid finishedGoodId, Guid serviceItemId, bool isActive)
        => ValidateAsync(id, finishedGoodId, null, serviceItemId, isActive);
}
