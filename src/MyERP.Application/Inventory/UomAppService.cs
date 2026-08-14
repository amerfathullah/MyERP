using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

/// <summary>
/// CRUD for Unit of Measure master data.
/// Per ERPNext: 239 standard UOMs seeded, users can add custom UOMs.
/// </summary>
[Authorize(MyERPPermissions.Items.Default)]
public class UomAppService : ApplicationService, IUomAppService
{
    private readonly IRepository<Uom, Guid> _repository;

    public UomAppService(IRepository<Uom, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<PagedResultDto<UomDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var totalCount = query.Count();
        var items = query
            .OrderBy(u => u.Name)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        return new PagedResultDto<UomDto>(
            totalCount,
            items.Select(u => new UomDto
            {
                Id = u.Id,
                UomName = u.Name,
                MustBeWholeNumber = u.MustBeWholeNumber,
                Category = u.Category,
                IsEnabled = u.IsEnabled,
            }).ToList());
    }

    public async Task<UomDto> GetAsync(Guid id)
    {
        var uom = await _repository.GetAsync(id);
        return new UomDto
        {
            Id = uom.Id,
            UomName = uom.Name,
            MustBeWholeNumber = uom.MustBeWholeNumber,
            Category = uom.Category,
            IsEnabled = uom.IsEnabled,
        };
    }

    [Authorize(MyERPPermissions.Items.Create)]
    public async Task<UomDto> CreateAsync(CreateUomDto input)
    {
        var uom = new Uom(GuidGenerator.Create(), input.UomName, CurrentTenant.Id)
        {
            MustBeWholeNumber = input.MustBeWholeNumber,
            Category = input.Category,
        };

        await _repository.InsertAsync(uom);

        return new UomDto
        {
            Id = uom.Id,
            UomName = uom.Name,
            MustBeWholeNumber = uom.MustBeWholeNumber,
            Category = uom.Category,
            IsEnabled = uom.IsEnabled,
        };
    }

    [Authorize(MyERPPermissions.Items.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}
