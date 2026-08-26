using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Utilities.Entities;

/// <summary>
/// Video — manages knowledge base, product, tutorial, and marketing videos.
/// Maps to ERPNext utilities/doctype/video.
/// </summary>
public class Video : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public string Title { get; set; } = null!;
    public VideoProvider Provider { get; set; }
    public string Url { get; set; } = null!;
    public string? YoutubeVideoId { get; set; }
    public DateTime? PublishDate { get; set; }
    public int? DurationSeconds { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public long LikeCount { get; set; }
    public long ViewCount { get; set; }
    public long DislikeCount { get; set; }
    public long CommentCount { get; set; }
    public bool IsActive { get; set; } = true;

    protected Video() { }

    public Video(
        Guid id,
        string title,
        VideoProvider provider,
        string url,
        string? youtubeVideoId = null,
        DateTime? publishDate = null,
        int? durationSeconds = null,
        string? description = null,
        string? imageUrl = null,
        bool isActive = true,
        Guid? tenantId = null)
        : base(id)
    {
        Title = Check.NotNullOrWhiteSpace(title, nameof(title), VideoConsts.MaxTitleLength);
        Provider = provider;
        Url = Check.NotNullOrWhiteSpace(url, nameof(url), VideoConsts.MaxUrlLength);
        YoutubeVideoId = youtubeVideoId;
        PublishDate = publishDate;
        DurationSeconds = durationSeconds;
        Description = description;
        ImageUrl = imageUrl;
        IsActive = isActive;
        TenantId = tenantId;
    }

    public void UpdateStats(long viewCount, long likeCount, long dislikeCount, long commentCount)
    {
        ViewCount = Math.Max(0, viewCount);
        LikeCount = Math.Max(0, likeCount);
        DislikeCount = Math.Max(0, dislikeCount);
        CommentCount = Math.Max(0, commentCount);
    }
}
