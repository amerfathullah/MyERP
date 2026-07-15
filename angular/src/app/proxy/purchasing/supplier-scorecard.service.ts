import type { CreateScorecardDto, CreateScorecardPeriodDto, ScorecardDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SupplierScorecardService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateScorecardDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ScorecardDto>({
      method: 'POST',
      url: '/api/app/supplier-scorecard',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/supplier-scorecard/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ScorecardDto>({
      method: 'GET',
      url: `/api/app/supplier-scorecard/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getBySupplierIdBySupplierId = (supplierId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ScorecardDto>({
      method: 'GET',
      url: `/api/app/supplier-scorecard/by-supplier-id/${supplierId}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ScorecardDto>>({
      method: 'GET',
      url: '/api/app/supplier-scorecard',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  submitPeriod = (scorecardId: string, input: CreateScorecardPeriodDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/supplier-scorecard/submit-period/${scorecardId}`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  updateScore = (id: string, newScore: number, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ScorecardDto>({
      method: 'PUT',
      url: `/api/app/supplier-scorecard/${id}/score`,
      params: { newScore },
    },
    { apiName: this.apiName,...config });
}