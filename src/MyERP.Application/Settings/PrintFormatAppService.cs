using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Settings.Entities;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using System.Linq;

namespace MyERP.Settings;

[Authorize]
public class PrintFormatAppService : MyERPAppService, IPrintFormatAppService
{
    private readonly IRepository<PrintFormat, Guid> _repository;

    public PrintFormatAppService(IRepository<PrintFormat, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<PrintFormatDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return new PrintFormatMapper().Map(entity);
    }

    public async Task<PagedResultDto<PrintFormatDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var totalCount = await AsyncExecuter.CountAsync(query);
        var entities = await AsyncExecuter.ToListAsync(
            query.OrderBy(x => x.Name)
                 .Skip(input.SkipCount)
                 .Take(input.MaxResultCount)
        );
        return new PagedResultDto<PrintFormatDto>(
            totalCount,
            entities.Select(e => new PrintFormatMapper().Map(e)).ToList()
        );
    }

    public async Task<PrintFormatDto> CreateAsync(CreateUpdatePrintFormatDto input)
    {
        var entity = new PrintFormat(GuidGenerator.Create(), Guid.Empty, input.Name, input.DocumentType, input.IsDefault, input.HtmlTemplate, input.FormatType, input.FormatData, CurrentTenant.Id);
        await _repository.InsertAsync(entity);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "PrintFormat", entity.Id,
            "Created", Guid.Empty,
            entity.Name, "Draft", "Active", CurrentUser.Id,
            $"Print format '{entity.Name}' created for {entity.DocumentType}", CurrentTenant.Id));

        return new PrintFormatMapper().Map(entity);
    }

    public async Task<PrintFormatDto> UpdateAsync(Guid id, CreateUpdatePrintFormatDto input)
    {
        var entity = await _repository.GetAsync(id);
        entity.Name = input.Name;
        entity.DocumentType = input.DocumentType;
        entity.HtmlTemplate = input.HtmlTemplate;
        entity.IsDefault = input.IsDefault;
        entity.FormatType = input.FormatType;
        entity.FormatData = input.FormatData;
        
        await _repository.UpdateAsync(entity);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "PrintFormat", entity.Id,
            "Updated", Guid.Empty,
            entity.Name, "Active", "Active", CurrentUser.Id,
            $"Print format '{entity.Name}' updated", CurrentTenant.Id));

        return new PrintFormatMapper().Map(entity);
    }

    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}
