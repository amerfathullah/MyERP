using System;
using Volo.Abp.Application.Dtos;

namespace MyERP.Accounting;

public class ModeOfPaymentDto : EntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public string Type { get; set; } = null!;
}
