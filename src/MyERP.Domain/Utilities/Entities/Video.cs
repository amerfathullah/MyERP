using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
    private static readonly string[] YoutubeDomains = ["youtube.com", "youtu.be", "youtube-nocookie.com"];
    private static readonly Regex YoutubeVideoIdRegex = new("^[A-Za-z0-9_-]{11}$", RegexOptions.Compiled);
    private static readonly Regex HostnameRegex = new("^[a-z0-9.-]+$", RegexOptions.Compiled);

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
        UpdateDetails(title, provider, url, youtubeVideoId, publishDate, durationSeconds, description, imageUrl, isActive);
        TenantId = tenantId;
    }

    public void UpdateDetails(
        string title,
        VideoProvider provider,
        string url,
        string? youtubeVideoId = null,
        DateTime? publishDate = null,
        int? durationSeconds = null,
        string? description = null,
        string? imageUrl = null,
        bool isActive = true)
    {
        Title = Check.NotNullOrWhiteSpace(title, nameof(title), VideoConsts.MaxTitleLength);
        Provider = provider;
        Url = Check.NotNullOrWhiteSpace(url, nameof(url), VideoConsts.MaxUrlLength).Trim();
        YoutubeVideoId = youtubeVideoId?.Trim();
        PublishDate = publishDate;
        DurationSeconds = durationSeconds;
        Description = description;
        ImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim();
        IsActive = isActive;

        ValidateAndSyncUrls();
    }

    /// <summary>
    /// Validates video/thumbnail URLs and keeps YoutubeVideoId synchronized with Url.
    /// Per ERPNext PR #65694 (commits 0f66a75773 &amp; ee95e8e0e5): prevents XSS payloads via
    /// non-http(s) links or arbitrary manipulated YouTube IDs.
    /// </summary>
    public void ValidateAndSyncUrls()
    {
        ValidateWebUrl(Url);

        if (!string.IsNullOrWhiteSpace(ImageUrl))
        {
            ValidateWebUrl(ImageUrl);
        }

        if (Provider == VideoProvider.YouTube)
        {
            var parsedId = ParseYoutubeVideoId(Url);
            if (string.IsNullOrEmpty(parsedId))
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", "Could not find a YouTube video ID in the URL.");
            }
            YoutubeVideoId = parsedId;
        }
        else if (!string.IsNullOrEmpty(YoutubeVideoId) && !YoutubeVideoIdRegex.IsMatch(YoutubeVideoId))
        {
            YoutubeVideoId = null;
        }
    }

    public static void ValidateWebUrl(string url)
    {
        var trimmed = url.Trim();
        if (trimmed.Any(char.IsWhiteSpace) || trimmed.Contains('"') || trimmed.Contains('\'') || trimmed.Contains('<') || trimmed.Contains('>'))
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Enter a valid http or https URL.");
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrWhiteSpace(uri.Host)
            || !HostnameRegex.IsMatch(uri.Host.ToLowerInvariant()))
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Enter a valid http or https URL.");
        }
    }

    public static string? ParseYoutubeVideoId(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        var trimmed = url.Trim();
        var candidate = trimmed.Contains("://", StringComparison.Ordinal) ? trimmed : $"https://{trimmed}";
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri)) return null;

        var host = uri.Host.ToLowerInvariant();
        if (!YoutubeDomains.Any(d => host == d || host.EndsWith($".{d}", StringComparison.Ordinal)))
        {
            return null;
        }

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string? videoId = null;
        if (host.EndsWith("youtu.be", StringComparison.Ordinal))
        {
            videoId = segments.Length > 0 ? segments[0] : null;
        }
        else if (segments.Length > 1 && segments[0] is "embed" or "shorts" or "live" or "v")
        {
            videoId = segments[1];
        }
        else
        {
            var query = uri.Query.TrimStart('?');
            if (!string.IsNullOrEmpty(query))
            {
                foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
                {
                    var eq = pair.IndexOf('=');
                    if (eq > 0 && string.Equals(pair[..eq], "v", StringComparison.Ordinal))
                    {
                        videoId = Uri.UnescapeDataString(pair[(eq + 1)..]);
                        break;
                    }
                }
            }
        }

        return !string.IsNullOrEmpty(videoId) && YoutubeVideoIdRegex.IsMatch(videoId) ? videoId : null;
    }

    public void UpdateStats(long viewCount, long likeCount, long dislikeCount, long commentCount)
    {
        ViewCount = Math.Max(0, viewCount);
        LikeCount = Math.Max(0, likeCount);
        DislikeCount = Math.Max(0, dislikeCount);
        CommentCount = Math.Max(0, commentCount);
    }
}
