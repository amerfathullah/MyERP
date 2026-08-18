using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Purchasing.DomainServices;
using MyERP.Purchasing.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Purchasing;

[Authorize(MyERPPermissions.PurchaseOrders.Default)]
public class SubcontractingBomAppService : ApplicationService, ISubcontractingBomAppService
{
    private readonly IRepository<SubcontractingBom, Guid> _repository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly SubcontractingBomValidationService _validationService;

    public SubcontractingBomAppService(
        IRepository<SubcontractingBom, Guid> repository,
        IRepository<Item, Guid> itemRepository,
        SubcontractingBomValidationService validationService)
    {
        _repository = repository;
        _itemRepository = itemRepository;
        _validationService = validationService;
    }

    public async Task<PagedResultDto<SubcontractingBomDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var totalCount = query.Count();
        var items = query.OrderByDescending(b => b.CreationTime).Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<SubcontractingBomDto>(totalCount, await MapWithItemNamesAsync(items));
    }

    public async Task<SubcontractingBomDto> GetAsync(Guid id)
    {
        var bom = await _repository.GetAsync(id);
        return (await MapWithItemNamesAsync(new[] { bom })).Single();
    }

    [Authorize(MyERPPermissions.PurchaseOrders.Create)]
    public async Task<SubcontractingBomDto> CreateAsync(CreateUpdateSubcontractingBomDto input)
    {
        await _validationService.ValidateAsync(Guid.Empty, input.FinishedGoodId, input.ServiceItemId, input.IsActive);

        var bom = new SubcontractingBom(GuidGenerator.Create(), input.FinishedGoodId, input.FinishedGoodQty,
            input.FinishedGoodBomId, input.ServiceItemId, input.ServiceItemQty, CurrentTenant.Id)
        {
            IsActive = input.IsActive,
        };
        await _repository.InsertAsync(bom);
        return (await MapWithItemNamesAsync(new[] { bom })).Single();
    }

    [Authorize(MyERPPermissions.PurchaseOrders.Edit)]
    public async Task<SubcontractingBomDto> UpdateAsync(Guid id, CreateUpdateSubcontractingBomDto input)
    {
        await _validationService.ValidateAsync(id, input.FinishedGoodId, input.ServiceItemId, input.IsActive);

        var bom = await _repository.GetAsync(id);
        bom.Update(input.FinishedGoodId, input.FinishedGoodQty, input.FinishedGoodBomId, input.ServiceItemId, input.ServiceItemQty, input.IsActive);
        await _repository.UpdateAsync(bom);
        return (await MapWithItemNamesAsync(new[] { bom })).Single();
    }

    [Authorize(MyERPPermissions.PurchaseOrders.Delete)]
    public async Task DeleteAsync(Guid id) => await _repository.DeleteAsync(id);

    private async Task<List<SubcontractingBomDto>> MapWithItemNamesAsync(IEnumerable<SubcontractingBom> boms)
    {
        var bomList = boms.ToList();
        var itemIds = bomList.Select(b => b.FinishedGoodId).Concat(bomList.Select(b => b.ServiceItemId)).Distinct().ToList();
        var itemQuery = await _itemRepository.GetQueryableAsync();
        var itemNames = itemQuery.Where(i => itemIds.Contains(i.Id))
            .Select(i => new { i.Id, i.ItemName, i.Uom }).ToList()
            .ToDictionary(i => i.Id, i => i);

        return bomList.Select(b => new SubcontractingBomDto
        {
            Id = b.Id,
            IsActive = b.IsActive,
            FinishedGoodId = b.FinishedGoodId,
            FinishedGoodName = itemNames.GetValueOrDefault(b.FinishedGoodId)?.ItemName,
            FinishedGoodQty = b.FinishedGoodQty,
            FinishedGoodBomId = b.FinishedGoodBomId,
            FinishedGoodUom = itemNames.GetValueOrDefault(b.FinishedGoodId)?.Uom,
            ServiceItemId = b.ServiceItemId,
            ServiceItemName = itemNames.GetValueOrDefault(b.ServiceItemId)?.ItemName,
            ServiceItemQty = b.ServiceItemQty,
            ServiceItemUom = itemNames.GetValueOrDefault(b.ServiceItemId)?.Uom,
            ConversionFactor = b.ConversionFactor,
        }).ToList();
    }
}
