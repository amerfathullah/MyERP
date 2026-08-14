using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Core.Entities;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Core;

[Authorize]
public class DocumentActivityLogAppService : ApplicationService, IDocumentActivityLogAppService
{
    private readonly IRepository<DocumentActivityLog, Guid> _repository;

    public DocumentActivityLogAppService(IRepository<DocumentActivityLog, Guid> repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Gets activity log entries for a specific document (audit trail).
    /// </summary>
    public async Task<List<DocumentActivityLogDto>> GetForDocumentAsync(string documentType, Guid documentId)
    {
        var query = await _repository.GetQueryableAsync();
        var logs = query
            .Where(l => l.DocumentType == documentType && l.DocumentId == documentId)
            .OrderByDescending(l => l.CreationTime)
            .Take(50)
            .ToList();

        return logs.Select(ObjectMapper.Map<DocumentActivityLog, DocumentActivityLogDto>).ToList();
    }

    /// <summary>
    /// Gets recent activity logs for a company (dashboard/feed).
    /// </summary>
    public async Task<PagedResultDto<DocumentActivityLogDto>> GetRecentAsync(
        Guid companyId, int skipCount = 0, int maxResultCount = 20)
    {
        var query = await _repository.GetQueryableAsync();
        var filtered = query.Where(l => l.CompanyId == companyId);
        var totalCount = filtered.Count();

        var logs = filtered
            .OrderByDescending(l => l.CreationTime)
            .Skip(skipCount)
            .Take(maxResultCount)
            .ToList();

        return new PagedResultDto<DocumentActivityLogDto>(
            totalCount,
            logs.Select(ObjectMapper.Map<DocumentActivityLog, DocumentActivityLogDto>).ToList());
    }
}
