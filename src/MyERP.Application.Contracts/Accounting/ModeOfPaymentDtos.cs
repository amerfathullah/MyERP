using System;
using Volo.Abp.Application.Dtos;

namespace MyERP.Accounting;

public class ModeOfPaymentDto : EntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public string Type { get; set; } = null!;
    public bool IsActive { get; set; }
    public Guid? DefaultAccountId { get; set; }
    public Guid? CompanyId { get; set; }
}

public class CreateUpdateModeOfPaymentDto
{
    public string Name { get; set; } = null!;
    public string Type { get; set; } = "Bank";
    public bool IsActive { get; set; } = true;
    public Guid? DefaultAccountId { get; set; }
    public Guid? CompanyId { get; set; }
}
