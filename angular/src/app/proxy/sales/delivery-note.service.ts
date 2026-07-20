import type { CreateDeliveryNoteDto, DeliveryNoteDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CompanyFilteredPagedRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class DeliveryNoteService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  amend = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DeliveryNoteDto>({
      method: 'POST',
      url: `/api/app/delivery-note/${id}/amend`,
    },
    { apiName: this.apiName,...config });
  

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
  

  getList = (input: CompanyFilteredPagedRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<DeliveryNoteDto>>({
      method: 'GET',
      url: '/api/app/delivery-note',
      params: { companyId: input.companyId, filter: input.filter, status: input.status, fromDate: input.fromDate, toDate: input.toDate, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DeliveryNoteDto>({
      method: 'POST',
      url: `/api/app/delivery-note/${id}/submit`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateDeliveryNoteDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DeliveryNoteDto>({
      method: 'PUT',
      url: `/api/app/delivery-note/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}