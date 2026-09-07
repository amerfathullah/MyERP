import type { UpdateVideoSettingsDto, VideoSettingsDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class VideoSettingsService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  get = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, VideoSettingsDto>({
      method: 'GET',
      url: '/api/app/video-settings',
    },
    { apiName: this.apiName,...config });
  

  update = (input: UpdateVideoSettingsDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, VideoSettingsDto>({
      method: 'PUT',
      url: '/api/app/video-settings',
      body: input,
    },
    { apiName: this.apiName,...config });
}