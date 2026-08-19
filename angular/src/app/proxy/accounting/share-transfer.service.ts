import type { CreateUpdateShareTransferDto, ShareTransferDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CompanyFilteredPagedRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class ShareTransferService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ShareTransferDto>({
      method: 'POST',
      url: `/api/app/share-transfer/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateUpdateShareTransferDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ShareTransferDto>({
      method: 'POST',
      url: '/api/app/share-transfer',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/share-transfer/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ShareTransferDto>({
      method: 'GET',
      url: `/api/app/share-transfer/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: CompanyFilteredPagedRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ShareTransferDto>>({
      method: 'GET',
      url: '/api/app/share-transfer',
      params: { companyId: input.companyId, filter: input.filter, status: input.status, fromDate: input.fromDate, toDate: input.toDate, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ShareTransferDto>({
      method: 'POST',
      url: `/api/app/share-transfer/${id}/submit`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateShareTransferDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ShareTransferDto>({
      method: 'PUT',
      url: `/api/app/share-transfer/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}