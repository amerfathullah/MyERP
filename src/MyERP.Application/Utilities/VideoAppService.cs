using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Permissions;
using MyERP.Utilities.Entities;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Utilities;

[Authorize(MyERPPermissions.Videos.Default)]
public class VideoAppService : MyERPAppService, IVideoAppService
{
    private readonly IRepository<Video, Guid> _repository;

    public VideoAppService(IRepository<Video, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<VideoDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return new VideoMapper().Map(entity);
    }

    public async Task<PagedResultDto<VideoDto>> GetListAsync(GetVideoListDto input)
    {
        var query = await _repository.GetQueryableAsync();

        if (input.Provider.HasValue)
        {
            query = query.Where(x => x.Provider == input.Provider.Value);
        }

        if (input.IsActive.HasValue)
        {
            query = query.Where(x => x.IsActive == input.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter.Trim().ToLower();
            query = query.Where(x => x.Title.ToLower().Contains(filter) ||
                                     x.Url.ToLower().Contains(filter) ||
                                     (x.Description != null && x.Description.ToLower().Contains(filter)));
        }

        var totalCount = await AsyncExecuter.CountAsync(query);
        var entities = await AsyncExecuter.ToListAsync(
            query.OrderBy(x => x.Title)
                 .Skip(input.SkipCount)
                 .Take(input.MaxResultCount));

        var dtos = entities.Select(new VideoMapper().Map).ToList();
        return new PagedResultDto<VideoDto>(totalCount, dtos);
    }

    [Authorize(MyERPPermissions.Videos.Create)]
    public async Task<VideoDto> CreateAsync(CreateUpdateVideoDto input)
    {
        var entity = new Video(
            GuidGenerator.Create(),
            input.Title.Trim(),
            input.Provider,
            input.Url.Trim(),
            input.YoutubeVideoId?.Trim(),
            input.PublishDate,
            input.DurationSeconds,
            input.Description?.Trim(),
            input.ImageUrl?.Trim(),
            input.IsActive,
            CurrentTenant.Id);

        await _repository.InsertAsync(entity);
        return new VideoMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.Videos.Edit)]
    public async Task<VideoDto> UpdateAsync(Guid id, CreateUpdateVideoDto input)
    {
        var entity = await _repository.GetAsync(id);

        entity.Title = input.Title.Trim();
        entity.Provider = input.Provider;
        entity.Url = input.Url.Trim();
        entity.YoutubeVideoId = input.YoutubeVideoId?.Trim();
        entity.PublishDate = input.PublishDate;
        entity.DurationSeconds = input.DurationSeconds;
        entity.Description = input.Description?.Trim();
        entity.ImageUrl = input.ImageUrl?.Trim();
        entity.IsActive = input.IsActive;

        await _repository.UpdateAsync(entity);
        return new VideoMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.Videos.Edit)]
    public async Task<VideoDto> UpdateStatsAsync(Guid id, UpdateVideoStatsDto input)
    {
        var entity = await _repository.GetAsync(id);
        entity.UpdateStats(input.ViewCount, input.LikeCount, input.DislikeCount, input.CommentCount);
        await _repository.UpdateAsync(entity);
        return new VideoMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.Videos.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}
