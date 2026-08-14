using System;
using Volo.Abp.Application.Dtos;

namespace MyERP.Accounting;

public class FinanceBookDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = null!;
    public bool IsDefault { get; set; }
    public string? Description { get; set; }
}

public class CreateFinanceBookDto
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = null!;
    public bool IsDefault { get; set; }
    public string? Description { get; set; }
}
