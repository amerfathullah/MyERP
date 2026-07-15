using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Core;

/// <summary>
/// Manages Email Templates — reusable templates for automated notifications.
/// Used by: dunning, auto-repeat, delivery dispatch, RFQ, payment reminders, PSOA.
/// Supports variable substitution via {{variable_name}} placeholders.
/// </summary>
[Authorize(MyERPPermissions.AutomationRules.Default)]
public class EmailTemplateAppService : ApplicationService
{
    private readonly IRepository<EmailTemplate, Guid> _repository;

    public EmailTemplateAppService(IRepository<EmailTemplate, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<EmailTemplateDto> GetAsync(Guid id)
    {
        var t = await _repository.GetAsync(id);
        return MapToDto(t);
    }

    public async Task<List<EmailTemplateDto>> GetListAsync(string? documentType = null)
    {
        var query = await _repository.GetQueryableAsync();
        if (!string.IsNullOrEmpty(documentType))
            query = query.Where(t => t.DocumentType == documentType);
        return query.OrderBy(t => t.Name).ToList().Select(MapToDto).ToList();
    }

    [Authorize(MyERPPermissions.AutomationRules.Create)]
    public async Task<EmailTemplateDto> CreateAsync(CreateEmailTemplateDto input)
    {
        var template = new EmailTemplate(
            GuidGenerator.Create(),
            input.Name,
            input.Subject,
            input.Body,
            CurrentTenant.Id);

        template.DocumentType = input.DocumentType;
        await _repository.InsertAsync(template);
        return MapToDto(template);
    }

    [Authorize(MyERPPermissions.AutomationRules.Edit)]
    public async Task<EmailTemplateDto> UpdateAsync(Guid id, UpdateEmailTemplateDto input)
    {
        var template = await _repository.GetAsync(id);
        template.Subject = input.Subject;
        template.Body = input.Body;
        template.DocumentType = input.DocumentType;
        await _repository.UpdateAsync(template);
        return MapToDto(template);
    }

    [Authorize(MyERPPermissions.AutomationRules.Delete)]
    public async Task DeleteAsync(Guid id) => await _repository.DeleteAsync(id);

    /// <summary>
    /// Preview a rendered template with sample variables.
    /// </summary>
    public async Task<RenderedTemplateDto> PreviewAsync(Guid id, Dictionary<string, string> variables)
    {
        var template = await _repository.GetAsync(id);
        return new RenderedTemplateDto
        {
            Subject = template.RenderSubject(variables),
            Body = template.RenderBody(variables)
        };
    }

    private static EmailTemplateDto MapToDto(EmailTemplate t) => new()
    {
        Id = t.Id,
        Name = t.Name,
        Subject = t.Subject,
        Body = t.Body,
        DocumentType = t.DocumentType
    };
}

#region DTOs

public class EmailTemplateDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Subject { get; set; } = null!;
    public string Body { get; set; } = null!;
    public string? DocumentType { get; set; }
}

public class CreateEmailTemplateDto
{
    public string Name { get; set; } = null!;
    public string Subject { get; set; } = null!;
    public string Body { get; set; } = null!;
    public string? DocumentType { get; set; }
}

public class UpdateEmailTemplateDto
{
    public string Subject { get; set; } = null!;
    public string Body { get; set; } = null!;
    public string? DocumentType { get; set; }
}

public class RenderedTemplateDto
{
    public string Subject { get; set; } = null!;
    public string Body { get; set; } = null!;
}

#endregion
