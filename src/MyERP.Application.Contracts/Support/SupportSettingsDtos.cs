using System;
using Volo.Abp.Application.Dtos;

namespace MyERP.Support;

public class SupportSettingsDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public bool TrackServiceLevelAgreement { get; set; }
    public bool AllowResettingServiceLevelAgreement { get; set; }
    public int? CloseIssueAfterDays { get; set; }
}

public class SaveSupportSettingsDto
{
    public Guid CompanyId { get; set; }
    public bool TrackServiceLevelAgreement { get; set; } = true;
    public bool AllowResettingServiceLevelAgreement { get; set; }
    public int? CloseIssueAfterDays { get; set; }
}
