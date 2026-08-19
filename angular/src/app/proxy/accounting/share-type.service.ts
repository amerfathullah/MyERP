import type { CreateUpdateShareTypeDto, ShareTypeDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { ListResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ShareTypeService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdateShareTypeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ShareTypeDto>({
      method: 'POST',
      url: '/api/app/share-type',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/share-type/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, ListResultDto<ShareTypeDto>>({
      method: 'GET',
      url: '/api/app/share-type',
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateShareTypeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ShareTypeDto>({
      method: 'PUT',
      url: `/api/app/share-type/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}