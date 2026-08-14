using System;
using Volo.Abp.Application.Dtos;

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
