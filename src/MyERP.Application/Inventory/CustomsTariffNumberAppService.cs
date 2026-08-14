using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

[Authorize(MyERPPermissions.CustomsTariffNumbers.Default)]
public class CustomsTariffNumberAppService : MyERPAppService, ICustomsTariffNumberAppService
{
    private readonly IRepository<CustomsTariffNumber, Guid> _repository;

    public CustomsTariffNumberAppService(IRepository<CustomsTariffNumber, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<CustomsTariffNumberDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return new CustomsTariffNumberMapper().Map(entity);
    }

    public async Task<PagedResultDto<CustomsTariffNumberDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var totalCount = await AsyncExecuter.CountAsync(query);
        var entities = await AsyncExecuter.ToListAsync(
            query.OrderBy(x => x.TariffNumber)
                 .Skip(input.SkipCount)
                 .Take(input.MaxResultCount)
        );

        return new PagedResultDto<CustomsTariffNumberDto>(
            totalCount,
            entities.Select(e => new CustomsTariffNumberMapper().Map(e)).ToList()
        );
    }

    [Authorize(MyERPPermissions.CustomsTariffNumbers.Create)]
    public async Task<CustomsTariffNumberDto> CreateAsync(CreateUpdateCustomsTariffNumberDto input)
    {
        var entity = new CustomsTariffNumber(
            GuidGenerator.Create(),
            input.CompanyId,
            input.TariffNumber,
            input.Description,
            CurrentTenant.Id);

        await _repository.InsertAsync(entity);
        return new CustomsTariffNumberMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.CustomsTariffNumbers.Edit)]
    public async Task<CustomsTariffNumberDto> UpdateAsync(Guid id, CreateUpdateCustomsTariffNumberDto input)
    {
        var entity = await _repository.GetAsync(id);
        entity.SetTariffNumber(input.TariffNumber);
        entity.Description = input.Description;

        await _repository.UpdateAsync(entity);
        return new CustomsTariffNumberMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.CustomsTariffNumbers.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}
