import type { CreateUpdateVideoDto, GetVideoListDto, UpdateVideoStatsDto, VideoDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class VideoService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdateVideoDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, VideoDto>({
      method: 'POST',
      url: '/api/app/video',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/video/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, VideoDto>({
      method: 'GET',
      url: `/api/app/video/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetVideoListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<VideoDto>>({
      method: 'GET',
      url: '/api/app/video',
      params: { filter: input.filter, provider: input.provider, isActive: input.isActive, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateVideoDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, VideoDto>({
      method: 'PUT',
      url: `/api/app/video/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  updateStats = (id: string, input: UpdateVideoStatsDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, VideoDto>({
      method: 'PUT',
      url: `/api/app/video/${id}/stats`,
      body: input,
    },
    { apiName: this.apiName,...config });
}