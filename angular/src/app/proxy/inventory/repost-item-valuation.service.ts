import type { CreateRepostItemValuationDto, RepostItemValuationDto, RepostItemValuationSummaryDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CompanyFilteredPagedRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class RepostItemValuationService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, RepostItemValuationDto>({
      method: 'POST',
      url: `/api/app/repost-item-valuation/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateRepostItemValuationDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, RepostItemValuationDto>({
      method: 'POST',
      url: '/api/app/repost-item-valuation',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, RepostItemValuationDto>({
      method: 'GET',
      url: `/api/app/repost-item-valuation/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: CompanyFilteredPagedRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<RepostItemValuationDto>>({
      method: 'GET',
      url: '/api/app/repost-item-valuation',
      params: { companyId: input.companyId, filter: input.filter, status: input.status, fromDate: input.fromDate, toDate: input.toDate, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getPendingCount = (companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, number>({
      method: 'GET',
      url: `/api/app/repost-item-valuation/pending-count/${companyId}`,
    },
    { apiName: this.apiName,...config });
  

  getSummary = (companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, RepostItemValuationSummaryDto>({
      method: 'GET',
      url: `/api/app/repost-item-valuation/summary/${companyId}`,
    },
    { apiName: this.apiName,...config });
  

  restart = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, RepostItemValuationDto>({
      method: 'POST',
      url: `/api/app/repost-item-valuation/${id}/restart`,
    },
    { apiName: this.apiName,...config });
}