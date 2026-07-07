using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Distributed;
using MiniExcelLibs;
using MyERP.Permissions;
using MyERP.Shared;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Caching;
using Volo.Abp.Content;
using Volo.Abp.Domain.Repositories;
using System.Linq.Dynamic.Core;

namespace MyERP.Authors;

[Authorize(MyERPPermissions.Authors.Default)]
public class AuthorAppService : ApplicationService, IAuthorAppService
{
    private readonly IRepository<Author, Guid> _repository;
    private readonly IDistributedCache<AuthorExcelDownloadTokenCacheItem, string> _excelDownloadTokenCache;

    public AuthorAppService(
        IRepository<Author, Guid> repository,
        IDistributedCache<AuthorExcelDownloadTokenCacheItem, string> excelDownloadTokenCache)
    {
        _repository = repository;
        _excelDownloadTokenCache = excelDownloadTokenCache;
    }

    public async Task<AuthorDto> GetAsync(Guid id)
    {
        var author = await _repository.GetAsync(id);
        return ObjectMapper.Map<Author, AuthorDto>(author);
    }

    public async Task<PagedResultDto<AuthorDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var queryable = await _repository.GetQueryableAsync();
        var query = queryable
            .OrderBy(input.Sorting.IsNullOrWhiteSpace() ? "Name" : input.Sorting)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount);

        var authors = await AsyncExecuter.ToListAsync(query);
        var totalCount = await AsyncExecuter.CountAsync(queryable);

        return new PagedResultDto<AuthorDto>(
            totalCount,
            ObjectMapper.Map<List<Author>, List<AuthorDto>>(authors)
        );
    }

    [Authorize(MyERPPermissions.Authors.Create)]
    public async Task<AuthorDto> CreateAsync(CreateUpdateAuthorDto input)
    {
        var author = ObjectMapper.Map<CreateUpdateAuthorDto, Author>(input);
        await _repository.InsertAsync(author);
        return ObjectMapper.Map<Author, AuthorDto>(author);
    }

    [Authorize(MyERPPermissions.Authors.Edit)]
    public async Task<AuthorDto> UpdateAsync(Guid id, CreateUpdateAuthorDto input)
    {
        var author = await _repository.GetAsync(id);
        ObjectMapper.Map(input, author);
        await _repository.UpdateAsync(author);
        return ObjectMapper.Map<Author, AuthorDto>(author);
    }

    [Authorize(MyERPPermissions.Authors.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    [AllowAnonymous]
    public async Task<IRemoteStreamContent> GetListAsExcelFileAsync(AuthorExcelDownloadDto input)
    {
        var downloadToken = await _excelDownloadTokenCache.GetAsync(input.DownloadToken);
        if (downloadToken == null || input.DownloadToken != downloadToken.Token)
        {
            throw new AbpAuthorizationException("Invalid download token: " + input.DownloadToken);
        }

        var queryable = await _repository.GetQueryableAsync();
        var query = queryable.OrderBy(input.Sorting.IsNullOrWhiteSpace() ? "Name" : input.Sorting);
        var authors = await AsyncExecuter.ToListAsync(query);

        var memoryStream = new MemoryStream();
        await memoryStream.SaveAsAsync(ObjectMapper.Map<List<Author>, List<AuthorExcelDto>>(authors));
        memoryStream.Seek(0, SeekOrigin.Begin);

        return new RemoteStreamContent(
            memoryStream,
            "Authors.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        );
    }

    public async Task<DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        var token = GuidGenerator.Create().ToString("N");

        await _excelDownloadTokenCache.SetAsync(
            token,
            new AuthorExcelDownloadTokenCacheItem { Token = token },
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30)
            });

        return new DownloadTokenResultDto
        {
            Token = token
        };
    }
}
