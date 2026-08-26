using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Utilities;

public class VideoDto : FullAuditedEntityDto<Guid>
{
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
    public bool IsActive { get; set; }
}

public class CreateUpdateVideoDto
{
    [Required]
    [StringLength(VideoConsts.MaxTitleLength)]
    public string Title { get; set; } = null!;

    public VideoProvider Provider { get; set; }

    [Required]
    [StringLength(VideoConsts.MaxUrlLength)]
    public string Url { get; set; } = null!;

    [StringLength(VideoConsts.MaxVideoIdLength)]
    public string? YoutubeVideoId { get; set; }

    public DateTime? PublishDate { get; set; }

    public int? DurationSeconds { get; set; }

    [StringLength(VideoConsts.MaxDescriptionLength)]
    public string? Description { get; set; }

    [StringLength(VideoConsts.MaxImageUrlLength)]
    public string? ImageUrl { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateVideoStatsDto
{
    public long ViewCount { get; set; }
    public long LikeCount { get; set; }
    public long DislikeCount { get; set; }
    public long CommentCount { get; set; }
}

public class GetVideoListDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public VideoProvider? Provider { get; set; }
    public bool? IsActive { get; set; }
}
