using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Assets.Entities;
using MyERP.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Assets;

[Authorize(MyERPPermissions.AssetCategories.Default)]
public class AssetShiftFactorAppService : ApplicationService, IAssetShiftFactorAppService
{
    private readonly IRepository<AssetShiftFactor, Guid> _repository;

    public AssetShiftFactorAppService(IRepository<AssetShiftFactor, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<AssetShiftFactorDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return MapToDto(entity);
    }

    public async Task<PagedResultDto<AssetShiftFactorDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var totalCount = await AsyncExecuter.CountAsync(query);
        var items = await AsyncExecuter.ToListAsync(
            query.OrderBy(s => s.ShiftName)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount));

        return new PagedResultDto<AssetShiftFactorDto>(totalCount, items.Select(MapToDto).ToList());
    }

    [Authorize(MyERPPermissions.AssetCategories.Create)]
    public async Task<AssetShiftFactorDto> CreateAsync(CreateUpdateAssetShiftFactorDto input)
    {
        if (input.Factor <= 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", "Factor");
        }

        var entity = new AssetShiftFactor(GuidGenerator.Create(), input.ShiftName, input.Factor, CurrentTenant.Id)
        {
            IsDefault = input.IsDefault,
        };
        await _repository.InsertAsync(entity);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "AssetShiftFactor", entity.Id,
            "Created", Guid.Empty,
            entity.ShiftName, "Draft", "Active", CurrentUser.Id,
            $"Asset shift factor '{entity.ShiftName}' created (Factor: {entity.Factor})", CurrentTenant.Id));

        return MapToDto(entity);
    }

    [Authorize(MyERPPermissions.AssetCategories.Edit)]
    public async Task<AssetShiftFactorDto> UpdateAsync(Guid id, CreateUpdateAssetShiftFactorDto input)
    {
        if (input.Factor <= 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", "Factor");
        }

        var entity = await _repository.GetAsync(id);
        entity.ShiftName = input.ShiftName;
        entity.Factor = input.Factor;
        entity.IsDefault = input.IsDefault;
        await _repository.UpdateAsync(entity);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "AssetShiftFactor", entity.Id,
            "Updated", Guid.Empty,
            entity.ShiftName, "Active", "Active", CurrentUser.Id,
            $"Asset shift factor '{entity.ShiftName}' updated (Factor: {entity.Factor})", CurrentTenant.Id));

        return MapToDto(entity);
    }

    [Authorize(MyERPPermissions.AssetCategories.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    private static AssetShiftFactorDto MapToDto(AssetShiftFactor e) => new()
    {
        Id = e.Id,
        ShiftName = e.ShiftName,
        Factor = e.Factor,
        IsDefault = e.IsDefault,
        CreationTime = e.CreationTime,
        LastModificationTime = e.LastModificationTime,
    };
}
