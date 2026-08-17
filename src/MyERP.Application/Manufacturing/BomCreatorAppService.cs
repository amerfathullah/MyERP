using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.DomainServices;
using MyERP.Manufacturing.DomainServices;
using MyERP.Manufacturing.Entities;
using MyERP.Permissions;
using MyERP.Shared;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Manufacturing;

[Authorize(MyERPPermissions.Manufacturing.Default)]
public class BomCreatorAppService : ApplicationService, IBomCreatorAppService
{
    private readonly IRepository<BomCreator, Guid> _repository;
    private readonly BomCreatorService _bomCreatorService;
    private readonly IDocumentNumberGenerator _numberGenerator;

    public BomCreatorAppService(
        IRepository<BomCreator, Guid> repository,
        BomCreatorService bomCreatorService,
        IDocumentNumberGenerator numberGenerator)
    {
        _repository = repository;
        _bomCreatorService = bomCreatorService;
        _numberGenerator = numberGenerator;
    }

    public async Task<PagedResultDto<BomCreatorDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = (await _repository.WithDetailsAsync()).AsQueryable();
        if (input.CompanyId.HasValue)
            query = query.Where(b => b.CompanyId == input.CompanyId.Value);

        var totalCount = query.Count();
        var items = query.OrderByDescending(b => b.CreationTime)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<BomCreatorDto>(totalCount, items.Select(ObjectMapper.Map<BomCreator, BomCreatorDto>).ToList());
    }

    public async Task<BomCreatorDto> GetAsync(Guid id)
    {
        var creator = (await _repository.WithDetailsAsync()).First(b => b.Id == id);
        return ObjectMapper.Map<BomCreator, BomCreatorDto>(creator);
    }

    [Authorize(MyERPPermissions.Manufacturing.Create)]
    public async Task<BomCreatorDto> CreateAsync(CreateUpdateBomCreatorDto input)
    {
        var creator = new BomCreator(GuidGenerator.Create(), input.CompanyId, input.FinishedGoodItemId, input.Qty, CurrentTenant.Id);
        ApplyInput(creator, input);
        await _repository.InsertAsync(creator);
        return ObjectMapper.Map<BomCreator, BomCreatorDto>(creator);
    }

    [Authorize(MyERPPermissions.Manufacturing.Edit)]
    public async Task<BomCreatorDto> UpdateAsync(Guid id, CreateUpdateBomCreatorDto input)
    {
        var creator = (await _repository.WithDetailsAsync()).First(b => b.Id == id);
        creator.FinishedGoodItemId = input.FinishedGoodItemId;
        creator.Qty = input.Qty;
        ApplyInput(creator, input);
        await _repository.UpdateAsync(creator);
        return ObjectMapper.Map<BomCreator, BomCreatorDto>(creator);
    }

    [Authorize(MyERPPermissions.Manufacturing.Delete)]
    public async Task DeleteAsync(Guid id) => await _repository.DeleteAsync(id);

    [Authorize(MyERPPermissions.Manufacturing.Create)]
    public async Task<BomCreatorDto> CreateBomsAsync(Guid id)
    {
        var creator = (await _repository.WithDetailsAsync()).First(b => b.Id == id);
        await _bomCreatorService.CreateBomsAsync(creator, _ => _numberGenerator.GenerateAsync("BOM", creator.CompanyId));
        await _repository.UpdateAsync(creator);
        return ObjectMapper.Map<BomCreator, BomCreatorDto>(creator);
    }

    private static void ApplyInput(BomCreator creator, CreateUpdateBomCreatorDto input)
    {
        creator.CompanyId = input.CompanyId;
        creator.Uom = input.Uom;
        creator.IsPhantom = input.IsPhantom;
        creator.RoutingId = input.RoutingId;
        creator.DefaultWarehouseId = input.DefaultWarehouseId;
        creator.RmCostAsPer = input.RmCostAsPer;
        creator.Remarks = input.Remarks;

        creator.ClearItems();
        foreach (var i in input.Items)
        {
            var item = creator.AddItem(i.ItemId, i.ItemName, i.FgItemId, i.Qty, i.Rate,
                i.IsExpandable, i.Uom, i.ConversionFactor, i.StockUom);
            item.OperationId = i.OperationId;
            item.IsSubcontracted = i.IsSubcontracted;
            item.IsPhantomItem = i.IsPhantomItem;
            item.SourcedBySupplier = i.SourcedBySupplier;
            item.Instruction = i.Instruction;
        }

        creator.RecalculateCost();
    }
}
