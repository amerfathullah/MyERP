using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Purchasing;

[Authorize(MyERPPermissions.PurchaseOrders.Default)]
public class IncotermAppService : ApplicationService, IIncotermAppService
{
    private readonly IRepository<Incoterm, Guid> _repository;

    public IncotermAppService(IRepository<Incoterm, Guid> repository)
    {
        _repository = repository;
    }

    private static IncotermDto ToDto(Incoterm entity) => new()
    {
        Id = entity.Id,
        Code = entity.Code,
        Title = entity.Title,
        Description = entity.Description,
        IsActive = entity.IsActive,
        CreationTime = entity.CreationTime,
    };

    public async Task<IncotermDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return ToDto(entity);
    }

    public async Task<PagedResultDto<IncotermDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var totalCount = query.Count();
        var list = query.OrderBy(x => x.Code).Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<IncotermDto>(totalCount, list.Select(ToDto).ToList());
    }

    [Authorize(MyERPPermissions.PurchaseOrders.Create)]
    public async Task<IncotermDto> CreateAsync(CreateUpdateIncotermDto input)
    {
        var entity = new Incoterm(GuidGenerator.Create(), input.Code, input.Title, CurrentTenant.Id)
        {
            Description = input.Description,
            IsActive = input.IsActive,
        };
        await _repository.InsertAsync(entity);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "Incoterm", entity.Id,
            "Created", Guid.Empty,
            entity.Code, "Draft", "Active", CurrentUser.Id,
            $"Incoterm '{entity.Code}' created", CurrentTenant.Id));

        return ToDto(entity);
    }

    [Authorize(MyERPPermissions.PurchaseOrders.Edit)]
    public async Task<IncotermDto> UpdateAsync(Guid id, CreateUpdateIncotermDto input)
    {
        var entity = await _repository.GetAsync(id);
        entity.SetCode(input.Code);
        entity.SetTitle(input.Title);
        entity.Description = input.Description;
        entity.IsActive = input.IsActive;
        await _repository.UpdateAsync(entity);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "Incoterm", entity.Id,
            "Updated", Guid.Empty,
            entity.Code, "Active", "Active", CurrentUser.Id,
            $"Incoterm '{entity.Code}' updated", CurrentTenant.Id));

        return ToDto(entity);
    }

    [Authorize(MyERPPermissions.PurchaseOrders.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}
