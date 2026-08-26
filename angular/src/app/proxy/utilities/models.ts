import type { FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { VideoProvider } from './video-provider.enum';

export interface VideoDto extends FullAuditedEntityDto<string> {
  title: string;
  provider: VideoProvider;
  url: string;
  youtubeVideoId?: string | null;
  publishDate?: string | null;
  durationSeconds?: number | null;
  description?: string | null;
  imageUrl?: string | null;
  likeCount: number;
  viewCount: number;
  dislikeCount: number;
  commentCount: number;
  isActive: boolean;
}

export interface CreateUpdateVideoDto {
  title: string;
  provider: VideoProvider;
  url: string;
  youtubeVideoId?: string | null;
  publishDate?: string | null;
  durationSeconds?: number | null;
  description?: string | null;
  imageUrl?: string | null;
  isActive?: boolean;
}

export interface UpdateVideoStatsDto {
  viewCount: number;
  likeCount: number;
  dislikeCount: number;
  commentCount: number;
}

export interface GetVideoListDto extends PagedAndSortedResultRequestDto {
  filter?: string | null;
  provider?: VideoProvider | null;
  isActive?: boolean | null;
}

export interface VideoSettingsDto extends FullAuditedEntityDto<string> {
  enableYoutubeTracking: boolean;
  apiKey?: string | null;
  frequencyMinutes: number;
}

export interface UpdateVideoSettingsDto {
  enableYoutubeTracking: boolean;
  apiKey?: string | null;
  frequencyMinutes: number;
}
