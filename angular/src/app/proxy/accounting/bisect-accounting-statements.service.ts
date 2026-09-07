import type { BisectAccountingStatementsDto, BisectAccountingStatementsGetListInput, CreateBisectAccountingStatementsDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class BisectAccountingStatementsService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  bisectLeft = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BisectAccountingStatementsDto>({
      method: 'POST',
      url: `/api/app/bisect-accounting-statements/${id}/bisect-left`,
    },
    { apiName: this.apiName,...config });
  

  bisectRight = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BisectAccountingStatementsDto>({
      method: 'POST',
      url: `/api/app/bisect-accounting-statements/${id}/bisect-right`,
    },
    { apiName: this.apiName,...config });
  

  createAndBuildTree = (input: CreateBisectAccountingStatementsDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BisectAccountingStatementsDto>({
      method: 'POST',
      url: '/api/app/bisect-accounting-statements/and-build-tree',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/bisect-accounting-statements/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BisectAccountingStatementsDto>({
      method: 'GET',
      url: `/api/app/bisect-accounting-statements/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: BisectAccountingStatementsGetListInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<BisectAccountingStatementsDto>>({
      method: 'GET',
      url: '/api/app/bisect-accounting-statements',
      params: { companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  moveUp = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BisectAccountingStatementsDto>({
      method: 'POST',
      url: `/api/app/bisect-accounting-statements/${id}/move-up`,
    },
    { apiName: this.apiName,...config });
}