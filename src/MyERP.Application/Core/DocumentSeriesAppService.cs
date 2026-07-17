using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Core;

public class DocumentSeriesDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string DocumentType { get; set; } = null!;
    public string Prefix { get; set; } = null!;
    public long CurrentNumber { get; set; }
    public int NumberPadding { get; set; }
}

public class CreateDocumentSeriesDto
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = null!;
    public string DocumentType { get; set; } = null!;
    public string Prefix { get; set; } = null!;
    public int NumberPadding { get; set; } = 5;
}

[Authorize(MyERPPermissions.Companies.Default)]
public class DocumentSeriesAppService : ApplicationService
{
    private readonly IRepository<DocumentSeries, Guid> _repository;
    public DocumentSeriesAppService(IRepository<DocumentSeries, Guid> repository) => _repository = repository;

    public async Task<PagedResultDto<DocumentSeriesDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var totalCount = query.Count();
        var items = query.OrderBy(d => d.DocumentType).ThenBy(d => d.Prefix)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<DocumentSeriesDto>(totalCount, items.Select(ObjectMapper.Map<DocumentSeries, DocumentSeriesDto>).ToList());
    }

    [Authorize(MyERPPermissions.Companies.Create)]
    public async Task<DocumentSeriesDto> CreateAsync(CreateDocumentSeriesDto input)
    {
        var ds = new DocumentSeries(GuidGenerator.Create(), input.CompanyId,
            input.Name, input.DocumentType, input.Prefix, CurrentTenant.Id)
        { NumberPadding = input.NumberPadding };
        await _repository.InsertAsync(ds);
        return ObjectMapper.Map<DocumentSeries, DocumentSeriesDto>(ds);
    }
}
