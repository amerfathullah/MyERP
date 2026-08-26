using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Utilities.Entities;

/// <summary>
/// Video Settings — configuration for automated YouTube tracking and metadata synchronization.
/// Maps to ERPNext utilities/doctype/video_settings.
/// </summary>
public class VideoSettings : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public bool EnableYoutubeTracking { get; set; }
    public string? ApiKey { get; set; }
    public int FrequencyMinutes { get; set; } = 60; // 30, 60, 360, 1440

    protected VideoSettings() { }

    public VideoSettings(
        Guid id,
        bool enableYoutubeTracking = false,
        string? apiKey = null,
        int frequencyMinutes = 60,
        Guid? tenantId = null)
        : base(id)
    {
        EnableYoutubeTracking = enableYoutubeTracking;
        ApiKey = apiKey;
        FrequencyMinutes = frequencyMinutes;
        TenantId = tenantId;
    }
}
