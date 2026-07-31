import type { GlRepostResultDto, RepostBatchGlDto, RepostGlDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class GlRepostService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getAllowedVoucherTypes = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, string[]>({
      method: 'GET',
      url: '/api/app/gl-repost/owed-voucher-types',
    },
    { apiName: this.apiName,...config });
  

  isRepostAllowed = (voucherType: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, boolean>({
      method: 'POST',
      url: '/api/app/gl-repost/is-repost-allowed',
      params: { voucherType },
    },
    { apiName: this.apiName,...config });
  

  repost = (input: RepostGlDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, GlRepostResultDto>({
      method: 'POST',
      url: '/api/app/gl-repost/repost',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  repostBatch = (input: RepostBatchGlDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, GlRepostResultDto>({
      method: 'POST',
      url: '/api/app/gl-repost/repost-batch',
      body: input,
    },
    { apiName: this.apiName,...config });
}