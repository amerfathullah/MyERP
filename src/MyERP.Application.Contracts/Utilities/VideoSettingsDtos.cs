using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Utilities;

public class VideoSettingsDto : FullAuditedEntityDto<Guid>
{
    public bool EnableYoutubeTracking { get; set; }
    public string? ApiKey { get; set; }
    public int FrequencyMinutes { get; set; }
}

public class UpdateVideoSettingsDto
{
    public bool EnableYoutubeTracking { get; set; }

    [StringLength(VideoConsts.MaxApiKeyLength)]
    public string? ApiKey { get; set; }

    public int FrequencyMinutes { get; set; } = 60;
}
