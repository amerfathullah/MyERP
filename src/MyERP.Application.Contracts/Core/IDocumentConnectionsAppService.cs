using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Core;

public interface IDocumentConnectionsAppService : IApplicationService
{
    Task<DocumentConnectionsDto> GetConnectionsAsync(string documentType, Guid documentId);

    /// <summary>
    /// Finds draft documents of a target type created from/linking back to a source document.
    /// Warns the user when attempting to create duplicate downstream documents (PR #57299).
    /// </summary>
    Task<List<ExistingDraftDto>> GetExistingDraftsAsync(string sourceDocType, Guid sourceId, string targetDocType);
}
