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

[Authorize(MyERPPermissions.TaxCategories.Default)]
public class ItemTaxTemplateAppService : ApplicationService, IItemTaxTemplateAppService
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
        if (input.Details == null || input.Details.Length == 0)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.DocumentMustHaveItems);
        }

        foreach (var d in input.Details)
        {
            if (!d.NotApplicable && d.TaxRate < 0)
            {
                throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                    .WithData("field", "TaxRate");
            }
        }

        var t = new ItemTaxTemplate(GuidGenerator.Create(), input.CompanyId, input.Title, CurrentTenant.Id);
        foreach (var d in input.Details)
            t.AddDetail(d.TaxAccountId, d.TaxRate, d.NotApplicable);
        await _repository.InsertAsync(t);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "ItemTaxTemplate", t.Id,
            "Created", t.CompanyId,
            t.Title, "Draft", "Active", CurrentUser.Id,
            $"Item tax template '{t.Title}' created", CurrentTenant.Id));

        return ObjectMapper.Map<ItemTaxTemplate, ItemTaxTemplateDto>(t);
    }

    [Authorize(MyERPPermissions.TaxCategories.Edit)]
    public async Task<ItemTaxTemplateDto> UpdateAsync(Guid id, UpdateItemTaxTemplateDto input)
    {
        if (input.Details == null || input.Details.Length == 0)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.DocumentMustHaveItems);
        }

        foreach (var d in input.Details)
        {
            if (!d.NotApplicable && d.TaxRate < 0)
            {
                throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                    .WithData("field", "TaxRate");
            }
        }

        var t = (await _repository.WithDetailsAsync()).First(x => x.Id == id);
        t.Rename(input.Title);
        t.SetDisabled(input.IsDisabled);
        t.ClearDetails();
        foreach (var d in input.Details)
            t.AddDetail(d.TaxAccountId, d.TaxRate, d.NotApplicable);
        await _repository.UpdateAsync(t);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "ItemTaxTemplate", t.Id,
            "Updated", t.CompanyId,
            t.Title, "Active", "Active", CurrentUser.Id,
            $"Item tax template '{t.Title}' updated", CurrentTenant.Id));

        return ObjectMapper.Map<ItemTaxTemplate, ItemTaxTemplateDto>(t);
    }

    [Authorize(MyERPPermissions.TaxCategories.Delete)]
    public async Task DeleteAsync(Guid id) => await _repository.DeleteAsync(id);
}
