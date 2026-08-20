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

[Authorize(MyERPPermissions.Locations.Default)]
public class LocationAppService : ApplicationService, ILocationAppService
{
    private readonly IRepository<Location, Guid> _repository;
    private readonly LocationMapper _mapper;

    public LocationAppService(
        IRepository<Location, Guid> repository,
        LocationMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<PagedResultDto<LocationDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var totalCount = await AsyncExecuter.CountAsync(query);
        var locations = await AsyncExecuter.ToListAsync(
            query.OrderBy(l => l.LocationName)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount));

        var parentIds = locations.Where(l => l.ParentLocationId.HasValue).Select(l => l.ParentLocationId!.Value).Distinct().ToList();
        var parentNames = parentIds.Count == 0
            ? new System.Collections.Generic.Dictionary<Guid, string>()
            : (await AsyncExecuter.ToListAsync(query.Where(l => parentIds.Contains(l.Id))))
                .ToDictionary(l => l.Id, l => l.LocationName);

        var dtos = locations.Select(l =>
        {
            var dto = _mapper.Map(l);
            if (l.ParentLocationId.HasValue && parentNames.TryGetValue(l.ParentLocationId.Value, out var pName))
                dto.ParentLocationName = pName;
            return dto;
        }).ToList();

        return new PagedResultDto<LocationDto>(totalCount, dtos);
    }

    public async Task<LocationDto> GetAsync(Guid id)
    {
        var location = await _repository.GetAsync(id);
        var dto = _mapper.Map(location);
        if (location.ParentLocationId.HasValue)
        {
            var parent = await _repository.FindAsync(location.ParentLocationId.Value);
            dto.ParentLocationName = parent?.LocationName;
        }
        return dto;
    }

    [Authorize(MyERPPermissions.Locations.Create)]
    public async Task<LocationDto> CreateAsync(CreateUpdateLocationDto input)
    {
        var location = new Location(GuidGenerator.Create(), input.LocationName, input.ParentLocationId, CurrentTenant.Id)
        {
            IsContainer = input.IsContainer,
            IsGroup = input.IsGroup,
            Latitude = input.Latitude,
            Longitude = input.Longitude,
        };

        await _repository.InsertAsync(location);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "Location", location.Id,
            "Created", Guid.Empty,
            location.LocationName, "Draft", "Active", CurrentUser.Id,
            $"Location '{location.LocationName}' created", CurrentTenant.Id));

        return _mapper.Map(location);
    }

    [Authorize(MyERPPermissions.Locations.Edit)]
    public async Task<LocationDto> UpdateAsync(Guid id, CreateUpdateLocationDto input)
    {
        var location = await _repository.GetAsync(id);

        if (input.ParentLocationId == id)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition).WithData("reason", "A location cannot be its own parent.");

        location.SetName(input.LocationName);
        location.ParentLocationId = input.ParentLocationId;
        location.IsContainer = input.IsContainer;
        location.IsGroup = input.IsGroup;
        location.Latitude = input.Latitude;
        location.Longitude = input.Longitude;

        await _repository.UpdateAsync(location);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "Location", location.Id,
            "Updated", Guid.Empty,
            location.LocationName, "Active", "Active", CurrentUser.Id,
            $"Location '{location.LocationName}' updated", CurrentTenant.Id));

        return _mapper.Map(location);
    }

    [Authorize(MyERPPermissions.Locations.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        var query = await _repository.GetQueryableAsync();
        var hasChildren = query.Any(l => l.ParentLocationId == id);
        if (hasChildren)
        {
            throw new BusinessException(MyERPDomainErrorCodes.LocationCannotBeDeleted)
                .WithData("reason", "This location has child locations linked to it.");
        }

        await _repository.DeleteAsync(id);
    }
}
