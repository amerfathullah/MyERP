import type { CreateDeliveryNoteDto, DeliveryNoteDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class DeliveryNoteService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DeliveryNoteDto>({
      method: 'POST',
      url: `/api/app/delivery-note/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateDeliveryNoteDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DeliveryNoteDto>({
      method: 'POST',
      url: '/api/app/delivery-note',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DeliveryNoteDto>({
      method: 'GET',
      url: `/api/app/delivery-note/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<DeliveryNoteDto>>({
      method: 'GET',
      url: '/api/app/delivery-note',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DeliveryNoteDto>({
      method: 'POST',
      url: `/api/app/delivery-note/${id}/submit`,
    },
    { apiName: this.apiName,...config });
}