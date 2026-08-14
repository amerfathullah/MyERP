using System;
using Volo.Abp.Application.Dtos;

namespace MyERP.Core;

public class EmailTemplateDto : EntityDto<Guid>
{
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
