using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Core;

public interface IDocumentConnectionsAppService : IApplicationService
{
    Task<DocumentConnectionsDto> GetConnectionsAsync(string documentType, Guid documentId);
}
