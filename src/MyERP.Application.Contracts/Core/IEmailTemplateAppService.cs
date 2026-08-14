using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Core;

public interface IEmailTemplateAppService : IApplicationService
{
    Task<EmailTemplateDto> GetAsync(Guid id);
    Task<List<EmailTemplateDto>> GetListAsync(string? documentType = null);
    Task<EmailTemplateDto> CreateAsync(CreateEmailTemplateDto input);
    Task<EmailTemplateDto> UpdateAsync(Guid id, UpdateEmailTemplateDto input);
    Task DeleteAsync(Guid id);
    Task<RenderedTemplateDto> PreviewAsync(Guid id, Dictionary<string, string> variables);
}
