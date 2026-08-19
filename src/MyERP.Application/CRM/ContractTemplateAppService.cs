using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.CRM.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.CRM;

[Authorize(MyERPPermissions.Leads.Default)]
public class ContractTemplateAppService : ApplicationService, IContractTemplateAppService
{
    private readonly IRepository<ContractTemplate, Guid> _repository;

    public ContractTemplateAppService(IRepository<ContractTemplate, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<ContractTemplateDto> GetAsync(Guid id)
    {
        var entity = (await _repository.WithDetailsAsync()).First(t => t.Id == id);
        return MapToDto(entity);
    }

    public async Task<PagedResultDto<ContractTemplateDto>> GetListAsync(GetContractTemplateListDto input)
    {
        var query = (await _repository.WithDetailsAsync()).AsQueryable();
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var f = input.Filter;
            query = query.Where(t => t.Title.Contains(f));
        }

        var totalCount = query.Count();
        var items = query.OrderBy(t => t.Title)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<ContractTemplateDto>(totalCount, items.Select(MapToDto).ToList());
    }

    [Authorize(MyERPPermissions.Leads.Create)]
    public async Task<ContractTemplateDto> CreateAsync(CreateUpdateContractTemplateDto input)
    {
        var entity = new ContractTemplate(GuidGenerator.Create(), input.Title, CurrentTenant.Id)
        {
            ContractTerms = input.ContractTerms,
            RequiresFulfilment = input.RequiresFulfilment,
        };

        foreach (var term in input.FulfilmentTerms)
        {
            entity.AddFulfilmentTerm(new ContractTemplateFulfilmentTerm(GuidGenerator.Create(), entity.Id, term.TermText));
        }

        await _repository.InsertAsync(entity);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "ContractTemplate", entity.Id,
            "Created", Guid.Empty,
            entity.Title, "Draft", "Active",
            CurrentUser.Id,
            $"Contract template '{entity.Title}' created with {entity.FulfilmentTerms.Count} fulfilment terms", CurrentTenant.Id));

        return MapToDto(entity);
    }

    [Authorize(MyERPPermissions.Leads.Edit)]
    public async Task<ContractTemplateDto> UpdateAsync(Guid id, CreateUpdateContractTemplateDto input)
    {
        var entity = (await _repository.WithDetailsAsync()).First(t => t.Id == id);
        entity.Title = input.Title;
        entity.ContractTerms = input.ContractTerms;
        entity.RequiresFulfilment = input.RequiresFulfilment;
        await _repository.UpdateAsync(entity);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "ContractTemplate", entity.Id,
            "Updated", Guid.Empty,
            entity.Title, "Active", "Active",
            CurrentUser.Id,
            $"Contract template '{entity.Title}' updated", CurrentTenant.Id));

        return MapToDto(entity);
    }

    [Authorize(MyERPPermissions.Leads.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    private static ContractTemplateDto MapToDto(ContractTemplate e) => new()
    {
        Id = e.Id,
        Title = e.Title,
        ContractTerms = e.ContractTerms,
        RequiresFulfilment = e.RequiresFulfilment,
        CreationTime = e.CreationTime,
        LastModificationTime = e.LastModificationTime,
        FulfilmentTerms = e.FulfilmentTerms.Select(t => new ContractTemplateFulfilmentTermDto
        {
            Id = t.Id,
            TermText = t.TermText,
        }).ToList(),
    };
}
