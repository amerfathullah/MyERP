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

    public SubcontractingBomValidationService(IRepository<Item, Guid> itemRepository, IRepository<SubcontractingBom, Guid> bomRepository)
    {
        _itemRepository = itemRepository;
        _bomRepository = bomRepository;
    }

    public async Task ValidateAsync(Guid id, Guid finishedGoodId, Guid serviceItemId, bool isActive)
    {
        var finishedGood = await _itemRepository.GetAsync(finishedGoodId);
        if (!finishedGood.IsActive)
            throw new BusinessException(MyERPDomainErrorCodes.SubcontractingBomFinishedGoodDisabled).WithData("item", finishedGood.ItemName);
        if (!finishedGood.MaintainStock)
            throw new BusinessException(MyERPDomainErrorCodes.SubcontractingBomFinishedGoodNotStockItem).WithData("item", finishedGood.ItemName);
        if (!finishedGood.DefaultBomId.HasValue)
            throw new BusinessException(MyERPDomainErrorCodes.SubcontractingBomFinishedGoodNoDefaultBom).WithData("item", finishedGood.ItemName);

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
}
