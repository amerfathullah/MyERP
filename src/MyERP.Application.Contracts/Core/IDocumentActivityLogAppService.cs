using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Core;

public interface IDocumentActivityLogAppService : IApplicationService
{
    Task<List<DocumentActivityLogDto>> GetForDocumentAsync(string documentType, Guid documentId);
    Task<PagedResultDto<DocumentActivityLogDto>> GetRecentAsync(Guid companyId, int skipCount = 0, int maxResultCount = 20);
}
