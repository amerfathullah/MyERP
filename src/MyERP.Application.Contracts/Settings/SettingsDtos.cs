using System;
using Volo.Abp.Application.Dtos;

namespace MyERP.Settings;

public class PrintFormatDto : FullAuditedEntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public string DocumentType { get; set; } = null!;
    public string HtmlTemplate { get; set; } = null!;
    public bool IsDefault { get; set; }
    public PrintFormatType FormatType { get; set; }
    public string? FormatData { get; set; }
}

public class CreateUpdatePrintFormatDto
{
    public string Name { get; set; } = null!;
    public string DocumentType { get; set; } = null!;
    public string HtmlTemplate { get; set; } = null!;
    public bool IsDefault { get; set; }
    public PrintFormatType FormatType { get; set; } = PrintFormatType.Custom;
    public string? FormatData { get; set; }
}
