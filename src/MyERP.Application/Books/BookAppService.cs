using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Distributed;
using MiniExcelLibs;
using MyERP.Authors;
using MyERP.Permissions;
using MyERP.Shared;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Caching;
using Volo.Abp.Content;
using Volo.Abp.Domain.Repositories;
using System.Linq.Dynamic.Core;

namespace MyERP.Books;

[Authorize(MyERPPermissions.Books.Default)]
public class BookAppService : ApplicationService, IBookAppService
{
    private readonly IRepository<Book, Guid> _repository;
    private readonly IRepository<Author, Guid> _authorRepository;
    private readonly IDistributedCache<BookExcelDownloadTokenCacheItem, string> _excelDownloadTokenCache;

    public BookAppService(
        IRepository<Book, Guid> repository,
        IRepository<Author, Guid> authorRepository,
        IDistributedCache<BookExcelDownloadTokenCacheItem, string> excelDownloadTokenCache)
    {
        _repository = repository;
        _authorRepository = authorRepository;
        _excelDownloadTokenCache = excelDownloadTokenCache;
    }

    public async Task<BookDto> GetAsync(Guid id)
    {
        var book = await _repository.GetAsync(id);
        var bookDto = ObjectMapper.Map<Book, BookDto>(book);
        await SetAuthorNamesAsync(new[] { bookDto });
        return bookDto;
    }

    public async Task<PagedResultDto<BookDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var queryable = await _repository.GetQueryableAsync();
        var query = queryable
            .OrderBy(input.Sorting.IsNullOrWhiteSpace() ? "Name" : input.Sorting)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount);

        var books = await AsyncExecuter.ToListAsync(query);
        var totalCount = await AsyncExecuter.CountAsync(queryable);

        var bookDtos = ObjectMapper.Map<List<Book>, List<BookDto>>(books);
        await SetAuthorNamesAsync(bookDtos);

        return new PagedResultDto<BookDto>(
            totalCount,
            bookDtos
        );
    }

    [Authorize(MyERPPermissions.Books.Create)]
    public async Task<BookDto> CreateAsync(CreateUpdateBookDto input)
    {
        var book = ObjectMapper.Map<CreateUpdateBookDto, Book>(input);
        await _repository.InsertAsync(book);
        return ObjectMapper.Map<Book, BookDto>(book);
    }

    [Authorize(MyERPPermissions.Books.Edit)]
    public async Task<BookDto> UpdateAsync(Guid id, CreateUpdateBookDto input)
    {
        var book = await _repository.GetAsync(id);
        ObjectMapper.Map(input, book);
        await _repository.UpdateAsync(book);
        return ObjectMapper.Map<Book, BookDto>(book);
    }

    private async Task SetAuthorNamesAsync(IReadOnlyCollection<BookDto> books)
    {
        if (books.Count == 0)
        {
            return;
        }

        var authorIds = books.Select(book => book.AuthorId).Distinct().ToList();
        var authors = await _authorRepository.GetListAsync(author => authorIds.Contains(author.Id));
        var authorLookup = authors.ToDictionary(author => author.Id, author => author.Name);

        foreach (var book in books)
        {
            book.AuthorName = authorLookup.GetValueOrDefault(book.AuthorId) ?? string.Empty;
        }
    }

    [Authorize(MyERPPermissions.Books.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    [AllowAnonymous]
    public async Task<IRemoteStreamContent> GetListAsExcelFileAsync(BookExcelDownloadDto input)
    {
        var downloadToken = await _excelDownloadTokenCache.GetAsync(input.DownloadToken);
        if (downloadToken == null || input.DownloadToken != downloadToken.Token)
        {
            throw new AbpAuthorizationException("Invalid download token: " + input.DownloadToken);
        }

        var queryable = await _repository.GetQueryableAsync();
        var query = queryable.OrderBy(input.Sorting.IsNullOrWhiteSpace() ? "Name" : input.Sorting);
        var books = await AsyncExecuter.ToListAsync(query);

        var authorIds = books.Select(book => book.AuthorId).Distinct().ToList();
        var authors = await _authorRepository.GetListAsync(author => authorIds.Contains(author.Id));
        var authorLookup = authors.ToDictionary(author => author.Id, author => author.Name);

        var memoryStream = new MemoryStream();
        await memoryStream.SaveAsAsync(books.Select(book => new BookExcelDto
        {
            Name = book.Name,
            AuthorName = authorLookup.GetValueOrDefault(book.AuthorId) ?? string.Empty,
            Type = book.Type,
            PublishDate = book.PublishDate,
            Price = book.Price
        }));
        memoryStream.Seek(0, SeekOrigin.Begin);

        return new RemoteStreamContent(
            memoryStream,
            "Books.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        );
    }

    public async Task<DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        var token = GuidGenerator.Create().ToString("N");

        await _excelDownloadTokenCache.SetAsync(
            token,
            new BookExcelDownloadTokenCacheItem { Token = token },
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
