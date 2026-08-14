using System;
using Volo.Abp.Application.Dtos;

namespace MyERP.Inventory;

public class CustomsTariffNumberDto : FullAuditedEntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string TariffNumber { get; set; } = null!;
    public string? Description { get; set; }
}
