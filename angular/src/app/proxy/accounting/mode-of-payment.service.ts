import type { CreateUpdateModeOfPaymentDto, ModeOfPaymentDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ModeOfPaymentService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdateModeOfPaymentDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ModeOfPaymentDto>({
      method: 'POST',
      url: '/api/app/mode-of-payment',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/mode-of-payment/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ModeOfPaymentDto>({
      method: 'GET',
      url: `/api/app/mode-of-payment/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ModeOfPaymentDto>>({
      method: 'GET',
      url: '/api/app/mode-of-payment',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateModeOfPaymentDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ModeOfPaymentDto>({
      method: 'PUT',
      url: `/api/app/mode-of-payment/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}