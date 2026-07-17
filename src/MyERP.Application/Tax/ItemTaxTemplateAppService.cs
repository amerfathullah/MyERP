using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Tax.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Tax;

public class ItemTaxTemplateDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string Title { get; set; } = null!;
    public bool IsDisabled { get; set; }
    public ItemTaxTemplateDetailDto[] Details { get; set; } = [];
}

public class ItemTaxTemplateDetailDto
{
    public Guid Id { get; set; }
    public Guid TaxAccountId { get; set; }
    public decimal TaxRate { get; set; }
    public bool NotApplicable { get; set; }
}

public class CreateItemTaxTemplateDto
{
    public Guid CompanyId { get; set; }
    public string Title { get; set; } = null!;
    public CreateItemTaxTemplateDetailDto[] Details { get; set; } = [];
}

public class CreateItemTaxTemplateDetailDto
{
    public Guid TaxAccountId { get; set; }
    public decimal TaxRate { get; set; }
    public bool NotApplicable { get; set; }
}

[Authorize(MyERPPermissions.TaxCategories.Default)]
public class ItemTaxTemplateAppService : ApplicationService
{
    private readonly IRepository<ItemTaxTemplate, Guid> _repository;

    public ItemTaxTemplateAppService(IRepository<ItemTaxTemplate, Guid> repository) => _repository = repository;

    public async Task<PagedResultDto<ItemTaxTemplateDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = (await _repository.WithDetailsAsync()).AsQueryable();
        var totalCount = query.Count();
        var items = query.OrderBy(t => t.Title)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<ItemTaxTemplateDto>(totalCount, items.Select(x => ObjectMapper.Map<ItemTaxTemplate, ItemTaxTemplateDto>(x)).ToList());
    }

    public async Task<ItemTaxTemplateDto> GetAsync(Guid id)
    {
        var t = (await _repository.WithDetailsAsync()).First(x => x.Id == id);
        return ObjectMapper.Map<ItemTaxTemplate, ItemTaxTemplateDto>(t);
    }

    [Authorize(MyERPPermissions.TaxCategories.Create)]
    public async Task<ItemTaxTemplateDto> CreateAsync(CreateItemTaxTemplateDto input)
    {
        var t = new ItemTaxTemplate(GuidGenerator.Create(), input.CompanyId, input.Title, CurrentTenant.Id);
        foreach (var d in input.Details)
            t.AddDetail(d.TaxAccountId, d.TaxRate, d.NotApplicable);
        await _repository.InsertAsync(t);
        return ObjectMapper.Map<ItemTaxTemplate, ItemTaxTemplateDto>(t);
    }

    [Authorize(MyERPPermissions.TaxCategories.Delete)]
    public async Task DeleteAsync(Guid id) => await _repository.DeleteAsync(id);
}
