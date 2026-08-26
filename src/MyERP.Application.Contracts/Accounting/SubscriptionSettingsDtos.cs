using System;
using Volo.Abp.Application.Dtos;

namespace MyERP.Accounting;

public class SubscriptionSettingsDto : FullAuditedEntityDto<Guid>
{
    public int GracePeriod { get; set; }
    public bool CancelAfterGrace { get; set; }
    public bool Prorate { get; set; }
}

public class UpdateSubscriptionSettingsDto
{
    public int GracePeriod { get; set; } = 1;
    public bool CancelAfterGrace { get; set; }
    public bool Prorate { get; set; } = true;
}
